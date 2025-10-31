using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpLogging;          // <- necesario para AddHttpLogging
using Microsoft.AspNetCore.DataProtection;        // <- para DataProtection
using Microsoft.AspNetCore.Authentication.Cookies;
using Organizacional.Data;
using Organizacional.Services;
using System.IO;                                  // <- DirectoryInfo
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;
using System.Net.Http.Json; // para PostAsJsonAsync/GetFromJsonAsync
using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

// DataProtection: persistir llaves (evita "Error unprotecting the session cookie")
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/aspnet/keys"))
    .SetApplicationName("TenereCliente")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

// HttpLogging (DEBE ir antes de Build)
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
});

// MVC + filtros
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Organizacional.Filters.EmpresaFilter>();
});

// Session
builder.Services.AddDistributedMemoryCache(); // requerido por Session

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Tenere.Session";
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // estamos en HTTPS
});

// DB
builder.Services.AddDbContext<OrganizacionalContext>(optionsBuilder =>
{
    optionsBuilder.UseMySql(
        builder.Configuration.GetConnectionString("conexion"),
        Microsoft.EntityFrameworkCore.ServerVersion.Parse("10.4.32-mariadb"));
    optionsBuilder.EnableDetailedErrors();
    optionsBuilder.EnableSensitiveDataLogging(); // Quitar luego en producción
    optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning); // suficiente para ver la inner
});

// Otros servicios
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<EmailService>();

builder.Services.Configure<MaintenanceNotificationsOptions>(
    builder.Configuration.GetSection("MaintenanceNotifications"));

builder.Services.AddSingleton<MaintenanceNotificationJob>();
builder.Services.AddHostedService<MaintenanceNotifier>();

builder.Services.AddHttpClient();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".Tenere.Auth";
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login"; // o la ruta que uses
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        // Útil para depurar si algo falla con cookies:
        options.Events = new CookieAuthenticationEvents
        {
            OnSigningIn = ctx => { Console.WriteLine("SigningIn"); return Task.CompletedTask; },
            OnSignedIn  = ctx => { Console.WriteLine("SignedIn"); return Task.CompletedTask; },
            OnValidatePrincipal = ctx => { Console.WriteLine("ValidatePrincipal"); return Task.CompletedTask; }
        };
    });

var app = builder.Build();

// Excepciones / HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errApp =>
    {
        errApp.Run(async ctx =>
        {
            var feat = ctx.Features.Get<IExceptionHandlerPathFeature>();
            Console.WriteLine($"[ERR] path={feat?.Path} ex={feat?.Error?.GetType().Name} msg={feat?.Error?.Message}");

            ctx.Response.StatusCode  = 500;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync(
                "<!doctype html><meta charset='utf-8'><title>Error</title>" +
                "<h1>Ups, ocurrió un error.</h1><p>Intenta de nuevo.</p>");
        });
    });
    app.UseHsts();
}

// IMPORTANT: proxy headers antes de lo demás
var fwd = new ForwardedHeadersOptions {
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Confía en el proxy (siempre que controles el perímetro)
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

// Logging de requests
app.UseHttpLogging();

// Si TLS termina en Apache/Nginx y ya rediriges allí, puedes comentar esta línea:
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) => {
  Console.WriteLine($"{ctx.Request.Method} {ctx.Request.Path}");
  await next();
});

