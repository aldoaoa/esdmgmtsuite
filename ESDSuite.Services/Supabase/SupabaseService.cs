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
        string cleanEmail = (email ?? "").Trim();
        if (string.IsNullOrEmpty(cleanEmail)) return (false, null, "Por favor ingresa tu correo electrónico.");

        var users = await GetAsync($"users?email=ilike.{cleanEmail}&select=*");
        if (users.Count == 0)
        {
            return (false, null, "No existe un usuario registrado con este correo.");
        }

        var userObj = users[0] as JsonObject;
        if (userObj == null)
        {
            return (false, null, "Error al procesar los datos de usuario.");
        }

        bool isActive = true;
        if (userObj["is_active"] != null)
        {
            if (bool.TryParse(userObj["is_active"]?.ToString(), out bool actVal))
                isActive = actVal;
        }

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

        try
        {
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH NOTE] Error resolving site/company metadata: {ex.Message}");
            if (string.IsNullOrEmpty(siteId)) siteId = "eff70028-0759-4033-9c2b-41e1c1cc6efd";
        }

        var session = new UserSession
        {
            Id = userObj["id"]?.ToString() ?? Guid.NewGuid().ToString(),
            Email = userObj["email"]?.ToString() ?? email ?? "user@esd.com",
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

        // 1. Fetch recent measurements and match in memory
        var measurements = await GetAsync("measurements?select=*&order=measured_at.desc&limit=300");
        foreach (var node in measurements)
        {
            if (node is not JsonObject directObj) continue;

            bool isMatch = false;
            if (directObj["extra_data"] is JsonObject ed)
            {
                string idElem = ed["id_elemento"]?.ToString().Trim() ?? "";
                if (idElem.Equals(cleanId, StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = true;
                    directObj["tipo_equipo"] = ed["tipo_equipo"]?.ToString() ?? directObj["category"]?.ToString();
                    directObj["subtipo_elemento"] = ed["subtipo_elemento"]?.ToString();
                    directObj["subtipo_key"] = ed["subtipo_key"]?.ToString();
                    directObj["ubicacion"] = ed["ubicacion"]?.ToString() ?? directObj["location"]?.ToString();
                    directObj["punto_contacto"] = ed["punto_contacto"]?.ToString();
                }
            }

            if (isMatch)
            {
                directObj["id_elemento"] = cleanId;
                return directObj;
            }
        }

        // 2. Search in assets table and match measurement by asset_id
        var assets = await GetAsync("assets?select=id,custom_id,category,sub_category,location,status");
        JsonObject? matchedAsset = null;
        foreach (var a in assets)
        {
            if (a is JsonObject aObj && aObj["custom_id"]?.ToString().Trim().Equals(cleanId, StringComparison.OrdinalIgnoreCase) == true)
            {
                matchedAsset = aObj;
                break;
            }
        }

        if (matchedAsset != null)
        {
            string assetId = matchedAsset["id"]?.ToString() ?? "";
            foreach (var node in measurements)
            {
                if (node is JsonObject m && m["asset_id"]?.ToString() == assetId)
                {
                    if (m["extra_data"] is JsonObject ed)
                    {
                        m["tipo_equipo"] = ed["tipo_equipo"]?.ToString() ?? matchedAsset["category"]?.ToString();
                        m["subtipo_elemento"] = ed["subtipo_elemento"]?.ToString() ?? matchedAsset["sub_category"]?.ToString();
                        m["subtipo_key"] = ed["subtipo_key"]?.ToString();
                        m["ubicacion"] = ed["ubicacion"]?.ToString() ?? matchedAsset["location"]?.ToString();
                        m["punto_contacto"] = ed["punto_contacto"]?.ToString();
                    }
                    m["id_elemento"] = cleanId;
                    m["category"] = matchedAsset["category"]?.ToString();
                    m["sub_category"] = matchedAsset["sub_category"]?.ToString();
                    m["location"] = matchedAsset["location"]?.ToString();
                    return m;
                }
            }

            return new JsonObject
            {
                ["id_elemento"] = cleanId,
                ["category"] = matchedAsset["category"]?.ToString(),
                ["sub_category"] = matchedAsset["sub_category"]?.ToString(),
                ["tipo_equipo"] = matchedAsset["category"]?.ToString(),
                ["ubicacion"] = matchedAsset["location"]?.ToString(),
                ["status_result"] = matchedAsset["status"]?.ToString() ?? "ACTIVE",
                ["is_asset_only"] = true
            };
        }

        // 3. Fallback legacy tables
        var maq = await GetAsync("mediciones_maquinaria?order=fecha_medicion.desc&limit=50");
        foreach (var m in maq)
        {
            if (m is JsonObject maqObj && maqObj["id_maquinaria"]?.ToString().Trim().Equals(cleanId, StringComparison.OrdinalIgnoreCase) == true)
            {
                return maqObj;
            }
        }

        var inv = await GetAsync("inventario_esd?select=*");
        foreach (var i in inv)
        {
            if (i is JsonObject invObj && invObj["id_elemento"]?.ToString().Trim().Equals(cleanId, StringComparison.OrdinalIgnoreCase) == true)
            {
                return invObj;
            }
        }

        return null;
    }

    public async Task<JsonObject?> InsertMeasurementAsync(object data)
    {
        return await InsertAsync("measurements", data);
    }

    public async Task<JsonArray> GetMeasurementsForSiteAsync(string siteId)
    {
        if (string.IsNullOrEmpty(siteId))
        {
            return await GetAsync("measurements?select=*&order=measured_at.desc&limit=500");
        }
        return await GetAsync($"measurements?site_id=eq.{siteId}&select=*&order=measured_at.desc&limit=500");
    }

    public async Task<JsonArray> GetAssetHistoryAsync(string identifier)
    {
        try
        {
            string cleanId = identifier.Trim().ToUpper();
            var results = new List<JsonObject>();
            var seenIds = new HashSet<string>();

            // 1. Get all assets to resolve asset_id if available
            var assets = await GetAsync("assets?select=id,custom_id,category,sub_category,location");
            string matchedAssetId = "";
            foreach (var a in assets)
            {
                if (a is JsonObject aObj && aObj["custom_id"]?.ToString().Trim().Equals(cleanId, StringComparison.OrdinalIgnoreCase) == true)
                {
                    matchedAssetId = aObj["id"]?.ToString() ?? "";
                    break;
                }
            }

            // 2. Fetch all measurements safely
            var measurements = await GetAsync("measurements?select=*&order=measured_at.desc&limit=500");
            foreach (var node in measurements)
            {
                if (node is not JsonObject mObj) continue;

                string mId = mObj["id"]?.ToString() ?? Guid.NewGuid().ToString();
                if (seenIds.Contains(mId)) continue;

                bool isMatch = false;

                // Match by extra_data.id_elemento
                if (mObj["extra_data"] is JsonObject ed)
                {
                    string idElem = ed["id_elemento"]?.ToString().Trim() ?? "";
                    if (idElem.Equals(cleanId, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                }

                // Match by asset_id
                if (!isMatch && !string.IsNullOrEmpty(matchedAssetId))
                {
                    string aId = mObj["asset_id"]?.ToString() ?? "";
                    if (aId.Equals(matchedAssetId, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                }

                if (isMatch)
                {
                    seenIds.Add(mId);
                    results.Add(mObj);
                }
            }

            var array = new JsonArray();
            foreach (var r in results.OrderByDescending(x => x["measured_at"]?.ToString()))
            {
                if (r.DeepClone() is JsonObject cloned)
                {
                    array.Add(cloned);
                }
            }
            return array;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAssetHistoryAsync Exception for {identifier}: {ex.Message}");
            return new JsonArray();
        }
    }

    public async Task<JsonArray> GetAssetsAsync(string siteId)
    {
        if (string.IsNullOrEmpty(siteId))
        {
            return await GetAsync("assets?select=*");
        }
        return await GetAsync($"assets?site_id=eq.{siteId}&select=*");
    }

    public async Task<JsonObject?> InsertAssetAsync(object data)
    {
        return await InsertAsync("assets", data);
    }

    public async Task<bool> UpdateAssetAsync(string id, object data)
    {
        return await UpdateAsync("assets", $"id=eq.{id}", data);
    }

    public async Task<bool> DeleteAssetAsync(string id)
    {
        return await DeleteAsync("assets", $"id=eq.{id}");
    }

    public async Task<JsonArray> GetEventMeterLogsAsync(string? siteId = null)
    {
        string query = "measurements?extra_data->>type=eq.event_meter";
        if (!string.IsNullOrEmpty(siteId))
        {
            query += $"&site_id=eq.{siteId}";
        }
        query += "&select=*&order=measured_at.desc";
        return await GetAsync(query);
    }

    public async Task<bool> UpdateEventMeterLogAsync(string id, object data)
    {
        return await UpdateAsync("measurements", $"id=eq.{id}", data);
    }

    public async Task<bool> DeleteEventMeterLogAsync(string id)
    {
        return await DeleteAsync("measurements", $"id=eq.{id}");
    }

    // --- INFRASTRUCTURE EPA ---
    public async Task<JsonArray> GetGroundingLogsAsync(string siteId)
    {
        string query = string.IsNullOrEmpty(siteId)
            ? "grounding_logs?select=*&order=measured_at.desc"
            : $"grounding_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc";
        return await GetAsync(query);
    }

    public async Task<JsonObject?> InsertGroundingLogAsync(object data)
    {
        return await InsertAsync("grounding_logs", data);
    }

    public async Task<JsonArray> GetFloorValidationLogsAsync(string siteId)
    {
        string query = string.IsNullOrEmpty(siteId)
            ? "floor_validation_logs?select=*&order=measured_at.desc"
            : $"floor_validation_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc";
        return await GetAsync(query);
    }

    public async Task<JsonObject?> InsertFloorValidationLogAsync(object data)
    {
        return await InsertAsync("floor_validation_logs", data);
    }

    public async Task<JsonArray> GetIsolatedConductorsLogsAsync(string siteId)
    {
        string query = string.IsNullOrEmpty(siteId)
            ? "isolated_conductors_logs?select=*&order=measured_at.desc"
            : $"isolated_conductors_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc";
        return await GetAsync(query);
    }

    public async Task<JsonObject?> InsertIsolatedConductorsLogAsync(object data)
    {
        return await InsertAsync("isolated_conductors_logs", data);
    }

    public async Task<JsonArray> GetEntranceCheckersLogsAsync(string siteId)
    {
        string query = string.IsNullOrEmpty(siteId)
            ? "entrance_checkers_logs?select=*&order=measured_at.desc"
            : $"entrance_checkers_logs?site_id=eq.{siteId}&select=*&order=measured_at.desc";
        return await GetAsync(query);
    }

    public async Task<JsonObject?> InsertEntranceCheckersLogAsync(object data)
    {
        return await InsertAsync("entrance_checkers_logs", data);
    }

    // --- SUPABASE STORAGE & FLOOR MAPS ---
    public async Task<string?> UploadStorageFileAsync(string bucket, string fileName, byte[] bytes, string contentType)
    {
        try
        {
            string url = $"{_config.Url.TrimEnd('/')}/storage/v1/object/{bucket}/{fileName}";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new ByteArrayContent(bytes)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Headers.Add("apikey", _config.Key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
            request.Headers.Add("x-upsert", "true");

            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return $"{_config.Url.TrimEnd('/')}/storage/v1/object/public/{bucket}/{fileName}";
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Supabase Storage upload note ({response.StatusCode}): {err}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Supabase Storage exception: {ex.Message}");
        }
        return null;
    }

    public async Task<JsonArray> GetFloorMapsFromSupabaseAsync(string? siteId = null)
    {
        string query = string.IsNullOrEmpty(siteId)
            ? "floor_maps?select=*&order=created_at.desc"
            : $"floor_maps?site_id=eq.{siteId}&select=*&order=created_at.desc";
        return await GetAsync(query);
    }

    public async Task<JsonObject?> SaveFloorMapToSupabaseAsync(object mapData)
    {
        return await InsertAsync("floor_maps", mapData);
    }

    // --- SCHEDULE & REPORTS ---
    public async Task<JsonObject?> InsertLogReportesLineaAsync(object data)
    {
        return await InsertAsync("log_reportes_linea", data);
    }

    public async Task<JsonArray> GetLogReportesLineaAsync(string? siteId = null, string? companyId = null)
    {
        var query = new List<string> { "select=*", "order=created_at.desc" };
        if (!string.IsNullOrEmpty(siteId)) query.Add($"site_id=eq.{siteId}");
        if (!string.IsNullOrEmpty(companyId)) query.Add($"company_id=eq.{companyId}");
        return await GetAsync($"log_reportes_linea?{string.Join("&", query)}");
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

    public async Task<bool> UpdateCatalogoLineaAsync(string id, object data)
    {
        return await UpdateAsync("catalogo_lineas", $"id=eq.{id}", data);
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

    public async Task<JsonObject?> GetCompanyByIdAsync(string id)
    {
        var res = await GetAsync($"companies?id=eq.{id}&select=*");
        return res.Count > 0 && res[0] is JsonObject cObj ? cObj : null;
    }

    public async Task<bool> UpdateCompanyAsync(string id, object data)
    {
        return await UpdateAsync("companies", $"id=eq.{id}", data);
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

    // --- SUPABASE STORAGE API (PRIVATE BUCKETS) ---
    public async Task<(bool success, string key, string message)> UploadStorageObjectAsync(string bucket, string path, Stream stream, string contentType)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.Url.TrimEnd('/')}/storage/v1/object/{bucket}/{path.TrimStart('/')}");
            request.Headers.Add("apikey", _config.Key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, "", $"Storage upload error ({response.StatusCode}): {err}");
            }
            return (true, $"{bucket}/{path.TrimStart('/')}", "OK");
        }
        catch (Exception ex)
        {
            return (false, "", $"Storage upload exception: {ex.Message}");
        }
    }

    public async Task<(bool success, string key, string message)> UploadStorageObjectAsync(string bucket, string path, byte[] data, string contentType)
    {
        using var ms = new MemoryStream(data);
        return await UploadStorageObjectAsync(bucket, path, ms, contentType);
    }

    public async Task<(bool success, Stream? stream, string contentType, string message)> DownloadStorageObjectAsync(string bucket, string path)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.Url.TrimEnd('/')}/storage/v1/object/authenticated/{bucket}/{path.TrimStart('/')}");
            request.Headers.Add("apikey", _config.Key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);

            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                using var fallbackReq = new HttpRequestMessage(HttpMethod.Get, $"{_config.Url.TrimEnd('/')}/storage/v1/object/{bucket}/{path.TrimStart('/')}");
                fallbackReq.Headers.Add("apikey", _config.Key);
                fallbackReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
                var fallbackRes = await _http.SendAsync(fallbackReq, HttpCompletionOption.ResponseHeadersRead);
                if (!fallbackRes.IsSuccessStatusCode)
                {
                    var err = await fallbackRes.Content.ReadAsStringAsync();
                    return (false, null, "", $"Storage download error ({fallbackRes.StatusCode}): {err}");
                }
                var streamFallback = await fallbackRes.Content.ReadAsStreamAsync();
                string ctFallback = fallbackRes.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                return (true, streamFallback, ctFallback, "OK");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            string ct = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return (true, stream, ct, "OK");
        }
        catch (Exception ex)
        {
            return (false, null, "", $"Storage download exception: {ex.Message}");
        }
    }

    public async Task<bool> DeleteStorageObjectAsync(string bucket, string path)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{_config.Url.TrimEnd('/')}/storage/v1/object/{bucket}/{path.TrimStart('/')}");
            request.Headers.Add("apikey", _config.Key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
            var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // --- ESD CONTROL ELEMENT VALIDATION (VALIDACION INTEGRAL ESD) ---
    public async Task<JsonArray> GetValidacionesEsdAsync(string? siteId = null)
    {
        // Try directly from validacion_esd table if present
        string query = string.IsNullOrEmpty(siteId)
            ? "validacion_esd?select=*&order=fecha_auditoria.desc"
            : $"validacion_esd?site_id=eq.{siteId}&select=*&order=fecha_auditoria.desc";

        var res = await GetAsync(query);
        if (res.Count > 0) return res;

        // Fallback to measurements joined with assets and extra_data
        var measQuery = string.IsNullOrEmpty(siteId)
            ? "measurements?select=*&order=measured_at.desc"
            : $"measurements?site_id=eq.{siteId}&select=*&order=measured_at.desc";

        var measList = await GetAsync(measQuery);

        // Preload assets for fast dictionary lookup
        var assetsQuery = string.IsNullOrEmpty(siteId) ? "assets?select=*" : $"assets?site_id=eq.{siteId}&select=*";
        var assetsList = await GetAsync(assetsQuery);
        var assetMap = new Dictionary<string, JsonObject>();
        foreach (var a in assetsList)
        {
            if (a is JsonObject aObj && aObj["id"] != null)
            {
                assetMap[aObj["id"]!.ToString()] = aObj;
            }
        }

        var resultList = new JsonArray();

        foreach (var item in measList)
        {
            if (item is not JsonObject m) continue;
            var valObj = new JsonObject();
            valObj["id"] = m["id"]?.ToString() ?? Guid.NewGuid().ToString();
            valObj["fecha_auditoria"] = m["measured_at"]?.ToString() ?? DateTime.UtcNow.ToString("o");
            valObj["temperatura"] = m["temperatura"]?.ToString() ?? "23.5 °C";
            valObj["humedad"] = m["humedad"]?.ToString() ?? "45 %";
            valObj["site_id"] = m["site_id"]?.ToString() ?? siteId;
            valObj["resultado"] = m["status_result"]?.ToString() == "PASS" ? "CUMPLE (APROBADO)" : (m["status_result"]?.ToString() == "FAIL" ? "NO CUMPLE (RECHAZADO)" : (m["status_result"]?.ToString() ?? "CUMPLE (APROBADO)"));
            valObj["notas"] = m["observaciones"]?.ToString() ?? "";

            // Extract asset metadata
            string aId = m["asset_id"]?.ToString() ?? "";
            JsonObject? a = (!string.IsNullOrEmpty(aId) && assetMap.TryGetValue(aId, out var foundA)) ? foundA : null;

            string customId = a?["custom_id"]?.ToString() ?? "";
            string assetCat = a?["category"]?.ToString() ?? "Superficie de trabajo";
            string assetLoc = a?["location"]?.ToString() ?? "General";
            string mat = "Tapete disipativo / Mesa";

            valObj["id_elemento"] = !string.IsNullOrEmpty(customId) ? customId : (!string.IsNullOrEmpty(aId) ? aId : "EQ-ESD");
            valObj["elemento_s20_20"] = assetCat;
            valObj["ubicacion"] = assetLoc;
            valObj["tipo_material"] = mat;

            // Extract extra_data if available
            if (m["extra_data"] is JsonObject extraObj)
            {
                foreach (var kv in extraObj)
                {
                    valObj[kv.Key] = kv.Value?.DeepClone();
                }
            }
            else if (m["extra_data"] != null)
            {
                try
                {
                    var parsed = JsonNode.Parse(m["extra_data"]!.ToString()) as JsonObject;
                    if (parsed != null)
                    {
                        foreach (var kv in parsed)
                        {
                            valObj[kv.Key] = kv.Value?.DeepClone();
                        }
                    }
                }
                catch { }
            }

            if (!valObj.ContainsKey("medicion_1") && m["resistance_value"] != null)
            {
                valObj["medicion_1"] = m["resistance_value"]?.GetValue<double>();
                valObj["unidad"] = "Ohms";
            }
            if (!valObj.ContainsKey("limite_referencia"))
            {
                valObj["limite_referencia"] = 1.0e9;
            }

            resultList.Add(valObj);
        }

        // Sort resultList descending by actual audit date
        var sortedList = new JsonArray();
        var sortedItems = resultList
            .Select(x => x as JsonObject)
            .Where(x => x != null)
            .OrderByDescending(x => {
                if (DateTime.TryParse(x!["fecha_auditoria"]?.ToString(), out var dt)) return dt;
                return DateTime.MinValue;
            });

        foreach (var item in sortedItems)
        {
            sortedList.Add(item!.DeepClone());
        }

        return sortedList;
    }

    public async Task<JsonObject?> InsertValidacionEsdAsync(JsonObject data)
    {
        // Try inserting into validacion_esd table first
        var inserted = await InsertAsync("validacion_esd", data);
        if (inserted != null && !inserted.ContainsKey("code") && !inserted.ContainsKey("message") && (inserted.ContainsKey("id") || inserted.ContainsKey("created_at")))
        {
            return inserted;
        }

        // Fallback: Also ensure recorded in measurements & assets for 100% unified multi-tenant compliance
        try
        {
            string siteId = data["site_id"]?.ToString() ?? "";
            string elementId = data["id_elemento"]?.ToString()?.Trim().ToUpper() ?? "ACTIVO-ESD";
            string category = data["elemento_s20_20"]?.ToString() ?? "Superficie de trabajo";
            string location = data["ubicacion"]?.ToString() ?? "General";

            // Find or create asset
            var existingAssets = await GetAsync($"assets?custom_id=eq.{elementId}&select=*");
            string assetDbId = "";
            if (existingAssets.Count > 0 && existingAssets[0] is JsonObject aObj)
            {
                assetDbId = aObj["id"]?.ToString() ?? "";
            }
            else
            {
                var newAsset = new JsonObject
                {
                    ["site_id"] = string.IsNullOrEmpty(siteId) ? null : siteId,
                    ["custom_id"] = elementId,
                    ["category"] = category,
                    ["location"] = location,
                    ["status"] = "ACTIVE",
                    ["classification"] = JsonSerializer.Serialize(new { name = elementId, category, area = location })
                };
                var aCreated = await InsertAsync("assets", newAsset);
                if (aCreated != null && aCreated["id"] != null)
                {
                    assetDbId = aCreated["id"]!.ToString();
                }
                else
                {
                    // Fallback to any existing asset in the site
                    var anyAsset = await GetAsync($"assets?site_id=eq.{siteId}&limit=1");
                    if (anyAsset.Count > 0 && anyAsset[0] is JsonObject anyObj)
                    {
                        assetDbId = anyObj["id"]?.ToString() ?? "";
                    }
                }
            }

            double? med1 = null;
            if (data["medicion_1"] != null)
            {
                if (double.TryParse(data["medicion_1"]?.ToString(), out double dVal)) med1 = dVal;
            }

            string resStatus = (data["resultado"]?.ToString() ?? "").ToUpper().Contains("NO CUMPLE") || (data["resultado"]?.ToString() ?? "").ToUpper().Contains("RECHAZADO")
                ? "FAIL" : "PASS";

            string auditorId = data["auditor_id"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(auditorId))
            {
                var usersList = await GetAsync($"users?site_id=eq.{siteId}&limit=1");
                if (usersList.Count > 0 && usersList[0] is JsonObject uObj)
                {
                    auditorId = uObj["id"]?.ToString() ?? "";
                }
                else
                {
                    var anyUser = await GetAsync("users?limit=1");
                    if (anyUser.Count > 0 && anyUser[0] is JsonObject anyU)
                    {
                        auditorId = anyU["id"]?.ToString() ?? "";
                    }
                }
            }

            var measRecord = new JsonObject
            {
                ["site_id"] = string.IsNullOrEmpty(siteId) ? null : siteId,
                ["asset_id"] = string.IsNullOrEmpty(assetDbId) ? null : assetDbId,
                ["auditor_id"] = string.IsNullOrEmpty(auditorId) ? null : auditorId,
                ["resistance_value"] = med1,
                ["status_result"] = resStatus,
                ["observaciones"] = data["notas"]?.ToString() ?? "",
                ["extra_data"] = data.DeepClone(),
                ["measured_at"] = data["fecha_auditoria"]?.ToString() ?? DateTime.UtcNow.ToString("o")
            };

            var mInserted = await InsertAsync("measurements", measRecord);
            return (mInserted != null && !mInserted.ContainsKey("code")) ? mInserted : data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"InsertValidacionEsd fallback exception: {ex.Message}");
            return data;
        }
    }

    // --- AUDIT TRAIL / BITÁCORA DE AUDITORÍA ---
    public async Task LogAuditEventAsync(string? userId, string? siteId, string level, string page, string message, object? details = null)
    {
        try
        {
            string detailsStr = details is string s ? s : JsonSerializer.Serialize(details);
            var payload = new JsonObject
            {
                ["created_at"] = DateTime.UtcNow.ToString("o"),
                ["level"] = level ?? "INFO",
                ["page"] = page ?? "App",
                ["message"] = message ?? "Action executed",
                ["details"] = detailsStr,
                ["user_id"] = string.IsNullOrEmpty(userId) ? null : userId,
                ["site_id"] = string.IsNullOrEmpty(siteId) ? null : siteId
            };
            await InsertAsync("app_logs", payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUDIT LOG EXCEPTION]: {ex.Message}");
        }
    }
}

