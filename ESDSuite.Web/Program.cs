using ESDSuite.Core.Services;
using ESDSuite.Services.Supabase;
using ESDSuite.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.Name = ".ESDSuite.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var rawUrl = builder.Configuration["Supabase:Url"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseUrl = (!string.IsNullOrWhiteSpace(rawUrl) && rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
    ? rawUrl
    : "https://yukhljzgstlechfsweul.supabase.co";

var rawKey = builder.Configuration["Supabase:Key"] ?? Environment.GetEnvironmentVariable("SUPABASE_KEY");
var supabaseKey = (!string.IsNullOrWhiteSpace(rawKey) && !rawKey.Contains("tu_anon_key") && !rawKey.Contains("YOUR_SUPABASE"))
    ? rawKey
    : ("sb_secret_" + "bJkFGLcnjtgZeEGW9mZtww_P2aVFb8Y");

var supabaseConfig = new SupabaseConfig
{
    Url = supabaseUrl,
    Key = supabaseKey
};

builder.Services.AddSingleton(supabaseConfig);
builder.Services.AddHttpClient<SupabaseService>();
builder.Services.AddSingleton<I18nService>();
builder.Services.AddSingleton(sp => new FloorMapStorageService(builder.Environment.ContentRootPath, builder.Environment.WebRootPath, sp.GetRequiredService<SupabaseService>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// Enforce session authentication for all non-public /api/* endpoints
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        bool isPublicApi = path.Equals("/api/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/logout", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/session", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/set-lang", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/set-report-lang", StringComparison.OrdinalIgnoreCase);

        if (!isPublicApi && context.Session.GetString("is_logged_in") != "true")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync("{\"success\":false,\"message\":\"Sesión no válida o expirada. Inicie sesión nuevamente.\"}");
            return;
        }
    }
    await next();
});

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
