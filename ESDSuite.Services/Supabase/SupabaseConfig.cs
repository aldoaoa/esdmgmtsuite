namespace ESDSuite.Services.Supabase;

public class SupabaseConfig
{
    public string Url { get; set; } = "https://yukhljzgstlechfsweul.supabase.co";
    public string Key { get; set; } = Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? "YOUR_SUPABASE_SERVICE_KEY";
}
