using System.Text.Json;

namespace ESDSuite.Core.Services;

public class I18nService
{
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _locales = new();
    private readonly string _localesDir;

    public I18nService(string? localesDir = null)
    {
        _localesDir = localesDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales");
        LoadLocales();
    }

    public void LoadLocales()
    {
        _locales.Clear();
        if (!Directory.Exists(_localesDir))
        {
            // Fallback search
            var altPath = Path.Combine(Directory.GetCurrentDirectory(), "locales");
            if (Directory.Exists(altPath))
            {
                LoadFromDir(altPath);
                return;
            }
        }
        else
        {
            LoadFromDir(_localesDir);
        }
    }

    private void LoadFromDir(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            var langCode = Path.GetFileNameWithoutExtension(file);
            try
            {
                var content = File.ReadAllText(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(content);
                if (dict != null)
                {
                    _locales[langCode] = dict;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading locale {file}: {ex.Message}");
            }
        }
    }

    public string Translate(string lang, string category, string key, string defaultValue)
    {
        lang = string.IsNullOrWhiteSpace(lang) ? "es" : lang.ToLower();

        if (_locales.TryGetValue(lang, out var catDict) && catDict.TryGetValue(category, out var keyDict) && keyDict.TryGetValue(key, out var val))
        {
            return val;
        }

        // Fallback to ES
        if (_locales.TryGetValue("es", out var esCat) && esCat.TryGetValue(category, out var esKey) && esKey.TryGetValue(key, out var esVal))
        {
            return esVal;
        }

        // Fallback to EN
        if (_locales.TryGetValue("en", out var enCat) && enCat.TryGetValue(category, out var enKey) && enKey.TryGetValue(key, out var enVal))
        {
            return enVal;
        }

        return defaultValue ?? $"[{category}.{key}]";
    }
}
