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
    public async Task<IActionResult> Index(string search, List<string> supplier, List<string> material, List<string> type, List<string> color, List<string> name, List<string> position, List<string> filler, List<string> mfi, List<string> monthlyQuantity, List<string> form)
    {
        // Detekce mobilu, pokud ano, přesměruj na mobilní verzi se všemi parametry
        if (Request.Headers["User-Agent"].ToString().Contains("Mobile"))
        {
            return RedirectToAction("Index_phone", new { search, supplier, material, type, color, name, position, filler, form, mfi, monthlyQuantity });
        }

        IQueryable<PhotoRecord> q = _context.Photos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(s)) ||
                (p.OriginalName != null && p.OriginalName.ToLower().Contains(s)) ||
                (p.Description != null && p.Description.ToLower().Contains(s)) ||
                (p.Notes != null && p.Notes.ToLower().Contains(s)) ||
                (p.Code != null && p.Code.ToLower().Contains(s))
            );
        }

        // --- ZMĚNA: Logika filtrování upravená pro vícenásobný výběr ---
        if (supplier != null && supplier.Any()) q = q.Where(p => supplier.Contains(p.Supplier));
        if (material != null && material.Any()) q = q.Where(p => material.Contains(p.Material));
        if (type != null && type.Any()) q = q.Where(p => type.Contains(p.Type));
        if (color != null && color.Any()) q = q.Where(p => color.Contains(p.Color));
        if (name != null && name.Any()) q = q.Where(p => name.Contains(p.Name));
        if (position != null && position.Any()) q = q.Where(p => position.Contains(p.Position));
        if (filler != null && filler.Any()) q = q.Where(p => filler.Contains(p.Filler));
        // --- PŘIDÁNO: Filtrování pro nové parametry ---
        if (form != null && form.Any()) q = q.Where(p => form.Contains(p.Form));
        if (mfi != null && mfi.Any()) q = q.Where(p => mfi.Contains(p.Mfi));
        if (monthlyQuantity != null && monthlyQuantity.Any()) q = q.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));


        var items = await q.OrderByDescending(p => p.UpdatedAt).ToListAsync();

        // Optimalizace: Načtení všech dat pro filtry v jednom dotazu
        var allPhotosForFilters = await _context.Photos.AsNoTracking().ToListAsync();

        var vm = new PhotoApp.ViewModels.PhotosIndexViewModel
        {
            Items = items,
            Suppliers = allPhotosForFilters.Select(p => p.Supplier).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Materials = allPhotosForFilters.Select(p => p.Material).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Types = allPhotosForFilters.Select(p => p.Type).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Colors = allPhotosForFilters.Select(p => p.Color).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Names = allPhotosForFilters.Select(p => p.Name).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Positions = allPhotosForFilters.Select(p => p.Position).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Fillers = allPhotosForFilters.Select(p => p.Filler).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Forms = allPhotosForFilters.Select(p => p.Form).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            MonthlyQuantities = allPhotosForFilters.Select(p => p.MonthlyQuantity).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Mfis = allPhotosForFilters.Select(p => p.Mfi).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),

            Search = search,
            Supplier = supplier,
            Material = material,
            Type = type,
            Color = color,
            // Zde se předpokládá, že ViewModel byl již upraven tak, aby Name byla List<string>
            // Pokud ne, bude potřeba upravit ViewModel. Prozatím `name` vynechávám, aby nedošlo k chybě.
            // Name = name, 
            Position = position,
            Filler = filler,
            Form = form,
            MonthlyQuantity = monthlyQuantity,
            Mfi = mfi
        };

        ViewBag.IsMobile = false;
        return View(vm);
    }
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index_phone(string search, List<string> supplier, List<string> material, List<string> type, List<string> color, List<string> name, List<string> position, List<string> filler, List<string> form, List<string> mfi, List<string> monthlyQuantity, bool forceDesktop = false)
    {
        if (forceDesktop)
        {
            // Přesměrování na desktopovou verzi se všemi filtry
            return RedirectToAction("Index", new { search, supplier, material, type, color, name, position, filler, form, mfi, monthlyQuantity });
        }

        // --- Logika pro mobilní zobrazení ---

        IQueryable<PhotoRecord> q2 = _context.Photos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q2 = q2.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(s)) ||
                (p.OriginalName != null && p.OriginalName.ToLower().Contains(s)) ||
                (p.Description != null && p.Description.ToLower().Contains(s)) ||
                (p.Notes != null && p.Notes.ToLower().Contains(s)) ||
                (p.Code != null && p.Code.ToLower().Contains(s))
            );
        }

        // --- ZMĚNA: Logika filtrování upravená pro vícenásobný výběr ---
        if (supplier != null && supplier.Any()) q2 = q2.Where(p => supplier.Contains(p.Supplier));
        if (material != null && material.Any()) q2 = q2.Where(p => material.Contains(p.Material));
        if (type != null && type.Any()) q2 = q2.Where(p => type.Contains(p.Type));
        if (color != null && color.Any()) q2 = q2.Where(p => color.Contains(p.Color));
        if (name != null && name.Any()) q2 = q2.Where(p => name.Contains(p.Name));
        if (position != null && position.Any()) q2 = q2.Where(p => position.Contains(p.Position));
        if (filler != null && filler.Any()) q2 = q2.Where(p => filler.Contains(p.Filler));
        if (form != null && form.Any()) q2 = q2.Where(p => form.Contains(p.Form));
        if (mfi != null && mfi.Any()) q2 = q2.Where(p => mfi.Contains(p.Mfi));
        if (monthlyQuantity != null && monthlyQuantity.Any()) q2 = q2.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));


        var items2 = await q2.OrderByDescending(p => p.UpdatedAt).ToListAsync();

        // Optimalizace: Načtení všech dat pro filtry v jednom dotazu
        var allPhotosForFilters = await _context.Photos.AsNoTracking().ToListAsync();

        var vm2 = new PhotoApp.ViewModels.PhotosIndexViewModel
        {
            Items = items2,
            // Naplnění seznamů pro filtry
            Suppliers = allPhotosForFilters.Select(p => p.Supplier).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Materials = allPhotosForFilters.Select(p => p.Material).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Types = allPhotosForFilters.Select(p => p.Type).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Colors = allPhotosForFilters.Select(p => p.Color).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Names = allPhotosForFilters.Select(p => p.Name).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Positions = allPhotosForFilters.Select(p => p.Position).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Fillers = allPhotosForFilters.Select(p => p.Filler).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Forms = allPhotosForFilters.Select(p => p.Form).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            MonthlyQuantities = allPhotosForFilters.Select(p => p.MonthlyQuantity).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),
            Mfis = allPhotosForFilters.Select(p => p.Mfi).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList(),

            // Přiřazení aktivních (vybraných) filtrů
            Search = search,
            Supplier = supplier,
            Material = material,
            Type = type,
            Color = color,
            Name = name,
            Position = position,
            Filler = filler,
            Form = form,
            Mfi = mfi,
            MonthlyQuantity = monthlyQuantity
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