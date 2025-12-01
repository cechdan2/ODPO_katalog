namespace PhotoApp.Models
{
    // Reprezentace záznamu odpovídající sloupcùm v Excelu (pro import do DB)
    public class PhotoRecord
    {
        public int Id { get; set; }

        // Excel: "Pozice" (napø. "19 + 20")
        public string? Position { get; set; }

        // Excel: "ID" (externí ID z Excelu)
        public string? ExternalId { get; set; }

        // Dodavatel (v Excelu sloupec "Dodavatel")
        public string? Supplier { get; set; } = "";

        // Excel: "Pùvodní název" (originální název / výrobce)
        public string? OriginalName { get; set; } = "";

        // Uživatelské/altersní pole Name (vaše pùvodní)
        public string? Name { get; set; } = "";

        // Kód / interní kód
        public string Code { get; set; } = "";

        // Typ / kategorie
        public string? Type { get; set; } = "";

        // Excel: "material"
        public string? Material { get; set; }

        // Excel: "forma"
        public string? Form { get; set; }

        // Excel: "plnivo"
        public string? Filler { get; set; }

        // Excel: "barva"
        public string? Color { get; set; }

        // Excel: "popis"
        public string? Description { get; set; }

        // Excel: "množství mìsíc(t)" — ponecháno jako string pro flexibilitu (mùže obsahovat text jako "kusová", "19+20" apod.)
        public string? MonthlyQuantity { get; set; }

        // Excel: "MFI" (mùže být èíslo nebo text, proto string)
        public string? Mfi { get; set; }

        // Poznámka (Excel: "Poznámka")
        public string? Notes { get; set; } = "";

        // Obrázek / fotka (Excel: "Fotka") — lze uložit jen název souboru nebo relativní cesta
        public string? PhotoFileName { get; set; }

        // Pùvodní pole pro obrázek (ponech pro kompatibilitu)
        public string? PhotoPath { get; set; }

        // Nové pole, používané v controlleru (relativní cesta v wwwroot)
        public string? ImagePath { get; set; }

        // *** NOVÉ POLE PRO VÍCE FOTEK ***
        // Obsahuje více cest oddìlených støedníkem (napø. "/uploads/foto1.jpg;/uploads/foto2.jpg")
        public string? AdditionalPhotos { get; set; }

        // Pøidejte tyto dva øádky:
        // --- PØIDEJTE TYTO DVA ØÁDKY ---
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        // -------------------------------

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // *** HELPER METODA PRO ZÍSKÁNÍ SEZNAMU DODATEÈNÝCH FOTEK ***
        // Neukládá se do DB, slouží pouze pro práci v kódu
        public List<string> GetAdditionalPhotosList()
        {
            if (string.IsNullOrWhiteSpace(AdditionalPhotos))
                return new List<string>();

            return AdditionalPhotos
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }
    }
}