//--- DeployStamp middleware (no limpiar sesión en la 1ª visita) ---
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value?.ToLowerInvariant() ?? "";

    // Rutas públicas y estáticos
    var isPublic =
        p.StartsWith("/auth") || p.StartsWith("/webhook") || p.StartsWith("/health") ||
        p.StartsWith("/favicon") || p.StartsWith("/robots") || p.StartsWith("/error");
    var isStatic = isPublic ||
        p.StartsWith("/css") || p.StartsWith("/js") || p.StartsWith("/lib") ||
        p.StartsWith("/images") || p.StartsWith("/organizacional.styles");

    if (!isStatic)
    {
        var deployStamp = builder.Configuration["Deploy:Stamp"] ?? "dev";
        var sessStamp   = ctx.Session.GetString("DeployStamp");

        if (string.IsNullOrEmpty(sessStamp))
        {
            // Primera vez fuera de /auth: solo inicializa
            ctx.Session.SetString("DeployStamp", deployStamp);
        }
        else if (!string.Equals(sessStamp, deployStamp, StringComparison.Ordinal))
        {
            // Solo limpia cuando realmente cambie el stamp entre despliegues
            ctx.Session.Clear();
            ctx.Session.SetString("DeployStamp", deployStamp);
        }
    }

    await next();
});

// --- GATE de protección ---
string[] publicPrefixes = new[] { "/auth", "/webhook", "/health", "/favicon", "/robots", "/error" };
string[] staticPrefixes = new[] { "/css", "/js", "/lib", "/images", "/organizacional.styles" };

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value?.ToLowerInvariant() ?? "";
    var uid = ctx.Session.GetInt32("IdUsuario") ?? 0;

    if (staticPrefixes.Any(pref => p.StartsWith(pref)) ||
        publicPrefixes.Any(pref => p.StartsWith(pref)))
    {
        await next();          // no bloquea login ni estáticos
        return;
    }

    if (uid > 0)               // ya hay sesión
    {
        await next();
        return;
    }

    var returnUrl = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
    ctx.Response.Redirect($"/Auth/Login?returnUrl={returnUrl}");
});

// Al entrar por raíz '/', redirige según sesión
app.MapGet("/", context =>
{
    bool logged =
        (context.User?.Identity?.IsAuthenticated ?? false) ||
        (context.Session.GetInt32("IdUsuario") != null);

    context.Response.Redirect(logged ? "/Dashboard/Index" : "/Auth/Login");
    return Task.CompletedTask;
});

// Rutas
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// ========== WhatsApp Webhook ==========
// Lee configuración
string verifyToken     = builder.Configuration["WABA_VERIFY_TOKEN"]   ?? "";
string appSecret       = builder.Configuration["META_APP_SECRET"]     ?? "";
string accessToken     = builder.Configuration["WABA_ACCESS_TOKEN"]   ?? "";
string phoneNumberId   = builder.Configuration["WABA_PHONE_NUMBER_ID"]?? "";
string tenereApi       = builder.Configuration["TENERE_API_BASE"]     ?? "http://127.0.0.1:5006";
string tenereKey       = builder.Configuration["TENERE_API_KEY"]      ?? "";
int    defaultUserId   = int.TryParse(builder.Configuration["TENERE_DEFAULT_USER_ID"], out var u) ? u : 12;

// Estado por usuario (thread-safe) — SOLO DECLARADO MÁS ABAJO, NO AQUÍ
var state = new ConcurrentDictionary<string, FlowState>();

app.MapGet("/webhooks/whatsapp", (HttpRequest req) =>
{
    var mode      = req.Query["hub.mode"];
    var token     = req.Query["hub.verify_token"];
    var challenge = req.Query["hub.challenge"].ToString();
    return (mode == "subscribe" && token == verifyToken) ? Results.Text(challenge) : Results.Unauthorized();
});

