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
                return null;
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
            return null;
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

    // --- AUTH ---
    public async Task<(bool Success, UserSession? Session, string Message)> IniciarSesionAsync(string email, string password)
    {
        string cleanEmail = email.Trim().ToLower();
        Console.WriteLine($"[IniciarSesionAsync] Attempting login for email: '{cleanEmail}'");

        var users = await GetAsync($"users?email=eq.{Uri.EscapeDataString(cleanEmail)}&select=*,sites!users_site_id_fkey(name),companies(name)");
        if (users.Count == 0)
        {
            users = await GetAsync($"users?email=eq.{Uri.EscapeDataString(cleanEmail)}&select=*");
        }

        if (users.Count == 0)
        {
            Console.WriteLine($"[IniciarSesionAsync] User not found for email: '{cleanEmail}'");
            return (false, null, "user_not_found");
        }

        var u = users[0] as JsonObject;
        if (u == null) return (false, null, "user_not_found");

        bool isActive = u["is_active"]?.GetValue<bool>() ?? true;
        if (!isActive)
        {
            Console.WriteLine($"[IniciarSesionAsync] Account inactive for email: '{cleanEmail}'");
            return (false, null, "account_inactive");
        }

        string storedHash = u["password_hash"]?.ToString() ?? "";
        bool passwordMatches = PasswordHasher.VerifyPassword(storedHash, password);
        Console.WriteLine($"[IniciarSesionAsync] Password match result for '{cleanEmail}': {passwordMatches}");

        if (!passwordMatches)
        {
            return (false, null, "invalid_password");
        }

        var session = new UserSession
        {
            Id = u["id"]?.ToString() ?? "",
            Email = u["email"]?.ToString() ?? cleanEmail,
            FullName = u["full_name"]?.ToString() ?? cleanEmail,
            Role = u["role"]?.ToString() ?? "AUDITOR",
            IsActive = true,
            SiteId = u["site_id"]?.ToString(),
            CompanyId = u["company_id"]?.ToString(),
            PasswordHash = storedHash
        };

        if (u["sites"] is JsonObject sObj && sObj["name"] != null)
        {
            session.SiteName = sObj["name"]!.ToString();
        }
        else if (!string.IsNullOrEmpty(session.SiteId))
        {
            var siteRes = await GetAsync($"sites?id=eq.{session.SiteId}&select=name");
            if (siteRes.Count > 0 && siteRes[0] is JsonObject s1)
            {
                session.SiteName = s1["name"]?.ToString() ?? "Site Principal";
            }
        }

        if (u["companies"] is JsonObject cObj && cObj["name"] != null)
        {
            session.CompanyName = cObj["name"]!.ToString();
        }

        string? permsRaw = u["permissions"]?.ToString();
        if (!string.IsNullOrEmpty(permsRaw))
        {
            try
            {
                var p = JsonSerializer.Deserialize<UserPermissions>(permsRaw);
                if (p != null) session.Permissions = p;
            }
            catch { }
        }

        return (true, session, "OK");
    }

    // --- AUDIT VERIFICATION (VALIDACION_ESD, INVENTARIO_ESD, MEDICIONES_MAQUINARIA) ---
    public async Task<JsonObject?> GetUltimaMedicionAsync(string idElemento)
    {
        string idClean = Uri.EscapeDataString(idElemento.Trim());
        
        // 1. Buscar en validacion_esd
        var valRes = await GetAsync($"validacion_esd?id_elemento=ilike.{idClean}&order=fecha_medicion.desc&limit=1");
        if (valRes.Count > 0 && valRes[0] is JsonObject vObj)
        {
            return vObj;
        }

        // 2. Buscar en mediciones_maquinaria
        var maqRes = await GetAsync($"mediciones_maquinaria?id_maquinaria=ilike.{idClean}&order=fecha_medicion.desc&limit=1");
        if (maqRes.Count > 0 && maqRes[0] is JsonObject mObj)
        {
            mObj["es_maquinaria"] = true;
            return mObj;
        }

        // 3. Buscar en inventario_esd
        var invRes = await GetAsync($"inventario_esd?id_producto=ilike.{idClean}&limit=1");
        if (invRes.Count > 0 && invRes[0] is JsonObject iObj)
        {
            return iObj;
        }

        return null;
    }

    public async Task<JsonObject?> InsertValidacionEsdAsync(object data)
    {
        return await InsertAsync("validacion_esd", data);
    }

    public async Task<bool> UpdateInventarioEsdStatusAsync(string idProducto, string fechaActual, string estatusEval)
    {
        return await UpdateAsync("inventario_esd", $"id_producto=ilike.{Uri.EscapeDataString(idProducto)}", new
        {
            fecha_ultima_verif = fechaActual,
            estatus_verificacion = estatusEval
        });
    }

    public async Task<bool> UpdateMedicionesMaquinariaStatusAsync(string idMaquinaria, string fechaActual, string estatusEval)
    {
        return await UpdateAsync("mediciones_maquinaria", $"id_maquinaria=ilike.{Uri.EscapeDataString(idMaquinaria)}", new
        {
            fecha_medicion = fechaActual,
            status_operativo = estatusEval,
            resultado_estatus = estatusEval
        });
    }

    // --- EVENT METER ---
    public async Task<JsonArray> GetEventMeterLogsAsync()
    {
        return await GetAsync("event_meter?select=*&order=fecha.desc");
    }

    public async Task<JsonObject?> InsertEventMeterLogAsync(object data)
    {
        return await InsertAsync("event_meter", data);
    }

    // --- DASHBOARD, MEASUREMENTS & INFRASTRUCTURE ---
    public async Task<JsonArray> GetAssetsAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("assets?select=*");
        return await GetAsync($"assets?site_id=eq.{siteId}&select=*");
    }

    public async Task<JsonObject?> InsertAssetAsync(object data)
    {
        return await InsertAsync("assets", data);
    }

    public async Task<bool> UpdateAssetStatusAsync(string siteId, string customId, string status)
    {
        return await UpdateAsync("assets", $"site_id=eq.{siteId}&custom_id=eq.{Uri.EscapeDataString(customId)}", new { status });
    }

    public async Task<JsonArray> GetMeasurementsAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("measurements?select=*&order=measured_at.desc");
        return await GetAsync($"measurements?site_id=eq.{siteId}&select=*&order=measured_at.desc");
    }

    public async Task<JsonObject?> InsertMeasurementAsync(object data)
    {
        return await InsertAsync("measurements", data);
    }

    public async Task<JsonArray> GetFloorValidationLogsAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("floor_validation_logs?select=*&order=measured_at.desc");
        return await GetAsync($"floor_validation_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc");
    }

    public async Task<JsonObject?> InsertFloorValidationLogAsync(object data)
    {
        return await InsertAsync("floor_validation_logs", data);
    }

    public async Task<JsonArray> GetGroundingLogsAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("grounding_logs?select=*&order=measured_at.desc");
        return await GetAsync($"grounding_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc");
    }

    public async Task<JsonObject?> InsertGroundingLogAsync(object data)
    {
        return await InsertAsync("grounding_logs", data);
    }

    public async Task<JsonArray> GetIsolatedConductorsLogsAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("isolated_conductors_logs?select=*&order=measured_at.desc");
        return await GetAsync($"isolated_conductors_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc");
    }

    public async Task<JsonObject?> InsertIsolatedConductorsLogAsync(object data)
    {
        return await InsertAsync("isolated_conductors_logs", data);
    }

    public async Task<JsonArray> GetEntranceCheckersLogsAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("entrance_checkers_logs?select=*&order=measured_at.desc");
        return await GetAsync($"entrance_checkers_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc");
    }

    public async Task<JsonObject?> InsertEntranceCheckersLogAsync(object data)
    {
        return await InsertAsync("entrance_checkers_logs", data);
    }

    // --- OFFICIAL LINE REPORTS ---
    public async Task<JsonObject?> InsertLogReportesLineaAsync(object data)
    {
        return await InsertAsync("log_reportes_linea", data);
    }

    // --- LAB SENSITIVITY ---
    public async Task<JsonArray> GetCatalogoSensibilidadAsync()
    {
        return await GetAsync("catalogo_sensibilidad?select=*&order=created_at.desc");
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

    // --- PRODUCT ROUTES & SEQUENCES ---
    public async Task<JsonArray> GetCatalogoProductosAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("catalogo_productos?select=*&order=created_at.desc");
        return await GetAsync($"catalogo_productos?site_id=eq.{siteId}&select=*&order=created_at.desc");
    }

    public async Task<JsonObject?> InsertCatalogoProductoAsync(object data)
    {
        return await InsertAsync("catalogo_productos", data);
    }

    public async Task<bool> UpdateCatalogoProductoRutaAsync(string nombreProducto, string siteId, object lineasAsociadas)
    {
        return await UpdateAsync("catalogo_productos", $"nombre_producto=eq.{Uri.EscapeDataString(nombreProducto)}&site_id=eq.{siteId}", new { lineas_asociadas = lineasAsociadas });
    }

    public async Task<JsonArray> GetCatalogoLineasAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("catalogo_lineas?select=*&order=nombre_linea.asc");
        return await GetAsync($"catalogo_lineas?site_id=eq.{siteId}&select=*&order=nombre_linea.asc");
    }

    public async Task<JsonObject?> InsertCatalogoLineaAsync(object data)
    {
        return await InsertAsync("catalogo_lineas", data);
    }

    // --- EMPLOYEES & TRAINING ---
    public async Task<JsonArray> GetEmpleadosBatasAsync(string? siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return await GetAsync("empleados_batas?select=*");
        return await GetAsync($"empleados_batas?site_id=eq.{siteId}&select=*");
    }

    public async Task<JsonObject?> InsertOrUpdateEmpleadoAsync(object data)
    {
        return await InsertAsync("empleados_batas", data);
    }

    public async Task<JsonArray> GetEntrenamientosEsdAsync()
    {
        return await GetAsync("entrenamientos_esd?select=*&order=created_at.desc");
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
            return await GetAsync($"users?site_id=eq.{siteId}&select=*,sites!users_site_id_fkey(name)&order=email.asc");
        }
        if (!string.IsNullOrEmpty(companyId))
        {
            return await GetAsync($"users?company_id=eq.{companyId}&select=*,sites!users_site_id_fkey(name)&order=email.asc");
        }
        return await GetAsync("users?select=*,sites!users_site_id_fkey(name)&order=email.asc");
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
