using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoApp.Data;
using System.IO.Compression;

namespace PhotoApp.Controllers
{
    [ApiController]
    [Route("api/admin/db")]
    [Authorize]
    public class DatabaseBackupController : ControllerBase
    {
        private readonly string _dbPath;           // absolute path to the sqlite file
        private readonly string _dbConnString;     // connection string used to open the DB
        private readonly string _backupFolder;
        private readonly ILogger<DatabaseBackupController> _logger;
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IWebHostEnvironment _env;
        private static readonly SemaphoreSlim _opLock = new(1, 1); // IDE0090: 'new' zjednodušen

        private const long MaxUploadBytes = 10L * 1024 * 1024 * 1024; // 10 737 418 240 bytes


        public DatabaseBackupController(IConfiguration config,
                                        ILogger<DatabaseBackupController> logger,
                                        IServiceProvider services,
                                        IHostApplicationLifetime appLifetime,
                                        IWebHostEnvironment env)
        {
            _logger = logger;
            _services = services;
            _appLifetime = appLifetime;
            _env = env ?? throw new ArgumentNullException(nameof(env));

            // Read possible DB config in multiple forms:
            var cfg = config["SqliteDbPath"]
                      ?? config.GetConnectionString("DefaultConnection")
                      ?? config["ConnectionStrings:Sqlite"];

            string? dbPath;
            string? connString;
            var contentRoot = _env.ContentRootPath ?? AppContext.BaseDirectory;

            if (string.IsNullOrWhiteSpace(cfg))
            {
                // default: content root + photoapp.db
                dbPath = Path.Combine(contentRoot, "photoapp.db");
                connString = $"Data Source={dbPath}";
            }
            // IDE0075: Použito Contains místo IndexOf >= 0
            else if (cfg.Contains("data source=", StringComparison.OrdinalIgnoreCase))
            {
                // cfg looks like connection string; parse Data Source out to an absolute path
                connString = cfg;
                // CS8603/CS8600: ExtractDataSource... nyní vrací string? a je třeba ošetřit null
                dbPath = ExtractDataSourceFromConnectionString(cfg, contentRoot);
                if (dbPath == null)
                {
                    _logger.LogWarning("Could not parse 'Data Source' from connection string, falling back to default. ConnString: {ConnString}", cfg);
                    dbPath = Path.Combine(contentRoot, "photoapp.db");
                    connString = $"Data Source={dbPath}"; // Musíme přepsat i connString, byl neplatný
                }
            }
            else
            {
                // cfg is a path (absolute or relative to content root)
                dbPath = cfg;
                if (!Path.IsPathRooted(dbPath))
                    dbPath = Path.GetFullPath(Path.Combine(contentRoot, dbPath));
                connString = $"Data Source={dbPath}";
            }

            // CS8618: Tímto přiřazením na konci je kompilátor spokojený
            _dbPath = dbPath;
            _dbConnString = connString;

            _backupFolder = Path.Combine(contentRoot, "db-backups");
            try { Directory.CreateDirectory(_backupFolder); } catch { /* ignore */ }

            // IDE0057: Substring zjednodušen pomocí 'range'
            _logger.LogInformation("DatabaseBackupController initialized. dbPath={DbPath}, connStringPreview={ConnPreview}", _dbPath, _dbConnString?[..Math.Min(80, _dbConnString.Length)]);
        }

