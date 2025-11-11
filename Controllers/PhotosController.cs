using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PhotoApp.Data;
using PhotoApp.Models;
using System.Linq.Expressions; // Přidáno pro Expression
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

    // --- HLAVNÍ METODA PRO ZOBRAZENÍ (DESKTOP) ---
    [Authorize]
    public async Task<IActionResult> Index(string search, List<string> supplier, List<string> material, List<string> type, List<string> color, List<string> name, List<string> position, List<string> filler, List<string> mfi, List<string> monthlyQuantity, List<string> form)
    {
        // Detekce mobilu, pokud ano, přesměruj na mobilní verzi se všemi parametry
        if (Request.Headers["User-Agent"].ToString().Contains("Mobile"))
        {
            return RedirectToAction("Index_phone", new { search, supplier, material, type, color, name, position, filler, form, mfi, monthlyQuantity });
        }

        // Zavolání nové sdílené metody
        var vm = await GetFilteredViewModel(search, supplier, material, type, color, name, position, filler, mfi, monthlyQuantity, form);

        ViewBag.IsMobile = false;
        return View(vm);
    }

    // --- HLAVNÍ METODA PRO ZOBRAZENÍ (MOBIL) ---
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index_phone(string search, List<string> supplier, List<string> material, List<string> type, List<string> color, List<string> name, List<string> position, List<string> filler, List<string> form, List<string> mfi, List<string> monthlyQuantity, bool forceDesktop = false)
    {
        if (forceDesktop)
        {
            // Přesměrování na desktopovou verzi se všemi filtry
            return RedirectToAction("Index", new { search, supplier, material, type, color, name, position, filler, form, mfi, monthlyQuantity });
        }

        // Zavolání nové sdílené metody
        var vm = await GetFilteredViewModel(search, supplier, material, type, color, name, position, filler, mfi, monthlyQuantity, form);

        ViewBag.IsMobile = true;
        return View("Index_phone", vm);
    }

    // --- NOVÁ SOUKROMÁ METODA PRO VEŠKEROU LOGIKU FILTROVÁNÍ ---
    private async Task<PhotoApp.ViewModels.PhotosIndexViewModel> GetFilteredViewModel(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form)
    {
        // 1. Základní "před-filtr" (týká se vyhledávání)
        //    Tento dotaz se aplikuje na VŠECHNY následující dotazy.
        IQueryable<PhotoRecord> baseQuery = _context.Photos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            baseQuery = baseQuery.Where(p =>
                (p.Name != null && p.Name.ToLower().Contains(s)) ||
                (p.OriginalName != null && p.OriginalName.ToLower().Contains(s)) ||
                (p.Description != null && p.Description.ToLower().Contains(s)) ||
                (p.Notes != null && p.Notes.ToLower().Contains(s)) ||
                (p.Code != null && p.Code.ToLower().Contains(s))
            );
        }

        // 2. Vytvoření oddělených IQueryable pro každý filtr + pro výsledné položky
        //    Všechny začínají s již aplikovaným 'search' filtrem.
        var itemsQuery = baseQuery;
        var supplierQuery = baseQuery;
        var materialQuery = baseQuery;
        var typeQuery = baseQuery;
        var colorQuery = baseQuery;
        var nameQuery = baseQuery;
        var positionQuery = baseQuery;
        var fillerQuery = baseQuery;
        var formQuery = baseQuery;
        var mfiQuery = baseQuery;
        var monthlyQuantityQuery = baseQuery;

        // 3. Křížová aplikace filtrů
        //    Každý aktivní filtr zužuje:
        //    a) Finální 'itemsQuery'
        //    b) VŠECHNY OSTATNÍ dotazy na filtry (ale ne sám sebe)

        if (supplier != null && supplier.Any())
        {
            itemsQuery = itemsQuery.Where(p => supplier.Contains(p.Supplier));
            // supplierQuery zůstává (nezúžíme ho)
            materialQuery = materialQuery.Where(p => supplier.Contains(p.Supplier));
            typeQuery = typeQuery.Where(p => supplier.Contains(p.Supplier));
            colorQuery = colorQuery.Where(p => supplier.Contains(p.Supplier));
            nameQuery = nameQuery.Where(p => supplier.Contains(p.Supplier));
            positionQuery = positionQuery.Where(p => supplier.Contains(p.Supplier));
            fillerQuery = fillerQuery.Where(p => supplier.Contains(p.Supplier));
            formQuery = formQuery.Where(p => supplier.Contains(p.Supplier));
            mfiQuery = mfiQuery.Where(p => supplier.Contains(p.Supplier));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => supplier.Contains(p.Supplier));
        }

        if (material != null && material.Any())
        {
            itemsQuery = itemsQuery.Where(p => material.Contains(p.Material));
            supplierQuery = supplierQuery.Where(p => material.Contains(p.Material));
            // materialQuery zůstává
            typeQuery = typeQuery.Where(p => material.Contains(p.Material));
            colorQuery = colorQuery.Where(p => material.Contains(p.Material));
            nameQuery = nameQuery.Where(p => material.Contains(p.Material));
            positionQuery = positionQuery.Where(p => material.Contains(p.Material));
            fillerQuery = fillerQuery.Where(p => material.Contains(p.Material));
            formQuery = formQuery.Where(p => material.Contains(p.Material));
            mfiQuery = mfiQuery.Where(p => material.Contains(p.Material));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => material.Contains(p.Material));
        }

        if (type != null && type.Any())
        {
            itemsQuery = itemsQuery.Where(p => type.Contains(p.Type));
            supplierQuery = supplierQuery.Where(p => type.Contains(p.Type));
            materialQuery = materialQuery.Where(p => type.Contains(p.Type));
            // typeQuery zůstává
            colorQuery = colorQuery.Where(p => type.Contains(p.Type));
            nameQuery = nameQuery.Where(p => type.Contains(p.Type));
            positionQuery = positionQuery.Where(p => type.Contains(p.Type));
            fillerQuery = fillerQuery.Where(p => type.Contains(p.Type));
            formQuery = formQuery.Where(p => type.Contains(p.Type));
            mfiQuery = mfiQuery.Where(p => type.Contains(p.Type));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => type.Contains(p.Type));
        }

        if (color != null && color.Any())
        {
            itemsQuery = itemsQuery.Where(p => color.Contains(p.Color));
            supplierQuery = supplierQuery.Where(p => color.Contains(p.Color));
            materialQuery = materialQuery.Where(p => color.Contains(p.Color));
            typeQuery = typeQuery.Where(p => color.Contains(p.Color));
            // colorQuery zůstává
            nameQuery = nameQuery.Where(p => color.Contains(p.Color));
            positionQuery = positionQuery.Where(p => color.Contains(p.Color));
            fillerQuery = fillerQuery.Where(p => color.Contains(p.Color));
            formQuery = formQuery.Where(p => color.Contains(p.Color));
            mfiQuery = mfiQuery.Where(p => color.Contains(p.Color));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => color.Contains(p.Color));
        }

        if (name != null && name.Any())
        {
            itemsQuery = itemsQuery.Where(p => name.Contains(p.Name));
            supplierQuery = supplierQuery.Where(p => name.Contains(p.Name));
            materialQuery = materialQuery.Where(p => name.Contains(p.Name));
            typeQuery = typeQuery.Where(p => name.Contains(p.Name));
            colorQuery = colorQuery.Where(p => name.Contains(p.Name));
            // nameQuery zůstává
            positionQuery = positionQuery.Where(p => name.Contains(p.Name));
            fillerQuery = fillerQuery.Where(p => name.Contains(p.Name));
            formQuery = formQuery.Where(p => name.Contains(p.Name));
            mfiQuery = mfiQuery.Where(p => name.Contains(p.Name));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => name.Contains(p.Name));
        }

        if (position != null && position.Any())
        {
            itemsQuery = itemsQuery.Where(p => position.Contains(p.Position));
            supplierQuery = supplierQuery.Where(p => position.Contains(p.Position));
            materialQuery = materialQuery.Where(p => position.Contains(p.Position));
            typeQuery = typeQuery.Where(p => position.Contains(p.Position));
            colorQuery = colorQuery.Where(p => position.Contains(p.Position));
            nameQuery = nameQuery.Where(p => position.Contains(p.Position));
            // positionQuery zůstává
            fillerQuery = fillerQuery.Where(p => position.Contains(p.Position));
            formQuery = formQuery.Where(p => position.Contains(p.Position));
            mfiQuery = mfiQuery.Where(p => position.Contains(p.Position));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => position.Contains(p.Position));
        }

        if (filler != null && filler.Any())
        {
            itemsQuery = itemsQuery.Where(p => filler.Contains(p.Filler));
            supplierQuery = supplierQuery.Where(p => filler.Contains(p.Filler));
            materialQuery = materialQuery.Where(p => filler.Contains(p.Filler));
            typeQuery = typeQuery.Where(p => filler.Contains(p.Filler));
            colorQuery = colorQuery.Where(p => filler.Contains(p.Filler));
            nameQuery = nameQuery.Where(p => filler.Contains(p.Filler));
            positionQuery = positionQuery.Where(p => filler.Contains(p.Filler));
            // fillerQuery zůstává
            formQuery = formQuery.Where(p => filler.Contains(p.Filler));
            mfiQuery = mfiQuery.Where(p => filler.Contains(p.Filler));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => filler.Contains(p.Filler));
        }

        if (form != null && form.Any())
        {
            itemsQuery = itemsQuery.Where(p => form.Contains(p.Form));
            supplierQuery = supplierQuery.Where(p => form.Contains(p.Form));
            materialQuery = materialQuery.Where(p => form.Contains(p.Form));
            typeQuery = typeQuery.Where(p => form.Contains(p.Form));
            colorQuery = colorQuery.Where(p => form.Contains(p.Form));
            nameQuery = nameQuery.Where(p => form.Contains(p.Form));
            positionQuery = positionQuery.Where(p => form.Contains(p.Form));
            fillerQuery = fillerQuery.Where(p => form.Contains(p.Form));
            // formQuery zůstává
            mfiQuery = mfiQuery.Where(p => form.Contains(p.Form));
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => form.Contains(p.Form));
        }

        if (mfi != null && mfi.Any())
        {
            itemsQuery = itemsQuery.Where(p => mfi.Contains(p.Mfi));
            supplierQuery = supplierQuery.Where(p => mfi.Contains(p.Mfi));
            materialQuery = materialQuery.Where(p => mfi.Contains(p.Mfi));
            typeQuery = typeQuery.Where(p => mfi.Contains(p.Mfi));
            colorQuery = colorQuery.Where(p => mfi.Contains(p.Mfi));
            nameQuery = nameQuery.Where(p => mfi.Contains(p.Mfi));
            positionQuery = positionQuery.Where(p => mfi.Contains(p.Mfi));
            fillerQuery = fillerQuery.Where(p => mfi.Contains(p.Mfi));
            formQuery = formQuery.Where(p => mfi.Contains(p.Mfi));
            // mfiQuery zůstává
            monthlyQuantityQuery = monthlyQuantityQuery.Where(p => mfi.Contains(p.Mfi));
        }

        if (monthlyQuantity != null && monthlyQuantity.Any())
        {
            itemsQuery = itemsQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            supplierQuery = supplierQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            materialQuery = materialQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            typeQuery = typeQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            colorQuery = colorQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            nameQuery = nameQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            positionQuery = positionQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            fillerQuery = fillerQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            formQuery = formQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            mfiQuery = mfiQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));
            // monthlyQuantityQuery zůstává
        }


        // 4. Asynchronní spuštění VŠECH dotazů
        //    Spustíme dotazy na seznamy filtrů paralelně pro efektivitu

        var itemsTask = itemsQuery
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        // Pomocná lokální funkce pro zjednodušení dotazů na filtry
        static Task<List<string>> GetFilterOptions(IQueryable<PhotoRecord> query, Expression<Func<PhotoRecord, string>> selector)
        {
            return query
                .Select(selector)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }

        // Paralelní spuštění všech dotazů na filtry
        var suppliersTask = GetFilterOptions(supplierQuery, p => p.Supplier);
        var materialsTask = GetFilterOptions(materialQuery, p => p.Material);
        var typesTask = GetFilterOptions(typeQuery, p => p.Type);
        var colorsTask = GetFilterOptions(colorQuery, p => p.Color);
        var namesTask = GetFilterOptions(nameQuery, p => p.Name);
        var positionsTask = GetFilterOptions(positionQuery, p => p.Position);
        var fillersTask = GetFilterOptions(fillerQuery, p => p.Filler);
        var formsTask = GetFilterOptions(formQuery, p => p.Form);
        var mfisTask = GetFilterOptions(mfiQuery, p => p.Mfi);
        var monthlyQuantitiesTask = GetFilterOptions(monthlyQuantityQuery, p => p.MonthlyQuantity);

        // Počkáme, až se VŠECHNY dotazy dokončí
        await Task.WhenAll(
            itemsTask, suppliersTask, materialsTask, typesTask, colorsTask,
            namesTask, positionsTask, fillersTask, formsTask, mfisTask, monthlyQuantitiesTask
        );

        // 5. Sestavení ViewModelu z výsledků
        var vm = new PhotoApp.ViewModels.PhotosIndexViewModel
        {
            Items = itemsTask.Result,
            Suppliers = suppliersTask.Result,
            Materials = materialsTask.Result,
            Types = typesTask.Result,
            Colors = colorsTask.Result,
            Names = namesTask.Result,
            Positions = positionsTask.Result,
            Fillers = fillersTask.Result,
            Forms = formsTask.Result,
            MonthlyQuantities = monthlyQuantitiesTask.Result,
            Mfis = mfisTask.Result,

            // Uložení aktuálně vybraných hodnot
            Search = search,
            Supplier = supplier,
            Material = material,
            Type = type,
            Color = color,
            Name = name,
            Position = position,
            Filler = filler,
            Form = form,
            MonthlyQuantity = monthlyQuantity,
            Mfi = mfi
        };

        return vm;
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