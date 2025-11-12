using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using PhotoApp.Data;
using PhotoApp.Models;
using System.IO.Compression;

public partial class PhotosController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PhotosController> _logger;

    public PhotosController(AppDbContext context, IWebHostEnvironment env, ILogger<PhotosController> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile excelFile)
    {
        // 🔹 ZMĚNA: Ověření souboru vrací JSON chybu
        if (excelFile == null || excelFile.Length == 0)
        {
            return BadRequest(new { success = false, message = "Nahrajte .xlsx soubor s daty." });
        }

        var warnings = new List<string>();

        // 🔹 ZMĚNA: Celý blok je v try...catch, aby odchytil pády (např. licence, Rich Data)
        try
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // ... (kód pro mazání a tvorbu 'temp' složky zůstává stejný) ...
            var tempRoot = Path.Combine(_env.WebRootPath, "temp");
            if (Directory.Exists(tempRoot))
            {
                try { Directory.Delete(tempRoot, true); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nepodařilo se smazat starý obsah složky temp.");
                    warnings.Add("Varování: Nepodařilo se vyčistit dočasnou složku.");
                }
            }
            Directory.CreateDirectory(tempRoot);

            var tempGuid = Guid.NewGuid().ToString();
            var tempFolder = Path.Combine(tempRoot, tempGuid);
            Directory.CreateDirectory(tempFolder);

            var imported = new List<PhotoRecord>();

            using var ms = new MemoryStream();
            await excelFile.CopyToAsync(ms);
            ms.Position = 0;

            // ... (kód pro rozbalení ZIPu zůstává stejný) ...
            using (var zip = new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read, false))
            {
                foreach (var entry in zip.Entries)
                {
                    var fullPath = Path.Combine(tempFolder, entry.FullName);
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        using var s = entry.Open();
                        using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                        await s.CopyToAsync(fs);
                    }
                }
            }

            // ... (kód pro načtení mediaList zůstává stejný) ...
            var mediaFolder = Path.Combine(tempFolder, "xl", "media");
            var mediaList = new List<string>();
            if (Directory.Exists(mediaFolder))
            {
                mediaList = Directory
                    .GetFiles(mediaFolder, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (int.TryParse(new string(name.Where(char.IsDigit).ToArray()), out int num))
                            return num;
                        return int.MaxValue;
                    })
                    .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _logger.LogInformation($"📂 Načteno {mediaList.Count} obrázků.");
            }
            else
            {
                _logger.LogWarning($"⚠️ Složka {mediaFolder} neexistuje.");
                warnings.Add("V Excelu nebyly nalezeny žádné obrázky.");
            }


            // 🔹 5) Načtení dat z Excelu
            ms.Position = 0;

            // ❗️ DŮLEŽITÉ: Ujistěte se, že máte v Program.cs nastavenou licenci!
            // ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(ms);
            var ws = package.Workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                // 🔹 ZMĚNA: Vrací JSON chybu
                return BadRequest(new { success = false, message = "Soubor neobsahuje žádný list." });
            }

            int startRow = 2;
            int endRow = ws.Dimension?.End.Row ?? 1;
            int colNotes = 12;
            int imageIndex = 0;

            for (int row = startRow; row <= endRow; row++)
            {
                // 🔹 ZMĚNA: Čtení textu pro kontrolu prázdného řádku
                bool rowEmpty = Enumerable.Range(1, colNotes)
                    .All(c => string.IsNullOrWhiteSpace(ws.Cells[row, c].Value?.ToString()));
                if (rowEmpty) continue;

                // 🔹 ZMĚNA: Použití .Value?.ToString() ?? "" pro VŠECHNY sloupce
                // Tím se opraví chyba "Rich Data" (Preserve) A chyby při ukládání NULL do databáze.
                var rec = new PhotoRecord
                {
                    Position = ws.Cells[row, 1].Value?.ToString()?.Trim() ?? "",
                    ExternalId = ws.Cells[row, 2].Value?.ToString()?.Trim() ?? "",
                    Supplier = ws.Cells[row, 3].Value?.ToString()?.Trim() ?? "",
                    OriginalName = ws.Cells[row, 4].Value?.ToString()?.Trim() ?? "",
                    Material = ws.Cells[row, 5].Value?.ToString()?.Trim() ?? "",
                    Form = ws.Cells[row, 6].Value?.ToString()?.Trim() ?? "",
                    Filler = ws.Cells[row, 7].Value?.ToString()?.Trim() ?? "",
                    Color = ws.Cells[row, 8].Value?.ToString()?.Trim() ?? "",
                    Description = ws.Cells[row, 9].Value?.ToString()?.Trim() ?? "",
                    MonthlyQuantity = ws.Cells[row, 10].Value?.ToString()?.Trim() ?? "",
                    Mfi = ws.Cells[row, 11].Value?.ToString()?.Trim() ?? "",
                    Notes = ws.Cells[row, colNotes].Value?.ToString()?.Trim() ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // ... (logika pro přiřazení obrázků zůstává stejná) ...
                if (imageIndex < mediaList.Count)
                {
                    var sourcePath = mediaList[imageIndex++];
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(sourcePath)}";
                    var destPath = Path.Combine(uploadsFolder, fileName);
                    System.IO.File.Copy(sourcePath, destPath, true);

                    rec.PhotoFileName = fileName;
                    rec.ImagePath = "/uploads/" + fileName;
                }
                else
                {
                    var defaultFileName = "no-image.png";
                    rec.PhotoFileName = defaultFileName;
                    rec.ImagePath = "/uploads/" + defaultFileName;
                }

                imported.Add(rec);
            }

            imported.Reverse();

            if (imported.Any())
            {
                _context.Photos.AddRange(imported);
                await _context.SaveChangesAsync();
            }

            // 🔹 ZMĚNA: Místo Redirect vracíme JSON o úspěchu
            return Ok(new
            {
                success = true,
                message = $"{imported.Count} záznamů úspěšně importováno.",
                warnings = warnings,
                redirectUrl = Url.Action("Index", "Photos") // URL pro JavaScript, kam přesměrovat
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selhání importu Excelu.");
            // 🔹 ZMĚNA: V případě pádu vracíme JSON chybu 500
            return StatusCode(500, new
            {
                success = false,
                message = "Při importu došlo k chybě: " + ex.Message
            });
        }
    }
}