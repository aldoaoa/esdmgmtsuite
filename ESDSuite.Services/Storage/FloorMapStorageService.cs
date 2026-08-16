using System.Text.Json;
using System.Text.Json.Nodes;
using ESDSuite.Core.Models;
using ESDSuite.Services.Supabase;

namespace ESDSuite.Services.Storage;

public class FloorMapStorageService
{
    private readonly string _storageFilePath;
    private readonly string _uploadsDirectory;
    private readonly SupabaseService? _supabase;
    private readonly object _lock = new();

    public FloorMapStorageService(string? contentRootPath = null, string? webRootPath = null, SupabaseService? supabase = null)
    {
        _supabase = supabase;
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
                    if (list.Count == 0)
                    {
                        list = GetDefaultMaps();
                        string jsonDefault = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(_storageFilePath, jsonDefault);
                    }
                }
                catch
                {
                    list = GetDefaultMaps();
                }
            }
        }

        // Try syncing/merging from Supabase if available
        if (_supabase != null)
        {
            try
            {
                var sbMaps = await _supabase.GetFloorMapsFromSupabaseAsync(siteId);
                if (sbMaps != null && sbMaps.Count > 0)
                {
                    foreach (var node in sbMaps)
                    {
                        if (node is JsonObject obj)
                        {
                            string id = obj["id"]?.ToString() ?? "";
                            if (string.IsNullOrEmpty(id)) continue;

                            var existing = list.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                            var pointsList = new List<FloorMapPoint>();
                            if (obj["points"] is JsonArray pa)
                            {
                                foreach (var pNode in pa)
                                {
                                    if (pNode is JsonObject pObj)
                                    {
                                        pointsList.Add(new FloorMapPoint
                                        {
                                            Id = pObj["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                                            Code = pObj["code"]?.ToString() ?? "1",
                                            Label = pObj["label"]?.ToString() ?? "Punto",
                                            XPercent = pObj["xPercent"]?.GetValue<double>() ?? 0,
                                            YPercent = pObj["yPercent"]?.GetValue<double>() ?? 0,
                                            LastResistanceOhms = pObj["lastResistanceOhms"] != null ? pObj["lastResistanceOhms"]!.GetValue<double>() : null
                                        });
                                    }
                                }
                            }

                            if (existing == null)
                            {
                                list.Add(new FloorMapConfig
                                {
                                    Id = id,
                                    SiteId = obj["site_id"]?.ToString() ?? "",
                                    AreaName = obj["area_name"]?.ToString() ?? "",
                                    AreaId = obj["area_name"]?.ToString() ?? "",
                                    MapName = obj["map_name"]?.ToString() ?? "",
                                    ImageUrl = obj["image_url"]?.ToString() ?? "",
                                    TotalAreaValue = obj["total_area_value"]?.GetValue<double>() ?? 500.0,
                                    AreaUnit = obj["area_unit"]?.ToString() ?? "m2",
                                    Points = pointsList
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Supabase floor_maps fetch note: {ex.Message}");
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
        if (string.IsNullOrWhiteSpace(map.Id))
        {
            map.Id = Guid.NewGuid().ToString();
        }

        var maps = await GetMapsAsync();
        int existingIndex = maps.FindIndex(m => m.Id.Equals(map.Id, StringComparison.OrdinalIgnoreCase));

        map.UpdatedAt = DateTime.UtcNow;

        if (existingIndex >= 0)
        {
            maps[existingIndex] = map;
        }
        else
        {
            map.CreatedAt = DateTime.UtcNow;
            maps.Add(map);
        }

        SaveAllMaps(maps);

        // Sync with Supabase asynchronously
        if (_supabase != null)
        {
            try
            {
                var sbPayload = new JsonObject
                {
                    ["id"] = map.Id,
                    ["site_id"] = map.SiteId,
                    ["area_name"] = map.AreaName,
                    ["map_name"] = map.MapName,
                    ["image_url"] = map.ImageUrl,
                    ["total_area_value"] = map.TotalAreaValue,
                    ["area_unit"] = map.AreaUnit,
                    ["points"] = JsonSerializer.SerializeToNode(map.Points),
                    ["updated_at"] = DateTime.UtcNow.ToString("o")
                };
                await _supabase.SaveFloorMapToSupabaseAsync(sbPayload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Supabase table floor_maps sync: {ex.Message}");
            }
        }

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

            string contentType = ext.ToLower() switch
            {
                ".svg" => "image/svg+xml",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/png"
            };

            // 1. Try uploading to Supabase Storage bucket 'maps'
            if (_supabase != null)
            {
                try
                {
                    string? publicUrl = await _supabase.UploadStorageFileAsync("maps", safeFileName, bytes, contentType);
                    if (!string.IsNullOrEmpty(publicUrl))
                    {
                        return publicUrl;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Supabase storage bucket upload fallback: {ex.Message}");
                }
            }

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
                MapName = "Plano Principal SMT 1 & Ensamble",
                ImageUrl = "/images/mockups/smt1_layout.svg",
                TotalAreaValue = 500.0,
                AreaUnit = "m2",
                Points = new List<FloorMapPoint>
                {
                    new FloorMapPoint { Id = "p1", Code = "1", Label = "Pick & Place Chip Shooter", XPercent = 25.0, YPercent = 29.0, LastResistanceOhms = 4.5e7, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p2", Code = "2", Label = "Aisle / Pasillo Central", XPercent = 49.0, YPercent = 23.0, LastResistanceOhms = 3.5e8, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p3", Code = "3", Label = "Centro Pasillo SMT", XPercent = 31.5, YPercent = 52.0, LastResistanceOhms = 2.8e7, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p4", Code = "4", Label = "Salida AOI Óptica", XPercent = 41.0, YPercent = 81.0, LastResistanceOhms = 5.2e7, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p5", Code = "5", Label = "ICT / In-Circuit Test Fixtures", XPercent = 63.5, YPercent = 78.0, LastResistanceOhms = 2.5e9, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p6", Code = "6", Label = "Packaging & ESD Shielding Bags", XPercent = 72.5, YPercent = 55.0, LastResistanceOhms = 6.1e7, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p7", Code = "7", Label = "Workbench 02 ESD", XPercent = 86.0, YPercent = 29.0, LastResistanceOhms = 1.8e9, MeasuredAt = DateTime.UtcNow },
                    new FloorMapPoint { Id = "p8", Code = "8", Label = "Workbench 01 ESD", XPercent = 68.0, YPercent = 19.0, LastResistanceOhms = 4.8e8, MeasuredAt = DateTime.UtcNow }
                }
            }
        };
    }
}
