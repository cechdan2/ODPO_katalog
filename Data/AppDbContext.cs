using Microsoft.EntityFrameworkCore;
using PhotoApp.Models;

namespace PhotoApp.Data
{
    // DbContext with explicit mapping PhotoRecord -> Photos and basic column configuration.
    // This ensures EF expects correct table/column names and migrations are clearer.
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // Existing DbSets
        public DbSet<FilterOption> FilterOptions { get; set; }
        public DbSet<CustomUser> Users { get; set; } = default!;
        public DbSet<PhotoRecord> Photos { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Explicitly map PhotoRecord to table "Photos"
            modelBuilder.Entity<PhotoRecord>(entity =>
            {
                entity.ToTable("Photos");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                      .IsRequired()
                      .HasMaxLength(250);

                entity.Property(e => e.Code)
                      .HasMaxLength(100);

                entity.Property(e => e.Type)
                      .HasMaxLength(100);

                entity.Property(e => e.Supplier)
                      .HasMaxLength(200);

                entity.Property(e => e.OriginalName)
                      .HasMaxLength(500);

                entity.Property(e => e.Material)
                      .HasMaxLength(150);

                entity.Property(e => e.Form)
                      .HasMaxLength(150);

                entity.Property(e => e.Filler)
                      .HasMaxLength(150);

                entity.Property(e => e.Color)
                      .HasMaxLength(150);

                entity.Property(e => e.Description)
                      .HasMaxLength(2000);

                entity.Property(e => e.MonthlyQuantity)
                      .HasMaxLength(200);

                entity.Property(e => e.Mfi)
                      .HasMaxLength(100);

                entity.Property(e => e.Notes)
                      .HasMaxLength(2000);

                entity.Property(e => e.PhotoFileName)
                      .HasMaxLength(500);

                entity.Property(e => e.PhotoPath)
                      .HasMaxLength(500);

                entity.Property(e => e.ImagePath)
                      .HasMaxLength(500);

                entity.Property(e => e.Position)
                      .HasMaxLength(200);

                entity.Property(e => e.ExternalId)
                      .HasMaxLength(200);

                // Default values for timestamps (SQLite syntax)
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // If using CustomUser (Identity), ensure mapping if a different table name is needed.
            // If using standard ASP.NET Identity, leave default or map to "AspNetUsers".
            modelBuilder.Entity<CustomUser>(entity =>
            {
                // Uncomment and adjust as needed:
                // entity.ToTable("AspNetUsers");
            });

            // Seeding logic for FilterOptions
            // This populates the database with initial values for dropdowns/filters
            int idCounter = 1;

            // --- SUPPLIER ---
            var suppliers = new[]
            {
                "Oprava", "AA Group", "AGC", "Agor", "Archeo", "Vážeme", "Badico", "BBH",
                "Delasitas", "Dijmex", "Duo Pet", "JMK", "Ecoprimus", "EF Recycling", "Eri-trade",
                "Rumpold", "Fatra", "Gabeo", "GID", "Neveon", "Gumotex", "GZR", "Chintex",
                "Inno Comp", "Repla", "Juta", "Kamiddos", "Kantoøík", "Kužílek", "KV Ekoplast",
                "Laszlo", "Leifheit", "Magna", "Mondeco", "Nexis", "Oceanize", "odpo",
                "Power-Full", "PFN", "PlastMetal", "Pošumavská", "Prodos rec", "Rapol",
                "Regoplast", "Remaq", "Renoplasti", "Reyond", "Silon Recy", "Suchan",
                "TKC Kunst", "Torray", "Valek", "Vansida", "Witt and M", "Witte", "Zeba", "ZMPB"
            };

            foreach (var s in suppliers)
            {
                modelBuilder.Entity<FilterOption>().HasData(new FilterOption
                { Id = idCounter++, Category = "supplier", Value = s });
            }

            // --- FORM ---
            var forms = new[]
            {
                "Form", "Regrind", "Scrap", "Regranulate", "Ingots", "Pellets",
                "Yarn", "Bales", "Lumps", "Rolls", "Other", "Virgin"
            };

            foreach (var f in forms)
            {
                modelBuilder.Entity<FilterOption>().HasData(new FilterOption
                { Id = idCounter++, Category = "form", Value = f });
            }

            // --- FILLER ---
            var fillers = new[]
            {
                "Filler", "GF", "TD", "MD", "TV", "CF", "LGF", "ESD"
            };

            foreach (var fi in fillers)
            {
                modelBuilder.Entity<FilterOption>().HasData(new FilterOption
                { Id = idCounter++, Category = "filler", Value = fi });
            }

            // --- COLOR ---
            var colors = new[]
            {
                "Colour", "Red", "Black", "Blue", "Grey"
            };

            foreach (var c in colors)
            {
                modelBuilder.Entity<FilterOption>().HasData(new FilterOption
                { Id = idCounter++, Category = "color", Value = c });
            }

            // --- MATERIAL ---
            var materials = new[] { "PP", "LDPE", "HDPE", "PC/ABS", "PA6", "PA66" };
            foreach (var m in materials)
            {
                modelBuilder.Entity<FilterOption>().HasData(new FilterOption
                { Id = idCounter++, Category = "material", Value = m });
            }
        }
    }
}