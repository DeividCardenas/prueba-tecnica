using FondoXYZ.Domain.Entities;
using FondoXYZ.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios al contenedor (MVC)
builder.Services.AddControllersWithViews();

// 2. Configuración de la base de datos (Inyección del DbContext)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Configuración de ASP.NET Core Identity
builder.Services.AddIdentity<Usuario, IdentityRole<int>>(options =>
{
    // Políticas de la prueba (Ej: clave de 4 números)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
});

var app = builder.Build();

// 4. Configurar el pipeline de solicitudes HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ¡IMPORTANTE! El orden de estos dos es estricto: primero Autenticación, luego Autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();