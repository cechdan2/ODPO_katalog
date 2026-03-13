namespace PhotoApp.Models
{
    // Reprezentace z�znamu odpov�daj�c� sloupc�m v Excelu (pro import do DB)
    public class PhotoRecord
    {
        public int Id { get; set; }

        // Excel: "Pozice" (nap�. "19 + 20")
        public string? Position { get; set; }

        // Excel: "ID" (extern� ID z Excelu)
        public string? ExternalId { get; set; }

        // Dodavatel (v Excelu sloupec "Dodavatel")
        public string? Supplier { get; set; } = "";

        // Excel: "P�vodn� n�zev" (origin�ln� n�zev / v�robce)
        public string? OriginalName { get; set; } = "";

        // U�ivatelsk�/altersn� pole Name (va�e p�vodn�)
        public string? Name { get; set; } = "";

        // K�d / intern� k�d
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

        // OnStock - previously MonthlyQuantity, renamed to reflect current stock levels
        public string? OnStock { get; set; }

        // Excel: "množství měsíc(t)" – new field, initially empty for user to fill in
        public string? MonthlyQuantity { get; set; }

        // Excel: "MFI" (může být číslo nebo text, proto string)
        public string? Mfi { get; set; }

        // Pozn�mka (Excel: "Pozn�mka")
        public string? Notes { get; set; } = "";

        // Obr�zek / fotka (Excel: "Fotka") � lze ulo�it jen n�zev souboru nebo relativn� cesta
        public string? PhotoFileName { get; set; }

        // P�vodn� pole pro obr�zek (ponech pro kompatibilitu)
        public string? PhotoPath { get; set; }

        // Nov� pole, pou��van� v controlleru (relativn� cesta v wwwroot)
        public string? ImagePath { get; set; }

        // *** NOV� POLE PRO V�CE FOTEK ***
        // Obsahuje v�ce cest odd�len�ch st�edn�kem (nap�. "/uploads/foto1.jpg;/uploads/foto2.jpg")
        public string? AdditionalPhotos { get; set; }

        // P�idejte tyto dva ��dky:
        // --- P�IDEJTE TYTO DVA ��DKY ---
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        // -------------------------------

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // *** HELPER METODA PRO Z�SK�N� SEZNAMU DODATE�N�CH FOTEK ***
        // Neukl�d� se do DB, slou�� pouze pro pr�ci v k�du
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