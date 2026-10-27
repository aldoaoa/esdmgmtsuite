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
        string baseUrl = (!string.IsNullOrWhiteSpace(_config.Url) && Uri.TryCreate(_config.Url.TrimEnd('/') + "/rest/v1/", UriKind.Absolute, out _))
            ? _config.Url.TrimEnd('/')
            : "https://yukhljzgstlechfsweul.supabase.co";
        _config.Url = baseUrl;
        _http.BaseAddress = new Uri(baseUrl + "/rest/v1/");
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

    public async Task<(bool Success, JsonObject? Data, string? Error)> InsertRecordAsync(string table, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(table, content);
            var resJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Supabase PostgREST Insert Error ({response.StatusCode})] on {table}: {resJson}");
                return (false, null, $"[{response.StatusCode}] {resJson}");
            }

            if (string.IsNullOrWhiteSpace(resJson))
            {
                return (true, new JsonObject(), null);
            }

            var node = JsonNode.Parse(resJson);
            if (node is JsonArray arr && arr.Count > 0)
            {
                return (true, arr[0] as JsonObject, null);
            }
            return (true, node as JsonObject, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Insert Exception] on {table}: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, JsonObject? Data, string? Error)> UpsertRecordAsync(string table, string conflictColumn, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{table}?on_conflict={conflictColumn}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
            var response = await _http.SendAsync(request);
            var resJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Supabase PostgREST Upsert Error ({response.StatusCode})] on {table}: {resJson}");
                return (false, null, $"[{response.StatusCode}] {resJson}");
            }

            if (string.IsNullOrWhiteSpace(resJson))
            {
                return (true, new JsonObject(), null);
            }

            var node = JsonNode.Parse(resJson);
            if (node is JsonArray arr && arr.Count > 0)
            {
                return (true, arr[0] as JsonObject, null);
            }
            return (true, node as JsonObject, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Upsert Exception] on {table}: {ex.Message}");
            return (false, null, ex.Message);
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
        var assets = await GetAsync("assets?select=id,custom_id,category,classification,location,status");
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
                    m["asset_status"] = matchedAsset["status"]?.ToString() ?? "ACTIVE";
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
                ["asset_status"] = matchedAsset["status"]?.ToString() ?? "ACTIVE",
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
            var assets = await GetAsync("assets?select=id,custom_id,category,classification,location");
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
            if (!request.Headers.Contains("apikey")) request.Headers.Add("apikey", _config.Key);
            if (request.Headers.Authorization == null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
            if (!request.Headers.Contains("x-upsert")) request.Headers.Add("x-upsert", "true");

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

    public async Task<bool> DeleteFloorMapFromSupabaseAsync(string mapId)
    {
        return await DeleteAsync("floor_maps", $"id=eq.{mapId}");
    }

    public async Task<bool> DeleteFloorMapsBySiteFromSupabaseAsync(string siteId)
    {
        return await DeleteAsync("floor_maps", $"site_id=eq.{siteId}");
    }

    // --- SCHEDULE & REPORTS ---
    public async Task<JsonObject?> InsertLogReportesLineaAsync(object data)
    {
        return await InsertAsync("log_reportes_linea", data);
    }

    // --- ESD FINDINGS (CAPA) ---
    public async Task<JsonArray> GetFindingsFromSupabaseAsync(string? siteId = null, string? status = null)
    {
        try
        {
            var query = new List<string> { "select=*", "order=created_at.desc" };
            if (!string.IsNullOrEmpty(siteId) && !siteId.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query.Add($"or=(site_id.eq.{siteId},site_id.is.null)");
            }
            if (!string.IsNullOrEmpty(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query.Add($"status=eq.{status}");
            }
            var res = await GetAsync($"esd_findings?{string.Join("&", query)}");
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase GetFindings Exception]: {ex.Message}");
        }

        // Fallback a app_logs (page='esd_findings') en Supabase para garantizar persistencia continua
        try
        {
            string query = "app_logs?page=eq.esd_findings&order=created_at.desc&limit=300";
            if (!string.IsNullOrEmpty(siteId) && !siteId.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = $"app_logs?page=eq.esd_findings&or=(site_id.eq.{siteId},site_id.is.null)&order=created_at.desc&limit=300";
            }
            var logs = await GetAsync(query);
            if (logs != null && logs.Count > 0)
            {
                var arr = new JsonArray();
                var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var log in logs)
                {
                    if (log is JsonObject logObj)
                    {
                        string details = logObj["details"]?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(details))
                        {
                            try
                            {
                                if (JsonNode.Parse(details) is JsonObject item)
                                {
                                    string itemCode = item["tracking_code"]?.ToString() ?? item["tracker_code"]?.ToString() ?? item["id"]?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(itemCode) && seenCodes.Contains(itemCode)) continue;
                                    if (!string.IsNullOrEmpty(itemCode)) seenCodes.Add(itemCode);

                                    if (!string.IsNullOrEmpty(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (!string.Equals(item["status"]?.ToString(), status, StringComparison.OrdinalIgnoreCase))
                                            continue;
                                    }
                                    arr.Add(item);
                                }
                            }
                            catch { }
                        }
                    }
                }
                return arr;
            }
        }
        catch { }

        return new JsonArray();
    }

    public async Task<JsonObject?> GetFindingByIdFromSupabaseAsync(string id)
    {
        try
        {
            var res = await GetAsync($"esd_findings?id=eq.{id}&select=*");
            if (res != null && res.Count > 0 && res[0] is JsonObject obj)
            {
                return obj;
            }
        }
        catch { }

        try
        {
            var logs = await GetAsync($"app_logs?page=eq.esd_findings&details=like.*{id}*&order=created_at.desc&limit=1");
            if (logs != null && logs.Count > 0 && logs[0] is JsonObject logObj)
            {
                string details = logObj["details"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(details))
                {
                    return JsonNode.Parse(details) as JsonObject;
                }
            }
        }
        catch { }

        return null;
    }

    public async Task<(bool Success, JsonObject? Finding, string? Error)> SaveFindingToSupabaseAsync(JsonObject finding)
    {
        string id = finding["id"]?.ToString() ?? Guid.NewGuid().ToString();
        if (!Guid.TryParse(id, out _)) id = Guid.NewGuid().ToString();
        finding["id"] = id;

        string trackingCode = finding["tracking_code"]?.ToString() ?? finding["tracker_code"]?.ToString() ?? id;
        string? siteId = finding["site_id"]?.ToString();

        // 1. Guardar o actualizar en tabla dedicada esd_findings de Supabase (UPSERT atómico)
        var (ok, data, error) = await UpsertRecordAsync("esd_findings", "id", finding);

        // 2. Persistir snapshot en bitácora de app_logs (sin columnas inexistentes)
        _ = Task.Run(async () =>
        {
            try
            {
                await LogAuditEventAsync(
                    finding["auditor_id"]?.ToString(),
                    siteId,
                    ok ? "INFO" : "ERROR",
                    "esd_findings",
                    trackingCode,
                    finding
                );
            }
            catch { }
        });

        if (ok)
        {
            return (true, data ?? finding, null);
        }

        Console.WriteLine($"[Supabase SaveFinding Error]: {error}");
        return (false, null, error ?? "Error al persistir en Supabase.");
    }

    public async Task<(bool Success, string? Error)> UpdateFindingInSupabaseAsync(string id, JsonObject finding)
    {
        try
        {
            var json = JsonSerializer.Serialize(finding);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"esd_findings?id=eq.{id}")
            {
                Content = content
            };
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Supabase Update Error ({response.StatusCode})] on esd_findings: {err}");
                return (false, $"[{response.StatusCode}] {err}");
            }

            string trackingCode = finding["tracking_code"]?.ToString() ?? finding["tracker_code"]?.ToString() ?? id;
            string? siteId = finding["site_id"]?.ToString();
            _ = Task.Run(async () =>
            {
                try
                {
                    await LogAuditEventAsync(finding["auditor_id"]?.ToString(), siteId, "UPDATE", "esd_findings", trackingCode, finding);
                }
                catch { }
            });

            return (true, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Update Exception] on esd_findings: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteFindingFromSupabaseAsync(string id)
    {
        try
        {
            var response = await _http.DeleteAsync($"esd_findings?id=eq.{id}");
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Supabase Delete Error ({response.StatusCode})] on esd_findings: {err}");
                return (false, $"[{response.StatusCode}] {err}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await LogAuditEventAsync(null, null, "DELETE", "esd_findings", id, new { deleted_id = id });
                }
                catch { }
            });

            return (true, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Delete Exception] on esd_findings: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // --- NORMATIVE LIMITS EN SUPABASE ---
    public async Task<JsonArray> GetNormativeLimitsFromSupabaseAsync(string siteId)
    {
        try
        {
            string query = string.IsNullOrEmpty(siteId) || siteId.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? "esd_normative_limits?select=*&order=table_number.asc,id.asc"
                : $"esd_normative_limits?or=(site_id.eq.{siteId},site_id.is.null)&select=*&order=table_number.asc,id.asc";
            var limits = await GetAsync(query);
            if (limits != null && limits.Count > 0) return limits;
        }
        catch { }

        try
        {
            var logs = await GetAsync($"app_logs?page=eq.normative_limits&message=eq.{siteId}&select=details,created_at&order=created_at.desc&limit=1");
            if (logs != null && logs.Count > 0 && logs[0] is JsonObject logObj)
            {
                string json = logObj["details"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var parsed = JsonNode.Parse(json) as JsonArray;
                    if (parsed != null && parsed.Count > 0) return parsed;
                }
            }
        }
        catch { }

        return new JsonArray();
    }

    public async Task<bool> SaveNormativeLimitsToSupabaseAsync(string siteId, JsonArray limits)
    {
        bool ok = false;
        try
        {
            foreach (var item in limits)
            {
                if (item is JsonObject obj)
                {
                    string limitId = obj["id"]?.ToString() ?? Guid.NewGuid().ToString();
                    var cleanObj = new JsonObject
                    {
                        ["id"] = limitId,
                        ["site_id"] = string.IsNullOrWhiteSpace(siteId) ? null : siteId,
                        ["company_id"] = obj["company_id"]?.ToString(),
                        ["table_number"] = obj["table_number"]?.ToString() ?? "Tabla 1",
                        ["table_title"] = obj["table_title"]?.ToString() ?? "Normativa",
                        ["item_name"] = obj["item_name"]?.ToString() ?? "Elemento",
                        ["test_type"] = obj["test_type"]?.ToString(),
                        ["operator"] = obj["operator"]?.ToString() ?? "<",
                        ["limit_value"] = obj["limit_value"]?.GetValue<double>() ?? 0,
                        ["unit"] = obj["unit"]?.ToString(),
                        ["limit_display"] = obj["limit_display"]?.ToString(),
                        ["test_method"] = obj["test_method"]?.ToString(),
                        ["frequency"] = obj["frequency"]?.ToString(),
                        ["severity"] = obj["severity"]?.ToString() ?? "MAJOR",
                        ["auto_finding"] = obj["auto_finding"]?.GetValue<bool>() ?? true,
                        ["updated_at"] = DateTime.UtcNow.ToString("o")
                    };

                    await UpsertRecordAsync("esd_normative_limits", "id", cleanObj);
                }
            }
            ok = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveNormativeLimits Exception]: {ex.Message}");
        }

        try
        {
            string json = limits.ToJsonString();
            await LogAuditEventAsync(null, siteId, "CONFIG", "normative_limits", siteId, json);
            ok = true;
        }
        catch { }

        return ok;
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

    public async Task<(bool success, int deletedCount)> DeleteCatalogoLineaAndChildrenAsync(string siteId, string nombreLinea, bool includeSubOperations = true)
    {
        var (success, deletedCount, _) = await ArchiveAndDeleteLineAsync(siteId, nombreLinea, includeSubOperations);
        return (success, deletedCount);
    }

    public async Task<(bool success, int updatedCount, string message)> RenameLocationCascadeAsync(string siteId, string oldName, string newName, bool isArea)
    {
        if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            return (false, 0, "El site, el nombre actual y el nuevo nombre son obligatorios.");
        }

        string oldTarget = oldName.Trim();
        string newTarget = newName.Trim();
        if (string.Equals(oldTarget, newTarget, StringComparison.OrdinalIgnoreCase))
        {
            return (true, 0, "El nombre no cambió.");
        }

        string oldPrefix = oldTarget + " ->";
        string newPrefix = newTarget + " ->";
        int updated = 0;

        // 1. Update catalogo_lineas
        var lines = await GetCatalogoLineasAsync(siteId);
        foreach (var item in lines)
        {
            if (item is JsonObject obj)
            {
                string id = obj["id"]?.ToString() ?? "";
                string lineName = obj["nombre_linea"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(id)) continue;

                string? updatedLineName = null;
                if (string.Equals(lineName, oldTarget, StringComparison.OrdinalIgnoreCase))
                {
                    updatedLineName = newTarget;
                }
                else if (isArea && lineName.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    updatedLineName = newPrefix + lineName.Substring(oldPrefix.Length);
                }

                if (updatedLineName != null)
                {
                    if (await UpdateCatalogoLineaAsync(id, new { nombre_linea = updatedLineName }))
                    {
                        updated++;
                    }
                }
            }
        }

        // 2. Update assets in this site
        var assets = await GetAssetsAsync(siteId);
        foreach (var item in assets)
        {
            if (item is JsonObject aObj)
            {
                string aId = aObj["id"]?.ToString() ?? "";
                string loc = aObj["location"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(aId)) continue;

                string? newLoc = null;
                if (string.Equals(loc, oldTarget, StringComparison.OrdinalIgnoreCase))
                {
                    newLoc = newTarget;
                }
                else if (isArea && loc.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    newLoc = newPrefix + loc.Substring(oldPrefix.Length);
                }

                if (newLoc != null)
                {
                    string rawClass = aObj["classification"]?.ToString() ?? "";
                    string newClass = rawClass;
                    if (!string.IsNullOrEmpty(rawClass) && rawClass.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            var classObj = JsonNode.Parse(rawClass) as JsonObject;
                            if (classObj != null)
                            {
                                string newArea = newLoc.Contains("->") ? newLoc.Split("->")[0].Trim() : newLoc;
                                classObj["area"] = newArea;
                                newClass = classObj.ToJsonString();
                            }
                        }
                        catch { }
                    }

                    await UpdateAssetAsync(aId, new { location = newLoc, classification = newClass });
                    updated++;
                }
            }
        }

        // 3. Update measurements in this site
        var measurements = await GetMeasurementsForSiteAsync(siteId);
        foreach (var item in measurements)
        {
            if (item is JsonObject mObj && mObj["extra_data"] is JsonObject ed)
            {
                string mId = mObj["id"]?.ToString() ?? "";
                string ub = ed["ubicacion"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(mId)) continue;

                string? newUb = null;
                if (string.Equals(ub, oldTarget, StringComparison.OrdinalIgnoreCase))
                {
                    newUb = newTarget;
                }
                else if (isArea && ub.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    newUb = newPrefix + ub.Substring(oldPrefix.Length);
                }

                if (newUb != null)
                {
                    var edClone = ed.DeepClone() as JsonObject ?? new JsonObject();
                    edClone["ubicacion"] = newUb;
                    await UpdateAsync("measurements", $"id=eq.{mId}", new { extra_data = edClone });
                    updated++;
                }
            }
        }

        // 4. Update EPA logs (grounding_logs, floor_validation_logs, isolated_conductors_logs, entrance_checkers_logs)
        await RenameInLogsTableAsync("grounding_logs", "location", siteId, oldTarget, newTarget, oldPrefix, newPrefix, isArea);
        await RenameInLogsTableAsync("floor_validation_logs", "room_name", siteId, oldTarget, newTarget, oldPrefix, newPrefix, isArea);
        await RenameInLogsTableAsync("isolated_conductors_logs", "location", siteId, oldTarget, newTarget, oldPrefix, newPrefix, isArea);
        await RenameInLogsTableAsync("entrance_checkers_logs", "location", siteId, oldTarget, newTarget, oldPrefix, newPrefix, isArea);

        return (true, updated, $"Ubicación actualizada a '{newTarget}' ({updated} registros vinculados actualizados).");
    }

    private async Task RenameInLogsTableAsync(string table, string colName, string siteId, string oldTarget, string newTarget, string oldPrefix, string newPrefix, bool isArea)
    {
        try
        {
            var rows = await GetAsync($"{table}?site_id=eq.{siteId}&select=id,{colName}");
            foreach (var r in rows)
            {
                if (r is JsonObject rObj)
                {
                    string rId = rObj["id"]?.ToString() ?? "";
                    string val = rObj[colName]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(rId)) continue;

                    string? newVal = null;
                    if (string.Equals(val, oldTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        newVal = newTarget;
                    }
                    else if (isArea && val.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        newVal = newPrefix + val.Substring(oldPrefix.Length);
                    }

                    if (newVal != null)
                    {
                        var patch = new JsonObject { [colName] = newVal };
                        await UpdateAsync(table, $"id=eq.{rId}", patch);
                    }
                }
            }
        }
        catch { }
    }

    public async Task<(bool success, int deletedCount, JsonObject? archivedGroup)> ArchiveAndDeleteLineAsync(
        string siteId,
        string nombreLinea,
        bool includeSubOperations = true,
        string? userId = null,
        string? userName = null)
    {
        if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(nombreLinea))
        {
            return (false, 0, null);
        }

        string target = nombreLinea.Trim();
        string prefix = target + " ->";

        bool MatchesLoc(string? loc)
        {
            if (string.IsNullOrWhiteSpace(loc)) return false;
            string clean = loc.Trim();
            return string.Equals(clean, target, StringComparison.OrdinalIgnoreCase)
                || (includeSubOperations && clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        var siteObj = await GetSiteByIdAsync(siteId);
        string siteName = siteObj?["name"]?.ToString() ?? "Site";
        string companyId = siteObj?["company_id"]?.ToString() ?? "";
        var compObj = !string.IsNullOrEmpty(companyId) ? await GetCompanyByIdAsync(companyId) : null;
        string companyName = compObj?["name"]?.ToString() ?? "Empresa";

        // 1. Collect matching lines
        var lines = await GetCatalogoLineasAsync(siteId);
        var matchedLineIds = new List<string>();
        var matchedLineNames = new JsonArray();
        foreach (var item in lines)
        {
            if (item is JsonObject obj)
            {
                string id = obj["id"]?.ToString() ?? "";
                string lineName = obj["nombre_linea"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(id) && MatchesLoc(lineName))
                {
                    matchedLineIds.Add(id);
                    matchedLineNames.Add(lineName);
                }
            }
        }

        // 2. Collect matching assets & measurements & EPA logs
        var assetsSnapshot = new JsonArray();
        var siteAssets = await GetAssetsAsync(siteId);
        var matchedAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedCustomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in siteAssets)
        {
            if (item is JsonObject aObj)
            {
                string loc = aObj["location"]?.ToString() ?? "";
                if (MatchesLoc(loc))
                {
                    assetsSnapshot.Add(aObj.DeepClone());
                    string aId = aObj["id"]?.ToString() ?? "";
                    string cId = aObj["custom_id"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(aId)) matchedAssetIds.Add(aId);
                    if (!string.IsNullOrEmpty(cId)) matchedCustomIds.Add(cId);
                }
            }
        }

        var measurementsSnapshot = new JsonArray();
        var siteMeasurements = await GetMeasurementsForSiteAsync(siteId);
        foreach (var item in siteMeasurements)
        {
            if (item is JsonObject mObj)
            {
                string aId = mObj["asset_id"]?.ToString() ?? "";
                string ub = "";
                string idElem = "";
                if (mObj["extra_data"] is JsonObject ed)
                {
                    ub = ed["ubicacion"]?.ToString() ?? "";
                    idElem = ed["id_elemento"]?.ToString() ?? "";
                }

                if (MatchesLoc(ub) || (!string.IsNullOrEmpty(aId) && matchedAssetIds.Contains(aId)) || (!string.IsNullOrEmpty(idElem) && matchedCustomIds.Contains(idElem)))
                {
                    measurementsSnapshot.Add(mObj.DeepClone());
                }
            }
        }

        var groundingSnapshot = new JsonArray();
        foreach (var item in await GetGroundingLogsAsync(siteId))
        {
            if (item is JsonObject gObj && MatchesLoc(gObj["location"]?.ToString())) groundingSnapshot.Add(gObj.DeepClone());
        }

        var floorSnapshot = new JsonArray();
        foreach (var item in await GetFloorValidationLogsAsync(siteId))
        {
            if (item is JsonObject fObj && (MatchesLoc(fObj["room_name"]?.ToString()) || MatchesLoc(fObj["location"]?.ToString()))) floorSnapshot.Add(fObj.DeepClone());
        }

        var isolatedSnapshot = new JsonArray();
        foreach (var item in await GetIsolatedConductorsLogsAsync(siteId))
        {
            if (item is JsonObject iObj && MatchesLoc(iObj["location"]?.ToString())) isolatedSnapshot.Add(iObj.DeepClone());
        }

        var checkersSnapshot = new JsonArray();
        foreach (var item in await GetEntranceCheckersLogsAsync(siteId))
        {
            if (item is JsonObject cObj && MatchesLoc(cObj["location"]?.ToString())) checkersSnapshot.Add(cObj.DeepClone());
        }

        // 3. Create Archive Group in app_logs if there are any assets, measurements, logs, or lines
        string groupId = Guid.NewGuid().ToString();
        string areaPart = target.Contains("->") ? target.Split("->")[0].Trim() : target;
        string opPart = target.Contains("->") ? string.Join(" -> ", target.Split("->").Skip(1)).Trim() : "";

        var groupRecord = new JsonObject
        {
            ["group_id"] = groupId,
            ["scope_type"] = includeSubOperations ? "AREA" : "OPERATION",
            ["company_id"] = companyId,
            ["company_name"] = companyName,
            ["original_site_id"] = siteId,
            ["original_site_name"] = siteName,
            ["original_location"] = target,
            ["original_area"] = areaPart,
            ["original_operation"] = opPart,
            ["deleted_at"] = DateTime.UtcNow.ToString("o"),
            ["deleted_by"] = userName ?? "Administrador",
            ["lines_count"] = matchedLineNames.Count,
            ["assets_count"] = assetsSnapshot.Count,
            ["measurements_count"] = measurementsSnapshot.Count,
            ["equipment_count"] = 0,
            ["logs_count"] = groundingSnapshot.Count + floorSnapshot.Count + isolatedSnapshot.Count + checkersSnapshot.Count,
            ["lines"] = matchedLineNames,
            ["assets"] = assetsSnapshot,
            ["measurements"] = measurementsSnapshot,
            ["equipment"] = new JsonArray(),
            ["grounding_logs"] = groundingSnapshot,
            ["floor_logs"] = floorSnapshot,
            ["isolated_logs"] = isolatedSnapshot,
            ["checker_logs"] = checkersSnapshot
        };

        await LogAuditEventAsync(userId, null, "ARCHIVE", "archived_location_group", groupId, groupRecord);

        // 4. Unlink matched assets & measurements (set site_id = null) so they are held in the archive group without losing any history
        foreach (var item in assetsSnapshot)
        {
            if (item is JsonObject aObj && aObj["id"] != null)
            {
                string id = aObj["id"]!.ToString();
                await UpdateAsync("assets", $"id=eq.{id}", new { site_id = (string?)null });
            }
        }
        foreach (var item in measurementsSnapshot)
        {
            if (item is JsonObject mObj && mObj["id"] != null)
            {
                string id = mObj["id"]!.ToString();
                await UpdateAsync("measurements", $"id=eq.{id}", new { site_id = (string?)null });
            }
        }
        foreach (var item in groundingSnapshot)
        {
            if (item is JsonObject gObj && gObj["id"] != null) await UpdateAsync("grounding_logs", $"id=eq.{gObj["id"]}", new { site_id = (string?)null });
        }
        foreach (var item in floorSnapshot)
        {
            if (item is JsonObject fObj && fObj["id"] != null) await UpdateAsync("floor_validation_logs", $"id=eq.{fObj["id"]}", new { site_id = (string?)null });
        }
        foreach (var item in isolatedSnapshot)
        {
            if (item is JsonObject iObj && iObj["id"] != null) await UpdateAsync("isolated_conductors_logs", $"id=eq.{iObj["id"]}", new { site_id = (string?)null });
        }
        foreach (var item in checkersSnapshot)
        {
            if (item is JsonObject cObj && cObj["id"] != null) await UpdateAsync("entrance_checkers_logs", $"id=eq.{cObj["id"]}", new { site_id = (string?)null });
        }

        // 5. Delete the lines from catalogo_lineas
        int deleted = 0;
        foreach (var lineId in matchedLineIds)
        {
            if (await DeleteAsync("catalogo_lineas", $"id=eq.{lineId}"))
            {
                deleted++;
            }
        }

        return (deleted > 0, deleted, groupRecord);
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

    public async Task<JsonObject?> GetSiteByIdAsync(string id)
    {
        var res = await GetAsync($"sites?id=eq.{id}&select=*");
        return res.Count > 0 && res[0] is JsonObject sObj ? sObj : null;
    }

    public async Task<bool> DeleteSiteCascadeAsync(string siteId)
    {
        var (success, _) = await ArchiveAndDeleteSiteAsync(siteId);
        return success;
    }

    public async Task<(bool success, JsonObject? archivedGroup)> ArchiveAndDeleteSiteAsync(string siteId, string? userId = null, string? userName = null)
    {
        if (string.IsNullOrWhiteSpace(siteId)) return (false, null);

        var siteObj = await GetSiteByIdAsync(siteId);
        string siteName = siteObj?["name"]?.ToString() ?? "Site Eliminado";
        string companyId = siteObj?["company_id"]?.ToString() ?? "";
        var compObj = !string.IsNullOrEmpty(companyId) ? await GetCompanyByIdAsync(companyId) : null;
        string companyName = compObj?["name"]?.ToString() ?? "Empresa";

        // 1. Collect all site data before deletion so everything is preserved & grouped
        var linesList = await GetCatalogoLineasAsync(siteId);
        var lineNames = new JsonArray();
        foreach (var l in linesList)
        {
            if (l is JsonObject lObj && lObj["nombre_linea"] != null)
            {
                lineNames.Add(lObj["nombre_linea"]!.ToString());
            }
        }

        var assetsSnapshot = await GetAssetsAsync(siteId);
        var measurementsSnapshot = await GetAsync($"measurements?site_id=eq.{siteId}&select=*&order=measured_at.desc");
        var equipmentSnapshot = await GetCatalogoEquiposAsync(siteId);
        var groundingSnapshot = await GetGroundingLogsAsync(siteId);
        var floorSnapshot = await GetFloorValidationLogsAsync(siteId);
        var isolatedSnapshot = await GetIsolatedConductorsLogsAsync(siteId);
        var checkersSnapshot = await GetEntranceCheckersLogsAsync(siteId);
        var reportsSnapshot = await GetLogReportesLineaAsync(siteId, null);
        var employeesSnapshot = await GetEmpleadosBatasAsync(siteId);

        string groupId = Guid.NewGuid().ToString();
        var groupRecord = new JsonObject
        {
            ["group_id"] = groupId,
            ["scope_type"] = "SITE",
            ["company_id"] = companyId,
            ["company_name"] = companyName,
            ["original_site_id"] = siteId,
            ["original_site_name"] = siteName,
            ["original_location"] = siteName,
            ["original_area"] = "Todas las Áreas del Site",
            ["original_operation"] = "",
            ["deleted_at"] = DateTime.UtcNow.ToString("o"),
            ["deleted_by"] = userName ?? "Administrador",
            ["lines_count"] = lineNames.Count,
            ["assets_count"] = assetsSnapshot.Count,
            ["measurements_count"] = measurementsSnapshot.Count,
            ["equipment_count"] = equipmentSnapshot.Count,
            ["logs_count"] = groundingSnapshot.Count + floorSnapshot.Count + isolatedSnapshot.Count + checkersSnapshot.Count + reportsSnapshot.Count,
            ["lines"] = lineNames,
            ["assets"] = assetsSnapshot,
            ["measurements"] = measurementsSnapshot,
            ["equipment"] = equipmentSnapshot,
            ["grounding_logs"] = groundingSnapshot,
            ["floor_logs"] = floorSnapshot,
            ["isolated_logs"] = isolatedSnapshot,
            ["checker_logs"] = checkersSnapshot,
            ["reports"] = reportsSnapshot,
            ["employees"] = employeesSnapshot
        };

        // Save archived group in app_logs with site_id = null so it survives site deletion
        await LogAuditEventAsync(userId, null, "ARCHIVE", "archived_location_group", groupId, groupRecord);

        // 2. Unlink operational records (set site_id = null) so they are preserved in Supabase instead of deleted
        await UpdateAsync("assets", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("measurements", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("catalogo_equipos", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("catalogo_productos", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("empleados_batas", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("grounding_logs", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("floor_validation_logs", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("isolated_conductors_logs", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("entrance_checkers_logs", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("log_reportes_linea", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("validacion_esd", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("app_logs", $"site_id=eq.{siteId}", new { site_id = (string?)null });
        await UpdateAsync("users", $"site_id=eq.{siteId}", new { site_id = (string?)null });

        // 3. Delete site lines and floor_maps
        await DeleteAsync("catalogo_lineas", $"site_id=eq.{siteId}");
        await DeleteAsync("floor_maps", $"site_id=eq.{siteId}");

        // 4. Delete the site row; if any table had a NOT NULL constraint on site_id, clean remaining FK rows (already safely snapshotted in groupRecord)
        bool ok = await DeleteAsync("sites", $"id=eq.{siteId}");
        if (!ok)
        {
            await DeleteAsync("catalogo_equipos", $"site_id=eq.{siteId}");
            await DeleteAsync("catalogo_productos", $"site_id=eq.{siteId}");
            await DeleteAsync("empleados_batas", $"site_id=eq.{siteId}");
            await DeleteAsync("grounding_logs", $"site_id=eq.{siteId}");
            await DeleteAsync("floor_validation_logs", $"site_id=eq.{siteId}");
            await DeleteAsync("isolated_conductors_logs", $"site_id=eq.{siteId}");
            await DeleteAsync("entrance_checkers_logs", $"site_id=eq.{siteId}");
            await DeleteAsync("log_reportes_linea", $"site_id=eq.{siteId}");
            await DeleteAsync("validacion_esd", $"site_id=eq.{siteId}");
            await DeleteAsync("measurements", $"site_id=eq.{siteId}");
            await DeleteAsync("assets", $"site_id=eq.{siteId}");
            await DeleteAsync("app_logs", $"site_id=eq.{siteId}");
            ok = await DeleteAsync("sites", $"id=eq.{siteId}");
        }

        return (ok, groupRecord);
    }

    public async Task<JsonArray> GetArchivedLocationGroupsAsync(string? companyId = null)
    {
        var result = new JsonArray();
        try
        {
            var logs = await GetAsync("app_logs?page=eq.archived_location_group&select=id,message,details,created_at&order=created_at.desc&limit=100");
            var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in logs)
            {
                if (item is JsonObject logObj)
                {
                    string groupId = logObj["message"]?.ToString() ?? "";
                    string detailsRaw = logObj["details"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(groupId) || seenGroups.Contains(groupId)) continue;

                    JsonObject? parsed = null;
                    try
                    {
                        parsed = JsonNode.Parse(detailsRaw) as JsonObject;
                    }
                    catch { }

                    if (parsed == null) continue;
                    seenGroups.Add(groupId);

                    if (string.Equals(parsed["status"]?.ToString(), "REASSIGNED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(parsed["status"]?.ToString(), "DELETED", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string grpCompId = parsed["company_id"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(companyId) && !string.IsNullOrEmpty(grpCompId) && !string.Equals(grpCompId, companyId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string scopeType = parsed["scope_type"]?.ToString() ?? "SITE";
                    string origSiteId = parsed["original_site_id"]?.ToString() ?? "";
                    string origSiteName = parsed["original_site_name"]?.ToString() ?? "Site";
                    string origLoc = parsed["original_location"]?.ToString() ?? origSiteName;
                    int lCount = parsed["lines_count"]?.GetValue<int>() ?? (parsed["lines"] as JsonArray)?.Count ?? 0;
                    int aCount = parsed["assets_count"]?.GetValue<int>() ?? (parsed["assets"] as JsonArray)?.Count ?? 0;
                    int mCount = parsed["measurements_count"]?.GetValue<int>() ?? (parsed["measurements"] as JsonArray)?.Count ?? 0;
                    int eCount = parsed["equipment_count"]?.GetValue<int>() ?? (parsed["equipment"] as JsonArray)?.Count ?? 0;
                    int lgCount = parsed["logs_count"]?.GetValue<int>() ?? 0;
                    var linesClone = (parsed["lines"] as JsonArray)?.DeepClone() ?? new JsonArray();

                    var summary = new JsonObject
                    {
                        ["group_id"] = groupId,
                        ["log_row_id"] = logObj["id"]?.ToString(),
                        ["scope_type"] = scopeType,
                        ["group_type"] = scopeType == "SITE" ? "site" : (scopeType == "AREA" ? "area" : "suboperation"),
                        ["group_name"] = origLoc,
                        ["company_id"] = grpCompId,
                        ["company_name"] = parsed["company_name"]?.ToString() ?? "Empresa",
                        ["original_site_id"] = origSiteId,
                        ["source_site_id"] = origSiteId,
                        ["original_site_name"] = origSiteName,
                        ["source_site_name"] = origSiteName,
                        ["original_location"] = origLoc,
                        ["original_area"] = parsed["original_area"]?.ToString() ?? "",
                        ["original_operation"] = parsed["original_operation"]?.ToString() ?? "",
                        ["deleted_at"] = parsed["deleted_at"]?.ToString() ?? logObj["created_at"]?.ToString(),
                        ["deleted_by"] = parsed["deleted_by"]?.ToString() ?? "Administrador",
                        ["lines_count"] = lCount,
                        ["assets_count"] = aCount,
                        ["measurements_count"] = mCount,
                        ["equipment_count"] = eCount,
                        ["logs_count"] = lgCount,
                        ["counts"] = new JsonObject
                        {
                            ["lines"] = lCount,
                            ["assets"] = aCount,
                            ["measurements"] = mCount,
                            ["equipment"] = eCount,
                            ["epa_logs"] = lgCount
                        },
                        ["lines"] = linesClone,
                        ["locations"] = linesClone.DeepClone()
                    };

                    result.Add(summary);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetArchivedLocationGroupsAsync Error: {ex.Message}");
        }
        return result;
    }

    public async Task<(bool success, string message)> ReassignArchivedLocationGroupAsync(string groupId, string targetSiteId, string? targetLocationOverride = null)
    {
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(targetSiteId))
        {
            return (false, "El ID del grupo y el Site destino son obligatorios.");
        }

        var logs = await GetAsync($"app_logs?page=eq.archived_location_group&message=eq.{groupId}&select=id,details&order=created_at.desc&limit=1");
        if (logs.Count == 0 || logs[0] is not JsonObject logRow)
        {
            return (false, "No se encontró el grupo en resguardo.");
        }

        JsonObject? groupObj = null;
        try
        {
            groupObj = JsonNode.Parse(logRow["details"]?.ToString() ?? "") as JsonObject;
        }
        catch { }

        if (groupObj == null)
        {
            return (false, "Error al leer los datos del grupo en resguardo.");
        }

        var targetSiteObj = await GetSiteByIdAsync(targetSiteId);
        if (targetSiteObj == null)
        {
            return (false, "El Site destino seleccionado no existe.");
        }

        string targetCompanyId = targetSiteObj["company_id"]?.ToString() ?? "";
        string targetSiteName = targetSiteObj["name"]?.ToString() ?? "Site Destino";
        string? cleanLocationOverride = !string.IsNullOrWhiteSpace(targetLocationOverride) ? targetLocationOverride.Trim() : null;

        // 1. Ensure lines exist in target site
        var existingTargetLines = await GetCatalogoLineasAsync(targetSiteId);
        var existingLineNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in existingTargetLines)
        {
            if (l is JsonObject lObj && lObj["nombre_linea"] != null)
            {
                existingLineNames.Add(lObj["nombre_linea"]!.ToString().Trim());
            }
        }

        if (!string.IsNullOrEmpty(cleanLocationOverride))
        {
            if (cleanLocationOverride.Contains("->"))
            {
                string parentArea = cleanLocationOverride.Split("->")[0].Trim();
                if (!string.IsNullOrEmpty(parentArea) && existingLineNames.Add(parentArea))
                {
                    await InsertCatalogoLineaAsync(new { site_id = targetSiteId, company_id = targetCompanyId, nombre_linea = parentArea });
                }
            }
            if (existingLineNames.Add(cleanLocationOverride))
            {
                await InsertCatalogoLineaAsync(new { site_id = targetSiteId, company_id = targetCompanyId, nombre_linea = cleanLocationOverride });
            }
        }
        else if (groupObj["lines"] is JsonArray linesArr)
        {
            foreach (var lNode in linesArr)
            {
                string lName = lNode?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(lName) && existingLineNames.Add(lName))
                {
                    await InsertCatalogoLineaAsync(new { site_id = targetSiteId, company_id = targetCompanyId, nombre_linea = lName });
                }
            }
        }

        int restoredAssets = 0;
        int restoredMeasurements = 0;
        int restoredEquipment = 0;

        // 2. Restore / reassign assets
        if (groupObj["assets"] is JsonArray assetsArr)
        {
            foreach (var item in assetsArr)
            {
                if (item is JsonObject aObj)
                {
                    string aId = aObj["id"]?.ToString() ?? "";
                    string finalLoc = !string.IsNullOrEmpty(cleanLocationOverride) ? cleanLocationOverride : (aObj["location"]?.ToString() ?? "General");

                    // Ensure finalLoc exists in catalogo_lineas of target site
                    if (!string.IsNullOrEmpty(finalLoc) && finalLoc != "N/A" && finalLoc != "General" && existingLineNames.Add(finalLoc))
                    {
                        await InsertCatalogoLineaAsync(new { site_id = targetSiteId, company_id = targetCompanyId, nombre_linea = finalLoc });
                    }

                    var existingAsset = !string.IsNullOrEmpty(aId) ? await GetAsync($"assets?id=eq.{aId}&select=id") : new JsonArray();
                    if (existingAsset.Count > 0)
                    {
                        await UpdateAsync("assets", $"id=eq.{aId}", new { site_id = targetSiteId, location = finalLoc });
                    }
                    else
                    {
                        var clone = aObj.DeepClone() as JsonObject ?? new JsonObject();
                        clone["site_id"] = targetSiteId;
                        clone["location"] = finalLoc;
                        await InsertAsync("assets", clone);
                    }
                    restoredAssets++;
                }
            }
        }

        // 3. Restore / reassign measurements
        if (groupObj["measurements"] is JsonArray measArr)
        {
            foreach (var item in measArr)
            {
                if (item is JsonObject mObj)
                {
                    string mId = mObj["id"]?.ToString() ?? "";
                    var edClone = (mObj["extra_data"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
                    if (!string.IsNullOrEmpty(cleanLocationOverride))
                    {
                        edClone["ubicacion"] = cleanLocationOverride;
                    }

                    var existingMeas = !string.IsNullOrEmpty(mId) ? await GetAsync($"measurements?id=eq.{mId}&select=id") : new JsonArray();
                    if (existingMeas.Count > 0)
                    {
                        await UpdateAsync("measurements", $"id=eq.{mId}", new { site_id = targetSiteId, extra_data = edClone });
                    }
                    else
                    {
                        var clone = mObj.DeepClone() as JsonObject ?? new JsonObject();
                        clone["site_id"] = targetSiteId;
                        clone["extra_data"] = edClone;
                        await InsertAsync("measurements", clone);
                    }
                    restoredMeasurements++;
                }
            }
        }

        // 4. Restore / reassign equipment
        if (groupObj["equipment"] is JsonArray eqArr)
        {
            foreach (var item in eqArr)
            {
                if (item is JsonObject eqObj)
                {
                    string eqId = eqObj["id"]?.ToString() ?? "";
                    var existingEq = !string.IsNullOrEmpty(eqId) ? await GetAsync($"catalogo_equipos?id=eq.{eqId}&select=id") : new JsonArray();
                    if (existingEq.Count > 0)
                    {
                        await UpdateAsync("catalogo_equipos", $"id=eq.{eqId}", new { site_id = targetSiteId });
                    }
                    else
                    {
                        var clone = eqObj.DeepClone() as JsonObject ?? new JsonObject();
                        clone["site_id"] = targetSiteId;
                        await InsertAsync("catalogo_equipos", clone);
                    }
                    restoredEquipment++;
                }
            }
        }

        // 5. Restore EPA logs
        await RestoreSnapshottedLogsAsync("grounding_logs", "location", groupObj["grounding_logs"] as JsonArray, targetSiteId, cleanLocationOverride);
        await RestoreSnapshottedLogsAsync("floor_validation_logs", "room_name", groupObj["floor_logs"] as JsonArray, targetSiteId, cleanLocationOverride);
        await RestoreSnapshottedLogsAsync("isolated_conductors_logs", "location", groupObj["isolated_logs"] as JsonArray, targetSiteId, cleanLocationOverride);
        await RestoreSnapshottedLogsAsync("entrance_checkers_logs", "location", groupObj["checker_logs"] as JsonArray, targetSiteId, cleanLocationOverride);

        // 6. Mark archive group as reassigned / remove from active archive
        await DeleteAsync("app_logs", $"page=eq.archived_location_group&message=eq.{groupId}");

        return (true, $"Grupo reasignado exitosamente a '{targetSiteName}' ({restoredAssets} activos, {restoredMeasurements} mediciones y {restoredEquipment} equipos restaurados).");
    }

    private async Task RestoreSnapshottedLogsAsync(string table, string locCol, JsonArray? arr, string targetSiteId, string? cleanLocationOverride)
    {
        if (arr == null) return;
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
            {
                string id = obj["id"]?.ToString() ?? "";
                var existing = !string.IsNullOrEmpty(id) ? await GetAsync($"{table}?id=eq.{id}&select=id") : new JsonArray();
                if (existing.Count > 0)
                {
                    var patch = new JsonObject { ["site_id"] = targetSiteId };
                    if (!string.IsNullOrEmpty(cleanLocationOverride)) patch[locCol] = cleanLocationOverride;
                    await UpdateAsync(table, $"id=eq.{id}", patch);
                }
                else
                {
                    var clone = obj.DeepClone() as JsonObject ?? new JsonObject();
                    clone["site_id"] = targetSiteId;
                    if (!string.IsNullOrEmpty(cleanLocationOverride)) clone[locCol] = cleanLocationOverride;
                    await InsertAsync(table, clone);
                }
            }
        }
    }

    public async Task<bool> DeleteArchivedLocationGroupPermanentlyAsync(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return false;

        var logs = await GetAsync($"app_logs?page=eq.archived_location_group&message=eq.{groupId}&select=id,details&order=created_at.desc&limit=1");
        if (logs.Count > 0 && logs[0] is JsonObject logRow)
        {
            try
            {
                var groupObj = JsonNode.Parse(logRow["details"]?.ToString() ?? "") as JsonObject;
                if (groupObj != null)
                {
                    if (groupObj["measurements"] is JsonArray measArr)
                    {
                        foreach (var m in measArr)
                        {
                            if (m is JsonObject mObj && mObj["id"] != null) await DeleteAsync("measurements", $"id=eq.{mObj["id"]}");
                        }
                    }
                    if (groupObj["assets"] is JsonArray assetsArr)
                    {
                        foreach (var a in assetsArr)
                        {
                            if (a is JsonObject aObj && aObj["id"] != null) await DeleteAsync("assets", $"id=eq.{aObj["id"]}");
                        }
                    }
                    if (groupObj["equipment"] is JsonArray eqArr)
                    {
                        foreach (var eq in eqArr)
                        {
                            if (eq is JsonObject eqObj && eqObj["id"] != null) await DeleteAsync("catalogo_equipos", $"id=eq.{eqObj["id"]}");
                        }
                    }
                }
            }
            catch { }
        }

        return await DeleteAsync("app_logs", $"page=eq.archived_location_group&message=eq.{groupId}");
    }

    public async Task<bool> DeleteCompanyCascadeAsync(string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return false;

        // 1. Delete all sites and their subtypes for this company
        var sites = await GetSitesAsync(companyId);
        foreach (var s in sites)
        {
            if (s is JsonObject sObj)
            {
                string sId = sObj["id"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(sId))
                {
                    await DeleteSiteCascadeAsync(sId);
                }
            }
        }

        // 2. Clean up company-level references
        await DeleteAsync("catalogo_lineas", $"company_id=eq.{companyId}");
        await DeleteAsync("log_reportes_linea", $"company_id=eq.{companyId}");
        await UpdateAsync("users", $"company_id=eq.{companyId}", new { company_id = (string?)null, site_id = (string?)null });
        SetCompanyLogoCache(companyId, null);

        // 3. Delete the company itself
        return await DeleteAsync("companies", $"id=eq.{companyId}");
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

    public async Task<JsonObject?> GetUserByIdAsync(string id)
    {
        var res = await GetAsync($"users?id=eq.{id}&select=*");
        return res.Count > 0 && res[0] is JsonObject uObj ? uObj : null;
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        await UpdateAsync("app_logs", $"user_id=eq.{id}", new { user_id = (string?)null });
        await UpdateAsync("measurements", $"auditor_id=eq.{id}", new { auditor_id = (string?)null });
        await UpdateAsync("grounding_logs", $"auditor_id=eq.{id}", new { auditor_id = (string?)null });
        await UpdateAsync("floor_validation_logs", $"auditor_id=eq.{id}", new { auditor_id = (string?)null });
        await UpdateAsync("isolated_conductors_logs", $"auditor_id=eq.{id}", new { auditor_id = (string?)null });
        await UpdateAsync("entrance_checkers_logs", $"auditor_id=eq.{id}", new { auditor_id = (string?)null });
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
            string assetQuery = !string.IsNullOrEmpty(siteId)
                ? $"assets?site_id=eq.{siteId}&custom_id=eq.{elementId}&select=*"
                : $"assets?custom_id=eq.{elementId}&select=*";
            var existingAssets = await GetAsync(assetQuery);
            string assetDbId = "";
            if (existingAssets.Count > 0 && existingAssets[0] is JsonObject aObj)
            {
                assetDbId = aObj["id"]?.ToString() ?? "";
                string existingStatus = aObj["status"]?.ToString() ?? "ACTIVE";
                bool isDecom = existingStatus.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase) ||
                               existingStatus.Equals("BAJA", StringComparison.OrdinalIgnoreCase) ||
                               existingStatus.Equals("DECOMMISSIONED", StringComparison.OrdinalIgnoreCase);
                if (isDecom && !string.IsNullOrEmpty(assetDbId))
                {
                    // REACTIVATE UPON AUDIT
                    await UpdateAsync("assets", $"id=eq.{assetDbId}", new { status = "ACTIVE" });
                }
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

    // --- PERSISTENT COMPANY LOGO SERVICE (SUPABASE DB + STORAGE) ---
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _companyLogoCache = new(StringComparer.OrdinalIgnoreCase);

    public void SetCompanyLogoCache(string companyId, string? dataUri)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return;
        _companyLogoCache[companyId.Trim()] = string.IsNullOrWhiteSpace(dataUri) ? "NONE" : dataUri;
    }

    public async Task SaveCompanyLogoAsync(string companyId, string dataUri, string? userId = null, string? siteId = null)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return;
        string cleanId = companyId.Trim();
        SetCompanyLogoCache(cleanId, dataUri);

        // 1. Persist in app_logs under page='company_logo_config' (guaranteed schema)
        await LogAuditEventAsync(userId, siteId, "CONFIG", "company_logo_config", cleanId, string.IsNullOrWhiteSpace(dataUri) ? "RESET" : dataUri);

        // 2. Also attempt to update companies.logo_url if column exists
        try
        {
            await UpdateCompanyAsync(cleanId, new { logo_url = string.IsNullOrWhiteSpace(dataUri) ? null : dataUri });
        }
        catch { }
    }

    public async Task<string> GetCompanyLogoDataUriAsync(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return "";
        string cleanId = companyId.Trim();

        if (_companyLogoCache.TryGetValue(cleanId, out var cached) && !string.IsNullOrEmpty(cached))
        {
            return cached == "NONE" ? "" : cached;
        }

        // 1. Check app_logs (page='company_logo_config')
        try
        {
            var logs = await GetAsync($"app_logs?page=eq.company_logo_config&message=eq.{cleanId}&select=details,created_at&order=created_at.desc&limit=1");
            if (logs.Count > 0 && logs[0] is JsonObject logObj)
            {
                string details = logObj["details"]?.ToString()?.Trim('"') ?? "";
                if (details == "RESET")
                {
                    _companyLogoCache[cleanId] = "NONE";
                    return "";
                }
                if (details.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    _companyLogoCache[cleanId] = details;
                    return details;
                }
            }
        }
        catch { }

        // 2. Check companies.logo_url if present
        try
        {
            var comp = await GetCompanyByIdAsync(cleanId);
            string? dbLogo = comp?["logo_url"]?.ToString();
            if (!string.IsNullOrEmpty(dbLogo) && dbLogo.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                _companyLogoCache[cleanId] = dbLogo;
                return dbLogo;
            }
        }
        catch { }

        // 3. Check Supabase Storage bucket 'audit-evidence' under prefix 'logos/'
        try
        {
            using var listReq = new HttpRequestMessage(HttpMethod.Post, $"{_config.Url.TrimEnd('/')}/storage/v1/object/list/audit-evidence");
            listReq.Headers.Add("apikey", _config.Key);
            listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
            listReq.Content = new StringContent("{\"prefix\":\"logos/\",\"limit\":100}", Encoding.UTF8, "application/json");

            var listRes = await _http.SendAsync(listReq);
            if (listRes.IsSuccessStatusCode)
            {
                var listJson = await listRes.Content.ReadAsStringAsync();
                if (JsonNode.Parse(listJson) is JsonArray items)
                {
                    string? latestName = null;
                    DateTime latestTime = DateTime.MinValue;

                    foreach (var item in items)
                    {
                        if (item is JsonObject obj)
                        {
                            string name = obj["name"]?.ToString() ?? "";
                            if (name.StartsWith($"{cleanId}_", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals($"{cleanId}.png", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals($"{cleanId}.jpg", StringComparison.OrdinalIgnoreCase))
                            {
                                DateTime dt = DateTime.MinValue;
                                if (DateTime.TryParse(obj["created_at"]?.ToString() ?? obj["updated_at"]?.ToString(), out var parsedDt))
                                {
                                    dt = parsedDt;
                                }
                                if (latestName == null || dt >= latestTime)
                                {
                                    latestTime = dt;
                                    latestName = name;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(latestName))
                    {
                        var (dlOk, dlStream, contentType, _) = await DownloadStorageObjectAsync("audit-evidence", $"logos/{latestName}");
                        if (dlOk && dlStream != null)
                        {
                            using (dlStream)
                            using (var ms = new MemoryStream())
                            {
                                await dlStream.CopyToAsync(ms);
                                byte[] bytes = ms.ToArray();
                                if (bytes.Length > 0)
                                {
                                    string ext = Path.GetExtension(latestName).ToLowerInvariant();
                                    string mime = ext switch
                                    {
                                        ".jpg" or ".jpeg" => "image/jpeg",
                                        ".svg" => "image/svg+xml",
                                        ".webp" => "image/webp",
                                        _ => !string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/") ? contentType : "image/png"
                                    };
                                    string dataUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                                    _companyLogoCache[cleanId] = dataUri;
                                    return dataUri;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COMPANY LOGO STORAGE LOOKUP]: {ex.Message}");
        }

        _companyLogoCache[cleanId] = "NONE";
        return "";
    }
}

