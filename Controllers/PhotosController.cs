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
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Globalization;

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

    // --- POMOCNÁ METODA PRO PARSOVÁNÍ MFI ---
    // Bere řetězec "32/55" nebo "5,12", vrátí pouze první celé číslo (32 nebo 5).
    private double? ParseMfi(string mfiString)
    {
        if (string.IsNullOrWhiteSpace(mfiString)) return null;

        // Rozdělíme podle lomítka, čárky nebo tečky a vezmeme první část
        var firstPart = mfiString.Split(new[] { '/', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        // Převedeme na číslo
        if (double.TryParse(firstPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }
        return null;
    }

    public IActionResult Import()
    {
        return View();
    }

    // --- HLAVNÍ METODA PRO ZOBRAZENÍ (DESKTOP) ---
    [Authorize]
    public async Task<IActionResult> Index(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form,
        double? minMfi, double? maxMfi)
    {
        if (Request.Headers["User-Agent"].ToString().Contains("Mobile"))
        {
            return RedirectToAction("Index_phone", new { search, supplier, material, type, color, name, position, filler, form, mfi, monthlyQuantity, minMfi, maxMfi });
        }

        var vm = await GetFilteredViewModel(search, supplier, material, type, color, name, position, filler, mfi, monthlyQuantity, form, minMfi, maxMfi);

        ViewBag.IsMobile = false;
        return View(vm);
    }

    // --- HLAVNÍ METODA PRO ZOBRAZENÍ (MOBIL) ---
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index_phone(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> form, List<string> mfi, List<string> monthlyQuantity,
        double? minMfi, double? maxMfi,
        bool forceDesktop = false)
    {
        if (forceDesktop)
        {
            return RedirectToAction("Index", new { search, supplier, material, type, color, name, position, filler, form, mfi, monthlyQuantity, minMfi, maxMfi });
        }

        var vm = await GetFilteredViewModel(search, supplier, material, type, color, name, position, filler, mfi, monthlyQuantity, form, minMfi, maxMfi);

        ViewBag.IsMobile = true;
        return View("Index_phone", vm);
    }

    // =========================================================
    // ===                  METODA EXPORT PDF                ===
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> ExportPdf(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form,
        double? minMfi, double? maxMfi,
        [FromQuery] List<string> columnsToInclude)
    {
        // 1. Získáme vyfiltrovaný dotaz (základní stringové filtry)
        var query = GetFilteredQuery(search, supplier, material, type, color, name, position, filler, mfi, monthlyQuantity, form);

        // 2. Spustíme dotaz a načteme data do paměti (BEZ ŘAZENÍ V SQL)
        var itemsList = await query.ToListAsync();

        // 3. Aplikujeme MFI číselný filtr v paměti (pokud je zadán rozsah)
        if (minMfi.HasValue || maxMfi.HasValue)
        {
            itemsList = itemsList.Where(p => {
                var val = ParseMfi(p.Mfi);
                if (!val.HasValue) return false; // Pokud má filtr, ale hodnota není číslo, skrýt

                bool condition = true;
                if (minMfi.HasValue) condition &= (val.Value >= minMfi.Value);
                if (maxMfi.HasValue) condition &= (val.Value <= maxMfi.Value);

                return condition;
            }).ToList();
        }

        // 4. FINÁLNÍ ŘAZENÍ (A-Z Material -> 0-9 Monthly Quantity)
        itemsList = itemsList
            .OrderBy(p => p.Material) // Primární: Abeceda
            .ThenBy(p => 
            {
                // Sekundární: Množství (převod stringu na double pro správné řazení)
                if (string.IsNullOrWhiteSpace(p.MonthlyQuantity)) return 0;
                
                // Nahradíme čárku tečkou pro jistotu a zkusíme parsovat
                var cleanVal = p.MonthlyQuantity.Replace(",", ".").Trim();
                // Vezmeme jen první část kdyby tam bylo "500 kg" -> "500"
                var numberPart = cleanVal.Split(' ')[0]; 

                if (double.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
                return 0; 
            })
            .ToList();

        // 5. Vytvoříme PDF dokument
        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25, Unit.Millimetre);
                page.Size(PageSizes.A4.Landscape());

                // ==========================================
                // --- KARTA HLAVIČKY (HEADER CARD) ---
                // ==========================================
                page.Header()
                    .PaddingBottom(10) // Mezera mezi kartou hlavičky a kartou tabulky
                    //                   // Styl karty:
                    //.Border(1)
                    //.BorderColor(Colors.Green.Lighten2)
                    //.Background(Color.FromHex("#f4fbf4")) // Velmi jemné šedé pozadí pro hlavičku
                    //.Padding(10) // Vnitřní odsazení v kartě
                    .Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Levá část    
                            row.RelativeItem(1).Column(c =>
                            {
                                string logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                                if (System.IO.File.Exists(logoPath))
                                {
                                    c.Item().AlignMiddle().MaxHeight(75).Image(logoPath).FitArea();
                                }

                            });



                            // Pravá část
                            row.RelativeItem(1).Column(c =>
                            {
                                c.Item().AlignRight().Text("Technical Data Sheet").SemiBold().FontColor(Color.FromHex("#182c25")).FontSize(16);
                                c.Item().AlignRight().Text("").FontColor(Color.FromHex("#182c25")).FontSize(5);
                                c.Item().AlignRight().Text($"Date: {DateTime.Now:d. M. yyyy}").FontColor(Color.FromHex("#182c25")).FontSize(9);
                                c.Item().AlignRight().Text($"Count: {itemsList.Count}") .FontColor(Color.FromHex("#182c25")).FontSize(9);
                                c.Item().AlignRight().Text("").FontSize(9);
                                c.Item().AlignRight().Text("Riegrova 59, CZ - 388 01 Blatná").FontColor(Color.FromHex("#182c25")).FontSize(10);
                                c.Item().AlignRight().Text("Laboratoř/Laboratory: Radomyšl 248, 387 31 Radomyšl").FontColor(Color.FromHex("#182c25")).FontSize(10);
                                c.Item().AlignRight().Text("tel. +420 723 007 734 / +420 778 020 315").FontColor(Color.FromHex("#182c25")).FontSize(10);
                            });
                        });
                        // Poznámka: Oddělovací čáru jsem smazal, protože samotná karta má rámeček.
                    });


                // ==========================================
                // --- KARTA OBSAHU (CONTENT CARD) ---
                // ==========================================
                page.Content()
                    // Styl karty:
                    .Border(1)
                    .BorderColor(Colors.Green.Lighten2)
                    .Background(Color.FromHex("#f4fbf4"))
                    .Padding(0) // Tabulka vypadá lépe, když jde až ke krajům, nebo dejte Padding(10)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            if (columnsToInclude.Contains("Id")) columns.ConstantColumn(40);
                            if (columnsToInclude.Contains("Supplier")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("Material")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("OriginalName")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("Color")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("Position")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("Form")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("Filler")) columns.RelativeColumn();
                            if (columnsToInclude.Contains("MonthlyQuantity")) columns.ConstantColumn(60);
                            if (columnsToInclude.Contains("Mfi")) columns.ConstantColumn(50);
                            if (columnsToInclude.Contains("Notes")) columns.RelativeColumn(2);
                        });

                        // Hlavička tabulky (uvnitř karty obsahu)
                        table.Header(header =>
                        {
                            // Přidal jsem jemné pozadí pro řádek s názvy sloupců
                            static IContainer HeaderCellStyle(IContainer c) => c
                                .Background(Colors.Green.Lighten4)
                                .BorderBottom(1)
                                .BorderRight(1)
                                .BorderColor(Colors.Green.Lighten2)
                                .Padding(5);

                            if (columnsToInclude.Contains("Id")) header.Cell().Element(HeaderCellStyle).Text("ID").SemiBold();
                            if (columnsToInclude.Contains("Supplier")) header.Cell().Element(HeaderCellStyle).Text("Supplier").SemiBold();
                            if (columnsToInclude.Contains("Material")) header.Cell().Element(HeaderCellStyle).Text("Material").SemiBold();
                            if (columnsToInclude.Contains("OriginalName")) header.Cell().Element(HeaderCellStyle).Text("Original Name").SemiBold();
                            if (columnsToInclude.Contains("Color")) header.Cell().Element(HeaderCellStyle).Text("Color").SemiBold();
                            if (columnsToInclude.Contains("Position")) header.Cell().Element(HeaderCellStyle).Text("Position").SemiBold();
                            if (columnsToInclude.Contains("Form")) header.Cell().Element(HeaderCellStyle).Text("Form").SemiBold();
                            if (columnsToInclude.Contains("Filler")) header.Cell().Element(HeaderCellStyle).Text("Filler").SemiBold();
                            if (columnsToInclude.Contains("MonthlyQuantity")) header.Cell().Element(HeaderCellStyle).Text("Qty").SemiBold();
                            if (columnsToInclude.Contains("Mfi")) header.Cell().Element(HeaderCellStyle).Text("MFI/°C/kg").SemiBold();
                            if (columnsToInclude.Contains("Notes")) header.Cell().Element(HeaderCellStyle).Text("Notes").SemiBold();
                        });

                        static IContainer DataCellStyle(IContainer c) => c.BorderBottom(1).BorderRight(1).BorderColor(Colors.Green.Lighten3).Padding(5);
                        

                        foreach (var item in itemsList)
                        {
                            if (columnsToInclude.Contains("Id")) table.Cell().Element(DataCellStyle).Text(item.Id);
                            if (columnsToInclude.Contains("Supplier")) table.Cell().Element(DataCellStyle).Text(item.Supplier);
                            if (columnsToInclude.Contains("Material")) table.Cell().Element(DataCellStyle).Text(item.Material);
                            if (columnsToInclude.Contains("OriginalName")) table.Cell().Element(DataCellStyle).Text(item.OriginalName);
                            if (columnsToInclude.Contains("Color")) table.Cell().Element(DataCellStyle).Text(item.Color);
                            if (columnsToInclude.Contains("Position")) table.Cell().Element(DataCellStyle).Text(item.Position);
                            if (columnsToInclude.Contains("Form")) table.Cell().Element(DataCellStyle).Text(item.Form);
                            if (columnsToInclude.Contains("Filler")) table.Cell().Element(DataCellStyle).Text(item.Filler);
                            if (columnsToInclude.Contains("MonthlyQuantity")) table.Cell().Element(DataCellStyle).Text(item.MonthlyQuantity);
                            if (columnsToInclude.Contains("Mfi")) table.Cell().Element(DataCellStyle).Text(item.Mfi);
                            if (columnsToInclude.Contains("Notes")) table.Cell().Element(DataCellStyle).Text(item.Notes);
                        }
                    });

                // --- Patička ---
                page.Footer()
                    .PaddingTop(5)
                    .AlignRight()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Samples_Export_{DateTime.Now:yyyy-MM-dd}.pdf");
    }

    // --- SDÍLENÁ METODA PRO FILTROVÁNÍ (String část) ---
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
        var item = await _context.Photos.FindAsync(id);
        if (item == null) return NotFound();

        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25, Unit.Millimetre);
                page.Size(PageSizes.A4.Portrait());
                page.DefaultTextStyle(x => x.FontSize(10)); // Globální velikost písma

                // --- HLAVIČKA ---
                page.Header()
                    .PaddingBottom(20)
                    .Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Levá část: Logo a Adresa
                            row.RelativeItem(2).Column(c =>
                            {
                                string logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                                if (System.IO.File.Exists(logoPath))
                                {
                                    c.Item().AlignLeft().Image(logoPath).FitArea(); // AlignLeft se volá na kontejneru -> OK // AlignLeft se volá na kontejneru -> OK
                                }

                                c.Item().PaddingTop(5).Text("odpo s.r.o.").SemiBold().FontSize(12).FontColor(Colors.Green.Darken2);
                                c.Item().Text("Riegrova 59, CZ - 388 01 Blatná").FontSize(9).FontColor(Colors.Grey.Darken1);
                                c.Item().Text("Production/Laboratory: Radomyšl 248, 387 31 Radomyšl").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });

                            // Pravá část: Nadpis a Metadata
                            row.RelativeItem(1).AlignRight().Column(c =>
                            {
                                c.Item().Text("SAMPLE DETAIL").ExtraBold().FontSize(16).FontColor(Colors.Grey.Darken3);
                                c.Item().PaddingTop(5).Text($"ID: {item.Id}").FontSize(10).SemiBold();
                                c.Item().Text($"Date: {DateTime.Now:d. M. yyyy}").FontSize(10);
                            });
                        });

                        // Oddělovací čára pod hlavičkou
                        col.Item().PaddingTop(10).BorderBottom(1.5f).BorderColor(Colors.Green.Medium);
                    });

                // --- OBSAH ---
                page.Content().PaddingVertical(10).Column(col =>
                {
                    // 1. Sekce: FOTOGRAFIE (pokud je vybrána)
                    if (columnsToInclude.Contains("Photo"))
                    {
                        var imageSrc = !string.IsNullOrWhiteSpace(item.ImagePath) ? item.ImagePath : (string.IsNullOrWhiteSpace(item.PhotoPath) ? null : item.PhotoPath);
                        if (!string.IsNullOrWhiteSpace(imageSrc))
                        {
                            var physicalPath = Path.Combine(_env.WebRootPath, imageSrc.TrimStart('~', '/'));
                            if (System.IO.File.Exists(physicalPath))
                            {
                                byte[] photoBytes = System.IO.File.ReadAllBytes(physicalPath);

                                col.Item()
                                   .PaddingBottom(20)
                                   .AlignCenter()
                                   .Border(1)
                                   .BorderColor(Colors.Grey.Lighten2)
                                   .Padding(5) // Rámeček kolem fotky
                                   .MaxHeight(250) // Omezení výšky
                                   .Image(photoBytes)
                                   .FitArea();
                            }
                        }
                    }

                    // 2. Sekce: TABULKA S DATY
                    col.Item().Table(table =>
                    {
                        // Definice sloupců: Popisek (fixní šířka) | Hodnota (zbytek)
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120); // Šířka sloupce pro názvy (Labels)
                            columns.RelativeColumn();    // Šířka pro hodnoty
                        });

                        // Pomocná funkce pro řádky tabulky se "Zebra" efektem
                        uint rowIndex = 0;
                        void AddRow(string label, string? value)
                        {
                            rowIndex++;
                            var bgColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten5; // Střídání barev

                            table.Cell().Background(bgColor).Padding(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                                 .Text(label).SemiBold().FontColor(Colors.Grey.Darken3);

                            table.Cell().Background(bgColor).Padding(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                                 .Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontColor(Colors.Black);
                        }

                        // Podmíněné přidávání řádků podle columnsToInclude
                                                if (columnsToInclude.Contains("Id")) AddRow("Internal ID", item.Id.ToString());
                        if (columnsToInclude.Contains("Supplier")) AddRow("Supplier", item.Supplier);
                        if (columnsToInclude.Contains("Material")) AddRow("Material", item.Material);
                        if (columnsToInclude.Contains("OriginalName")) AddRow("Name", item.OriginalName);
                        if (columnsToInclude.Contains("Form")) AddRow("Form", item.Form);
                        if (columnsToInclude.Contains("Filler")) AddRow("Filler", item.Filler);
                        if (columnsToInclude.Contains("Color")) AddRow("Color", item.Color);
                        if (columnsToInclude.Contains("Position")) AddRow("Position", item.Position?.ToString());
                        if (columnsToInclude.Contains("MonthlyQuantity")) AddRow("Quantity (month)", item.MonthlyQuantity?.ToString());
                        if (columnsToInclude.Contains("Mfi")) AddRow("MFI/°C/kg", item.Mfi);
                        if (columnsToInclude.Contains("Notes")) AddRow("Notes", item.Notes);

                        if (columnsToInclude.Contains("CreatedAt"))
                            AddRow("Created", (item.CreatedAt == DateTime.MinValue) ? "-" : item.CreatedAt.ToLocalTime().ToString("g"));

                        if (columnsToInclude.Contains("UpdatedAt"))
                            AddRow("Updated", (item.UpdatedAt == DateTime.MinValue) ? "-" : item.UpdatedAt.ToLocalTime().ToString("g"));
                    });
                });

                // --- PATIČKA ---
                page.Footer()
                    .PaddingTop(10)
                    .Row(row =>
                    {
                        // Vlevo: Malé info
                        row.RelativeItem().Text(x =>
                        {
                            x.Span("Generated via IS odpo s.r.o.").FontSize(8).FontColor(Colors.Grey.Medium);
                        });

                        // Vpravo: Číslování
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Page ").FontSize(9);
                            x.CurrentPageNumber().FontSize(9);
                            x.Span(" of ").FontSize(9);
                            x.TotalPages().FontSize(9);
                        });
                    });
            });
        }).GeneratePdf();

        return File(pdfBytes, "application/pdf", $"Sample_{item.Id}_{item.OriginalName ?? "Detail"}.pdf");
    }
    // --- METODA PRO VIEWMODEL ---
    private async Task<PhotoApp.ViewModels.PhotosIndexViewModel> GetFilteredViewModel(
        string search, List<string> supplier, List<string> material, List<string> type,
        List<string> color, List<string> name, List<string> position, List<string> filler,
        List<string> mfi, List<string> monthlyQuantity, List<string> form,
        double? minMfi = null, double? maxMfi = null)
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
            monthlyQuantityQuery = monthlyQuantityQuery.Where(nameFilter);
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
            formQuery = formQuery.Where(monthlyQuantityFilter);
            mfiQuery = mfiQuery.Where(monthlyQuantityFilter);
        }

        var itemsList = await itemsQuery.OrderByDescending(p => p.UpdatedAt).ToListAsync();

        if (minMfi.HasValue || maxMfi.HasValue)
        {
            itemsList = itemsList.Where(p => {
                var val = ParseMfi(p.Mfi);
                if (!val.HasValue) return false;
                bool condition = true;
                if (minMfi.HasValue) condition &= (val.Value >= minMfi.Value);
                if (maxMfi.HasValue) condition &= (val.Value <= maxMfi.Value);
                return condition;
            }).ToList();
        }

        static Task<List<string>> GetFilterOptions(IQueryable<PhotoRecord> query, Expression<Func<PhotoRecord, string>> selector)
        {
            return query
                .Select(selector)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }

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
            suppliersTask, materialsTask, typesTask, colorsTask,
            namesTask, positionsTask, fillersTask, formsTask, mfisTask, monthlyQuantitiesTask
        );

        var vm = new PhotoApp.ViewModels.PhotosIndexViewModel
        {
            Items = itemsList,
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
        string defaultPlaceholder = "/photos/default.JPEG";

        if (PhotoFile != null && PhotoFile.Length > 0)
        {
            if (PhotoFile.Length > MaxFileSize)
            {
                ModelState.AddModelError("PhotoFile", "File is too large.");
                return View(photoModel);
            }

            if (!PermittedTypes.Contains(PhotoFile.ContentType))
            {
                ModelState.AddModelError("PhotoFile", "Unsupported file type.");
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
        else
        {
            savedPath = defaultPlaceholder;
        }

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
                        ModelState.AddModelError("AdditionalPhotoFiles", $"File {file.FileName} is too large.");
                        continue;
                    }

                    if (!PermittedTypes.Contains(file.ContentType))
                    {
                        ModelState.AddModelError("AdditionalPhotoFiles", $"File {file.FileName} format not supported.");
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
            PhotoPath = savedPath,
            ImagePath = savedPath,
            AdditionalPhotos = string.Join(";", additionalPaths),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Add(photo);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var photo = await _context.Photos.FindAsync(id);
        if (photo == null) return NotFound();

        return View(photo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PhotoRecord photoModel, IFormFile? PhotoFile, List<IFormFile>? AdditionalPhotoFiles)
    {
        if (id != photoModel.Id) return NotFound();

        if (!ModelState.IsValid) return View(photoModel);

        try
        {
            var existing = await _context.Photos.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();

            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                if (PhotoFile.Length > MaxFileSize)
                {
                    ModelState.AddModelError("PhotoFile", "File is too large.");
                    return View(photoModel);
                }

                if (!PermittedTypes.Contains(PhotoFile.ContentType))
                {
                    ModelState.AddModelError("PhotoFile", "Unsupported file type.");
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

            if (AdditionalPhotoFiles != null && AdditionalPhotoFiles.Any(f => f.Length > 0))
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var additionalPaths = new List<string>();

                if (!string.IsNullOrWhiteSpace(existing.AdditionalPhotos))
                {
                    additionalPaths.AddRange(
                        existing.AdditionalPhotos.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                    );
                }

                foreach (var file in AdditionalPhotoFiles)
                {
                    if (file.Length > 0)
                    {
                        if (file.Length > MaxFileSize)
                        {
                            ModelState.AddModelError("AdditionalPhotoFiles", $"File {file.FileName} is too large.");
                            continue;
                        }

                        if (!PermittedTypes.Contains(file.ContentType))
                        {
                            ModelState.AddModelError("AdditionalPhotoFiles", $"File {file.FileName} format not supported.");
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

                existing.AdditionalPhotos = string.Join(";", additionalPaths);
            }

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

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "samples.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportJson()
    {
        var data = await _context.Photos.OrderByDescending(x => x.UpdatedAt).ToListAsync();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", "samples.json");
    }

    [HttpGet]
    public async Task<IActionResult> ExportZip()
    {
        var photos = await _context.Photos.OrderBy(p => p.Id).ToListAsync();

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Samples");

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
            "samples_with_images.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdditionalPhoto(int id, string photoPath)
    {
        var photo = await _context.Photos.FindAsync(id);
        if (photo == null) return NotFound();

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAdditionalPhotos(int id, List<IFormFile>? AdditionalPhotoFiles)
    {
        var photo = await _context.Photos.FindAsync(id);
        if (photo == null) return NotFound();

        if (AdditionalPhotoFiles == null || !AdditionalPhotoFiles.Any(f => f.Length > 0))
        {
            TempData["Error"] = "No photos selected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var uploads = Path.Combine(_env.WebRootPath, "uploads");
        if (!Directory.Exists(uploads))
            Directory.CreateDirectory(uploads);

        var additionalPaths = new List<string>();

        if (!string.IsNullOrWhiteSpace(photo.AdditionalPhotos))
        {
            additionalPaths.AddRange(
                photo.AdditionalPhotos.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
            );
        }

        foreach (var file in AdditionalPhotoFiles)
        {
            if (file.Length > 0)
            {
                if (file.Length > MaxFileSize)
                {
                    TempData["Error"] = $"File {file.FileName} is too large (max 5 MB).";
                    continue;
                }

                if (!PermittedTypes.Contains(file.ContentType))
                {
                    TempData["Error"] = $"File {file.FileName} format not supported.";
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

        TempData["Success"] = "Photos uploaded successfully!";
        return RedirectToAction(nameof(Details), new { id });
    }
}