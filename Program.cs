using Microsoft.EntityFrameworkCore;
using Organizacional.Data;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Organizacional.Filters.EmpresaFilter>();
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // ⏳ 30 minutos
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<OrganizacionalContext>(optionsBuilder =>
    optionsBuilder.UseMySql(
        builder.Configuration.GetConnectionString("conexion"),
        Microsoft.EntityFrameworkCore.ServerVersion.Parse("10.4.32-mariadb")
    ));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
