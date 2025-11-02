using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PhotoApp.Data;
using PhotoApp.Models;
using System.Text;
using System.Text.Json;

namespace PhotoApp.Controllers;

[Authorize]
public class PhotosController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] PermittedTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };

    public PhotosController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public IActionResult Import()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Index(string search, string supplier, string material, string type, string color, string name, string position, string filler)
    {
        // Detekce mobilu, pokud ano, přesměruj na mobilní verzi
        if (Request.Headers["User-Agent"].ToString().Contains("Mobile"))
        {
            return RedirectToAction("Index_phone", new { search, supplier, material, type, color, name, position, filler });
        }

        IQueryable<PhotoRecord> q = _context.Photos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                (p.Name != null && EF.Functions.Like(p.Name, $"%{s}%")) ||
                (p.OriginalName != null && EF.Functions.Like(p.OriginalName, $"%{s}%")) ||
                (p.Description != null && EF.Functions.Like(p.Description, $"%{s}%")) ||
                (p.Notes != null && EF.Functions.Like(p.Notes, $"%{s}%")) ||
                (p.Code != null && EF.Functions.Like(p.Code, $"%{s}%"))
            );
        }
        if (!string.IsNullOrWhiteSpace(supplier)) q = q.Where(p => p.Supplier == supplier);
        if (!string.IsNullOrWhiteSpace(material)) q = q.Where(p => p.Material == material);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(p => p.Type == type);
        if (!string.IsNullOrWhiteSpace(color)) q = q.Where(p => p.Color == color);
        if (!string.IsNullOrWhiteSpace(name)) q = q.Where(p => p.Name == name);
        if (!string.IsNullOrWhiteSpace(position)) q = q.Where(p => p.Position == position);
        if (!string.IsNullOrWhiteSpace(filler)) q = q.Where(p => p.Filler == filler);

        var items = await q.OrderByDescending(p => p.UpdatedAt).ToListAsync();

        var vm = new PhotoApp.ViewModels.PhotosIndexViewModel
        {
            Items = items,
            Suppliers = await _context.Photos.Where(p => p.Supplier != null).Select(p => p.Supplier).Distinct().OrderBy(x => x).ToListAsync(),
            Materials = await _context.Photos.Where(p => p.Material != null).Select(p => p.Material).Distinct().OrderBy(x => x).ToListAsync(),
            Types = await _context.Photos.Where(p => p.Type != null).Select(p => p.Type).Distinct().OrderBy(x => x).ToListAsync(),
            Colors = await _context.Photos.Where(p => p.Color != null).Select(p => p.Color).Distinct().OrderBy(x => x).ToListAsync(),
            Names = await _context.Photos.Where(p => p.Name != null).Select(p => p.Name).Distinct().OrderBy(x => x).ToListAsync(),
            Positions = await _context.Photos.Where(p => p.Position != null).Select(p => p.Position).Distinct().OrderBy(x => x).ToListAsync(),
            Fillers = await _context.Photos.Where(p => p.Filler != null).Select(p => p.Filler).Distinct().OrderBy(x => x).ToListAsync(),
            Search = search,
            Supplier = supplier,
            Material = material,
            Type = type,
            Color = color,
            Name = name,
            Position = position,
            Filler = filler
        };

        ViewBag.IsMobile = false;
        return View(vm);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index_phone(string search, string supplier, string material, string type, string color, string name, string position, string filler, bool forceDesktop = false)
    {
        if (forceDesktop)
        {
            var q = _context.Photos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(p =>
                    EF.Functions.Like(p.Name, $"%{s}%") ||
                    EF.Functions.Like(p.OriginalName, $"%{s}%") ||
                    EF.Functions.Like(p.Description, $"%{s}%") ||
                    EF.Functions.Like(p.Notes, $"%{s}%") ||
                    EF.Functions.Like(p.Code, $"%{s}%")
                );
            }

            if (!string.IsNullOrWhiteSpace(supplier)) q = q.Where(p => p.Supplier == supplier);
            if (!string.IsNullOrWhiteSpace(material)) q = q.Where(p => p.Material == material);
            if (!string.IsNullOrWhiteSpace(type)) q = q.Where(p => p.Type == type);
            if (!string.IsNullOrWhiteSpace(color)) q = q.Where(p => p.Color == color);
            if (!string.IsNullOrWhiteSpace(name)) q = q.Where(p => p.Name == name);
            if (!string.IsNullOrWhiteSpace(position)) q = q.Where(p => p.Position == position);
            if (!string.IsNullOrWhiteSpace(filler)) q = q.Where(p => p.Filler == filler);

            var items = await q.OrderByDescending(p => p.UpdatedAt).ToListAsync();

            var vm = new PhotoApp.ViewModels.PhotosIndexViewModel
            {
                Items = items,
                Suppliers = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Supplier)).Select(p => p.Supplier).Distinct().OrderBy(x => x).ToListAsync(),
                Materials = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Material)).Select(p => p.Material).Distinct().OrderBy(x => x).ToListAsync(),
                Types = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Type)).Select(p => p.Type).Distinct().OrderBy(x => x).ToListAsync(),
                Colors = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Color)).Select(p => p.Color).Distinct().OrderBy(x => x).ToListAsync(),
                Names = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Name)).Select(p => p.Name).Distinct().OrderBy(x => x).ToListAsync(),
                Positions = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Position)).Select(p => p.Position).Distinct().OrderBy(x => x).ToListAsync(),
                Fillers = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Filler)).Select(p => p.Filler).Distinct().OrderBy(x => x).ToListAsync(),
                Search = search,
                Supplier = supplier,
                Material = material,
                Type = type,
                Color = color,
                Name = name,
                Position = position,
                Filler = filler
            };

            ViewBag.IsMobile = false;
            return View(vm);
        }

        var q2 = _context.Photos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q2 = q2.Where(p =>
                EF.Functions.Like(p.Name, $"%{s}%") ||
                EF.Functions.Like(p.OriginalName, $"%{s}%") ||
                EF.Functions.Like(p.Description, $"%{s}%") ||
                EF.Functions.Like(p.Notes, $"%{s}%") ||
                EF.Functions.Like(p.Code, $"%{s}%")
            );
        }

        if (!string.IsNullOrWhiteSpace(supplier)) q2 = q2.Where(p => p.Supplier == supplier);
        if (!string.IsNullOrWhiteSpace(material)) q2 = q2.Where(p => p.Material == material);
        if (!string.IsNullOrWhiteSpace(type)) q2 = q2.Where(p => p.Type == type);
        if (!string.IsNullOrWhiteSpace(color)) q2 = q2.Where(p => p.Color == color);
        if (!string.IsNullOrWhiteSpace(name)) q2 = q2.Where(p => p.Name == name);
        if (!string.IsNullOrWhiteSpace(position)) q2 = q2.Where(p => p.Position == position);
        if (!string.IsNullOrWhiteSpace(filler)) q2 = q2.Where(p => p.Filler == filler);

        var items2 = await q2.OrderByDescending(p => p.UpdatedAt).ToListAsync();

        var vm2 = new PhotoApp.ViewModels.PhotosIndexViewModel
        {
            Items = items2,
            Suppliers = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Supplier)).Select(p => p.Supplier).Distinct().OrderBy(x => x).ToListAsync(),
            Materials = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Material)).Select(p => p.Material).Distinct().OrderBy(x => x).ToListAsync(),
            Types = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Type)).Select(p => p.Type).Distinct().OrderBy(x => x).ToListAsync(),
            Colors = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Color)).Select(p => p.Color).Distinct().OrderBy(x => x).ToListAsync(),
            Names = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Name)).Select(p => p.Name).Distinct().OrderBy(x => x).ToListAsync(),
            Positions = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Position)).Select(p => p.Position).Distinct().OrderBy(x => x).ToListAsync(),
            Fillers = await _context.Photos.Where(p => !string.IsNullOrEmpty(p.Filler)).Select(p => p.Filler).Distinct().OrderBy(x => x).ToListAsync(),
            Search = search,
            Supplier = supplier,
            Material = material,
            Type = type,
            Color = color,
            Name = name,
            Position = position,
            Filler = filler
        };

        ViewBag.IsMobile = true;
        return View("Index_phone", vm2);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PhotoRecord photoModel, IFormFile? PhotoFile, List<IFormFile>? AdditionalPhotoFiles)
    {
        if (!ModelState.IsValid)
            return View(photoModel);

        string? savedPath = null;

        // Zpracování hlavní fotky
        if (PhotoFile != null && PhotoFile.Length > 0)
        {
            if (PhotoFile.Length > MaxFileSize)
            {
                ModelState.AddModelError("PhotoFile", "Soubor je příliš velký.");
                return View(photoModel);
            }

            if (!PermittedTypes.Contains(PhotoFile.ContentType))
            {
                ModelState.AddModelError("PhotoFile", "Nepodporovaný typ souboru.");
                return View(photoModel);
            }

            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            var fileName = Guid.NewGuid() + Path.GetExtension(PhotoFile.FileName);
            var path = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await PhotoFile.CopyToAsync(stream);
            }

            savedPath = "/uploads/" + fileName;
        }

        // Zpracování dodatečných fotek
        var additionalPaths = new List<string>();
        if (AdditionalPhotoFiles != null && AdditionalPhotoFiles.Any(f => f.Length > 0))
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            foreach (var file in AdditionalPhotoFiles)
            {
                if (file.Length > 0)
                {
                    if (file.Length > MaxFileSize)
                    {
                        ModelState.AddModelError("AdditionalPhotoFiles", $"Soubor {file.FileName} je příliš velký.");
                        continue;
                    }

                    if (!PermittedTypes.Contains(file.ContentType))
                    {
                        ModelState.AddModelError("AdditionalPhotoFiles", $"Soubor {file.FileName} má nepodporovaný formát.");
                        continue;
                    }

                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var path = Path.Combine(uploads, fileName);

                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    additionalPaths.Add("/uploads/" + fileName);
                }
            }
        }

        var photo = new PhotoRecord
        {
            Position = photoModel.Position,
            ExternalId = photoModel.ExternalId,
            OriginalName = photoModel.OriginalName ?? "",
            Material = photoModel.Material,
            Form = photoModel.Form,
            Filler = photoModel.Filler,
            Color = photoModel.Color,
            Mfi = photoModel.Mfi,
            MonthlyQuantity = photoModel.MonthlyQuantity,
            Name = string.IsNullOrWhiteSpace(photoModel.Name) ? "Unnamed" : photoModel.Name,
            Code = photoModel.Code ?? "",
            Type = photoModel.Type ?? "",
            Supplier = photoModel.Supplier ?? "",
            Description = photoModel.Description ?? "",
            Notes = photoModel.Notes ?? "",
            PhotoPath = savedPath ?? photoModel.PhotoPath,
            ImagePath = savedPath ?? photoModel.ImagePath,
            AdditionalPhotos = string.Join(";", additionalPaths), // Uložit dodatečné fotky
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Add(photo);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var photo = await _context.Photos.FindAsync(id);
        if (photo == null)
            return NotFound();

        return View(photo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Edit(int id, PhotoRecord photoModel, IFormFile? PhotoFile, List<IFormFile>? AdditionalPhotoFiles)
    {
        if (id != photoModel.Id)
            return NotFound();

        if (!ModelState.IsValid)
            return View(photoModel);

        try
        {
            var existing = await _context.Photos.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();

            // zpracování hlavní fotky (JEN POKUD JE NOVÁ)
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                if (PhotoFile.Length > MaxFileSize)
                {
                    ModelState.AddModelError("PhotoFile", "Soubor je příliš velký.");
                    return View(photoModel);
                }

                if (!PermittedTypes.Contains(PhotoFile.ContentType))
                {
                    ModelState.AddModelError("PhotoFile", "Nepodporovaný typ souboru.");
                    return View(photoModel);
                }

                var uploads = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(PhotoFile.FileName);
                var path = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await PhotoFile.CopyToAsync(stream);
                }

                // Smazat starou hlavní fotku
                if (!string.IsNullOrEmpty(existing.PhotoPath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, existing.PhotoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                existing.PhotoPath = "/uploads/" + fileName;
                existing.ImagePath = "/uploads/" + fileName;
            }
            // ❌ POKUD NENÍ NOVÁ FOTKA, PONECHAT STAROU (NIC NEDĚLAT)

            // *** ZPRACOVÁNÍ DODATEČNÝCH FOTEK ***
            if (AdditionalPhotoFiles != null && AdditionalPhotoFiles.Any(f => f.Length > 0))
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var additionalPaths = new List<string>();

                // *** ZACHOVAT STÁVAJÍCÍ DODATEČNÉ FOTKY ***
                if (!string.IsNullOrWhiteSpace(existing.AdditionalPhotos))
                {
                    additionalPaths.AddRange(
                        existing.AdditionalPhotos.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                    );
                }

                // *** PŘIDAT NOVÉ DODATEČNÉ FOTKY ***
                foreach (var file in AdditionalPhotoFiles)
                {
                    if (file.Length > 0)
                    {
                        if (file.Length > MaxFileSize)
                        {
                            ModelState.AddModelError("AdditionalPhotoFiles", $"Soubor {file.FileName} je příliš velký.");
                            continue;
                        }

                        if (!PermittedTypes.Contains(file.ContentType))
                        {
                            ModelState.AddModelError("AdditionalPhotoFiles", $"Soubor {file.FileName} má nepodporovaný formát.");
                            continue;
                        }

                        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                        var path = Path.Combine(uploads, fileName);

                        using (var stream = new FileStream(path, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        additionalPaths.Add("/uploads/" + fileName);
                    }
                }

                // Uložit všechny cesty (staré + nové)
                existing.AdditionalPhotos = string.Join(";", additionalPaths);
            }

            // *** AKTUALIZACE OSTATNÍCH POLÍ (BEZ FOTEK!) ***
            existing.Position = photoModel.Position;
            existing.ExternalId = photoModel.ExternalId;
            existing.OriginalName = photoModel.OriginalName;
            existing.Material = photoModel.Material;
            existing.Form = photoModel.Form;
            existing.Filler = photoModel.Filler;
            existing.Color = photoModel.Color;
            existing.Mfi = photoModel.Mfi;
            existing.MonthlyQuantity = photoModel.MonthlyQuantity;
            existing.Name = photoModel.Name;
            existing.Code = photoModel.Code;
            existing.Type = photoModel.Type;
            existing.Supplier = photoModel.Supplier;
            existing.Description = photoModel.Description;
            existing.Notes = photoModel.Notes;
            // ❌ NEAKTUALIZOVAT ImagePath z photoModel!
            // ❌ NEAKTUALIZOVAT PhotoPath z photoModel!
            // ❌ NEAKTUALIZOVAT AdditionalPhotos z photoModel!
            existing.UpdatedAt = DateTime.UtcNow;

            _context.Update(existing);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Photos.Any(e => e.Id == photoModel.Id))
                return NotFound();
            else
                throw;
        }
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    [AllowAnonymous]
    [Route("diag/dbinfo")]
    public async Task<IActionResult> DiagDbInfo([FromServices] AppDbContext ctx)
    {
        var conn = ctx.Database.GetDbConnection();
        var connStr = conn?.ConnectionString ?? "(no connection string)";
        var dataSource = "(unknown)";
        try
        {
            dataSource = connStr.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
                ? connStr
                : Path.Combine(AppContext.BaseDirectory, "photoapp.db");
        }
        catch { }

        var count = 0;
        try { count = await ctx.Photos.CountAsync(); } catch (Exception ex) { return Problem(ex.Message); }

        return Ok(new { connStr, dataSource, count });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var photo = await _context.Photos.FirstOrDefaultAsync(m => m.Id == id);
        if (photo == null) return NotFound();

        return View(photo);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var photo = await _context.Photos.FirstOrDefaultAsync(m => m.Id == id);
        if (photo == null) return NotFound();

        return View(photo);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var photo = await _context.Photos.FindAsync(id);
        if (photo != null)
        {
            if (!string.IsNullOrEmpty(photo.PhotoPath))
            {
                var filePath = Path.Combine(_env.WebRootPath, photo.PhotoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Smazat dodatečné fotky
            if (!string.IsNullOrEmpty(photo.AdditionalPhotos))
            {
                var additionalPaths = photo.AdditionalPhotos.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in additionalPaths)
                {
                    var filePath = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }

            _context.Photos.Remove(photo);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv()
    {
        var data = await _context.Photos.OrderByDescending(x => x.UpdatedAt).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id;Name;Code;Type;Supplier;Notes;PhotoPath;UpdatedAt");

        foreach (var p in data)
        {
            sb.AppendLine($"{p.Id};{p.Name};{p.Code};{p.Type};{p.Supplier};{p.Notes};{p.PhotoPath};{p.UpdatedAt:yyyy-MM-dd HH:mm}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "vzorky.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportJson()
    {
        var data = await _context.Photos.OrderByDescending(x => x.UpdatedAt).ToListAsync();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", "vzorky.json");
    }

    [HttpGet]
    public async Task<IActionResult> ExportZip()
    {
        var photos = await _context.Photos.OrderBy(p => p.Id).ToListAsync();

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Vzorky");

        var headers = new[]
        {
            "Position","ExternalId","Supplier","OriginalName",
            "Material","Form","Filler","Color",
            "Description","MonthlyQuantity","MFI","Notes","Photo"
        };
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
        }

        for (int i = 0; i < photos.Count; i++)
        {
            var p = photos[i];
            int row = i + 2;

            ws.Cells[row, 1].Value = p.Position;
            ws.Cells[row, 2].Value = p.ExternalId;
            ws.Cells[row, 3].Value = p.Supplier;
            ws.Cells[row, 4].Value = p.OriginalName;
            ws.Cells[row, 5].Value = p.Material;
            ws.Cells[row, 6].Value = p.Form;
            ws.Cells[row, 7].Value = p.Filler;
            ws.Cells[row, 8].Value = p.Color;
            ws.Cells[row, 9].Value = p.Description;
            ws.Cells[row, 10].Value = p.MonthlyQuantity;
            ws.Cells[row, 11].Value = p.Mfi;
            ws.Cells[row, 12].Value = p.Notes;

            if (!string.IsNullOrEmpty(p.ImagePath))
            {
                var relative = p.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(_env.WebRootPath, relative);
                if (System.IO.File.Exists(fullPath))
                {
                    using var stream = System.IO.File.OpenRead(fullPath);
                    var pic = ws.Drawings.AddPicture($"img_{row}", stream);

                    pic.From.Row = row - 1;
                    pic.From.Column = 12;
                    pic.SetSize(80, 80);
                    ws.Row(row).Height = 60;
                }
            }
        }

        ws.Cells[1, 1, photos.Count + 1, headers.Length].AutoFitColumns();
        ws.Column(13).Width = 15;

        var bytes = package.GetAsByteArray();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "vzorky_s_obrazky.xlsx");
    }

    // *** NOVÁ AKCE PRO SMAZÁNÍ JEDNÉ DODATEČNÉ FOTKY ***
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdditionalPhoto(int id, string photoPath)
    {
        var photo = await _context.Photos.FindAsync(id);
        if (photo == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(photo.AdditionalPhotos))
            return RedirectToAction(nameof(Details), new { id });

        var paths = photo.AdditionalPhotos.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (paths.Contains(photoPath))
        {
            var filePath = Path.Combine(_env.WebRootPath, photoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            paths.Remove(photoPath);
            photo.AdditionalPhotos = string.Join(";", paths);
            photo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id });
    }


    // *** NOVÁ AKCE PRO RYCHLÉ NAHRÁNÍ DODATEČNÝCH FOTEK Z DETAILU ***
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAdditionalPhotos(int id, List<IFormFile>? AdditionalPhotoFiles)
    {
        var photo = await _context.Photos.FindAsync(id);
        if (photo == null)
            return NotFound();

        if (AdditionalPhotoFiles == null || !AdditionalPhotoFiles.Any(f => f.Length > 0))
        {
            TempData["Error"] = "Nevybrali jste žádné fotky.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var uploads = Path.Combine(_env.WebRootPath, "uploads");
        if (!Directory.Exists(uploads))
            Directory.CreateDirectory(uploads);

        var additionalPaths = new List<string>();

        // Zachovat stávající fotky
        if (!string.IsNullOrWhiteSpace(photo.AdditionalPhotos))
        {
            additionalPaths.AddRange(
                photo.AdditionalPhotos.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
            );
        }

        // Přidat nové fotky
        foreach (var file in AdditionalPhotoFiles)
        {
            if (file.Length > 0)
            {
                if (file.Length > MaxFileSize)
                {
                    TempData["Error"] = $"Soubor {file.FileName} je příliš velký (max 5 MB).";
                    continue;
                }

                if (!PermittedTypes.Contains(file.ContentType))
                {
                    TempData["Error"] = $"Soubor {file.FileName} má nepodporovaný formát.";
                    continue;
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                var path = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                additionalPaths.Add("/uploads/" + fileName);
            }
        }

        photo.AdditionalPhotos = string.Join(";", additionalPaths);
        photo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Fotky byly úspěšně nahrány!";
        return RedirectToAction(nameof(Details), new { id });
    }

}