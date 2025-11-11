using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PhotoApp.Data;
using PhotoApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq.Expressions; // Přidáno pro Expression
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting; // <-- Důležité pro _webHostEnvironment
using System.Net.Http;



namespace PhotoApp.Controllers;

[Authorize]
public class PhotosController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] PermittedTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };
    private readonly IWebHostEnvironment _webHostEnvironment;

    public PhotosController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        //_env = env;
        _webHostEnvironment = webHostEnvironment;
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

    // =========================================================
    // ===                 OPRAVENÁ METODA EXPORT PDF        ===
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> ExportPdf(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form,
        [FromQuery] List<string> columnsToInclude)
    {
        // 1. Získáme vyfiltrovaný dotaz
        var query = GetFilteredQuery(search, supplier, material, type, color, name, position, filler, mfi, monthlyQuantity, form);

        // 2. Spustíme dotaz
        var items = await query.OrderBy(p => p.Id).ToListAsync();

        // 3. Vytvoříme PDF dokument
        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25, Unit.Millimetre);

                // <-- ZMĚNA: Zpět na orientaci na šířku
                page.Size(PageSizes.A4.Landscape());

                // --- Vlastní hlavička s logem a oddělením ---
                page.Header()
                    .PaddingBottom(10) // Odsazení hlavičky od obsahu
                    .Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Levá část: Kontaktní údaje a logo
                            row.RelativeItem(2).Column(c =>
                            {
                                c.Item().Text("odpo s.r.o.")
                                    .SemiBold().FontSize(14);
                                c.Item().Text("Riegrova 59, CZ - 388 01 Blatná")
                                    .FontSize(10);
                                c.Item().Text("Laboratoř/Laboratory: Radomyšl 248, 387 31 Radomyšl")
                                    .FontSize(10);

                                // Cesta k logu
                                // (Zvažte použití IWebHostEnvironment pro spolehlivější cestu)
                                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo.png");

                                if (System.IO.File.Exists(logoPath))
                                {
                                    c.Item().PaddingTop(5)
                                        .MaxHeight(50)  // Omezení výšky kontejneru
                                        .Image(logoPath)
                                        .FitArea();     // Přizpůsobení obrázku kontejneru
                                }
                                else
                                {
                                    c.Item().PaddingTop(5)
                                        .Text("Logo nenalezeno! Zkontrolujte cestu k 'wwwroot/logo.png'")
                                        .FontSize(8).FontColor(Colors.Red.Medium);
                                }
                            });

                            // Pravá část: Detaily exportu (datum, počet)
                            row.RelativeItem(1).Column(c =>
                            {
                                c.Item().AlignRight().Text("Export vzorků")
                                    .SemiBold().FontSize(16);
                                c.Item().AlignRight().Text($"Datum: {DateTime.Now:d. M. yyyy}")
                                    .FontSize(9);
                                c.Item().AlignRight().Text($"Počet: {items.Count}")
                                    .FontSize(9);
                            });
                        });

                        // Horizontální čára pro vizuální oddělení
                        col.Item().PaddingTop(5).BorderBottom(1)
                            .BorderColor(Colors.Grey.Medium);
                    });


                // --- Obsah (tabulka) ---
                page.Content().Table(table =>
                {
                    // Definice sloupců
                    table.ColumnsDefinition(columns =>
                    {
                        if (columnsToInclude.Contains("Id")) columns.ConstantColumn(50);
                        if (columnsToInclude.Contains("Supplier")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("Material")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("OriginalName")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("Color")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("Position")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("Form")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("Filler")) columns.RelativeColumn();
                        if (columnsToInclude.Contains("Mfi")) columns.ConstantColumn(60);
                        if (columnsToInclude.Contains("Notes")) columns.RelativeColumn(2);
                    });

                    // Hlavička tabulky
                    table.Header(header =>
                    {
                        static IContainer HeaderCellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(4);

                        if (columnsToInclude.Contains("Id")) header.Cell().Element(HeaderCellStyle).Text("ID").SemiBold();
                        if (columnsToInclude.Contains("Supplier")) header.Cell().Element(HeaderCellStyle).Text("Supplier").SemiBold();
                        if (columnsToInclude.Contains("Material")) header.Cell().Element(HeaderCellStyle).Text("Material").SemiBold();
                        if (columnsToInclude.Contains("OriginalName")) header.Cell().Element(HeaderCellStyle).Text("Original Name").SemiBold();
                        if (columnsToInclude.Contains("Color")) header.Cell().Element(HeaderCellStyle).Text("Color").SemiBold();
                        if (columnsToInclude.Contains("Position")) header.Cell().Element(HeaderCellStyle).Text("Position").SemiBold();
                        if (columnsToInclude.Contains("Form")) header.Cell().Element(HeaderCellStyle).Text("Form").SemiBold();
                        if (columnsToInclude.Contains("Filler")) header.Cell().Element(HeaderCellStyle).Text("Filler").SemiBold();
                        if (columnsToInclude.Contains("Mfi")) header.Cell().Element(HeaderCellStyle).Text("MFI").SemiBold();
                        if (columnsToInclude.Contains("Notes")) header.Cell().Element(HeaderCellStyle).Text("Notes").SemiBold();
                    });

                    // Data (řádky)
                    static IContainer DataCellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

                    foreach (var item in items)
                    {
                        if (columnsToInclude.Contains("Id")) table.Cell().Element(DataCellStyle).Text(item.Id);
                        if (columnsToInclude.Contains("Supplier")) table.Cell().Element(DataCellStyle).Text(item.Supplier);
                        if (columnsToInclude.Contains("Material")) table.Cell().Element(DataCellStyle).Text(item.Material);
                        if (columnsToInclude.Contains("OriginalName")) table.Cell().Element(DataCellStyle).Text(item.OriginalName);
                        if (columnsToInclude.Contains("Color")) table.Cell().Element(DataCellStyle).Text(item.Color);
                        if (columnsToInclude.Contains("Position")) table.Cell().Element(DataCellStyle).Text(item.Position);
                        if (columnsToInclude.Contains("Form")) table.Cell().Element(DataCellStyle).Text(item.Form);
                        if (columnsToInclude.Contains("Filler")) table.Cell().Element(DataCellStyle).Text(item.Filler);
                        if (columnsToInclude.Contains("Mfi")) table.Cell().Element(DataCellStyle).Text(item.Mfi);
                        if (columnsToInclude.Contains("Notes")) table.Cell().Element(DataCellStyle).Text(item.Notes);
                    }
                });

                // --- Patička ---
                page.Footer()
                    .AlignRight()
                    .Text(x =>
                    {
                        x.Span("Strana ");
                        x.CurrentPageNumber();
                        x.Span(" z ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf();

        // 4. Vrátíme soubor ke stažení
        return File(pdfBytes, "application/pdf", $"Vzorky_Export_{DateTime.Now:yyyy-MM-dd}.pdf");
    }
    // --- KROK A: SDÍLENÁ METODA POUZE PRO FILTROVÁNÍ (pro PDF) ---
    private IQueryable<PhotoRecord> GetFilteredQuery(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form)
    {
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

        // Aplikace filtrů na hlavní dotaz (pro položky)
        if (supplier != null && supplier.Any()) baseQuery = baseQuery.Where(p => supplier.Contains(p.Supplier));
        if (material != null && material.Any()) baseQuery = baseQuery.Where(p => material.Contains(p.Material));
        if (type != null && type.Any()) baseQuery = baseQuery.Where(p => type.Contains(p.Type));
        if (color != null && color.Any()) baseQuery = baseQuery.Where(p => color.Contains(p.Color));
        if (name != null && name.Any()) baseQuery = baseQuery.Where(p => name.Contains(p.Name));
        if (position != null && position.Any()) baseQuery = baseQuery.Where(p => position.Contains(p.Position));
        if (filler != null && filler.Any()) baseQuery = baseQuery.Where(p => filler.Contains(p.Filler));
        if (form != null && form.Any()) baseQuery = baseQuery.Where(p => form.Contains(p.Form));
        if (mfi != null && mfi.Any()) baseQuery = baseQuery.Where(p => mfi.Contains(p.Mfi));
        if (monthlyQuantity != null && monthlyQuantity.Any()) baseQuery = baseQuery.Where(p => monthlyQuantity.Contains(p.MonthlyQuantity));

        return baseQuery;
    }


    [HttpGet]
    public async Task<IActionResult> ExportSinglePdf(
        int id,
        [FromQuery] List<string> columnsToInclude)
    {
        // 1. Najdeme záznam 
        //    POZOR: Nahraďte 'Photos' skutečným názvem vaší DbSet (viz bod 2 výše)
        var item = await _context.Photos.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25, Unit.Millimetre);
                page.Size(PageSizes.A4.Portrait()); // Na výšku

                // --- Hlavička (stejná jako v minulém exportu) ---
                page.Header()
                    .PaddingBottom(10)
                    .Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Levá část
                            row.RelativeItem(2).Column(c =>
                            {
                                c.Item().Text("odpo s.r.o.").SemiBold().FontSize(14);
                                c.Item().Text("Riegrova 59, CZ - 388 01 Blatná").FontSize(10);
                                c.Item().Text("Laboratoř/Laboratory: Radomyšl 248, 387 31 Radomyšl").FontSize(10);

                                // Použijeme IWebHostEnvironment pro cestu k logu
                                string logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "wwwroot/logo.png");

                                if (System.IO.File.Exists(logoPath))
                                {
                                    // OPRAVA POŘADÍ: .MaxHeight() je před .Image()
                                    c.Item().PaddingTop(5).MaxHeight(50).Image(logoPath).FitArea();
                                }
                            });

                            // Pravá část
                            row.RelativeItem(1).Column(c =>
                            {
                                c.Item().AlignRight().Text("Detail vzorku").SemiBold().FontSize(16);
                                c.Item().AlignRight().Text($"ID: {item.Id}").FontSize(9);
                                c.Item().AlignRight().Text($"Datum: {DateTime.Now:d. M. yyyy}").FontSize(9);
                            });
                        });
                        col.Item().PaddingTop(5).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                    });

                // --- Obsah stránky ---
                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Pomocná lokální funkce pro vykreslení pole
                    static void AddField(ColumnDescriptor column, string label, string? value)
                    {
                        column.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span($"{label}: ").SemiBold();
                            text.Span(string.IsNullOrWhiteSpace(value) ? "-" : value);
                        });
                    }

                    // --- 1. Fotka (pokud je vybrána) ---
                    if (columnsToInclude.Contains("Photo"))
                    {
                        var imageSrc = !string.IsNullOrWhiteSpace(item.ImagePath) ? item.ImagePath : (string.IsNullOrWhiteSpace(item.PhotoPath) ? null : item.PhotoPath);

                        if (!string.IsNullOrWhiteSpace(imageSrc))
                        {
                            var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, imageSrc.TrimStart('~', '/'));

                            if (System.IO.File.Exists(physicalPath))
                            {
                                byte[] photoBytes = System.IO.File.ReadAllBytes(physicalPath);

                                // *** OPRAVA POŘADÍ ZDE ***
                                col.Item().AlignCenter()
                                    .MaxHeight(250) // <-- Musí být PŘED .Image()
                                    .Image(photoBytes)
                                    .FitArea();

                                col.Item().PaddingBottom(10); // Mezera pod fotkou
                            }
                        }
                    }

                    // --- 2. Datová pole (pokud jsou vybrána) ---
                    if (columnsToInclude.Contains("Id")) AddField(col, "ID", item.Id.ToString());
                    if (columnsToInclude.Contains("Position")) AddField(col, "Position", item.Position?.ToString());
                    if (columnsToInclude.Contains("Supplier")) AddField(col, "Supplier", item.Supplier);
                    if (columnsToInclude.Contains("OriginalName")) AddField(col, "Name", item.OriginalName);
                    if (columnsToInclude.Contains("Material")) AddField(col, "Material", item.Material);
                    if (columnsToInclude.Contains("Form")) AddField(col, "Form", item.Form);
                    if (columnsToInclude.Contains("Filler")) AddField(col, "Filler", item.Filler);
                    if (columnsToInclude.Contains("Color")) AddField(col, "Color", item.Color);
                    if (columnsToInclude.Contains("MonthlyQuantity")) AddField(col, "Quantity (month)", item.MonthlyQuantity?.ToString());
                    if (columnsToInclude.Contains("Mfi")) AddField(col, "MFI", item.Mfi);
                    if (columnsToInclude.Contains("Notes")) AddField(col, "Notes", item.Notes);
                    if (columnsToInclude.Contains("CreatedAt")) AddField(col, "Created", (item.CreatedAt == DateTime.MinValue) ? "-" : item.CreatedAt.ToLocalTime().ToString("g"));
                    if (columnsToInclude.Contains("UpdatedAt")) AddField(col, "Updated", (item.UpdatedAt == DateTime.MinValue) ? "-" : item.UpdatedAt.ToLocalTime().ToString("g"));

                    // --- 3. QR Kód (pokud je vybrán) ---
                    if (columnsToInclude.Contains("QrCode"))
                    {
                        var publicUrl = Url.Action("DetailsAnonymous", "Home", new { id = item.Id }, Request.Scheme ?? "https");
                        var qrApi = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={System.Uri.EscapeDataString(publicUrl)}";

                        try
                        {
                            using (var httpClient = new HttpClient())
                            {
                                byte[] qrBytes = httpClient.GetByteArrayAsync(qrApi).Result;
                                col.Item().PaddingTop(10).Text("Public Link (QR):").SemiBold();

                                // *** OPRAVA POŘADÍ ZDE ***
                                col.Item()
                                    .MaxWidth(150) // <-- Musí být PŘED .Image()
                                    .Image(qrBytes)
                                    .FitArea();
                            }
                        }
                        catch (Exception)
                        {
                            AddField(col, "Public Link (QR)", "Nelze vygenerovat QR kód.");
                        }
                    }
                });

                // --- Patička ---
                page.Footer()
                    .AlignRight()
                    .Text(x =>
                    {
                        x.Span("Strana ");
                        x.CurrentPageNumber();
                        x.Span(" z ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf();

        // 4. Vrátíme soubor
        return File(pdfBytes, "application/pdf", $"Vzorek_{item.Id}_{item.Name ?? "Detail"}.pdf");
    }

    // =================================================================
    // ===                 OPRAVENÁ METODA PRO VIEWMODEL             ===
    // =================================================================
    private async Task<PhotoApp.ViewModels.PhotosIndexViewModel> GetFilteredViewModel(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form)
    {
        // 1. Základní dotaz (včetně vyhledávání)
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

        // 2. Vytvoření oddělených dotazů pro každý filtr + pro výsledné položky
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

        // 3. Křížová aplikace filtrů (OPRAVENO)

        // Vytvoření zástupných expression, aby byl kód čistší
        Expression<Func<PhotoRecord, bool>> supplierFilter = p => supplier.Contains(p.Supplier);
        Expression<Func<PhotoRecord, bool>> materialFilter = p => material.Contains(p.Material);
        Expression<Func<PhotoRecord, bool>> typeFilter = p => type.Contains(p.Type);
        Expression<Func<PhotoRecord, bool>> colorFilter = p => color.Contains(p.Color);
        Expression<Func<PhotoRecord, bool>> nameFilter = p => name.Contains(p.Name);
        Expression<Func<PhotoRecord, bool>> positionFilter = p => position.Contains(p.Position);
        Expression<Func<PhotoRecord, bool>> fillerFilter = p => filler.Contains(p.Filler);
        Expression<Func<PhotoRecord, bool>> formFilter = p => form.Contains(p.Form);
        Expression<Func<PhotoRecord, bool>> mfiFilter = p => mfi.Contains(p.Mfi);
        Expression<Func<PhotoRecord, bool>> monthlyQuantityFilter = p => monthlyQuantity.Contains(p.MonthlyQuantity);


        if (supplier != null && supplier.Any())
        {
            itemsQuery = itemsQuery.Where(supplierFilter);
            materialQuery = materialQuery.Where(supplierFilter);
            typeQuery = typeQuery.Where(supplierFilter);
            colorQuery = colorQuery.Where(supplierFilter);
            nameQuery = nameQuery.Where(supplierFilter);
            positionQuery = positionQuery.Where(supplierFilter);
            fillerQuery = fillerQuery.Where(supplierFilter);
            formQuery = formQuery.Where(supplierFilter);
            mfiQuery = mfiQuery.Where(supplierFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(supplierFilter);
        }

        if (material != null && material.Any())
        {
            itemsQuery = itemsQuery.Where(materialFilter);
            supplierQuery = supplierQuery.Where(materialFilter);
            typeQuery = typeQuery.Where(materialFilter);
            colorQuery = colorQuery.Where(materialFilter);
            nameQuery = nameQuery.Where(materialFilter);
            positionQuery = positionQuery.Where(materialFilter);
            fillerQuery = fillerQuery.Where(materialFilter);
            formQuery = formQuery.Where(materialFilter);
            mfiQuery = mfiQuery.Where(materialFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(materialFilter);
        }

        if (type != null && type.Any())
        {
            itemsQuery = itemsQuery.Where(typeFilter);
            supplierQuery = supplierQuery.Where(typeFilter);
            materialQuery = materialQuery.Where(typeFilter);
            colorQuery = colorQuery.Where(typeFilter);
            nameQuery = nameQuery.Where(typeFilter);
            positionQuery = positionQuery.Where(typeFilter);
            fillerQuery = fillerQuery.Where(typeFilter);
            formQuery = formQuery.Where(typeFilter);
            mfiQuery = mfiQuery.Where(typeFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(typeFilter);
        }

        if (color != null && color.Any())
        {
            itemsQuery = itemsQuery.Where(colorFilter);
            supplierQuery = supplierQuery.Where(colorFilter);
            materialQuery = materialQuery.Where(colorFilter);
            typeQuery = typeQuery.Where(colorFilter);
            nameQuery = nameQuery.Where(colorFilter);
            positionQuery = positionQuery.Where(colorFilter);
            fillerQuery = fillerQuery.Where(colorFilter);
            formQuery = formQuery.Where(colorFilter);
            mfiQuery = mfiQuery.Where(colorFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(colorFilter);
        }

        if (name != null && name.Any())
        {
            itemsQuery = itemsQuery.Where(nameFilter);
            supplierQuery = supplierQuery.Where(nameFilter);
            materialQuery = materialQuery.Where(nameFilter);
            typeQuery = typeQuery.Where(nameFilter);
            colorQuery = colorQuery.Where(nameFilter);
            positionQuery = positionQuery.Where(nameFilter);
            fillerQuery = fillerQuery.Where(nameFilter);
            formQuery = formQuery.Where(nameFilter);
            mfiQuery = mfiQuery.Where(nameFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(nameFilter); // <<< ZDE BYLA CHYBA (DNS)
        }

        if (position != null && position.Any())
        {
            itemsQuery = itemsQuery.Where(positionFilter);
            supplierQuery = supplierQuery.Where(positionFilter);
            materialQuery = materialQuery.Where(positionFilter);
            typeQuery = typeQuery.Where(positionFilter);
            colorQuery = colorQuery.Where(positionFilter);
            nameQuery = nameQuery.Where(positionFilter);
            fillerQuery = fillerQuery.Where(positionFilter);
            formQuery = formQuery.Where(positionFilter);
            mfiQuery = mfiQuery.Where(positionFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(positionFilter);
        }

        if (filler != null && filler.Any())
        {
            itemsQuery = itemsQuery.Where(fillerFilter);
            supplierQuery = supplierQuery.Where(fillerFilter);
            materialQuery = materialQuery.Where(fillerFilter);
            typeQuery = typeQuery.Where(fillerFilter);
            colorQuery = colorQuery.Where(fillerFilter);
            nameQuery = nameQuery.Where(fillerFilter);
            positionQuery = positionQuery.Where(fillerFilter);
            formQuery = formQuery.Where(fillerFilter);
            mfiQuery = mfiQuery.Where(fillerFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(fillerFilter);
        }

        if (form != null && form.Any())
        {
            itemsQuery = itemsQuery.Where(formFilter);
            supplierQuery = supplierQuery.Where(formFilter);
            materialQuery = materialQuery.Where(formFilter);
            typeQuery = typeQuery.Where(formFilter);
            colorQuery = colorQuery.Where(formFilter);
            nameQuery = nameQuery.Where(formFilter);
            positionQuery = positionQuery.Where(formFilter);
            fillerQuery = fillerQuery.Where(formFilter);
            mfiQuery = mfiQuery.Where(formFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(formFilter);
        }

        if (mfi != null && mfi.Any())
        {
            itemsQuery = itemsQuery.Where(mfiFilter);
            supplierQuery = supplierQuery.Where(mfiFilter);
            materialQuery = materialQuery.Where(mfiFilter);
            typeQuery = typeQuery.Where(mfiFilter);
            colorQuery = colorQuery.Where(mfiFilter);
            nameQuery = nameQuery.Where(mfiFilter);
            positionQuery = positionQuery.Where(mfiFilter);
            fillerQuery = fillerQuery.Where(mfiFilter);
            formQuery = formQuery.Where(mfiFilter);
            monthlyQuantityQuery = monthlyQuantityQuery.Where(mfiFilter);
        }

        if (monthlyQuantity != null && monthlyQuantity.Any())
        {
            itemsQuery = itemsQuery.Where(monthlyQuantityFilter);
            supplierQuery = supplierQuery.Where(monthlyQuantityFilter);
            materialQuery = materialQuery.Where(monthlyQuantityFilter);
            typeQuery = typeQuery.Where(monthlyQuantityFilter);
            colorQuery = colorQuery.Where(monthlyQuantityFilter);
            nameQuery = nameQuery.Where(monthlyQuantityFilter);
            positionQuery = positionQuery.Where(monthlyQuantityFilter);
            fillerQuery = fillerQuery.Where(monthlyQuantityFilter);
            formQuery = formQuery.Where(monthlyQuantityFilter); // <<< ZDE BYLA CHYBA (p.Form)
            mfiQuery = mfiQuery.Where(monthlyQuantityFilter);
        }

        // 4. Asynchronní spuštění VŠECH dotazů
        var itemsTask = itemsQuery
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        // Pomocná lokální funkce
        static Task<List<string>> GetFilterOptions(IQueryable<PhotoRecord> query, Expression<Func<PhotoRecord, string>> selector)
        {
            return query
                .Select(selector)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }

        // Paralelní spuštění
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


        await Task.WhenAll(
            itemsTask, suppliersTask, materialsTask, typesTask, colorsTask,
            namesTask, positionsTask, fillersTask, formsTask, mfisTask, monthlyQuantitiesTask
        );

        // 5. Sestavení ViewModelu
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

    // ... Zbytek vašich metod (Create, Edit, Delete, atd.) ...

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