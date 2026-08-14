using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ESDSuite.Core.Models;
using ESDSuite.Services.Auth;

namespace ESDSuite.Services.Supabase;

public class SupabaseService
{
    private readonly HttpClient _http;
    private readonly SupabaseConfig _config;

    public SupabaseService(HttpClient http, SupabaseConfig config)
    {
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(_config.Url.TrimEnd('/') + "/rest/v1/");
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("apikey", _config.Key);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
        _http.DefaultRequestHeaders.Add("Prefer", "return=representation");
    }

    private async Task<JsonArray> GetAsync(string tableWithQuery)
    {
        try
        {
            var response = await _http.GetAsync(tableWithQuery);
            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GetAsync Error ({response.StatusCode}) for {tableWithQuery}: {errStr}");
                return new JsonArray();
            }
            var json = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(json);
            return node as JsonArray ?? new JsonArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAsync Exception for {tableWithQuery}: {ex.Message}");
            return new JsonArray();
        }
    }

    private async Task<JsonObject?> InsertAsync(string table, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(table, content);
            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"InsertAsync Error ({response.StatusCode}) for {table}: {errStr}");
                try
                {
                    var errObj = JsonNode.Parse(errStr) as JsonObject;
                    if (errObj != null) return errObj;
                }
                catch { }
                return new JsonObject { ["message"] = errStr };
            }

            var resJson = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(resJson);
            if (node is JsonArray arr && arr.Count > 0)
            {
                return arr[0] as JsonObject;
            }
            return node as JsonObject;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"InsertAsync Exception for {table}: {ex.Message}");
            return new JsonObject { ["message"] = ex.Message };
        }
    }

    private async Task<bool> UpdateAsync(string table, string query, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{table}?{query}")
            {
                Content = content
            };
            var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> DeleteAsync(string table, string query)
    {
        try
        {
            var response = await _http.DeleteAsync($"{table}?{query}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // --- AUTHENTICATION SERVICE ---
    public async Task<(bool Success, UserSession? Session, string Message)> IniciarSesionAsync(string email, string password)
    {
        string cleanEmail = email.Trim();
        var users = await GetAsync($"users?email=ilike.{cleanEmail}&select=*");
        if (users.Count == 0)
        {
            return (false, null, "No existe un usuario registrado con este correo.");
        }

        var userObj = users[0] as JsonObject;
        if (userObj == null)
        {
            return (false, null, "Error al procesar usuario.");
        }

        bool isActive = userObj["is_active"]?.GetValue<bool>() ?? true;
        if (!isActive)
        {
            return (false, null, "Tu cuenta de usuario está inactiva.");
        }

        string storedHash = userObj["password_hash"]?.ToString() ?? "";
        bool pwdValid = PasswordHasher.VerifyPassword(storedHash, password);

        if (!pwdValid)
        {
            return (false, null, "La contraseña ingresada es incorrecta.");
        }

        string siteId = userObj["site_id"]?.ToString() ?? "";
        string companyId = userObj["company_id"]?.ToString() ?? "";

        string siteName = "Queretaro Plant";
        string companyName = "BCS AIS";

        if (string.IsNullOrEmpty(siteId))
        {
            var defaultSites = await GetAsync("sites?select=id,name,company_id&limit=1");
            if (defaultSites.Count > 0 && defaultSites[0] is JsonObject dsObj)
            {
                siteId = dsObj["id"]?.ToString() ?? "eff70028-0759-4033-9c2b-41e1c1cc6efd";
                siteName = dsObj["name"]?.ToString() ?? siteName;
                if (string.IsNullOrEmpty(companyId)) companyId = dsObj["company_id"]?.ToString() ?? "";
            }
            else
            {
                siteId = "eff70028-0759-4033-9c2b-41e1c1cc6efd";
            }
        }
        else
        {
            var sites = await GetAsync($"sites?id=eq.{siteId}&select=name,company_id");
            if (sites.Count > 0 && sites[0] is JsonObject sObj)
            {
                siteName = sObj["name"]?.ToString() ?? siteName;
                if (string.IsNullOrEmpty(companyId)) companyId = sObj["company_id"]?.ToString() ?? "";
            }
        }

        if (!string.IsNullOrEmpty(companyId))
        {
            var companies = await GetAsync($"companies?id=eq.{companyId}&select=name");
            if (companies.Count > 0 && companies[0] is JsonObject cObj)
            {
                companyName = cObj["name"]?.ToString() ?? companyName;
            }
        }

        var session = new UserSession
        {
            Id = userObj["id"]?.ToString() ?? Guid.NewGuid().ToString(),
            Email = userObj["email"]?.ToString() ?? email,
            FullName = userObj["full_name"]?.ToString() ?? userObj["email"]?.ToString() ?? "Usuario",
            Role = userObj["role"]?.ToString() ?? "AUDITOR",
            CompanyId = companyId,
            SiteId = siteId,
            CompanyName = companyName,
            SiteName = siteName,
            IsLoggedIn = true
        };

        return (true, session, "OK");
    }

    // --- AUDIT & MEASUREMENTS ---
    public async Task<JsonObject?> GetUltimaMedicionAsync(string idElemento)
    {
        string cleanId = idElemento.Trim().ToUpper();

        var assets = await GetAsync($"assets?custom_id=eq.{cleanId}&select=id,custom_id,category,location,status");
        if (assets.Count > 0 && assets[0] is JsonObject asset)
        {
            string assetId = asset["id"]?.ToString() ?? "";
            var measurements = await GetAsync($"measurements?asset_id=eq.{assetId}&order=measured_at.desc&limit=1");
            if (measurements.Count > 0 && measurements[0] is JsonObject m)
            {
                m["id_elemento"] = cleanId;
                m["category"] = asset["category"]?.ToString();
                m["location"] = asset["location"]?.ToString();
                return m;
            }
        }

        var maq = await GetAsync($"mediciones_maquinaria?id_maquinaria=eq.{cleanId}&order=fecha_medicion.desc&limit=1");
        if (maq.Count > 0) return maq[0] as JsonObject;

        var inv = await GetAsync($"inventario_esd?id_elemento=eq.{cleanId}&select=*");
        if (inv.Count > 0) return inv[0] as JsonObject;

        var val = await GetAsync($"validacion_esd?id_elemento=eq.{cleanId}&order=fecha_medicion.desc&limit=1");
        if (val.Count > 0) return val[0] as JsonObject;

        return null;
    }

    public async Task<JsonObject?> InsertMeasurementAsync(object data)
    {
        return await InsertAsync("measurements", data);
    }

    public async Task<JsonArray> GetAssetsAsync(string siteId)
    {
        return await GetAsync($"assets?site_id=eq.{siteId}&select=*");
    }

    public async Task<JsonObject?> InsertAssetAsync(object data)
    {
        return await InsertAsync("assets", data);
    }

    public async Task<JsonArray> GetEventMeterLogsAsync()
    {
        return await GetAsync("measurements?extra_data->>type=eq.event_meter&select=*&order=measured_at.desc");
    }

    // --- INFRASTRUCTURE EPA ---
    public async Task<JsonArray> GetGroundingLogsAsync(string siteId)
    {
        return await GetAsync($"grounding_logs?site_id=eq.{siteId}&select=*&order=created_at.desc");
    }

    public async Task<JsonObject?> InsertGroundingLogAsync(object data)
    {
        return await InsertAsync("grounding_logs", data);
    }

    public async Task<JsonArray> GetFloorValidationLogsAsync(string siteId)
    {
        return await GetAsync($"floor_validation_logs?site_id=eq.{siteId}&select=*&order=created_at.desc");
    }

    public async Task<JsonObject?> InsertFloorValidationLogAsync(object data)
    {
        return await InsertAsync("floor_validation_logs", data);
    }

    public async Task<JsonArray> GetIsolatedConductorsLogsAsync(string siteId)
    {
        return await GetAsync($"isolated_conductors_logs?site_id=eq.{siteId}&select=*&order=created_at.desc");
    }

    public async Task<JsonObject?> InsertIsolatedConductorsLogAsync(object data)
    {
        return await InsertAsync("isolated_conductors_logs", data);
    }

    public async Task<JsonArray> GetEntranceCheckersLogsAsync(string siteId)
    {
        return await GetAsync($"entrance_checkers_logs?site_id=eq.{siteId}&select=*&order=created_at.desc");
    }

    public async Task<JsonObject?> InsertEntranceCheckersLogAsync(object data)
    {
        return await InsertAsync("entrance_checkers_logs", data);
    }

    // --- SCHEDULE & REPORTS ---
    public async Task<JsonObject?> InsertLogReportesLineaAsync(object data)
    {
        return await InsertAsync("log_reportes_linea", data);
    }

    // --- SENSITIVITY LAB ---
    public async Task<JsonArray> GetCatalogoSensibilidadAsync()
    {
        return await GetAsync("catalogo_sensibilidad?select=*&order=numero_parte.asc");
    }

    public async Task<JsonObject?> InsertCatalogoSensibilidadAsync(object data)
    {
        return await InsertAsync("catalogo_sensibilidad", data);
    }

    public async Task<JsonArray> GetComponentesSensibilidadAsync(string idProducto)
    {
        return await GetAsync($"componentes_sensibilidad?id_producto=eq.{idProducto}&select=*");
    }

    public async Task<JsonObject?> InsertComponenteSensibilidadAsync(object data)
    {
        return await InsertAsync("componentes_sensibilidad", data);
    }

    // --- PRODUCT ROUTES ---
    public async Task<JsonArray> GetCatalogoProductosAsync(string siteId)
    {
        return await GetAsync($"catalogo_productos?site_id=eq.{siteId}&select=*&order=nombre_producto.asc");
    }

    public async Task<JsonObject?> InsertCatalogoProductoAsync(object data)
    {
        return await InsertAsync("catalogo_productos", data);
    }

    public async Task<bool> UpdateCatalogoProductoRutaAsync(string nombreProducto, string siteId, object lineasAsociadas)
    {
        return await UpdateAsync("catalogo_productos", $"nombre_producto=eq.{nombreProducto}&site_id=eq.{siteId}", new { lineas_asociadas = lineasAsociadas });
    }

    public async Task<JsonArray> GetCatalogoLineasAsync(string? siteId = null)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("catalogo_lineas?select=*&order=nombre_linea.asc");
        return await GetAsync($"catalogo_lineas?site_id=eq.{siteId}&select=*&order=nombre_linea.asc");
    }

    public async Task<JsonObject?> InsertCatalogoLineaAsync(object data)
    {
        return await InsertAsync("catalogo_lineas", data);
    }

    // --- EMPLOYEES & TRAINING EXAMS ---
    public async Task<JsonArray> GetEmpleadosBatasAsync(string siteId)
    {
        return await GetAsync($"empleados_batas?site_id=eq.{siteId}&select=*&order=num_empleado.asc");
    }

    public async Task<JsonObject?> InsertOrUpdateEmpleadoAsync(JsonObject emp)
    {
        string numEmp = emp["num_empleado"]?.ToString() ?? "";
        string siteId = emp["site_id"]?.ToString() ?? "";

        var existing = await GetAsync($"empleados_batas?num_empleado=eq.{numEmp}&site_id=eq.{siteId}&select=*");
        if (existing.Count > 0)
        {
            bool ok = await UpdateAsync("empleados_batas", $"num_empleado=eq.{numEmp}&site_id=eq.{siteId}", emp);
            return ok ? emp : null;
        }
        return await InsertAsync("empleados_batas", emp);
    }

    public async Task<JsonArray> GetEntrenamientosEsdAsync()
    {
        return await GetAsync("entrenamientos_esd?select=*&order=fecha_entrenamiento.desc");
    }

    public async Task<JsonObject?> InsertEntrenamientoEsdAsync(object data)
    {
        return await InsertAsync("entrenamientos_esd", data);
    }

    // --- MEASUREMENT EQUIPMENT ---
    public async Task<JsonArray> GetCatalogoEquiposAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("catalogo_equipos?select=*&order=codigo_equipo.asc");
        return await GetAsync($"catalogo_equipos?site_id=eq.{siteId}&select=*&order=codigo_equipo.asc");
    }

    public async Task<JsonObject?> InsertCatalogoEquipoAsync(object data)
    {
        return await InsertAsync("catalogo_equipos", data);
    }

    public async Task<bool> UpdateCatalogoEquipoAsync(string id, object data)
    {
        return await UpdateAsync("catalogo_equipos", $"id=eq.{id}", data);
    }

    public async Task<bool> DeleteCatalogoEquipoAsync(string id)
    {
        return await DeleteAsync("catalogo_equipos", $"id=eq.{id}");
    }

    // --- SETTINGS, TENANTS & USERS ---
    public async Task<JsonArray> GetCompaniesAsync()
    {
        return await GetAsync("companies?select=*&order=name.asc");
    }

    public async Task<JsonObject?> InsertCompanyAsync(object data)
    {
        return await InsertAsync("companies", data);
    }

    public async Task<JsonArray> GetSitesAsync(string? companyId = null)
    {
        if (string.IsNullOrEmpty(companyId)) return await GetAsync("sites?select=*,companies(name)&order=name.asc");
        return await GetAsync($"sites?company_id=eq.{companyId}&select=*,companies(name)&order=name.asc");
    }

    public async Task<JsonObject?> InsertSiteAsync(object data)
    {
        return await InsertAsync("sites", data);
    }

    public async Task<bool> UpdateSiteAsync(string id, object data)
    {
        return await UpdateAsync("sites", $"id=eq.{id}", data);
    }

    public async Task<JsonArray> GetUsersAsync(string? companyId = null, string? siteId = null)
    {
        if (!string.IsNullOrEmpty(siteId))
        {
            return await GetAsync($"users?site_id=eq.{siteId}&select=*,sites!users_site_id_fkey(name),companies(name)&order=email.asc");
        }
        if (!string.IsNullOrEmpty(companyId))
        {
            return await GetAsync($"users?company_id=eq.{companyId}&select=*,sites!users_site_id_fkey(name),companies(name)&order=email.asc");
        }
        return await GetAsync("users?select=*,sites!users_site_id_fkey(name),companies(name)&order=email.asc");
    }

    public async Task<JsonObject?> InsertUserAsync(object data)
    {
        return await InsertAsync("users", data);
    }

    public async Task<bool> UpdateUserAsync(string id, object data)
    {
        return await UpdateAsync("users", $"id=eq.{id}", data);
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        return await DeleteAsync("users", $"id=eq.{id}");
    }
}
