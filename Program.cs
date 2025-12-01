using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PhotoApp.Data;
using PhotoApp.Models;
using PhotoApp.Services;

var builder = WebApplication.CreateBuilder(args);
ExcelPackage.License.SetNonCommercialPersonal("My Name");

// Configure SQLite connection string:
// prefer ConnectionStrings:DefaultConnection, otherwise fallback to SqliteDbPath or default file
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var dbPath = builder.Configuration["SqliteDbPath"] ?? "photoapp.db";
    // if user provided just a file name, convert to Data Source=... format
    if (!dbPath.Trim().Contains('=')) // use char overload for single-character search
        connectionString = $"Data Source={dbPath}";
    else
        connectionString = dbPath;
}

var env = builder.Environment;
var contentRoot = env.ContentRootPath; // absolutní cesta k projektu
var dbFile = Path.Combine(contentRoot, "photoapp.db"); // canonical path used by app
var connectionStringa = $"Data Source={dbFile}";


// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionStringa));



builder.WebHost.ConfigureKestrel(options =>
{
    // Nastavení maximální velikosti tìla požadavku (napø. 10 GB)
    options.Limits.MaxRequestBodySize = 10L * 1024 * 1024 * 1024;
});

// ----------------------------
// Nastavení limitu pro multipart/form-data
// ----------------------------
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10L * 1024 * 1024 * 1024; // 10 GB
});


builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
});

// (volitelnì) expose the db path to configuration for other controllers
builder.Configuration["SqliteDbPath"] = dbFile;

// Cookie authentication (vlastní)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();

// registrace vlastního user servisu
builder.Services.AddScoped<IUserService, EfUserService>();

var app = builder.Build();

// AUTOMATICKÁ MIGRACE + seed uživatele
// AUTOMATICKÁ MIGRACE + seed uživatelù
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 1. Aplikovat migrace (vytvoøí tabulky, pokud nejsou)
    db.Database.Migrate();

    // 2. Definice uživatelù, které chceme mít v systému
    var usersToSeed = new[]
    {
        new { Name = "admin", Pass = "P@ssw0rd!" },
        new { Name = "Jan", Pass = "Honzik123!" },
        new { Name = "Ladislav", Pass = "Ladislav123!" },
        new { Name = "Jakub", Pass = "Jakub123!" }
    };

    var hasher = new PasswordHasher<CustomUser>();
    bool changesMade = false;

    foreach (var userInfo in usersToSeed)
    {
        // Kontrola: Pokud uživatel s tímto jménem NEEXISTUJE, vytvoøíme ho
        if (!db.Users.Any(u => u.UserName == userInfo.Name))
        {
            var newUser = new CustomUser
            {
                // Id se vytvoøí samo díky = Guid.NewGuid().ToString() v modelu
                UserName = userInfo.Name
            };

            // Vygenerování hashe
            newUser.PasswordHash = hasher.HashPassword(newUser, userInfo.Pass);

            db.Users.Add(newUser);
            changesMade = true;
            Console.WriteLine($"Vytvoøen uživatel: {userInfo.Name}");
        }
    }

    // 3. Uložení zmìn do DB (pokud jsme nìkoho pøidali)
    if (changesMade)
    {
        db.SaveChanges();
    }
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();


app.UseRouting();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();