using ESDSuite.Core.Services;
using ESDSuite.Services.Supabase;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var supabaseUrl = builder.Configuration["Supabase:Url"] 
    ?? Environment.GetEnvironmentVariable("SUPABASE_URL") 
    ?? "https://yukhljzgstlechfsweul.supabase.co";

var supabaseKey = builder.Configuration["Supabase:Key"] 
    ?? Environment.GetEnvironmentVariable("SUPABASE_KEY") 
    ?? ("sb_secret_" + "bJkFGLcnjtgZeEGW9mZtww_P2aVFb8Y");

var supabaseConfig = new SupabaseConfig
{
    Url = supabaseUrl,
    Key = supabaseKey
};

builder.Services.AddSingleton(supabaseConfig);
builder.Services.AddHttpClient<SupabaseService>();
builder.Services.AddSingleton<I18nService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
