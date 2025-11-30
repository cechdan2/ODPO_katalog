using System.ComponentModel.DataAnnotations;

namespace PhotoApp.Models
{
    public class FilterOption
    {
        public int Id { get; set; }

        // Kategorie určuje, o jaký seznam jde (např. "supplier", "form", "filler", "color")
        [Required]
        [StringLength(50)]
        public string Category { get; set; }

        // Samotná hodnota (např. "Fatra", "Pellets", "Red")
        [Required]
        [StringLength(100)]
        public string Value { get; set; }

        // Pro případné řazení (pokud chcete mít "Other" vždy na konci apod.)
        public int SortOrder { get; set; } = 0;
    }
}