        // GET: api/admin/db/backup
        // returns a zip containing database.db and uploads/*
        [HttpGet("backup")]
        public async Task<IActionResult> GetBackup()
        {
            await _opLock.WaitAsync();
            try
            {
                if (!System.IO.File.Exists(_dbPath))
                {
                    _logger.LogWarning("DB file not found at {DbPath}", _dbPath);
                    return NotFound("DB file not found.");
                }

                // 1) create a consistent tmp copy of DB using BackupDatabase
                var tmpDb = Path.Combine(Path.GetTempPath(), $"photoapp_backup_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.db");
                try
                {
                    // IDE0063: 'using' zjednodušen
                    using var source = new SqliteConnection(_dbConnString);
                    using var dest = new SqliteConnection($"Data Source={tmpDb}");
                    await source.OpenAsync();
                    await dest.OpenAsync();
                    source.BackupDatabase(dest);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create tmp DB via BackupDatabase, attempting direct copy fallback.");
                    try
                    {
                        System.IO.File.Copy(_dbPath, tmpDb, overwrite: true);
                    }
                    catch (Exception copyEx)
                    {
                        _logger.LogError(copyEx, "Fallback copy also failed.");
                        return StatusCode(500, "Failed to prepare DB for backup: " + copyEx.Message);
                    }
                }

                // 2) create zip in memory (stream) containing the tmp db and uploads folder
                var zipName = $"backup_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
                var uploadsFolder = Path.Combine(_env.WebRootPath ?? "", "uploads");

                // IDE0063: 'using' zjednodušen
                using var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    // add database.db entry
                    var dbEntry = zip.CreateEntry("database.db", CompressionLevel.Optimal);
                    // IDE0063: 'using' zjednodušen
                    using (var zs = dbEntry.Open())
                    using (var fs = System.IO.File.Open(tmpDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        await fs.CopyToAsync(zs);
                    }

                    // add uploads recursively
                    if (Directory.Exists(uploadsFolder))
                    {
                        var files = Directory.GetFiles(uploadsFolder, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var relPath = Path.GetRelativePath(uploadsFolder, file).Replace('\\', '/');
                            var entryPath = Path.Combine("uploads", relPath).Replace('\\', '/');
                            var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);

                            // try to open with read sharing (retry lightly if needed)
                            // IDE0063: 'using' zjednodušen
                            using (var zs = entry.Open())
                            using (var fs = System.IO.File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                await fs.CopyToAsync(zs);
                            }
                        }
                    }
                }

                ms.Position = 0;
                // schedule tmpDb deletion after response completes
                Response.OnCompleted(() =>
                {
                    try { if (System.IO.File.Exists(tmpDb)) System.IO.File.Delete(tmpDb); } catch { }
                    return Task.CompletedTask;
                });

                return File(ms.ToArray(), "application/zip", zipName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating DB+uploads backup");
                return StatusCode(500, "Failed to create backup: " + ex.Message);
            }
            finally
            {
                _opLock.Release();
            }
        }

        [HttpGet("ui")]
        [Authorize]
        public IActionResult Ui() => Redirect("/Admin/Database");

