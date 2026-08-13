using System.Text.Json.Serialization;

namespace ESDSuite.Core.Models;

public class UserSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "AUDITOR";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("is_logged_in")]
    public bool IsLoggedIn { get; set; } = true;

    [JsonPropertyName("site_id")]
    public string? SiteId { get; set; }

    [JsonPropertyName("company_id")]
    public string? CompanyId { get; set; }

    [JsonPropertyName("password_hash")]
    public string? PasswordHash { get; set; }

    [JsonIgnore]
    public string? PermissionsJson { get; set; }

    [JsonPropertyName("site_name")]
    public string SiteName { get; set; } = "Site Principal";

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = "ESD Enterprise";

    [JsonPropertyName("permissions")]
    public UserPermissions Permissions { get; set; } = new UserPermissions();
}

public class UserPermissions
{
    [JsonPropertyName("audit")]
    public bool Audit { get; set; } = true;

    [JsonPropertyName("view")]
    public bool View { get; set; } = true;

    [JsonPropertyName("inventory")]
    public bool Inventory { get; set; } = true;

    [JsonPropertyName("reports")]
    public bool Reports { get; set; } = true;

    [JsonPropertyName("settings")]
    public bool Settings { get; set; } = false;
}
