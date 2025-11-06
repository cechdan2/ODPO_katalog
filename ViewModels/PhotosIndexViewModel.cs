using PhotoApp.Models;
using System.Collections.Generic;

namespace PhotoApp.ViewModels
{
    public class PhotosIndexViewModel
    {
        // Výsledné položky pro zobrazení
        public List<PhotoRecord> Items { get; set; } = new List<PhotoRecord>();

        // Seznamy VŠECH MOŽNÝCH hodnot pro naplnění <select> dropdownů
        public List<string> Suppliers { get; set; } = new List<string>();
        public List<string> Materials { get; set; } = new List<string>();
        public List<string> Types { get; set; } = new List<string>();
        public List<string> Colors { get; set; } = new List<string>();
        public List<string> Names { get; set; } = new List<string>();
        public List<string> Positions { get; set; } = new List<string>();
        public List<string> Fillers { get; set; } = new List<string>();
        public List<string> MonthlyQuantities { get; set; } = new List<string>();
        public List<string> Mfis { get; set; } = new List<string>();
        public List<string> Forms { get; set; } = new List<string>();

        // --- ZMĚNA ZDE ---
        // Vlastnosti pro uchování AKTUÁLNĚ VYBRANÝCH hodnot z filtrů
        // Nyní jsou to seznamy, aby mohly obsahovat více hodnot.
        public string Search { get; set; }
        public List<string> Supplier { get; set; } = new List<string>();
        public List<string> Material { get; set; } = new List<string>();
        public List<string> Type { get; set; } = new List<string>();
        public List<string> Color { get; set; } = new List<string>();
        public List<string> Name { get; set; } = new List<string>();
        public List<string> Position { get; set; } = new List<string>();
        public List<string> Filler { get; set; } = new List<string>();
        public List<string> MonthlyQuantity { get; set; } = new List<string>();
        public List<string> Mfi { get; set; } = new List<string>();
        public List<string> Form { get; set; } = new List<string>();
    }
}