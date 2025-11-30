using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoApp.Data;
using PhotoApp.Models;

namespace PhotoApp.Controllers
{
    [ApiController]
    [Route("api/filters")]
    [Authorize] // Přístup pouze pro přihlášené
    public class FilterOptionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FilterOptionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/filters
        // Vrátí všechny možnosti seskupené podle kategorie
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var options = await _context.FilterOptions
                                        .OrderBy(x => x.Category)
                                        .ThenBy(x => x.Value)
                                        .ToListAsync();
            return Ok(options);
        }

        // POST: api/filters
        // Přidá novou možnost
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FilterOptionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Category) || string.IsNullOrWhiteSpace(dto.Value))
                return BadRequest("Category and Value are required.");

            // Kontrola duplicit
            var exists = await _context.FilterOptions.AnyAsync(x =>
                x.Category == dto.Category && x.Value == dto.Value);

            if (exists)
                return Conflict("This option already exists.");

            var option = new FilterOption
            {
                Category = dto.Category,
                Value = dto.Value
            };

            _context.FilterOptions.Add(option);
            await _context.SaveChangesAsync();

            return Ok(option);
        }

        // DELETE: api/filters/{id}
        // Smaže možnost
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var option = await _context.FilterOptions.FindAsync(id);
            if (option == null)
                return NotFound();

            _context.FilterOptions.Remove(option);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DTO pro příjem dat z frontendu
        public class FilterOptionDto
        {
            public string Category { get; set; }
            public string Value { get; set; }
        }
    }
}