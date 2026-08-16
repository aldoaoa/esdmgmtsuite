using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using ESDSuite.Core.Constants;
using ESDSuite.Core.Helpers;
using ESDSuite.Core.Models;
using ESDSuite.Services.Auth;
using ESDSuite.Services.Supabase;

using ESDSuite.Services.Storage;

namespace ESDSuite.Web.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly FloorMapStorageService _mapStorage;
    private const string DefaultSiteId = "eff70028-0759-4033-9c2b-41e1c1cc6efd";
    private const string DefaultAuditorId = "84d85bea-272c-42d1-ad14-35eb702f1e56";

    private string CurrentUserRole
    {
        get
        {
            string? role = HttpContext.Session.GetString("user_role");
            if (string.IsNullOrEmpty(role))
            {
                role = Request.Headers["X-User-Role"].FirstOrDefault();
            }
            return !string.IsNullOrWhiteSpace(role) ? role : "Auditor";
        }
    }
    private string? CurrentUserCompanyId => HttpContext.Session.GetString("company_id");
    private string CurrentUserSiteId => HttpContext.Session.GetString("site_id") ?? DefaultSiteId;

    private bool IsSuperAdmin
    {
        get
        {
            string r = CurrentUserRole.Trim().ToLower().Replace("_", "").Replace(" ", "").Replace("-", "");
            return r == "superadmin" || r == "root";
        }
    }

    private bool IsCompanyAdmin
    {
        get
        {
            if (IsSuperAdmin) return true;
            string r = CurrentUserRole.Trim().ToLower().Replace("_", "").Replace(" ", "").Replace("-", "");
            return r == "companyadmin";
        }
    }

    private bool IsSiteAdmin
    {
        get
        {
            if (IsCompanyAdmin) return true;
            string r = CurrentUserRole.Trim().ToLower().Replace("_", "").Replace(" ", "").Replace("-", "");
            return r == "siteadmin" || r == "admin" || r == "administrador";
        }
    }

    public ApiController(SupabaseService supabase, FloorMapStorageService mapStorage)
    {
        _supabase = supabase;
        _mapStorage = mapStorage;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] JsonObject payload)
    {
        string email = payload["email"]?.ToString() ?? "";
        string pwd = payload["password"]?.ToString() ?? "";

        var (success, session, message) = await _supabase.IniciarSesionAsync(email, pwd);
        if (success && session != null)
        {
            HttpContext.Session.SetString("user_id", session.Id);
            HttpContext.Session.SetString("user_email", session.Email);
            HttpContext.Session.SetString("user_name", session.FullName);
            HttpContext.Session.SetString("user_role", session.Role);
            HttpContext.Session.SetString("site_id", !string.IsNullOrEmpty(session.SiteId) ? session.SiteId : DefaultSiteId);
            HttpContext.Session.SetString("company_id", session.CompanyId ?? "");
            HttpContext.Session.SetString("site_name", session.SiteName ?? "");
            HttpContext.Session.SetString("company_name", session.CompanyName ?? "");
            HttpContext.Session.SetString("is_logged_in", "true");

            return Ok(new { success = true, user = session });
        }

        return BadRequest(new { success = false, message });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { success = true });
    }

    [HttpGet("session")]
    public IActionResult GetSession()
    {
        bool isLoggedIn = HttpContext.Session.GetString("is_logged_in") == "true";
        if (!isLoggedIn)
        {
            return Ok(new { isLoggedIn = false });
        }

        return Ok(new
        {
            isLoggedIn = true,
            user_id = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId,
            user_email = HttpContext.Session.GetString("user_email"),
            user_name = HttpContext.Session.GetString("user_name"),
            user_role = CurrentUserRole,
            site_id = CurrentUserSiteId,
            company_id = CurrentUserCompanyId,
            site_name = HttpContext.Session.GetString("site_name"),
            company_name = HttpContext.Session.GetString("company_name"),
            can_create_company = IsSuperAdmin,
            can_create_site = IsCompanyAdmin,
            can_create_area = IsSiteAdmin,
            can_create_user = IsSiteAdmin,
            can_change_site = IsCompanyAdmin,
            lang = HttpContext.Session.GetString("lang") ?? Request.Cookies["esd360_lang"] ?? "en",
            version = EsdConstants.SystemVersion
        });
    }

    [HttpPost("set-site")]
    public async Task<IActionResult> SetActiveSite([FromBody] JsonObject payload)
    {
        string targetSiteId = payload["site_id"]?.ToString() ?? "";
        string targetSiteName = payload["site_name"]?.ToString() ?? "";

        if (string.IsNullOrEmpty(targetSiteId)) return BadRequest(new { success = false, message = "site_id invalido" });

        if (IsSuperAdmin)
        {
            HttpContext.Session.SetString("site_id", targetSiteId);
            if (!string.IsNullOrEmpty(targetSiteName)) HttpContext.Session.SetString("site_name", targetSiteName);
            return Ok(new { success = true });
        }

        if (IsCompanyAdmin)
        {
            var allowedSites = await _supabase.GetSitesAsync(CurrentUserCompanyId);
            bool isAllowed = false;
            foreach (var s in allowedSites)
            {
                if (s is JsonObject sObj && sObj["id"]?.ToString() == targetSiteId)
                {
                    isAllowed = true; break;
                }
            }
            if (!isAllowed)
            {
                return StatusCode(403, new { success = false, message = "No tienes permiso para acceder a sitios de otra empresa." });
            }

            HttpContext.Session.SetString("site_id", targetSiteId);
            if (!string.IsNullOrEmpty(targetSiteName)) HttpContext.Session.SetString("site_name", targetSiteName);
            return Ok(new { success = true });
        }

        return StatusCode(403, new { success = false, message = "Tu rol de usuario está asignado exclusivamente a tu planta y no permite cambiar de site." });
    }

    [HttpPost("set-lang")]
    public IActionResult SetLanguage([FromBody] JsonObject payload)
    {
        string lang = payload["lang"]?.ToString() ?? "en";
        HttpContext.Session.SetString("lang", lang);
        Response.Cookies.Append("esd360_lang", lang, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return Ok(new { success = true, lang });
    }

    [HttpGet("dashboard-metrics")]
    public async Task<IActionResult> GetDashboardMetrics([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        
        var assets = await _supabase.GetAssetsAsync(targetSite);
        var measurements = await _supabase.GetMeasurementsForSiteAsync(targetSite);
        
        var assetMap = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in assets)
        {
            if (node is JsonObject aObj)
            {
                string customId = aObj["custom_id"]?.ToString().Trim() ?? "";
                if (!string.IsNullOrEmpty(customId))
                {
                    assetMap[customId] = aObj;
                }
            }
        }

        foreach (var node in measurements)
        {
            if (node is not JsonObject mObj) continue;

            string idElem = "";
            if (mObj["extra_data"] is JsonObject ed)
            {
                idElem = ed["id_elemento"]?.ToString().Trim() ?? "";
            }

            if (string.IsNullOrEmpty(idElem) && mObj["asset_id"] != null)
            {
                string aId = mObj["asset_id"]?.ToString() ?? "";
                var matched = assetMap.Values.FirstOrDefault(x => x["id"]?.ToString() == aId);
                if (matched != null) idElem = matched["custom_id"]?.ToString() ?? "";
            }

            if (!string.IsNullOrEmpty(idElem) && !assetMap.ContainsKey(idElem))
            {
                string cat = "Mobiliario ESD";
                string loc = "N/A";
                if (mObj["extra_data"] is JsonObject edObj)
                {
                    cat = edObj["tipo_equipo"]?.ToString() ?? cat;
                    loc = edObj["ubicacion"]?.ToString() ?? loc;
                }
                assetMap[idElem] = new JsonObject
                {
                    ["id"] = mObj["asset_id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    ["custom_id"] = idElem,
                    ["category"] = cat,
                    ["location"] = loc,
                    ["status"] = mObj["status_result"]?.ToString() ?? "ACTIVE"
                };
            }
        }

        var floors = await _supabase.GetFloorValidationLogsAsync(targetSite);
        var grounding = await _supabase.GetGroundingLogsAsync(targetSite);
        var entrance = await _supabase.GetEntranceCheckersLogsAsync(targetSite);

        var unifiedAssets = new JsonArray();
        foreach (var a in assetMap.Values)
        {
            unifiedAssets.Add(a.DeepClone());
        }

        return Ok(new
        {
            totalAssets = assetMap.Count,
            totalFloors = floors.Count,
            totalGrounding = grounding.Count,
            totalEntrance = entrance.Count,
            assetsData = unifiedAssets
        });
    }

    private async Task<string> GetOrCreateAssetIdAsync(string customId, string siteId, string category, string location, string? subCategory = null)
    {
        string cleanCustomId = customId.Trim().ToUpper();
        string cleanLocation = location.Trim().ToUpper();
        var existingAssets = await _supabase.GetAssetsAsync(siteId);

        foreach (var node in existingAssets)
        {
            if (node is JsonObject aObj)
            {
                string idVal = aObj["custom_id"]?.ToString() ?? aObj["asset_id"]?.ToString() ?? "";
                if (string.Equals(idVal.Trim(), cleanCustomId, StringComparison.OrdinalIgnoreCase))
                {
                    string existingDbId = aObj["id"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(existingDbId))
                    {
                        await _supabase.UpdateAssetAsync(existingDbId, new
                        {
                            location = cleanLocation,
                            category = category,
                            sub_category = subCategory ?? category,
                            classification = category,
                            status = "ACTIVE"
                        });
                    }
                    return !string.IsNullOrEmpty(existingDbId) ? existingDbId : Guid.NewGuid().ToString();
                }
            }
        }

        string? companyId = HttpContext.Session.GetString("company_id");
        if (string.IsNullOrEmpty(companyId) && !string.IsNullOrEmpty(siteId))
        {
            var sites = await _supabase.GetSitesAsync();
            foreach (var s in sites)
            {
                if (s is JsonObject sObj && sObj["id"]?.ToString() == siteId)
                {
                    companyId = sObj["company_id"]?.ToString();
                    break;
                }
            }
        }

        // Insert new asset if not existing
        var newAsset = await _supabase.InsertAssetAsync(new
        {
            site_id = siteId,
            company_id = companyId,
            custom_id = cleanCustomId,
            category = category,
            sub_category = subCategory ?? category,
            classification = category,
            location = cleanLocation,
            status = "ACTIVE"
        });

        return newAsset?["id"]?.ToString() ?? Guid.NewGuid().ToString();
    }

    [HttpGet("audit/last-measurement/{id}")]
    public async Task<IActionResult> GetUltimaMedicion([FromRoute] string id)
    {
        var result = await _supabase.GetUltimaMedicionAsync(id);
        return Ok(new { found = result != null, data = result });
    }

    [HttpPost("audit/submit-form")]
    public async Task<IActionResult> SubmitAuditForm([FromBody] JsonObject payload)
    {
        string idElemento = payload["id_elemento"]?.ToString() ?? "";
        string tipoEquipo = payload["tipo_equipo"]?.ToString() ?? "Mobiliario ESD";
        string subtipoElemento = payload["subtipo_elemento"]?.ToString() ?? "";
        string subtipoKey = payload["subtipo_key"]?.ToString() ?? "";
        string ubicacion = payload["ubicacion"]?.ToString() ?? "N/A";
        string puntoContacto = payload["punto_contacto"]?.ToString() ?? "";
        string auditor = HttpContext.Session.GetString("user_name") 
            ?? HttpContext.Session.GetString("user_email") 
            ?? payload["auditor"]?.ToString() 
            ?? "Auditor ESD";
        string comentarios = payload["comentarios"]?.ToString() ?? "";
        string siteId = HttpContext.Session.GetString("site_id") ?? payload["site_id"]?.ToString() ?? DefaultSiteId;
        string auditorId = HttpContext.Session.GetString("user_id") ?? payload["auditor_id"]?.ToString() ?? DefaultAuditorId;
        string fechaActual = DateTime.Now.ToString("o");

        string assetId = await GetOrCreateAssetIdAsync(idElemento, siteId, tipoEquipo, ubicacion, subtipoElemento);

        string estatusEval = "PENDIENTE";
        JsonObject? resInsert = null;

        bool isIonizer = tipoEquipo.Trim().ToLower().Contains("ionizad") || tipoEquipo.Trim().ToLower() == "ionizador" || subtipoKey.Contains("ioniz") || subtipoKey == "benchtop" || subtipoKey == "overhead" || subtipoKey == "ceiling";

        if (isIonizer)
        {
            double tiempoDescarga = payload["tiempo_descarga"]?.GetValue<double>() ?? 0;
            int voltajeBalance = payload["voltaje_balance"]?.GetValue<int>() ?? 0;

            estatusEval = AuditEvaluationEngine.EvaluateIonizer(tiempoDescarga, voltajeBalance);

            var measurementData = new
            {
                site_id = siteId,
                asset_id = assetId,
                auditor_id = auditorId,
                static_field_value = (double)voltajeBalance,
                status_result = estatusEval == "PASA" ? "PASS" : "FAIL",
                observaciones = comentarios,
                temperatura = "23.5",
                humedad = "45.0",
                extra_data = new
                {
                    id_elemento = idElemento,
                    tipo_equipo = tipoEquipo,
                    subtipo_elemento = subtipoElemento,
                    subtipo_key = subtipoKey,
                    ubicacion = ubicacion,
                    punto_contacto = puntoContacto,
                    tiempo_descarga = tiempoDescarga,
                    voltaje_balance = voltajeBalance,
                    auditor = auditor
                },
                measured_at = fechaActual
            };

            resInsert = await _supabase.InsertMeasurementAsync(measurementData);
            return Ok(new { success = resInsert != null, estatus = estatusEval, data = resInsert });
        }
        else
        {
            double resistencia = payload["resistencia"]?.GetValue<double>() ?? 0;
            int voltajeCampo = payload["voltaje_campo"]?.GetValue<int>() ?? 0;
            var medicionesExtra = payload["mediciones_extra"] as JsonArray ?? new JsonArray();

            estatusEval = AuditEvaluationEngine.EvaluateFurnitureOrMachinery(resistencia, voltajeCampo);

            var measurementData = new
            {
                site_id = siteId,
                asset_id = assetId,
                auditor_id = auditorId,
                resistance_value = resistencia,
                static_field_value = (double)voltajeCampo,
                status_result = estatusEval == "PASA" ? "PASS" : "FAIL",
                observaciones = comentarios,
                temperatura = "23.5",
                humedad = "45.0",
                extra_data = new
                {
                    id_elemento = idElemento,
                    tipo_equipo = tipoEquipo,
                    subtipo_elemento = subtipoElemento,
                    subtipo_key = subtipoKey,
                    ubicacion = ubicacion,
                    punto_contacto = puntoContacto,
                    mediciones_extra = medicionesExtra,
                    auditor = auditor
                },
                measured_at = fechaActual
            };

            resInsert = await _supabase.InsertMeasurementAsync(measurementData);
            return Ok(new { success = resInsert != null, estatus = estatusEval, data = resInsert });
        }
    }

    // --- EVENT METER ---
    [HttpGet("event-meter")]
    public async Task<IActionResult> GetEventMeterLogs([FromQuery] string? siteId = null)
    {
        string targetSiteId = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var data = await _supabase.GetEventMeterLogsAsync(targetSiteId);
        
        var formatted = data.Select(item =>
        {
            var obj = item?.AsObject();
            if (obj == null) return null;

            var extra = obj["extra_data"]?.AsObject();
            var assets = obj["assets"]?.AsObject();

            string id = obj["id"]?.ToString() ?? "";
            string measuredAt = obj["measured_at"]?.ToString() ?? "";
            string statusResult = obj["status_result"]?.ToString() ?? "PASS";
            string observaciones = obj["observaciones"]?.ToString() ?? "";
            string auditorId = obj["auditor_id"]?.ToString() ?? "";
            string assetId = assets?["asset_id"]?.ToString() ?? obj["asset_id"]?.ToString() ?? "";
            string areaLine = extra?["linea_ubicacion"]?.ToString() ?? assets?["area_line"]?.ToString() ?? "";
            string idOp = extra?["id_operacion"]?.ToString() ?? assetId;
            string tipoContacto = extra?["tipo_contacto"]?.ToString() ?? "Maquinaria";
            int cantEventos = extra?["cantidad_eventos"]?.GetValue<int>() ?? 0;
            double voltMax = obj["static_field_value"]?.GetValue<double>() ?? extra?["voltaje_maximo"]?.GetValue<double>() ?? 0;
            double? temp = extra?["temperatura"] != null ? extra["temperatura"]?.GetValue<double>() : null;
            double? hum = extra?["humedad"] != null ? extra["humedad"]?.GetValue<double>() : null;
            string tiempoAnalisis = extra?["tiempo_analisis"]?.ToString() 
                ?? (extra?["tiempo_analisis_valor"] != null ? $"{extra["tiempo_analisis_valor"]} {extra["tiempo_analisis_unidad"] ?? "min"}" : "-");
            double? tiempoValor = extra?["tiempo_analisis_valor"] != null ? extra["tiempo_analisis_valor"]?.GetValue<double>() : null;
            string tiempoUnidad = extra?["tiempo_analisis_unidad"]?.ToString() ?? "min";

            return new
            {
                id,
                measured_at = measuredAt,
                linea_ubicacion = areaLine,
                id_operacion = idOp,
                tipo_contacto = tipoContacto,
                tiempo_analisis = tiempoAnalisis,
                tiempo_analisis_valor = tiempoValor,
                tiempo_analisis_unidad = tiempoUnidad,
                cantidad_eventos = cantEventos,
                voltaje_maximo = voltMax,
                temperatura = temp,
                humedad = hum,
                observaciones,
                status_result = statusResult,
                auditor_id = auditorId,
                can_edit = IsSiteAdmin,
                can_delete = IsSiteAdmin
            };
        }).Where(x => x != null);

        return Ok(formatted);
    }

    [HttpPost("event-meter")]
    public async Task<IActionResult> AddEventMeterLog([FromBody] JsonObject payload)
    {
        string linea = payload["linea_ubicacion"]?.ToString() ?? "SMT-01";
        string idOp = payload["id_operacion"]?.ToString() ?? "OP-01";
        string tipoContacto = payload["tipo_contacto"]?.ToString() ?? "Maquinaria";
        double tiempoValor = payload["tiempo_analisis_valor"] != null ? (payload["tiempo_analisis_valor"]?.GetValue<double>() ?? 30) : 30;
        string tiempoUnidad = payload["tiempo_analisis_unidad"]?.ToString() ?? "min";
        string tiempoAnalisis = payload["tiempo_analisis"]?.ToString() ?? $"{tiempoValor} {tiempoUnidad}";
        int cantEventos = payload["cantidad_eventos"]?.GetValue<int>() ?? 0;
        double voltMax = payload["voltaje_maximo"]?.GetValue<double>() ?? 0;
        double? temp = payload["temperatura"] != null ? payload["temperatura"]?.GetValue<double>() : 23.5;
        double? hum = payload["humedad"] != null ? payload["humedad"]?.GetValue<double>() : 45.0;
        string notas = payload["notas"]?.ToString() ?? "";
        string siteId = HttpContext.Session.GetString("site_id") ?? payload["site_id"]?.ToString() ?? DefaultSiteId;
        string auditorId = HttpContext.Session.GetString("user_id") ?? payload["auditor_id"]?.ToString() ?? DefaultAuditorId;

        string assetId = await GetOrCreateAssetIdAsync(idOp, siteId, "Event Meter", linea);

        string estatus = AuditEvaluationEngine.EvaluateEventMeter(voltMax);
        string statusResult = (estatus == "APROBADO" && voltMax <= 100.0) ? "PASS" : "FAIL";

        var dataToInsert = new
        {
            site_id = siteId,
            asset_id = assetId,
            auditor_id = auditorId,
            static_field_value = voltMax,
            status_result = statusResult,
            observaciones = notas,
            extra_data = new
            {
                id_operacion = idOp,
                linea_ubicacion = linea,
                tipo_contacto = tipoContacto,
                tiempo_analisis = tiempoAnalisis,
                tiempo_analisis_valor = tiempoValor,
                tiempo_analisis_unidad = tiempoUnidad,
                cantidad_eventos = cantEventos,
                voltaje_maximo = voltMax,
                temperatura = temp,
                humedad = hum,
                type = "event_meter"
            },
            measured_at = DateTime.Now.ToString("o")
        };

        var result = await _supabase.InsertMeasurementAsync(dataToInsert);
        return Ok(new { 
            success = result != null, 
            estatus = statusResult, 
            voltaje_maximo = voltMax, 
            is_out_of_limit = (statusResult == "FAIL" || voltMax > 100.0),
            data = result 
        });
    }

    [HttpPut("event-meter/{id}")]
    public async Task<IActionResult> UpdateEventMeterLog(string id, [FromBody] JsonObject payload)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos de Administrador para modificar registros de Event Meter." });
        }

        string linea = payload["linea_ubicacion"]?.ToString() ?? "SMT-01";
        string idOp = payload["id_operacion"]?.ToString() ?? "OP-01";
        string tipoContacto = payload["tipo_contacto"]?.ToString() ?? "Maquinaria";
        double tiempoValor = payload["tiempo_analisis_valor"] != null ? (payload["tiempo_analisis_valor"]?.GetValue<double>() ?? 30) : 30;
        string tiempoUnidad = payload["tiempo_analisis_unidad"]?.ToString() ?? "min";
        string tiempoAnalisis = payload["tiempo_analisis"]?.ToString() ?? $"{tiempoValor} {tiempoUnidad}";
        int cantEventos = payload["cantidad_eventos"]?.GetValue<int>() ?? 0;
        double voltMax = payload["voltaje_maximo"]?.GetValue<double>() ?? 0;
        double? temp = payload["temperatura"] != null ? payload["temperatura"]?.GetValue<double>() : 23.5;
        double? hum = payload["humedad"] != null ? payload["humedad"]?.GetValue<double>() : 45.0;
        string notas = payload["notas"]?.ToString() ?? "";
        string siteId = HttpContext.Session.GetString("site_id") ?? payload["site_id"]?.ToString() ?? DefaultSiteId;

        string assetId = await GetOrCreateAssetIdAsync(idOp, siteId, "Event Meter", linea);
        string estatus = AuditEvaluationEngine.EvaluateEventMeter(voltMax);
        string statusResult = (estatus == "APROBADO" && voltMax <= 100.0) ? "PASS" : "FAIL";

        var dataToUpdate = new
        {
            asset_id = assetId,
            static_field_value = voltMax,
            status_result = statusResult,
            observaciones = notas,
            extra_data = new
            {
                id_operacion = idOp,
                linea_ubicacion = linea,
                tipo_contacto = tipoContacto,
                tiempo_analisis = tiempoAnalisis,
                tiempo_analisis_valor = tiempoValor,
                tiempo_analisis_unidad = tiempoUnidad,
                cantidad_eventos = cantEventos,
                voltaje_maximo = voltMax,
                temperatura = temp,
                humedad = hum,
                type = "event_meter"
            }
        };

        bool ok = await _supabase.UpdateEventMeterLogAsync(id, dataToUpdate);
        return Ok(new { success = ok, estatus });
    }

    [HttpDelete("event-meter/{id}")]
    public async Task<IActionResult> DeleteEventMeterLog(string id)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos de Administrador para eliminar registros de Event Meter." });
        }

        bool ok = await _supabase.DeleteEventMeterLogAsync(id);
        return Ok(new { success = ok });
    }

    [HttpGet("line-assets")]
    public async Task<IActionResult> GetLineAssets([FromQuery] string? line = null, [FromQuery] string? siteId = null)
    {
        string targetSiteId = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var allAssets = await _supabase.GetAssetsAsync(targetSiteId);
        var measurements = await _supabase.GetMeasurementsForSiteAsync(targetSiteId);
        
        var assetMap = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        // 1. Index assets from assets table
        foreach (var node in allAssets)
        {
            if (node is JsonObject aObj)
            {
                string customId = aObj["custom_id"]?.ToString() ?? aObj["asset_id"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(customId)) continue;
                string loc = aObj["location"]?.ToString() ?? aObj["area_line"]?.ToString() ?? "";
                string cat = aObj["category"]?.ToString() ?? aObj["element_type"]?.ToString() ?? "Mobiliario ESD";
                string subcat = aObj["sub_category"]?.ToString() ?? aObj["element_subtype"]?.ToString() ?? cat;

                assetMap[customId] = new JsonObject
                {
                    ["id"] = aObj["id"]?.ToString(),
                    ["asset_id"] = customId,
                    ["element_type"] = cat,
                    ["element_subtype"] = subcat,
                    ["area_line"] = loc
                };
            }
        }

        // 2. Index assets recorded via floor audits/measurements if missing or update location
        foreach (var node in measurements)
        {
            if (node is not JsonObject mObj) continue;
            if (mObj["extra_data"] is not JsonObject ed) continue;

            string idElem = ed["id_elemento"]?.ToString() ?? ed["id_operacion"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(idElem)) continue;

            string loc = ed["ubicacion"]?.ToString() ?? ed["linea_ubicacion"]?.ToString() ?? "";
            string cat = ed["tipo_equipo"]?.ToString() ?? ed["tipo_contacto"]?.ToString() ?? "Mobiliario ESD";
            string subcat = ed["subtipo_elemento"]?.ToString() ?? ed["subtipo_key"]?.ToString() ?? cat;

            if (!assetMap.TryGetValue(idElem, out var existing))
            {
                assetMap[idElem] = new JsonObject
                {
                    ["id"] = mObj["asset_id"]?.ToString() ?? idElem,
                    ["asset_id"] = idElem,
                    ["element_type"] = cat,
                    ["element_subtype"] = subcat,
                    ["area_line"] = loc
                };
            }
            else if (string.IsNullOrEmpty(existing["area_line"]?.ToString()) || existing["area_line"]?.ToString() == "N/A")
            {
                existing["area_line"] = loc;
            }
        }

        // 3. Filter by selected line/area
        var filtered = assetMap.Values.Where(a =>
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            string area = a["area_line"]?.ToString() ?? "";
            string cleanLine = line.Trim();
            return string.Equals(area, cleanLine, StringComparison.OrdinalIgnoreCase) ||
                   area.StartsWith(cleanLine + " ->", StringComparison.OrdinalIgnoreCase) ||
                   area.Contains(cleanLine, StringComparison.OrdinalIgnoreCase);
        }).OrderBy(x => x["asset_id"]?.ToString());

        return Ok(filtered);
    }

    [HttpPost("evaluate-resistance")]
    public IActionResult EvaluateResistance([FromBody] JsonObject payload)
    {
        string category = payload["category"]?.ToString() ?? "";
        string valStr = payload["value"]?.ToString() ?? "";

        double? parsed = ResistanceParser.ParseResistance(valStr);
        if (parsed == null)
        {
            return Ok(new { valid = false, message = "Valor inválido" });
        }

        string status = ResistanceParser.EvaluateStatus(category, parsed.Value);
        EsdConstants.InfoElementosEsd.TryGetValue(category, out var info);

        return Ok(new
        {
            valid = true,
            parsedValue = parsed.Value,
            formattedValue = $"{parsed.Value:E2} Ω",
            status,
            limit = info?.Limite ?? "N/A",
            method = info?.Metodo ?? "N/A"
        });
    }

    // --- INFRASTRUCTURE EPA ---
    [HttpGet("infra/grounding")]
    public async Task<IActionResult> GetGroundingLogs([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var data = await _supabase.GetGroundingLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/grounding")]
    public async Task<IActionResult> AddGroundingLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null || string.IsNullOrWhiteSpace(payload["site_id"]?.ToString()))
            payload["site_id"] = CurrentUserSiteId;
        if (payload["auditor_id"] == null || string.IsNullOrWhiteSpace(payload["auditor_id"]?.ToString()))
            payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;
        if (payload["measured_at"] == null)
            payload["measured_at"] = DateTime.UtcNow.ToString("o");
        
        double ohms = payload["resistance_ohms"]?.GetValue<double>() ?? 0;
        string type = payload["point_type"]?.ToString() ?? "";
        double limit = type.Contains("Auxiliary") ? 25.0 : 2.0;
        payload["status_result"] = ohms <= limit ? "PASS" : "FAIL";

        var result = await _supabase.InsertGroundingLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("infra/floors")]
    public async Task<IActionResult> GetFloorLogs([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var data = await _supabase.GetFloorValidationLogsAsync(targetSite);
        var resultList = new JsonArray();

        // 1. Add records from Supabase
        if (data != null)
        {
            foreach (var item in data)
            {
                if (item != null) resultList.Add(item.DeepClone());
            }
        }

        // 2. If Supabase is empty or to complement with map point records
        if (resultList.Count == 0)
        {
            var maps = await _mapStorage.GetMapsAsync(targetSite);
            foreach (var m in maps)
            {
                if (m.Points == null) continue;
                foreach (var pt in m.Points.Where(p => p.LastResistanceOhms.HasValue))
                {
                    double ohms = pt.LastResistanceOhms!.Value;
                    resultList.Add(new JsonObject
                    {
                        ["id"] = pt.Id,
                        ["site_id"] = m.SiteId,
                        ["room_name"] = m.AreaName,
                        ["location"] = m.AreaName,
                        ["point_number"] = int.TryParse(pt.Code, out int pn) ? pn : 1,
                        ["point_id"] = pt.Label ?? $"Punto {pt.Code}",
                        ["ptp_resistance"] = ohms < 1000 ? $"{ohms:F1}" : ohms.ToString("E2"),
                        ["resistance_ohms"] = ohms,
                        ["temp_hum"] = "23.5°C / 45%",
                        ["status_result"] = ohms <= 1.0e9 ? "PASS" : "FAIL",
                        ["measured_at"] = (pt.MeasuredAt ?? DateTime.UtcNow).ToString("o")
                    });
                }
            }
        }

        return Ok(resultList);
    }

    [HttpPost("infra/floors")]
    public async Task<IActionResult> AddFloorLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null || string.IsNullOrWhiteSpace(payload["site_id"]?.ToString()))
            payload["site_id"] = CurrentUserSiteId;
        if (payload["auditor_id"] == null || string.IsNullOrWhiteSpace(payload["auditor_id"]?.ToString()))
            payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;
        if (payload["measured_at"] == null)
            payload["measured_at"] = DateTime.UtcNow.ToString("o");

        double ohms = payload["resistance_ohms"]?.GetValue<double>() ?? 0;
        payload["status_result"] = ohms <= 1.0e9 ? "PASS" : "FAIL";

        var result = await _supabase.InsertFloorValidationLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("infra/isolated")]
    public async Task<IActionResult> GetIsolatedLogs([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var data = await _supabase.GetIsolatedConductorsLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/isolated")]
    public async Task<IActionResult> AddIsolatedLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null || string.IsNullOrWhiteSpace(payload["site_id"]?.ToString()))
            payload["site_id"] = CurrentUserSiteId;
        if (payload["auditor_id"] == null || string.IsNullOrWhiteSpace(payload["auditor_id"]?.ToString()))
            payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;
        if (payload["measured_at"] == null)
            payload["measured_at"] = DateTime.UtcNow.ToString("o");

        double volts = payload["max_voltage"]?.GetValue<double>() ?? 0;
        payload["status_result"] = volts <= 35.0 ? "PASS" : "FAIL";

        var result = await _supabase.InsertIsolatedConductorsLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("infra/checkers")]
    public async Task<IActionResult> GetCheckersLogs([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var data = await _supabase.GetEntranceCheckersLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/checkers")]
    public async Task<IActionResult> AddCheckersLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null || string.IsNullOrWhiteSpace(payload["site_id"]?.ToString()))
            payload["site_id"] = CurrentUserSiteId;
        if (payload["auditor_id"] == null || string.IsNullOrWhiteSpace(payload["auditor_id"]?.ToString()))
            payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;
        if (payload["measured_at"] == null)
            payload["measured_at"] = DateTime.UtcNow.ToString("o");

        double refLeft = payload["reference_left"]?.GetValue<double>() ?? 0;
        double readLeft = payload["reading_left"]?.GetValue<double>() ?? 0;
        double refRight = payload["reference_right"]?.GetValue<double>() ?? 0;
        double readRight = payload["reading_right"]?.GetValue<double>() ?? 0;

        double devLeft = Math.Abs(readLeft - refLeft);
        double devRight = Math.Abs(readRight - refRight);

        payload["deviation_left"] = devLeft;
        payload["deviation_right"] = devRight;
        double maxAllowedDev = 1e9 * 0.05;
        payload["status_result"] = (devLeft <= maxAllowedDev && devRight <= maxAllowedDev) ? "PASS" : "FAIL";

        var result = await _supabase.InsertEntranceCheckersLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    // --- SCHEDULE & OFFICIAL LINE REPORTS ---
    [HttpPost("schedule/generate-line-report")]
    public async Task<IActionResult> GenerateLineReport([FromBody] JsonObject payload)
    {
        string linea = payload["linea"]?.ToString() ?? "Línea 1";
        string auditor = payload["auditor"]?.ToString() ?? HttpContext.Session.GetString("user_name") ?? "Auditor ESD";
        string comentarios = payload["comentarios"]?.ToString() ?? "Cumple con las normativas ANSI/ESD S20.20.";
        var rows = payload["rows"] as JsonArray ?? new JsonArray();

        // 1. Log report entry
        var logResult = await _supabase.InsertLogReportesLineaAsync(new
        {
            linea_ubicacion = linea,
            auditor = auditor,
            comentarios = comentarios
        });

        int dbId = 1;
        if (logResult != null && logResult["id"] != null)
        {
            int.TryParse(logResult["id"]!.ToString(), out dbId);
        }

        // 2. Generate HTML Certificate
        var (html, year) = LineReportGenerator.GenerateLineReportHtml(linea, rows, auditor, comentarios, dbId);
        string folio = $"BCS-LV-{dbId:D3}-{year}";

        return Ok(new { success = true, html, folio });
    }

    // --- ASSET DIRECTORY (INVENTORY & MEASUREMENT HISTORY) ---
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventoryDirectory([FromQuery] string? siteId = null, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] string? status = null)
    {
        string activeSiteId = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        
        var assets = await _supabase.GetAssetsAsync(activeSiteId);
        var measurements = await _supabase.GetMeasurementsForSiteAsync(activeSiteId);
        
        var assetMap = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        // 1. Populate from assets table
        foreach (var node in assets)
        {
            if (node is JsonObject aObj)
            {
                string customId = aObj["custom_id"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(customId)) continue;

                assetMap[customId] = new JsonObject
                {
                    ["id"] = aObj["id"]?.ToString(),
                    ["asset_id"] = customId,
                    ["custom_id"] = customId,
                    ["category"] = aObj["category"]?.ToString() ?? "Mobiliario ESD",
                    ["sub_category"] = aObj["sub_category"]?.ToString() ?? aObj["category"]?.ToString() ?? "Mobiliario ESD",
                    ["location"] = aObj["location"]?.ToString() ?? "N/A",
                    ["status"] = aObj["status"]?.ToString() ?? "ACTIVE",
                    ["last_verification"] = null,
                    ["next_verification"] = null,
                    ["auditor"] = "Auditor ESD",
                    ["punto_contacto"] = "",
                    ["resistance_value"] = null,
                    ["static_field_value"] = null,
                    ["extra_points"] = new JsonArray(),
                    ["total_audits"] = 0
                };
            }
        }

        // 2. Merge measurements and index assets created directly in audits
        foreach (var node in measurements)
        {
            if (node is not JsonObject mObj) continue;

            string idElem = "";
            if (mObj["extra_data"] is JsonObject ed)
            {
                idElem = ed["id_elemento"]?.ToString().Trim() ?? "";
            }

            if (string.IsNullOrEmpty(idElem) && mObj["asset_id"] != null)
            {
                string aId = mObj["asset_id"]?.ToString() ?? "";
                var matched = assetMap.Values.FirstOrDefault(x => x["id"]?.ToString() == aId);
                if (matched != null) idElem = matched["custom_id"]?.ToString() ?? "";
            }

            if (string.IsNullOrEmpty(idElem)) continue;

            if (!assetMap.TryGetValue(idElem, out var entry))
            {
                entry = new JsonObject
                {
                    ["id"] = mObj["asset_id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    ["asset_id"] = idElem,
                    ["custom_id"] = idElem,
                    ["category"] = "Mobiliario ESD",
                    ["sub_category"] = "Mobiliario ESD",
                    ["location"] = "N/A",
                    ["status"] = "ACTIVE",
                    ["last_verification"] = null,
                    ["next_verification"] = null,
                    ["auditor"] = "Auditor ESD",
                    ["punto_contacto"] = "",
                    ["resistance_value"] = null,
                    ["static_field_value"] = null,
                    ["extra_points"] = new JsonArray(),
                    ["total_audits"] = 0
                };
                assetMap[idElem] = entry;
            }

            int count = entry["total_audits"]?.GetValue<int>() ?? 0;
            entry["total_audits"] = count + 1;

            string measuredAt = mObj["measured_at"]?.ToString() ?? "";
            string existingLast = entry["last_verification"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(existingLast) || string.Compare(measuredAt, existingLast) > 0)
            {
                entry["last_verification"] = measuredAt;
                
                if (DateTime.TryParse(measuredAt, out var dt))
                {
                    entry["next_verification"] = dt.AddDays(30).ToString("yyyy-MM-dd");
                }

                entry["status"] = mObj["status_result"]?.ToString() ?? "PASS";
                
                if (mObj["resistance_value"] != null) entry["resistance_value"] = mObj["resistance_value"]?.GetValue<double>();
                if (mObj["static_field_value"] != null) entry["static_field_value"] = mObj["static_field_value"]?.GetValue<double>();

                if (mObj["extra_data"] is JsonObject edObj)
                {
                    if (edObj["tipo_equipo"] != null) entry["category"] = edObj["tipo_equipo"]?.ToString();
                    if (edObj["subtipo_elemento"] != null) entry["sub_category"] = edObj["subtipo_elemento"]?.ToString();
                    if (edObj["subtipo_key"] != null) entry["subtipo_key"] = edObj["subtipo_key"]?.ToString();
                    if (edObj["ubicacion"] != null) entry["location"] = edObj["ubicacion"]?.ToString();
                    if (edObj["punto_contacto"] != null) entry["punto_contacto"] = edObj["punto_contacto"]?.ToString();
                    if (edObj["auditor"] != null) entry["auditor"] = edObj["auditor"]?.ToString();
                    if (edObj["tiempo_descarga"] != null) entry["tiempo_descarga"] = edObj["tiempo_descarga"]?.GetValue<double>();
                    if (edObj["voltaje_balance"] != null) entry["voltaje_balance"] = edObj["voltaje_balance"]?.GetValue<int>();
                    if (edObj["mediciones_extra"] is JsonArray extraArr) entry["extra_points"] = extraArr.DeepClone();
                }
            }
        }

        var resultList = assetMap.Values.ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string sLower = search.Trim().ToLower();
            resultList = resultList.Where(x => 
                (x["asset_id"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["category"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["sub_category"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["location"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["punto_contacto"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["auditor"]?.ToString().ToLower().Contains(sLower) ?? false)
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
        {
            resultList = resultList.Where(x => x["category"]?.ToString().Equals(category, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            resultList = resultList.Where(x => x["status"]?.ToString().Equals(status, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
        }

        return Ok(resultList.OrderByDescending(x => x["last_verification"]?.ToString() ?? ""));
    }

    [HttpGet("inventory/history/{id}")]
    public async Task<IActionResult> GetAssetAuditHistory([FromRoute] string id)
    {
        try
        {
            var history = await _supabase.GetAssetHistoryAsync(id);
            return Ok(history);
        }
        catch
        {
            return Ok(new JsonArray());
        }
    }

    // --- SENSITIVITY LAB ---
    [HttpGet("lab/catalog")]
    public async Task<IActionResult> GetLabCatalog()
    {
        var data = await _supabase.GetCatalogoSensibilidadAsync();
        return Ok(data);
    }

    [HttpPost("lab/catalog")]
    public async Task<IActionResult> AddLabCatalog([FromBody] JsonObject payload)
    {
        var result = await _supabase.InsertCatalogoSensibilidadAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("lab/components")]
    public async Task<IActionResult> GetLabComponents([FromQuery] string idProducto)
    {
        var data = await _supabase.GetComponentesSensibilidadAsync(idProducto);
        return Ok(data);
    }

    [HttpPost("lab/components")]
    public async Task<IActionResult> AddLabComponent([FromBody] JsonObject payload)
    {
        var result = await _supabase.InsertComponenteSensibilidadAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    // --- PRODUCT ROUTES ---
    [HttpGet("routes/products")]
    public async Task<IActionResult> GetProductRoutes([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetCatalogoProductosAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("routes/products")]
    public async Task<IActionResult> AddProductRoute([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null)
        {
            payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        }
        var result = await _supabase.InsertCatalogoProductoAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpPost("routes/update-sequence")]
    public async Task<IActionResult> UpdateProductSequence([FromBody] JsonObject payload)
    {
        string nombre = payload["nombre_producto"]?.ToString() ?? "";
        string siteId = payload["site_id"]?.ToString() ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var lineas = payload["lineas_asociadas"];

        bool success = await _supabase.UpdateCatalogoProductoRutaAsync(nombre, siteId, lineas!);
        return Ok(new { success });
    }

    [HttpGet("lines")]
    [HttpGet("routes/lines")]
    public async Task<IActionResult> GetLines([FromQuery] string? siteId)
    {
        await BackfillLinesCompanyIdAsync();
        var data = await _supabase.GetCatalogoLineasAsync(siteId);
        return Ok(data);
    }

    [HttpPost("lines")]
    [HttpPost("routes/lines")]
    public async Task<IActionResult> AddLine([FromBody] JsonObject payload)
    {
        string siteId = payload["site_id"]?.ToString() ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        string nombreLinea = payload["nombre_linea"]?.ToString()?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(nombreLinea)) return BadRequest(new { success = false, message = "El nombre de la área/línea es obligatorio." });

        payload["site_id"] = siteId;

        // Resolve company_id automatically from payload, session, or site mapping
        string? companyId = payload["company_id"]?.ToString();
        if (string.IsNullOrEmpty(companyId))
        {
            companyId = HttpContext.Session.GetString("company_id");
            if (string.IsNullOrEmpty(companyId) && !string.IsNullOrEmpty(siteId))
            {
                var sites = await _supabase.GetSitesAsync();
                foreach (var s in sites)
                {
                    if (s is JsonObject sObj && sObj["id"]?.ToString() == siteId)
                    {
                        companyId = sObj["company_id"]?.ToString();
                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(companyId))
        {
            payload["company_id"] = companyId;
        }

        // Duplicate check in site
        var existingLines = await _supabase.GetCatalogoLineasAsync(siteId);
        foreach (var l in existingLines)
        {
            if (l is JsonObject lObj)
            {
                string existingName = lObj["nombre_linea"]?.ToString()?.Trim() ?? "";
                if (string.Equals(existingName, nombreLinea, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = $"Ya existe el área/línea '{nombreLinea}' en este site." });
                }
            }
        }

        var result = await _supabase.InsertCatalogoLineaAsync(payload);
        return Ok(new { success = result != null && result["id"] != null, data = result });
    }

    private async Task BackfillLinesCompanyIdAsync()
    {
        try
        {
            var allLines = await _supabase.GetCatalogoLineasAsync(null);
            bool hasNullCompany = false;
            foreach (var l in allLines)
            {
                if (l is JsonObject lObj && string.IsNullOrEmpty(lObj["company_id"]?.ToString()))
                {
                    hasNullCompany = true;
                    break;
                }
            }

            if (!hasNullCompany) return;

            var sites = await _supabase.GetSitesAsync();
            var siteCompanyMap = new Dictionary<string, string>();
            foreach (var s in sites)
            {
                if (s is JsonObject sObj)
                {
                    string id = sObj["id"]?.ToString() ?? "";
                    string compId = sObj["company_id"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(compId))
                    {
                        siteCompanyMap[id] = compId;
                    }
                }
            }

            foreach (var l in allLines)
            {
                if (l is JsonObject lObj)
                {
                    string lineId = lObj["id"]?.ToString() ?? "";
                    string lSiteId = lObj["site_id"]?.ToString() ?? "";
                    string lCompId = lObj["company_id"]?.ToString() ?? "";

                    if (string.IsNullOrEmpty(lCompId) && !string.IsNullOrEmpty(lSiteId) && siteCompanyMap.TryGetValue(lSiteId, out var targetCompId))
                    {
                        await _supabase.UpdateCatalogoLineaAsync(lineId, new { company_id = targetCompId });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error backfilling catalogo_lineas company_id: {ex.Message}");
        }
    }

    // --- EMPLOYEES & TRAINING EXAMS ---
    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetEmpleadosBatasAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("employees")]
    public async Task<IActionResult> SaveEmployee([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null)
        {
            payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        }
        var result = await _supabase.InsertOrUpdateEmpleadoAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("training/history")]
    public async Task<IActionResult> GetTrainingHistory()
    {
        var data = await _supabase.GetEntrenamientosEsdAsync();
        return Ok(data);
    }

    [HttpPost("training/submit-exam")]
    public async Task<IActionResult> SubmitExam([FromBody] JsonObject payload)
    {
        string q1 = payload["q1"]?.ToString() ?? "";
        string q2 = payload["q2"]?.ToString() ?? "";
        string q3 = payload["q3"]?.ToString() ?? "";
        string numEmp = payload["num_empleado"]?.ToString() ?? "";
        string nomEmp = payload["nombre_empleado"]?.ToString() ?? "";

        int aciertos = 0;
        if (q1.Contains("3.5 x 10^7") || q1.Contains("3.5x10^7")) aciertos++;
        if (q2.Contains("30 cm")) aciertos++;
        if (q3.Contains("Neutralizar cargas")) aciertos++;

        double score = Math.Round((aciertos / 3.0) * 100.0, 1);
        bool passed = score >= 80.0;

        var examData = new
        {
            num_empleado = numEmp.Trim().ToUpper(),
            nombre_empleado = nomEmp.Trim(),
            fecha_entrenamiento = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            calificacion_total = score,
            detalle_respuestas = new { q1, q2, q3, score, passed }
        };

        var result = await _supabase.InsertEntrenamientoEsdAsync(examData);
        return Ok(new { success = result != null, score, passed, data = result });
    }

    // --- MEASUREMENT EQUIPMENT ---
    [HttpGet("equipment")]
    public async Task<IActionResult> GetEquipment([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? "";
        if (!IsSuperAdmin && !IsCompanyAdmin)
        {
            targetSite = CurrentUserSiteId;
        }

        var data = await _supabase.GetCatalogoEquiposAsync(string.IsNullOrEmpty(targetSite) ? null : targetSite);
        return Ok(data);
    }

    [HttpPost("equipment")]
    public async Task<IActionResult> AddEquipment([FromBody] JsonObject payload)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para registrar equipos de medición. Requiere rol SiteAdmin o superior." });
        }

        string siteId = payload["site_id"]?.ToString() ?? CurrentUserSiteId;
        if (!IsSuperAdmin && !IsCompanyAdmin)
        {
            siteId = CurrentUserSiteId;
        }
        payload["site_id"] = siteId;

        string code = payload["codigo_equipo"]?.ToString()?.Trim() ?? "";
        string name = payload["nombre_equipo"]?.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { success = false, message = "El código y el nombre del equipo son obligatorios." });
        }

        // Calculate calibration status based on fecha_proxima_calibracion
        string nextDateStr = payload["fecha_proxima_calibracion"]?.ToString() ?? "";
        string estatus = "VIGENTE";
        if (DateTime.TryParse(nextDateStr, out DateTime nextDate))
        {
            double daysLeft = (nextDate.Date - DateTime.Now.Date).TotalDays;
            if (daysLeft < 0) estatus = "VENCIDO";
            else if (daysLeft <= 30) estatus = "PROXIMO_VENCER";
            else estatus = "VIGENTE";
        }
        payload["estatus"] = estatus;

        var result = await _supabase.InsertCatalogoEquipoAsync(payload);
        return Ok(new { success = result != null && result["id"] != null, data = result });
    }

    [HttpDelete("equipment/{id}")]
    public async Task<IActionResult> DeleteEquipment(string id)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para eliminar equipos de medición." });
        }
        bool success = await _supabase.DeleteCatalogoEquipoAsync(id);
        return Ok(new { success });
    }

    // --- SETTINGS, TENANTS & USERS ---
    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var allCompanies = await _supabase.GetCompaniesAsync();
        if (IsSuperAdmin) return Ok(allCompanies);

        var filtered = new JsonArray();
        foreach (var c in allCompanies)
        {
            if (c is JsonObject cObj && cObj["id"]?.ToString() == CurrentUserCompanyId)
            {
                filtered.Add(cObj);
            }
        }
        return Ok(filtered);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> AddCompany([FromBody] JsonObject payload)
    {
        if (!IsSuperAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para crear empresas. Requiere rol SuperAdmin." });
        }

        string name = payload["name"]?.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { success = false, message = "El nombre de la empresa es obligatorio." });
        
        payload.Remove("code");

        // 1. Duplicate check (case-insensitive)
        var existing = await _supabase.GetCompaniesAsync();
        foreach (var c in existing)
        {
            if (c is JsonObject cObj)
            {
                string existingName = cObj["name"]?.ToString()?.Trim() ?? "";
                if (string.Equals(existingName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = $"Ya existe una empresa registrada con el nombre '{name}'." });
                }
            }
        }

        var result = await _supabase.InsertCompanyAsync(payload);
        if (result != null && result["id"] == null)
        {
            string errStr = result["message"]?.ToString() ?? result["details"]?.ToString() ?? "Error al insertar en Supabase.";
            return BadRequest(new { success = false, message = errStr });
        }

        return Ok(new { success = result != null && result["id"] != null, data = result });
    }

    [HttpGet("sites")]
    public async Task<IActionResult> GetSites([FromQuery] string? companyId)
    {
        if (IsSuperAdmin)
        {
            var data = await _supabase.GetSitesAsync(companyId);
            return Ok(data);
        }

        if (IsCompanyAdmin)
        {
            var data = await _supabase.GetSitesAsync(CurrentUserCompanyId);
            return Ok(data);
        }

        // SiteAdmin / Auditor / Viewer: Return ONLY assigned site
        var allSites = await _supabase.GetSitesAsync(CurrentUserCompanyId);
        var singleSiteList = new JsonArray();
        foreach (var s in allSites)
        {
            if (s is JsonObject sObj && sObj["id"]?.ToString() == CurrentUserSiteId)
            {
                singleSiteList.Add(sObj);
            }
        }
        return Ok(singleSiteList);
    }

    [HttpPost("sites")]
    public async Task<IActionResult> AddSite([FromBody] JsonObject payload)
    {
        if (!IsCompanyAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para crear sites/plantas. Requiere rol CompanyAdmin o SuperAdmin." });
        }

        if (!IsSuperAdmin)
        {
            // Force site to belong to CompanyAdmin's own company
            payload["company_id"] = CurrentUserCompanyId;
        }

        string name = payload["name"]?.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { success = false, message = "El nombre del site es obligatorio." });

        var result = await _supabase.InsertSiteAsync(payload);
        return Ok(new { success = result != null && result["id"] != null, data = result });
    }

    [HttpGet("hierarchy")]
    public async Task<IActionResult> GetHierarchy()
    {
        var companies = await _supabase.GetCompaniesAsync();
        var sites = await _supabase.GetSitesAsync();
        var allDbLines = await _supabase.GetCatalogoLineasAsync();

        var tree = new JsonArray();
        foreach (var c in companies)
        {
            if (c is JsonObject cObj)
            {
                string companyId = cObj["id"]?.ToString() ?? "";

                // Non-SuperAdmin CANNOT view other companies!
                if (!IsSuperAdmin && companyId != CurrentUserCompanyId) continue;

                var companyCopy = cObj.DeepClone() as JsonObject ?? new JsonObject();
                var companySites = new JsonArray();

                foreach (var s in sites)
                {
                    if (s is JsonObject sObj && sObj["company_id"]?.ToString() == companyId)
                    {
                        string siteId = sObj["id"]?.ToString() ?? "";

                        // SiteAdmin / Auditor / Viewer CANNOT view other sites!
                        if (!IsCompanyAdmin && siteId != CurrentUserSiteId) continue;

                        var siteCopy = sObj.DeepClone() as JsonObject ?? new JsonObject();
                        var siteLocations = new JsonArray();
                        var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var lineItem in allDbLines)
                        {
                            if (lineItem is JsonObject lineObj && lineObj["site_id"]?.ToString() == siteId)
                            {
                                string lName = lineObj["nombre_linea"]?.ToString()?.Trim() ?? "";
                                if (!string.IsNullOrEmpty(lName) && seenLocations.Add(lName))
                                {
                                    siteLocations.Add(lName);
                                }
                            }
                        }

                        siteCopy["locations"] = siteLocations;
                        companySites.Add(siteCopy);
                    }
                }

                companyCopy["sites"] = companySites;
                tree.Add(companyCopy);
            }
        }

        return Ok(tree);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? companyId, [FromQuery] string? siteId)
    {
        if (IsSuperAdmin)
        {
            var data = await _supabase.GetUsersAsync(companyId, siteId);
            return Ok(data);
        }

        if (IsCompanyAdmin)
        {
            var data = await _supabase.GetUsersAsync(CurrentUserCompanyId, null);
            return Ok(data);
        }

        if (IsSiteAdmin)
        {
            var data = await _supabase.GetUsersAsync(null, CurrentUserSiteId);
            return Ok(data);
        }

        return StatusCode(403, new { success = false, message = "No tienes permisos para listar usuarios." });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] JsonObject payload)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para registrar usuarios. Se requiere rol SiteAdmin o superior." });
        }

        string targetRole = payload["role"]?.ToString() ?? "Auditor";

        if (!IsSuperAdmin)
        {
            // CompanyAdmin and SiteAdmin can ONLY create users for their assigned company
            payload["company_id"] = CurrentUserCompanyId;

            if (!IsCompanyAdmin)
            {
                // SiteAdmin can ONLY create users for their assigned site
                payload["site_id"] = CurrentUserSiteId;

                if (string.Equals(targetRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetRole, "CompanyAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "SiteAdmin solo puede crear usuarios con rol SiteAdmin, Auditor o Viewer." });
                }
            }
            else
            {
                if (string.Equals(targetRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "CompanyAdmin no puede otorgar el rol SuperAdmin." });
                }
            }
        }

        string pwd = payload["password"]?.ToString() ?? "123456";
        payload["password_hash"] = PasswordHasher.HashPassword(pwd);
        payload.Remove("password");

        var result = await _supabase.InsertUserAsync(payload);
        return Ok(new { success = result != null && result["id"] != null, data = result });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para eliminar usuarios." });
        }
        bool success = await _supabase.DeleteUserAsync(id);
        return Ok(new { success });
    }

    // --- ESD FLOOR MAPS & HEATMAP ENGINE ---
    [HttpGet("maps")]
    public async Task<IActionResult> GetFloorMaps([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var maps = await _mapStorage.GetMapsAsync(targetSite);
        return Ok(maps);
    }

    [HttpGet("maps/{id}")]
    public async Task<IActionResult> GetFloorMapById(string id)
    {
        var map = await _mapStorage.GetMapByIdAsync(id);
        if (map == null) return NotFound(new { success = false, message = "Mapa no encontrado" });
        return Ok(map);
    }

    [HttpPost("maps/upload")]
    public async Task<IActionResult> SaveFloorMap([FromBody] JsonObject payload)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para configurar mapas de planta." });
        }

        string mapId = payload["id"]?.ToString() ?? Guid.NewGuid().ToString();
        string siteId = payload["siteId"]?.ToString() ?? CurrentUserSiteId;
        string areaName = payload["areaName"]?.ToString() ?? "Área General";
        string mapName = payload["mapName"]?.ToString() ?? $"Plano {areaName}";
        string imageBase64 = payload["imageBase64"]?.ToString() ?? "";
        string imageUrl = payload["imageUrl"]?.ToString() ?? "";
        double totalArea = payload["totalAreaValue"]?.GetValue<double>() ?? 500.0;
        string unit = payload["areaUnit"]?.ToString() ?? "m2";

        if (!string.IsNullOrEmpty(imageBase64))
        {
            string savedUrl = await _mapStorage.SaveImageFromBase64Async(mapName, imageBase64);
            if (!string.IsNullOrEmpty(savedUrl))
            {
                imageUrl = savedUrl;
            }
        }

        if (string.IsNullOrEmpty(imageUrl))
        {
            imageUrl = "/images/mockups/smt1_layout.svg";
        }

        var points = new List<FloorMapPoint>();
        if (payload["points"] is JsonArray pArr)
        {
            foreach (var pNode in pArr)
            {
                if (pNode is JsonObject pObj)
                {
                    points.Add(new FloorMapPoint
                    {
                        Id = pObj["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                        Code = pObj["code"]?.ToString() ?? (points.Count + 1).ToString(),
                        Label = pObj["label"]?.ToString() ?? $"Punto {points.Count + 1}",
                        XPercent = pObj["xPercent"]?.GetValue<double>() ?? 0,
                        YPercent = pObj["yPercent"]?.GetValue<double>() ?? 0,
                        LastResistanceOhms = pObj["lastResistanceOhms"] != null ? pObj["lastResistanceOhms"]!.GetValue<double>() : null
                    });
                }
            }
        }

        var mapConfig = new FloorMapConfig
        {
            Id = mapId,
            SiteId = siteId,
            AreaName = areaName,
            AreaId = areaName,
            MapName = mapName,
            ImageUrl = imageUrl,
            TotalAreaValue = totalArea,
            AreaUnit = unit,
            Points = points
        };

        var saved = await _mapStorage.SaveMapAsync(mapConfig);
        return Ok(new { success = true, map = saved });
    }

    [HttpPost("maps/points")]
    public async Task<IActionResult> SaveFloorMapPoints([FromBody] SaveMapPointsDto dto)
    {
        if (string.IsNullOrEmpty(dto.MapId)) return BadRequest(new { success = false, message = "ID de mapa requerido" });
        bool ok = await _mapStorage.SaveMapPointsAsync(dto.MapId, dto.Points);
        return Ok(new { success = ok });
    }

    [HttpDelete("maps/{id}")]
    public async Task<IActionResult> DeleteFloorMap(string id)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para eliminar mapas." });
        }
        bool ok = await _mapStorage.DeleteMapAsync(id);
        return Ok(new { success = ok });
    }

    [HttpPost("maps/measurements")]
    public async Task<IActionResult> SaveFloorMeasurementsBatch([FromBody] SaveFloorMeasurementBatchDto dto)
    {
        if (string.IsNullOrEmpty(dto.MapId)) return BadRequest(new { success = false, message = "ID de mapa requerido" });

        var map = await _mapStorage.GetMapByIdAsync(dto.MapId);
        if (map == null) return NotFound(new { success = false, message = "Mapa no encontrado" });

        string siteId = !string.IsNullOrEmpty(dto.SiteId) ? dto.SiteId : map.SiteId;
        string auditorId = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;
        string areaName = !string.IsNullOrEmpty(dto.AreaName) ? dto.AreaName : map.AreaName;

        // 1. Update map points in storage
        foreach (var p in dto.Points)
        {
            var match = map.Points.FirstOrDefault(mp => mp.Id.Equals(p.Id, StringComparison.OrdinalIgnoreCase) || mp.Code.Equals(p.Code, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.LastResistanceOhms = p.LastResistanceOhms;
                match.MeasuredAt = DateTime.UtcNow;
            }
            else
            {
                p.MeasuredAt = DateTime.UtcNow;
                map.Points.Add(p);
            }

            // 2. Also register individual log in Supabase floor_validation_logs
            if (p.LastResistanceOhms.HasValue)
            {
                double ohms = p.LastResistanceOhms.Value;
                var logPayload = new JsonObject
                {
                    ["site_id"] = siteId,
                    ["auditor_id"] = auditorId,
                    ["room_name"] = areaName,
                    ["location"] = areaName,
                    ["point_number"] = int.TryParse(p.Code, out int pNum) ? pNum : 1,
                    ["point_id"] = p.Label ?? $"Punto {p.Code}",
                    ["ptp_resistance"] = ohms.ToString("E2"),
                    ["resistance_ohms"] = ohms,
                    ["temp_hum"] = $"{dto.Temperature}°C / {dto.Humidity}%",
                    ["status_result"] = ohms <= 1.0e9 ? "PASS" : "FAIL",
                    ["measured_at"] = DateTime.UtcNow.ToString("o")
                };

                await _supabase.InsertFloorValidationLogAsync(logPayload);
            }
        }

        await _mapStorage.SaveMapAsync(map);

        return Ok(new { success = true, map });
    }
}