        // POST: api/admin/db/restore
        // accepts a ZIP that contains database.db and uploads/* and restores both
        [HttpPost("restore")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<IActionResult> RestoreBackup([FromForm] Microsoft.AspNetCore.Http.IFormFile backupZip)
        {
            if (backupZip == null || backupZip.Length == 0)
                return BadRequest("No file uploaded.");

            if (backupZip.Length > MaxUploadBytes)
                return BadRequest("Uploaded file too large.");

            await _opLock.WaitAsync();
            var tmpFolder = Path.Combine(Path.GetTempPath(), $"photoapp_import_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpFolder);
            try
            {
                // 1) save uploaded zip to tmp
                var zipPath = Path.Combine(tmpFolder, "backup.zip");
                // IDE0063: 'using' zjednodušen (FileStream je IAsyncDisposable -> await using)
                await using (var fs = System.IO.File.Create(zipPath))
                {
                    await backupZip.CopyToAsync(fs);
                }

                // 2) extract
                // Původní kód zde měl duplicitní blok pro uložení souboru, ten byl odstraněn.

                // DIAGNOSTIKA: log velikosti a prvních pár bytů (magic)
                long savedSize = new FileInfo(zipPath).Length;
                _logger.LogInformation("Uploaded backup saved to {ZipPath}, size={Size} bytes", zipPath, savedSize);

                // check for minimal ZIP signature (PK\x03\x04)
                byte[] header = new byte[4];
                // IDE0063: 'using' zjednodušen
                using (var fh = System.IO.File.OpenRead(zipPath))
                {
                    // CA1835: Použito přetížení ReadAsync(Memory<byte>)
                    await fh.ReadAsync(header);
                }
                bool looksLikeZip = header.Length == 4 && header[0] == (byte)'P' && header[1] == (byte)'K' && (header[2] == 3 || header[2] == 5 || header[2] == 7);
                if (!looksLikeZip)
                {
                    _logger.LogWarning("Uploaded file does not start with ZIP signature. Header bytes: {Header}", BitConverter.ToString(header));
                    return BadRequest("Uploaded file does not appear to be a ZIP archive (invalid signature).");
                }

                // Pokusíme se otevřít ZIP virtuálně a vypsat seznam položek - to odhalí poškození
                try
                {
                    // IDE0063: 'using' zjednodušen
                    using var zipStream = System.IO.File.OpenRead(zipPath);
                    using var zip = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false);
                    _logger.LogInformation("ZIP contains {Count} entries. First entries: {Names}", zip.Entries.Count, string.Join(", ", zip.Entries.Take(10).Select(e => e.FullName)));
                    // verify that database.db exists at root
                    var dbEntry = zip.GetEntry("database.db");
                    if (dbEntry == null)
                    {
                        _logger.LogWarning("ZIP does not contain database.db at root. Entries: {Entries}", string.Join(", ", zip.Entries.Select(e => e.FullName)));
                        return BadRequest("ZIP does not contain database.db at the root.");
                    }
                    // optionally verify uploads entries exist - not mandatory
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read ZIP archive after saving upload");
                    // vrať konkrétní chybovou zprávu (bez odhalení citlivých cest)
                    return BadRequest("Uploaded file is not a valid ZIP archive or it is corrupted: " + ex.Message);
                }

                // Pokud prošlo, extrahujme bezpečně
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, tmpFolder, overwriteFiles: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract uploaded ZIP using ZipFile.ExtractToDirectory");
                    return BadRequest("Extraction failed: " + ex.Message);
                }

                // 3) find database.db inside extracted folder
                var extractedDb = Path.Combine(tmpFolder, "database.db");
                if (!System.IO.File.Exists(extractedDb))
                {
                    return BadRequest("ZIP does not contain database.db at the root.");
                }

                // 4) integrity check of extracted DB
                try
                {
                    using var checkConn = new SqliteConnection($"Data Source={extractedDb}");
                    await checkConn.OpenAsync();
                    using var checkCmd = checkConn.CreateCommand();
                    checkCmd.CommandText = "PRAGMA integrity_check;";
                    var res = (string?)await checkCmd.ExecuteScalarAsync(); // Ošetření pro případný null výsledek
                    if (!string.Equals(res, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest($"Uploaded DB failed integrity_check: {res}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Integrity check failed or could not open extracted DB.");
                    return BadRequest("Uploaded DB failed integrity check or could not be opened.");
                }

                // 5) Save fallback of running DB (best-effort)
                var fallback = Path.Combine(_backupFolder, $"pre-restore-{DateTime.UtcNow:yyyyMMdd_HHmmss}.sqlite");
                if (System.IO.File.Exists(_dbPath))
                {
                    try
                    {
                        // IDE0063: 'using' zjednodušen
                        using var source = new SqliteConnection(_dbConnString);
                        using var destFallback = new SqliteConnection($"Data Source={fallback}");
                        await source.OpenAsync();
                        await destFallback.OpenAsync();
                        source.BackupDatabase(destFallback);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create fallback backup before restore (continuing).");
                    }
                }

                // 6) Try to close DI DbContexts (best-effort) to minimize locks
                try
                {
                    // IDE0063: 'using' zjednodušen
                    using var scope = _services?.CreateScope();
                    if (scope != null)
                    {
                        var appDb = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
                        // Použití 'is not null' pattern matching
                        if (appDb is not null)
                        {
                            try { await appDb.Database.CloseConnectionAsync(); } catch { /* ignore */ }
                            try { appDb.ChangeTracker.Clear(); } catch { /* ignore */ }
                            try { appDb.Dispose(); } catch { /* ignore */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve/close AppDbContext before restore (continuing).");
                }

                // 7) Import database content into running DB via Backup API
                try
                {
                    // IDE0063: 'using' zjednodušen
                    using var src = new SqliteConnection($"Data Source={extractedDb}");
                    using var dest = new SqliteConnection(_dbConnString);
                    await src.OpenAsync();
                    await dest.OpenAsync();
                    src.BackupDatabase(dest);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply uploaded DB into running DB.");
                    return StatusCode(500, "Failed to apply uploaded DB into running DB: " + ex.Message);
                }

                // 8) WAL checkpoint if needed
                try
                {
                    // IDE0063: 'using' zjednodušen
                    using var conn = new SqliteConnection(_dbConnString);
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "wal_checkpoint failed after restore (continuing).");
                }

                // 9) Restore uploads folder (if present in zip)
                var extractedUploads = Path.Combine(tmpFolder, "uploads");
                var targetUploads = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                try
                {
                    if (Directory.Exists(extractedUploads))
                    {
                        // remove existing uploads folder (best-effort) and copy new
                        if (Directory.Exists(targetUploads))
                        {
                            Directory.Delete(targetUploads, true);
                        }
                        Directory.CreateDirectory(targetUploads);
                        CopyDirectory(extractedUploads, targetUploads);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore uploads fully. Some files may be missing.");
                    // continue - DB was restored
                }

                _logger.LogInformation("Restore applied into running DB and uploads restored (if present). Fallback saved to {Fallback}", fallback);

                // 10) cleanup and redirect to Photos/Index
                return RedirectToAction("Index", "Photos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restore failed");
                return StatusCode(500, $"Restore failed: {ex.Message}");
            }
            finally
            {
                try { if (Directory.Exists(tmpFolder)) Directory.Delete(tmpFolder, true); } catch { }
                _opLock.Release();
            }
        }

        // helper to copy directory recursively
        // CA1822: Metoda označena jako 'static'
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var targetSub = dir.Replace(sourceDir, targetDir);
                Directory.CreateDirectory(targetSub);
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var dest = file.Replace(sourceDir, targetDir);
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                System.IO.File.Copy(file, dest, overwrite: true);
            }
        }

        // CS8603: Návratový typ změněn na 'string?' (nullable)
        private static string? ExtractDataSourceFromConnectionString(string connString, string contentRoot)
        {
            if (string.IsNullOrWhiteSpace(connString)) return null;
            var lower = connString.ToLowerInvariant();
            var key = "data source=";
            var idx = lower.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { key = "datasource="; idx = lower.IndexOf(key, StringComparison.OrdinalIgnoreCase); }
            if (idx < 0) return null; // Vrací null, proto musí být návratový typ 'string?'
            var start = idx + key.Length;
            // IDE0057: Substring zjednodušen pomocí 'range'
            var rest = connString[start..].Trim();
            var endIdx = rest.IndexOf(';');
            // IDE0057: Substring zjednodušen pomocí 'range'
            var path = endIdx >= 0 ? rest[..endIdx] : rest;
            path = path.Trim().Trim('"').Trim('\'');
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(contentRoot ?? Directory.GetCurrentDirectory(), path));
            }
            return path;
        }

        // kept for compatibility if some callers still use UploadFileModel
        public class UploadFileModel
        {
            // CS8618: Použito 'null!' pro potlačení varování u vlastnosti vyplněné binderem
            public Microsoft.AspNetCore.Http.IFormFile File { get; set; } = null!;
        }
    }
}