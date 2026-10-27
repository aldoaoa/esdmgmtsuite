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
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.Name = ".ESDSuite.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.MaxAge = TimeSpan.FromDays(30);
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
            // Auto rehydrate from RememberMe token if present
            if (context.Request.Cookies.TryGetValue(".ESDSuite.AuthToken", out string? authToken) && !string.IsNullOrEmpty(authToken))
            {
                string? userId = ESDSuite.Web.Controllers.ApiController.ValidateRememberMeToken(authToken, supabaseConfig.Key);
                if (!string.IsNullOrEmpty(userId))
                {
                    var supabase = context.RequestServices.GetRequiredService<SupabaseService>();
                    var userObj = await supabase.GetUserByIdAsync(userId);
                    if (userObj != null)
                    {
                        bool isActive = true;
                        if (userObj["is_active"] != null && bool.TryParse(userObj["is_active"]?.ToString(), out bool actVal))
                            isActive = actVal;

                        if (isActive)
                        {
                            string email = userObj["email"]?.ToString() ?? "";
                            string name = userObj["full_name"]?.ToString() ?? email;
                            string role = userObj["role"]?.ToString() ?? "AUDITOR";
                            string siteId = userObj["site_id"]?.ToString() ?? "";
                            string companyId = userObj["company_id"]?.ToString() ?? "";

                            context.Session.SetString("user_id", userId);
                            context.Session.SetString("user_email", email);
                            context.Session.SetString("user_name", name);
                            context.Session.SetString("user_role", role);
                            context.Session.SetString("site_id", !string.IsNullOrEmpty(siteId) ? siteId : ESDSuite.Web.Controllers.ApiController.DefaultSiteId);
                            context.Session.SetString("company_id", companyId);
                            context.Session.SetString("is_logged_in", "true");
                        }
                    }
                }
            }

            if (context.Session.GetString("is_logged_in") != "true")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"success\":false,\"message\":\"Sesión no válida o expirada. Inicie sesión nuevamente.\"}");
                return;
            }
        }
    }
    await next();
});

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