app.MapPost("/webhooks/whatsapp", async (HttpRequest req, IHttpClientFactory f) =>
{
    var raw = await new StreamReader(req.Body).ReadToEndAsync();
    if (!ValidateSignature(appSecret, raw, req.Headers["X-Hub-Signature-256"])) return Results.Unauthorized();

    var json  = JsonNode.Parse(raw);
    var value = json?["entry"]?[0]?["changes"]?[0]?["value"];
    var msg   = value?["messages"]?[0];
    if (msg is null) return Results.Ok();

    var from = msg["from"]!.ToString();
    var type = msg["type"]?.ToString() ?? "text";
    var http = f.CreateClient();

    async Task SendText(string body)
    {
        var payload = new {
            messaging_product = "whatsapp",
            to = from,
            type = "text",
            text = new { body }
        };
        await http.PostAsJsonAsync(
            $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages?access_token={accessToken}", payload);
    }

    async Task SendFincasList()
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Remove("X-Api-Key");
        c.DefaultRequestHeaders.Add("X-Api-Key", tenereKey);

        var empresas = await c.GetFromJsonAsync<List<EmpresaDto>>($"{tenereApi}/api/empresas");
        if (empresas is null || empresas.Count == 0)
        {
            await SendText("No hay fincas registradas en el sistema.");
            return;
        }

        var rows = empresas.Take(10).Select(e => new { id = $"emp_{e.IdEmpresa}", title = e.Nombre }).ToList();

        var payload = new {
            messaging_product = "whatsapp",
            to = from,
            type = "interactive",
            interactive = new {
                type = "list",
                body = new { text = "Elige la *finca* para el pendiente:" }, // *negrita* de WhatsApp
                action = new {
                    button = "Seleccionar",
                    sections = new [] {
                        new { title = "Fincas", rows = rows }
                    }
                }
            }
        };
        await http.PostAsJsonAsync(
            $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages?access_token={accessToken}", payload);
    }

    static string NormalizaCel(string input)
    {
        var digits = new string((input ?? "").Where(char.IsDigit).ToArray());
        return digits; // 7–15 dígitos
    }

    if (type == "interactive")
    {
        var iType = msg["interactive"]?["type"]?.ToString();
        if (iType == "list_reply")
        {
            var selId   = msg["interactive"]?["list_reply"]?["id"]?.ToString();
            var selText = msg["interactive"]?["list_reply"]?["title"]?.ToString();

            if (!string.IsNullOrEmpty(selId) && selId.StartsWith("emp_"))
            {
                var idEmp = int.Parse(selId.Split('_')[1]);
                state[from] = new FlowState("await_nombre", idEmp, null, null, null, null, selText ?? "Finca seleccionada");

                await SendText($"Finca seleccionada: *{selText}*\n\n" +
                               "Por favor, escribe tu *nombre completo*:");
            }
            return Results.Ok();
        }
    }

    var text = msg["text"]?["body"]?.ToString() ?? "";
    var lower = text.Trim().ToLowerInvariant();

    if (lower == "cancelar" || lower == "salir")
    {
        state.TryRemove(from, out _);
        await SendText("Flujo cancelado. Escribe *crear* cuando quieras iniciar de nuevo.");
        return Results.Ok();
    }

    if (!state.TryGetValue(from, out var st))
    {
        if (lower == "crear" || lower == "nuevo" || lower == "pendiente")
        {
            state[from] = new FlowState("await_finca", null, null, null, null, null, null);
            await SendFincasList();
        }
        else
        {
            await SendText("Escribe *crear* para registrar un pendiente.\n" +
                           "También puedes escribir *cancelar* en cualquier momento para terminar.");
        }
        return Results.Ok();
    }

    switch (st.Phase)
    {
        case "await_finca":
            await SendFincasList();
            return Results.Ok();

        case "await_nombre":
        {
            var nombre = text.Trim();
            if (string.IsNullOrWhiteSpace(nombre) || nombre.Length < 3)
            {
                await SendText("Por favor escribe tu *nombre completo* (mín. 3 caracteres).");
                return Results.Ok();
            }
            var next = st with { Phase = "await_ubicacion", Nombre = nombre };
            state[from] = next;
            await SendText("Gracias. Ahora dime la *ubicación* donde ocurre el problema (ej.: bodega principal, finca El Roble, km 3 vía X).");
            return Results.Ok();
        }

        case "await_ubicacion":
        {
            var ubic = text.Trim();
            if (string.IsNullOrWhiteSpace(ubic) || ubic.Length < 3)
            {
                await SendText("Por favor indica una *ubicación* válida (mín. 3 caracteres).");
                return Results.Ok();
            }
            var next = st with { Phase = "await_desc", Ubicacion = ubic };
            state[from] = next;
            await SendText("Perfecto. Ahora describe *brevemente el problema*.");
            return Results.Ok();
        }

        case "await_desc":
        {
            var desc = text.Trim();
            if (string.IsNullOrWhiteSpace(desc) || desc.Length < 5)
            {
                await SendText("La *descripción* es muy corta. Por favor agrega un poco más de detalle.");
                return Results.Ok();
            }
            var next = st with { Phase = "await_cel", Desc = desc };
            state[from] = next;
            await SendText("Para finalizar, comparte un *número de contacto (celular)*.");
            return Results.Ok();
        }

        case "await_cel":
        {
            var celNorm = NormalizaCel(text);
            if (celNorm.Length < 7 || celNorm.Length > 15)
            {
                await SendText("El *número de contacto* no parece válido. Envíalo nuevamente (puedes incluir +57 o solo los dígitos).");
                return Results.Ok();
            }

            var final = st with { Celular = celNorm };

            if (!final.IdEmpresa.HasValue)
            {
                state[from] = new FlowState("await_finca", null, null, null, null, null, null);
                await SendText("Debemos elegir la *finca* primero.");
                await SendFincasList();
                return Results.Ok();
            }

            var descripcionCompuesta =
                $"*Nombre:* {final.Nombre}\n" +
                $"*Finca:* {final.NombreFinca ?? "(id " + final.IdEmpresa.Value + ")"}\n" +
                $"*Ubicación:* {final.Ubicacion}\n" +
                $"*Descripción:* {final.Desc}\n" +
                $"*Contacto:* {celNorm}";

            var c = f.CreateClient();
            c.DefaultRequestHeaders.Remove("X-Api-Key");
            c.DefaultRequestHeaders.Add("X-Api-Key", tenereKey);

            var body = new {
                Descripcion    = descripcionCompuesta,
                IdEmpresa      = final.IdEmpresa.Value,
                IdUsuarioSubio = defaultUserId
            };

            var resp = await c.PostAsJsonAsync($"{tenereApi}/api/pendientes", body);

            if (resp.IsSuccessStatusCode)
            {
                var obj = await resp.Content.ReadFromJsonAsync<JsonNode>();
                var num = obj?["numeroDocumento"]?.ToString() ?? obj?["NumeroDocumento"]?.ToString() ?? "?";
                await SendText($"✅ Pendiente *creado*.\n*Número:* {num}\n\n¡Gracias! Si necesitas otro, escribe *crear*.");
            }
            else
            {
                var err = await resp.Content.ReadAsStringAsync();
                await SendText("❌ No pude crear el pendiente.\n" +
                               "Intenta de nuevo con *crear* o escribe *cancelar*.\n" +
                               $"Detalle: {err}");
            }

            state.TryRemove(from, out _);
            return Results.Ok();
        }
    }

    await SendText("No entendí. Escribe *crear* para iniciar o *cancelar* para terminar.");
    return Results.Ok();
});

static bool ValidateSignature(string appSecret, string payload, string sigHeader)
{
    if (string.IsNullOrEmpty(sigHeader)) return false;
    var expected = "sha256=" + Convert.ToHexString(
        new HMACSHA256(Encoding.UTF8.GetBytes(appSecret)).ComputeHash(Encoding.UTF8.GetBytes(payload))
    ).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sigHeader));
}
// ========== /WhatsApp Webhook ==========

app.Run();

// ---------- Tipos al FINAL del archivo (después de app.Run) ----------
public record EmpresaDto(int IdEmpresa, string Nombre);

// Mantén FlowState aquí abajo también:
public record FlowState(
    string Phase,
    int?   IdEmpresa,
    string? Nombre,
    string? Ubicacion,
    string? Desc,
    string? Celular,
    string? NombreFinca
);