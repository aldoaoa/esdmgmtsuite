using System.Text.Json.Serialization;

namespace ESDSuite.Core.Models;

public class FloorMapPoint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("code")]
    public string Code { get; set; } = "1";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "Punto de Medición 1";

    [JsonPropertyName("xPercent")]
    public double XPercent { get; set; }

    [JsonPropertyName("yPercent")]
    public double YPercent { get; set; }

    [JsonPropertyName("lastResistanceOhms")]
    public double? LastResistanceOhms { get; set; }

    [JsonPropertyName("measuredAt")]
    public DateTime? MeasuredAt { get; set; }

    [JsonPropertyName("statusResult")]
    public string StatusResult => LastResistanceOhms == null ? "PENDING"
        : (LastResistanceOhms <= 1.0e8 ? "PASS"
        : (LastResistanceOhms <= 1.0e9 ? "WARNING" : "FAIL"));
}

public class FloorMapConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("siteId")]
    public string SiteId { get; set; } = string.Empty;

    [JsonPropertyName("areaName")]
    public string AreaName { get; set; } = string.Empty;

    [JsonPropertyName("areaId")]
    public string AreaId { get; set; } = string.Empty;

    [JsonPropertyName("mapName")]
    public string MapName { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("totalAreaValue")]
    public double TotalAreaValue { get; set; } = 500.0;

    [JsonPropertyName("areaUnit")]
    public string AreaUnit { get; set; } = "m2"; // "m2" or "ft2"

    [JsonPropertyName("points")]
    public List<FloorMapPoint> Points { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SaveMapPointsDto
{
    [JsonPropertyName("mapId")]
    public string MapId { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public List<FloorMapPoint> Points { get; set; } = new();
}

public class SaveFloorMeasurementBatchDto
{
    [JsonPropertyName("mapId")]
    public string MapId { get; set; } = string.Empty;

    [JsonPropertyName("siteId")]
    public string SiteId { get; set; } = string.Empty;

    [JsonPropertyName("areaName")]
    public string AreaName { get; set; } = string.Empty;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 23.5;

    [JsonPropertyName("humidity")]
    public double Humidity { get; set; } = 45.0;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public List<FloorMapPoint> Points { get; set; } = new();
}
