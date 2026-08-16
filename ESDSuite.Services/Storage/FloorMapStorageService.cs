using System.Text.Json;
using ESDSuite.Core.Models;

namespace ESDSuite.Services.Storage;

public class FloorMapStorageService
{
    private readonly string _storageFilePath;
    private readonly string _uploadsDirectory;
    private readonly object _lock = new();

    public FloorMapStorageService(string? contentRootPath = null, string? webRootPath = null)
    {
        string root = !string.IsNullOrEmpty(contentRootPath) ? contentRootPath : AppDomain.CurrentDomain.BaseDirectory;
        string appData = Path.Combine(root, "App_Data");
        if (!Directory.Exists(appData))
        {
            Directory.CreateDirectory(appData);
        }
        _storageFilePath = Path.Combine(appData, "floor_maps.json");

        string wwwroot = !string.IsNullOrEmpty(webRootPath) ? webRootPath : Path.Combine(root, "wwwroot");
        _uploadsDirectory = Path.Combine(wwwroot, "uploads", "maps");
        if (!Directory.Exists(_uploadsDirectory))
        {
            Directory.CreateDirectory(_uploadsDirectory);
        }
    }

    public async Task<List<FloorMapConfig>> GetMapsAsync(string? siteId = null)
    {
        List<FloorMapConfig> list;
        lock (_lock)
        {
            if (!File.Exists(_storageFilePath))
            {
                list = GetDefaultMaps();
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(_storageFilePath);
                    list = JsonSerializer.Deserialize<List<FloorMapConfig>>(json) ?? new List<FloorMapConfig>();
                }
                catch
                {
                    list = new List<FloorMapConfig>();
                }
            }
        }

        if (!string.IsNullOrEmpty(siteId))
        {
            return list.Where(m => string.IsNullOrEmpty(m.SiteId) || m.SiteId.Equals(siteId, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return await Task.FromResult(list);
    }

    public async Task<FloorMapConfig?> GetMapByIdAsync(string mapId)
    {
        var maps = await GetMapsAsync();
        return maps.FirstOrDefault(m => m.Id.Equals(mapId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<FloorMapConfig> SaveMapAsync(FloorMapConfig map)
    {
        var maps = await GetMapsAsync();
        int existingIndex = maps.FindIndex(m => m.Id.Equals(map.Id, StringComparison.OrdinalIgnoreCase));

        map.UpdatedAt = DateTime.UtcNow;

        if (existingIndex >= 0)
        {
            maps[existingIndex] = map;
        }
        else
        {
            if (string.IsNullOrEmpty(map.Id)) map.Id = Guid.NewGuid().ToString();
            map.CreatedAt = DateTime.UtcNow;
            maps.Add(map);
        }

        SaveAllMaps(maps);
        return map;
    }

    public async Task<bool> SaveMapPointsAsync(string mapId, List<FloorMapPoint> points)
    {
        var maps = await GetMapsAsync();
        var map = maps.FirstOrDefault(m => m.Id.Equals(mapId, StringComparison.OrdinalIgnoreCase));
        if (map == null) return false;

        map.Points = points;
        map.UpdatedAt = DateTime.UtcNow;

        SaveAllMaps(maps);
        return true;
    }

    public async Task<bool> DeleteMapAsync(string mapId)
    {
        var maps = await GetMapsAsync();
        int removed = maps.RemoveAll(m => m.Id.Equals(mapId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveAllMaps(maps);
            return true;
        }
        return false;
    }

    public async Task<string> SaveImageFromBase64Async(string fileName, string base64Data)
    {
        try
        {
            string cleanBase64 = base64Data;
            if (cleanBase64.Contains(","))
            {
                cleanBase64 = cleanBase64.Substring(cleanBase64.IndexOf(",") + 1);
            }

            byte[] bytes = Convert.FromBase64String(cleanBase64);
            string ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext)) ext = ".png";

            string safeFileName = $"map_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
            string fullPath = Path.Combine(_uploadsDirectory, safeFileName);

            await File.WriteAllBytesAsync(fullPath, bytes);
            return $"/uploads/maps/{safeFileName}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving map image: {ex.Message}");
            return string.Empty;
        }
    }

    private void SaveAllMaps(List<FloorMapConfig> maps)
    {
        lock (_lock)
        {
            string json = JsonSerializer.Serialize(maps, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storageFilePath, json);
        }
    }

    private List<FloorMapConfig> GetDefaultMaps()
    {
        return new List<FloorMapConfig>
        {
            new FloorMapConfig
            {
                Id = "map-smt-01",
                SiteId = "eff70028-0759-4033-9c2b-41e1c1cc6efd",
                AreaName = "SMT 1",
                AreaId = "SMT 1",
                MapName = "Plano Principal SMT 1",
                ImageUrl = "/images/mockups/smt1_layout.svg",
                TotalAreaValue = 500.0,
                AreaUnit = "m2",
                Points = new List<FloorMapPoint>
                {
                    new FloorMapPoint { Id = "p1", Code = "1", Label = "Entrada SMT 1", XPercent = 18.5, YPercent = 25.0, LastResistanceOhms = 4.2e7, MeasuredAt = DateTime.UtcNow.AddDays(-1) },
                    new FloorMapPoint { Id = "p2", Code = "2", Label = "Área Feeder / Carga", XPercent = 45.0, YPercent = 22.0, LastResistanceOhms = 6.8e7, MeasuredAt = DateTime.UtcNow.AddDays(-1) },
                    new FloorMapPoint { Id = "p3", Code = "3", Label = "Centro Pasillo Pick&Place", XPercent = 78.0, YPercent = 28.0, LastResistanceOhms = 3.5e7, MeasuredAt = DateTime.UtcNow.AddDays(-1) },
                    new FloorMapPoint { Id = "p4", Code = "4", Label = "Salida Horno Reflujo", XPercent = 30.0, YPercent = 75.0, LastResistanceOhms = 5.1e7, MeasuredAt = DateTime.UtcNow.AddDays(-1) },
                    new FloorMapPoint { Id = "p5", Code = "5", Label = "Inspección Óptica AOI", XPercent = 72.0, YPercent = 72.0, LastResistanceOhms = 8.9e7, MeasuredAt = DateTime.UtcNow.AddDays(-1) }
                }
            }
        };
    }
}
