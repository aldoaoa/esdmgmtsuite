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

    private readonly SupabaseService _supabase;
    private readonly FloorMapStorageService _mapStorage;
    private readonly IWebHostEnvironment _env;

    public ApiController(SupabaseService supabase, FloorMapStorageService mapStorage, IWebHostEnvironment env)
    {
        _supabase = supabase;
        _mapStorage = mapStorage;
        _env = env;
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
            lang = HttpContext.Session.GetString("lang") ?? Request.Cookies["esd360_lang"] ?? "es",
            report_lang = HttpContext.Session.GetString("report_lang") ?? Request.Cookies["esd360_report_lang"] ?? HttpContext.Session.GetString("lang") ?? "es",
            version = EsdConstants.SystemVersion
        });
    }

    [HttpPost("set-site")]
    public async Task<IActionResult> SetActiveSite([FromBody] JsonObject payload)
    {
        string targetSiteId = payload["site_id"]?.ToString() ?? "";
        string targetSiteName = payload["site_name"]?.ToString() ?? "";

        if (string.IsNullOrEmpty(targetSiteId)) return BadRequest(new { success = false, message = "Site ID requerido." });

        // Resolve company information for the target site
        string? targetCompanyId = null;
        string? targetCompanyName = null;
        try
        {
            var allSites = await _supabase.GetSitesAsync();
            var matchedSite = allSites.FirstOrDefault(s => s is JsonObject sObj && string.Equals(sObj["id"]?.ToString(), targetSiteId, StringComparison.OrdinalIgnoreCase)) as JsonObject;
            if (matchedSite != null)
            {
                targetCompanyId = matchedSite["company_id"]?.ToString();
                if (string.IsNullOrEmpty(targetSiteName) && matchedSite["name"] != null)
                {
                    targetSiteName = matchedSite["name"]!.ToString();
                }

                if (!string.IsNullOrEmpty(targetCompanyId))
                {
                    var comp = await _supabase.GetCompanyByIdAsync(targetCompanyId);
                    if (comp != null && comp["name"] != null)
                    {
                        targetCompanyName = comp["name"]!.ToString();
                    }
                }
            }
        }
        catch { }

        if (IsSuperAdmin)
        {
            HttpContext.Session.SetString("site_id", targetSiteId);
            if (!string.IsNullOrEmpty(targetSiteName)) HttpContext.Session.SetString("site_name", targetSiteName);
            if (!string.IsNullOrEmpty(targetCompanyId)) HttpContext.Session.SetString("company_id", targetCompanyId);
            if (!string.IsNullOrEmpty(targetCompanyName)) HttpContext.Session.SetString("company_name", targetCompanyName);
            return Ok(new { success = true });
        }

        if (IsCompanyAdmin)
        {
            var allowedSites = await _supabase.GetSitesAsync(CurrentUserCompanyId);
            bool isAllowed = false;
            foreach (var s in allowedSites)
            {
                if (s is JsonObject sObj && string.Equals(sObj["id"]?.ToString(), targetSiteId, StringComparison.OrdinalIgnoreCase))
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
            if (!string.IsNullOrEmpty(targetCompanyId)) HttpContext.Session.SetString("company_id", targetCompanyId);
            if (!string.IsNullOrEmpty(targetCompanyName)) HttpContext.Session.SetString("company_name", targetCompanyName);
            return Ok(new { success = true });
        }

        return StatusCode(403, new { success = false, message = "Tu rol de usuario está asignado exclusivamente a tu planta y no permite cambiar de site." });
    }

    [HttpPost("set-lang")]
    public IActionResult SetLanguage([FromBody] JsonObject payload)
    {
        string lang = payload["lang"]?.ToString() ?? "es";
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

    [HttpPost("set-report-lang")]
    public IActionResult SetReportLanguage([FromBody] JsonObject payload)
    {
        string lang = payload["lang"]?.ToString() ?? "es";
        HttpContext.Session.SetString("report_lang", lang);
        Response.Cookies.Append("esd360_report_lang", lang, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return Ok(new { success = true, report_lang = lang });
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
                        ["ptp_resistance"] = FormatScientific(ohms),
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
        
        var resultList = new JsonArray();
        foreach (var item in data)
        {
            if (item is JsonObject obj)
            {
                var clone = JsonNode.Parse(obj.ToJsonString())?.AsObject() ?? new JsonObject();
                string commentsStr = obj["comments"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(commentsStr) && commentsStr.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        var parsed = JsonNode.Parse(commentsStr)?.AsObject();
                        if (parsed != null)
                        {
                            if (parsed["points"] != null) clone["points"] = parsed["points"]?.DeepClone();
                            if (parsed["status_result"] != null) clone["status_result"] = parsed["status_result"]?.ToString();
                            if (parsed["asset_id"] != null) clone["asset_id"] = parsed["asset_id"]?.ToString();
                            if (parsed["notes"] != null) clone["notes"] = parsed["notes"]?.ToString();
                        }
                    }
                    catch { }
                }
                
                if (clone["status_result"] == null)
                {
                    double v = clone["max_voltage"]?.GetValue<double>() ?? 0;
                    clone["status_result"] = (v >= -35.0 && v <= 35.0) ? "PASS" : "FAIL";
                }
                
                resultList.Add(clone);
            }
        }
        return Ok(resultList);
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

        double maxAbsVolt = 0;
        double maxSignedVolt = 0;
        bool hasFail = false;
        
        var pointsArr = payload["points"] as JsonArray;
        if (pointsArr != null && pointsArr.Count > 0)
        {
            for (int i = 0; i < pointsArr.Count; i++)
            {
                if (pointsArr[i] is JsonObject pObj)
                {
                    double v = pObj["voltage"]?.GetValue<double>() ?? 0;
                    if (Math.Abs(v) >= maxAbsVolt || i == 0)
                    {
                        maxAbsVolt = Math.Abs(v);
                        maxSignedVolt = v;
                    }
                    bool pPass = v >= -35.0 && v <= 35.0;
                    pObj["status"] = pPass ? "PASS" : "FAIL";
                    if (!pPass) hasFail = true;
                }
            }
            payload["max_voltage"] = maxSignedVolt;
        }
        else
        {
            double v = payload["max_voltage"]?.GetValue<double>() ?? 0;
            hasFail = v < -35.0 || v > 35.0;
            payload["max_voltage"] = v;
        }

        string overallStatus = hasFail ? "FAIL" : "PASS";
        payload["status_result"] = overallStatus;

        var structuredComments = new JsonObject
        {
            ["notes"] = payload["notes"]?.ToString() ?? payload["comments"]?.ToString() ?? "",
            ["status_result"] = overallStatus,
            ["asset_id"] = payload["asset_id"]?.ToString() ?? payload["operation_id"]?.ToString() ?? "",
            ["points"] = pointsArr?.DeepClone() ?? new JsonArray()
        };
        payload["comments"] = structuredComments.ToJsonString();

        var insertPayload = new JsonObject
        {
            ["site_id"] = payload["site_id"]?.ToString(),
            ["auditor_id"] = payload["auditor_id"]?.ToString(),
            ["location"] = payload["location"]?.ToString() ?? "General",
            ["operation_id"] = payload["operation_id"]?.ToString() ?? "ISO-01",
            ["max_voltage"] = payload["max_voltage"]?.GetValue<double>() ?? 0,
            ["comments"] = payload["comments"]?.ToString(),
            ["measured_at"] = payload["measured_at"]?.ToString()
        };

        var result = await _supabase.InsertIsolatedConductorsLogAsync(insertPayload);
        return Ok(new { success = result != null, status_result = overallStatus, max_voltage = payload["max_voltage"]?.GetValue<double>() ?? 0, data = result });
    }

    [HttpPost("infra/isolated/upload-photo")]
    public async Task<IActionResult> UploadIsolatedPhoto([FromBody] JsonObject payload)
    {
        string base64 = payload["base64"]?.ToString() ?? "";
        string fileName = payload["filename"]?.ToString() ?? $"iso_{DateTime.UtcNow.Ticks}.jpg";
        if (string.IsNullOrEmpty(base64)) return BadRequest(new { success = false, message = "No image data" });

        int commaIdx = base64.IndexOf(',');
        if (commaIdx >= 0) base64 = base64.Substring(commaIdx + 1);

        byte[] bytes = Convert.FromBase64String(base64);
        string? url = await _supabase.UploadStorageFileAsync("evidence", $"isolated/{fileName}", bytes, "image/jpeg");
        if (string.IsNullOrEmpty(url))
        {
            string uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "isolated");
            Directory.CreateDirectory(uploadsDir);
            string localPath = Path.Combine(uploadsDir, fileName);
            await System.IO.File.WriteAllBytesAsync(localPath, bytes);
            url = $"/uploads/isolated/{fileName}";
        }
        return Ok(new { success = true, url = url });
    }

    [HttpGet("infra/checkers")]
    public async Task<IActionResult> GetCheckersLogs([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var data = await _supabase.GetEntranceCheckersLogsAsync(targetSite);
        if (data != null)
        {
            foreach (var item in data)
            {
                if (item is JsonObject obj)
                {
                    string rawObs = obj["observations"]?.ToString() ?? "";
                    if (rawObs.StartsWith("{") && rawObs.EndsWith("}"))
                    {
                        try
                        {
                            var parsed = JsonNode.Parse(rawObs) as JsonObject;
                            if (parsed != null)
                            {
                                if (parsed["evidence_url"] != null) obj["evidence_url"] = parsed["evidence_url"]?.ToString();
                                if (parsed["equipment_id"] != null) obj["equipment_id"] = parsed["equipment_id"]?.ToString();
                                if (parsed["equipment_code"] != null) obj["equipment_code"] = parsed["equipment_code"]?.ToString();
                                if (parsed["equipment_name"] != null) obj["equipment_name"] = parsed["equipment_name"]?.ToString();
                                if (parsed["location"] != null) obj["location"] = parsed["location"]?.ToString();
                                if (parsed["auditor_name"] != null) obj["auditor_name"] = parsed["auditor_name"]?.ToString();
                                if (parsed["notes"] != null) obj["observations"] = parsed["notes"]?.ToString();
                            }
                        }
                        catch { }
                    }
                }
            }
        }
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

        double minRange = payload["range_min"]?.GetValue<double>() ?? 1e3;
        double maxRange = payload["range_max"]?.GetValue<double>() ?? 1e12;
        if (minRange <= 0) minRange = 1e3;
        if (maxRange <= minRange) maxRange = 1e12;

        double totalDecades = Math.Log10(maxRange) - Math.Log10(minRange);
        if (totalDecades <= 0) totalDecades = 9.0;

        double devLeftPct = 0;
        if (readLeft > 0 && refLeft > 0)
        {
            double logDiff = Math.Abs(Math.Log10(readLeft) - Math.Log10(refLeft));
            devLeftPct = (logDiff / totalDecades) * 100.0;
        }

        double devRightPct = 0;
        if (readRight > 0 && refRight > 0)
        {
            double logDiff = Math.Abs(Math.Log10(readRight) - Math.Log10(refRight));
            devRightPct = (logDiff / totalDecades) * 100.0;
        }

        bool isPass = devLeftPct <= 5.0 && devRightPct <= 5.0;
        string status = isPass ? "PASS" : "FAIL";

        string rawObs = payload["observations"]?.ToString() ?? payload["notes"]?.ToString() ?? "";
        string evidenceUrl = payload["evidence_url"]?.ToString() ?? "";
        string equipmentId = payload["equipment_id"]?.ToString() ?? "";
        string equipmentName = payload["equipment_name"]?.ToString() ?? "";
        string location = payload["location"]?.ToString() ?? "";
        string auditorName = payload["auditor_name"]?.ToString() ?? HttpContext.Session.GetString("user_name") ?? "Auditor";

        if (!string.IsNullOrEmpty(evidenceUrl) && evidenceUrl.StartsWith("data:image/"))
        {
            try
            {
                int commaIdx = evidenceUrl.IndexOf(',');
                if (commaIdx >= 0)
                {
                    string header = evidenceUrl.Substring(0, commaIdx);
                    string base64Data = evidenceUrl.Substring(commaIdx + 1);
                    string ext = header.Contains("png") ? ".png" : (header.Contains("webp") ? ".webp" : ".jpg");
                    string contentType = header.Contains("png") ? "image/png" : (header.Contains("webp") ? "image/webp" : "image/jpeg");
                    byte[] bytes = Convert.FromBase64String(base64Data);
                    string targetSite = payload["site_id"]?.ToString() ?? CurrentUserSiteId;
                    string safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
                    string storagePath = $"{targetSite}/{safeName}";
                    
                    var (upSuccess, stKey, upMsg) = await _supabase.UploadStorageObjectAsync("audit-evidence", storagePath, bytes, contentType);
                    if (upSuccess)
                    {
                        evidenceUrl = $"/api/evidence/{targetSite}/{safeName}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BASE64 UPLOAD EXCEPTION]: {ex.Message}");
            }
        }

        JsonObject obsJson = new JsonObject
        {
            ["notes"] = rawObs,
            ["evidence_url"] = evidenceUrl,
            ["equipment_id"] = equipmentId,
            ["equipment_name"] = equipmentName,
            ["location"] = location,
            ["auditor_name"] = auditorName
        };

        var insertPayload = new JsonObject
        {
            ["site_id"] = payload["site_id"]?.ToString(),
            ["auditor_id"] = payload["auditor_id"]?.ToString(),
            ["checker_id"] = payload["checker_id"]?.ToString() ?? "CHECKER-01",
            ["reference_left"] = refLeft,
            ["reading_left"] = readLeft,
            ["deviation_left"] = Math.Round(devLeftPct, 2),
            ["reference_right"] = refRight,
            ["reading_right"] = readRight,
            ["deviation_right"] = Math.Round(devRightPct, 2),
            ["status_result"] = status,
            ["observations"] = obsJson.ToJsonString(),
            ["measured_at"] = payload["measured_at"]?.ToString()
        };

        var result = await _supabase.InsertEntranceCheckersLogAsync(insertPayload);
        return Ok(new { success = result != null, status_result = status, deviation_left = Math.Round(devLeftPct, 2), deviation_right = Math.Round(devRightPct, 2), data = result });
    }

    // --- SCHEDULE & OFFICIAL LINE REPORTS ---
    [HttpPost("schedule/generate-line-report")]
    public async Task<IActionResult> GenerateLineReport([FromBody] JsonObject payload)
    {
        string linea = payload["linea"]?.ToString()?.Trim() ?? "Línea 1";
        string auditor = payload["auditor"]?.ToString()?.Trim() ?? HttpContext.Session.GetString("user_name") ?? "Auditor ESD";
        string comentarios = payload["comentarios"]?.ToString()?.Trim() ?? "";
        
        string activeSiteId = payload["site_id"]?.ToString() ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        string activeCompanyId = payload["company_id"]?.ToString() ?? HttpContext.Session.GetString("company_id") ?? "";
        
        // 1. Resolve Company & Site Names
        string companyName = HttpContext.Session.GetString("company_name") ?? "BCS Automotive Interface Solutions";
        string siteName = HttpContext.Session.GetString("site_name") ?? "Queretaro Plant";
        string? logoUrl = null;

        if (!string.IsNullOrEmpty(activeCompanyId))
        {
            var compObj = await _supabase.GetCompanyByIdAsync(activeCompanyId);
            if (compObj != null)
            {
                companyName = compObj["name"]?.ToString() ?? companyName;
                logoUrl = compObj["logo_url"]?.ToString();
            }
        }

        // Check local branding cache if logoUrl not in DB
        if (string.IsNullOrEmpty(logoUrl) && !string.IsNullOrEmpty(activeCompanyId))
        {
            logoUrl = GetLocalCompanyLogo(activeCompanyId);
        }

        // If SuperAdmin without company branding, default to esd360 logo
        if (string.IsNullOrEmpty(logoUrl) && IsSuperAdmin && string.IsNullOrEmpty(activeCompanyId))
        {
            logoUrl = "/images/esd360-logo.png";
        }

        // 2. Generate High-Entropy Unique ID with Temporal Component & Backend Retry Loop
        string compCode = GetAbbreviation(companyName, 3, "BCS");
        string siteCode = GetAbbreviation(siteName, 3, "QRO");
        string yearMonth = DateTime.UtcNow.ToString("yyMM");
        
        string uniqueFolio;
        int attempts = 0;
        do
        {
            string hexId = Guid.NewGuid().ToString("N")[..8].ToUpper();
            uniqueFolio = $"{compCode}-{siteCode}-LV-{yearMonth}-{hexId}";
            attempts++;
        } while (IsFolioTaken(uniqueFolio, activeSiteId) && attempts < 20);

        // 3. Populate Rows with Live Asset Data & Most Recent Measurements
        var rows = payload["rows"] as JsonArray ?? new JsonArray();
        if (rows.Count == 0)
        {
            try
            {
                var siteAssets = await GetEnrichedSiteAssetsAsync(activeSiteId);
                string cleanLine = linea.Trim();

                var matchingAssets = siteAssets.Where(a => {
                    string loc = a["location"]?.ToString() ?? "";
                    if (string.Equals(loc, cleanLine, StringComparison.OrdinalIgnoreCase)) return true;
                    if (loc.IndexOf(cleanLine, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (cleanLine.IndexOf(loc, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                    string locLeaf = loc.Contains("->") ? loc.Split("->").Last().Trim() : loc;
                    string lineLeaf = cleanLine.Contains("->") ? cleanLine.Split("->").Last().Trim() : cleanLine;
                    if (string.Equals(locLeaf, lineLeaf, StringComparison.OrdinalIgnoreCase)) return true;
                    if (locLeaf.IndexOf(lineLeaf, StringComparison.OrdinalIgnoreCase) >= 0 || lineLeaf.IndexOf(locLeaf, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                    return false;
                }).ToList();

                // If specific line had no direct match, include all assets if requested or general
                if (matchingAssets.Count == 0 && (cleanLine.Equals("ALL", StringComparison.OrdinalIgnoreCase) || cleanLine.Contains("General") || cleanLine.Contains("Planta")))
                {
                    matchingAssets = siteAssets;
                }

                foreach (var a in matchingAssets)
                {
                    rows.Add(a.DeepClone());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error auto-populating line assets with live measurements: {ex.Message}");
            }
        }

        // 4. Generate HTML Report with full measurement details, language localization and corporate layout
        string reportLang = payload["report_lang"]?.ToString() ?? payload["lang"]?.ToString() ?? HttpContext.Session.GetString("report_lang") ?? Request.Cookies["esd360_report_lang"] ?? HttpContext.Session.GetString("lang") ?? Request.Cookies["esd360_lang"] ?? "es";

        string html = LineReportGenerator.GenerateLineReportHtml(
            linea,
            rows,
            auditor,
            comentarios,
            uniqueFolio,
            companyName,
            siteName,
            logoUrl,
            DateTime.UtcNow,
            reportLang
        );

        // 5. Save local cache AND upload Report HTML to Supabase Storage
        string storagePath = $"reports/{compCode}_{siteCode}/{uniqueFolio}.html";
        string downloadUrl = $"/api/schedule/reports/{uniqueFolio}/view";
        try
        {
            // Save local cache file
            string reportsDir = Path.Combine(_env.WebRootPath, "uploads", "reports");
            Directory.CreateDirectory(reportsDir);
            string localPath = Path.Combine(reportsDir, $"{uniqueFolio}.html");
            await System.IO.File.WriteAllTextAsync(localPath, html, System.Text.Encoding.UTF8);

            // Upload to Supabase Storage bucket 'audit-evidence'
            var uploadBytes = System.Text.Encoding.UTF8.GetBytes(html);
            var (uploadOk, key, msg) = await _supabase.UploadStorageObjectAsync("audit-evidence", storagePath, uploadBytes, "text/html");
            if (uploadOk)
            {
                downloadUrl = $"/storage/v1/object/authenticated/audit-evidence/{storagePath}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving or uploading report: {ex.Message}");
        }

        // 6. Log report entry into log_reportes_linea
        var logResult = await _supabase.InsertLogReportesLineaAsync(new
        {
            linea_ubicacion = linea,
            auditor = auditor,
            comentarios = comentarios
        });

        // 7. Save structured record in reports index
        SaveReportToIndex(new JsonObject
        {
            ["folio"] = uniqueFolio,
            ["report_type"] = "LINE_VALIDATION",
            ["type_name"] = "Validación de Línea (LV)",
            ["linea"] = linea,
            ["auditor"] = auditor,
            ["comentarios"] = comentarios,
            ["company_id"] = activeCompanyId,
            ["company_name"] = companyName,
            ["site_id"] = activeSiteId,
            ["site_name"] = siteName,
            ["created_at"] = DateTime.UtcNow.ToString("o"),
            ["storage_path"] = storagePath,
            ["download_url"] = downloadUrl,
            ["total_assets"] = rows.Count
        });

        return Ok(new { success = true, folio = uniqueFolio, html, download_url = downloadUrl });
    }

    [HttpGet("schedule/reports")]
    public async Task<IActionResult> GetScheduleReports([FromQuery] string? siteId = null, [FromQuery] string? search = null, [FromQuery] string? line = null, [FromQuery] string? reportType = null)
    {
        string activeSiteId = !string.IsNullOrEmpty(siteId) ? siteId : (HttpContext.Session.GetString("site_id") ?? DefaultSiteId);
        string? activeCompanyId = HttpContext.Session.GetString("company_id");

        // Multitenancy validation:
        if (!IsSuperAdmin)
        {
            if (IsCompanyAdmin)
            {
                var allowedSites = await _supabase.GetSitesAsync(CurrentUserCompanyId);
                bool allowed = allowedSites.Any(s => s is JsonObject sObj && string.Equals(sObj["id"]?.ToString(), activeSiteId, StringComparison.OrdinalIgnoreCase));
                if (!allowed)
                {
                    return StatusCode(403, new { success = false, message = "Acceso no autorizado a este site." });
                }
            }
            else
            {
                string userSiteId = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
                if (!string.Equals(activeSiteId, userSiteId, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { success = false, message = "Acceso no autorizado a este site." });
                }
            }
        }

        var reports = GetReportsFromIndex(activeSiteId, search, line, reportType, activeCompanyId);
        return Ok(new { success = true, reports });
    }

    [HttpGet("schedule/reports/{folio}/view")]
    public async Task<IActionResult> ViewScheduleReport(string folio)
    {
        string safeFolio = Path.GetFileName(folio).Replace("..", "").Trim();
        string localPath = Path.Combine(_env.WebRootPath, "uploads", "reports", $"{safeFolio}.html");
        
        string activeSiteId = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;

        // 1. Lookup report record in reports_history.json to verify site / tenant authorization
        JsonObject? reportRecord = null;
        try
        {
            string histPath = Path.Combine(_env.WebRootPath, "data", "reports_history.json");
            if (System.IO.File.Exists(histPath))
            {
                var list = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(histPath)) as JsonArray;
                reportRecord = list?.FirstOrDefault(x => string.Equals(x?["folio"]?.ToString(), safeFolio, StringComparison.OrdinalIgnoreCase)) as JsonObject;
            }
        }
        catch { }

        // Multitenancy access validation:
        if (reportRecord != null)
        {
            string rSiteId = reportRecord["site_id"]?.ToString() ?? "";
            string rCompanyId = reportRecord["company_id"]?.ToString() ?? "";

            if (!IsSuperAdmin)
            {
                if (IsCompanyAdmin)
                {
                    if (!string.IsNullOrEmpty(rCompanyId) && !string.Equals(rCompanyId, CurrentUserCompanyId, StringComparison.OrdinalIgnoreCase))
                    {
                        return StatusCode(403, "Acceso no autorizado al reporte de otra empresa.");
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(rSiteId) && !string.Equals(rSiteId, activeSiteId, StringComparison.OrdinalIgnoreCase))
                    {
                        return StatusCode(403, "Acceso no autorizado al reporte de otra planta/site.");
                    }
                }
            }
        }

        // 2. Check local file cache first
        if (System.IO.File.Exists(localPath))
        {
            string html = await System.IO.File.ReadAllTextAsync(localPath, System.Text.Encoding.UTF8);
            return Content(html, "text/html");
        }

        // 3. Download from Supabase Storage bucket 'audit-evidence' using explicit storagePath
        string? storagePath = reportRecord?["storage_path"]?.ToString();
        if (!string.IsNullOrEmpty(storagePath))
        {
            var (ok, stream, ct, msg) = await _supabase.DownloadStorageObjectAsync("audit-evidence", storagePath);
            if (ok && stream != null)
            {
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                string html = await reader.ReadToEndAsync();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                    await System.IO.File.WriteAllTextAsync(localPath, html, System.Text.Encoding.UTF8);
                }
                catch { }
                return Content(html, "text/html");
            }
        }

        // 4. Try candidate paths in Supabase Storage
        var candidatePaths = new[]
        {
            $"reports/BCS_QUE/{safeFolio}.html",
            $"reports/BCS_QRO/{safeFolio}.html",
            $"reports/{safeFolio}.html"
        };
        foreach (var cPath in candidatePaths)
        {
            var (ok, stream, ct, msg) = await _supabase.DownloadStorageObjectAsync("audit-evidence", cPath);
            if (ok && stream != null)
            {
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                string html = await reader.ReadToEndAsync();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                    await System.IO.File.WriteAllTextAsync(localPath, html, System.Text.Encoding.UTF8);
                }
                catch { }
                return Content(html, "text/html");
            }
        }

        return NotFound("Reporte no encontrado.");
    }

    // --- ASSET DIRECTORY & MEASUREMENT CONSOLIDATION HELPER ---
    private async Task<List<JsonObject>> GetEnrichedSiteAssetsAsync(string activeSiteId)
    {
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

                string rawClass = aObj["classification"]?.ToString() ?? "";
                string assetName = customId;
                string subtype = aObj["category"]?.ToString() ?? "Mobiliario ESD";
                string assetArea = "";
                string periodicity = "1m";
                string notes = "";

                if (rawClass.StartsWith("{") && rawClass.EndsWith("}"))
                {
                    try
                    {
                        var classObj = JsonNode.Parse(rawClass) as JsonObject;
                        if (classObj != null)
                        {
                            if (classObj["name"] != null && !string.IsNullOrWhiteSpace(classObj["name"]?.ToString())) assetName = classObj["name"]?.ToString()!;
                            if (classObj["subtype"] != null && !string.IsNullOrWhiteSpace(classObj["subtype"]?.ToString())) subtype = classObj["subtype"]?.ToString()!;
                            if (classObj["area"] != null && !string.IsNullOrWhiteSpace(classObj["area"]?.ToString())) assetArea = classObj["area"]?.ToString()!;
                            if (classObj["periodicity"] != null && !string.IsNullOrWhiteSpace(classObj["periodicity"]?.ToString())) periodicity = classObj["periodicity"]?.ToString()!;
                            if (classObj["notes"] != null) notes = classObj["notes"]?.ToString() ?? "";
                        }
                    }
                    catch { }
                }
                else if (!string.IsNullOrEmpty(rawClass))
                {
                    subtype = rawClass;
                }

                string loc = aObj["location"]?.ToString() ?? "N/A";
                if (string.IsNullOrEmpty(assetArea) && loc.Contains("->"))
                {
                    assetArea = loc.Split("->")[0].Trim();
                }

                assetMap[customId] = new JsonObject
                {
                    ["id"] = aObj["id"]?.ToString(),
                    ["asset_id"] = customId,
                    ["custom_id"] = customId,
                    ["name"] = string.IsNullOrEmpty(assetName) ? customId : assetName,
                    ["category"] = aObj["category"]?.ToString() ?? "Mobiliario ESD",
                    ["sub_category"] = subtype,
                    ["location"] = loc,
                    ["area"] = string.IsNullOrEmpty(assetArea) ? "General" : assetArea,
                    ["periodicity"] = periodicity,
                    ["notes"] = notes,
                    ["created_at"] = aObj["created_at"]?.ToString() ?? DateTime.UtcNow.ToString("o"),
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
                    string per = entry["periodicity"]?.ToString() ?? "1m";
                    var dtNext = CalculateNextDueDate(dt, per);
                    entry["next_verification"] = dtNext.HasValue ? dtNext.Value.ToString("yyyy-MM-dd") : "Permanente";
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

        return assetMap.Values.ToList();
    }

    // --- ASSET DIRECTORY (INVENTORY & MEASUREMENT HISTORY) ---
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventoryDirectory([FromQuery] string? siteId = null, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] string? status = null)
    {
        string activeSiteId = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var resultList = await GetEnrichedSiteAssetsAsync(activeSiteId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string sLower = search.Trim().ToLower();
            resultList = resultList.Where(x => 
                (x["asset_id"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["name"]?.ToString().ToLower().Contains(sLower) ?? false) ||
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
            if (status.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase) || status.Equals("BAJA", StringComparison.OrdinalIgnoreCase))
            {
                resultList = resultList.Where(x => {
                    string st = x["status"]?.ToString() ?? "";
                    return st.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase) || st.Equals("BAJA", StringComparison.OrdinalIgnoreCase) || st.Equals("DECOMMISSIONED", StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }
            else if (status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                resultList = resultList.Where(x => {
                    string st = x["status"]?.ToString() ?? "";
                    return !st.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase) && !st.Equals("BAJA", StringComparison.OrdinalIgnoreCase) && !st.Equals("DECOMMISSIONED", StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }
            else
            {
                resultList = resultList.Where(x => x["status"]?.ToString().Equals(status, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            }
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

    [HttpPost("inventory/assets")]
    public async Task<IActionResult> AddAsset([FromBody] JsonObject payload)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Sesión no válida." });
        }

        string customId = payload["custom_id"]?.ToString().Trim() ?? "";
        if (string.IsNullOrEmpty(customId))
        {
            return BadRequest(new { success = false, message = "El ID / Código del activo es obligatorio." });
        }

        string targetSite = payload["site_id"]?.ToString() ?? CurrentUserSiteId;
        string category = payload["category"]?.ToString() ?? "Mobiliario ESD";
        string subtype = payload["subtype"]?.ToString() ?? payload["sub_category"]?.ToString() ?? category;
        string name = payload["name"]?.ToString() ?? customId;
        string location = payload["location"]?.ToString() ?? "N/A";
        string area = payload["area"]?.ToString() ?? (location.Contains("->") ? location.Split("->")[0].Trim() : "General");
        string periodicity = payload["periodicity"]?.ToString() ?? "1m";
        string notes = payload["notes"]?.ToString() ?? "";
        string createdAt = payload["created_at"]?.ToString() ?? DateTime.UtcNow.ToString("o");

        JsonObject classObj = new JsonObject
        {
            ["name"] = name,
            ["subtype"] = subtype,
            ["area"] = area,
            ["periodicity"] = periodicity,
            ["notes"] = notes
        };

        var insertPayload = new JsonObject
        {
            ["site_id"] = targetSite,
            ["custom_id"] = customId,
            ["category"] = category,
            ["classification"] = classObj.ToJsonString(),
            ["location"] = location,
            ["status"] = "ACTIVE",
            ["created_at"] = createdAt
        };

        var result = await _supabase.InsertAssetAsync(insertPayload);

        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "AssetDirectory",
            $"Alta de nuevo activo '{customId}' ({name}) en ubicación '{location}' con periodicidad '{periodicity}'.",
            new { customId, name, category, subtype, location, area, periodicity, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        return Ok(new { success = result != null, data = result });
    }

    [HttpPut("inventory/assets/{id}")]
    public async Task<IActionResult> UpdateAsset(string id, [FromBody] JsonObject payload)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Sesión no válida." });
        }

        string targetSite = payload["site_id"]?.ToString() ?? CurrentUserSiteId;
        string customId = payload["custom_id"]?.ToString() ?? id;
        string category = payload["category"]?.ToString() ?? "Mobiliario ESD";
        string subtype = payload["subtype"]?.ToString() ?? payload["sub_category"]?.ToString() ?? category;
        string name = payload["name"]?.ToString() ?? customId;
        string location = payload["location"]?.ToString() ?? "N/A";
        string area = payload["area"]?.ToString() ?? (location.Contains("->") ? location.Split("->")[0].Trim() : "General");
        string periodicity = payload["periodicity"]?.ToString() ?? "1m";
        string notes = payload["notes"]?.ToString() ?? "";

        JsonObject classObj = new JsonObject
        {
            ["name"] = name,
            ["subtype"] = subtype,
            ["area"] = area,
            ["periodicity"] = periodicity,
            ["notes"] = notes
        };

        var updatePayload = new JsonObject
        {
            ["category"] = category,
            ["classification"] = classObj.ToJsonString(),
            ["location"] = location
        };

        if (payload["status"] != null) updatePayload["status"] = payload["status"]?.ToString();

        // Resolve ID: if id is custom_id or UUID
        string targetId = id;
        if (!Guid.TryParse(id, out _))
        {
            var siteAssets = await _supabase.GetAssetsAsync(targetSite);
            var match = siteAssets.FirstOrDefault(a => a is JsonObject aObj && string.Equals(aObj["custom_id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase)) as JsonObject;
            if (match != null && match["id"] != null)
            {
                targetId = match["id"]!.ToString();
            }
            else
            {
                // Create asset record if it originated from measurements without master asset row
                updatePayload["site_id"] = targetSite;
                updatePayload["custom_id"] = customId;
                updatePayload["status"] = "ACTIVE";
                updatePayload["created_at"] = DateTime.UtcNow.ToString("o");
                var ins = await _supabase.InsertAssetAsync(updatePayload);
                return Ok(new { success = ins != null });
            }
        }

        bool success = await _supabase.UpdateAssetAsync(targetId, updatePayload);

        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "AssetDirectory",
            $"Modificación de activo '{customId}' ({name}) en ubicación '{location}'.",
            new { id = targetId, customId, name, location, periodicity, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        return Ok(new { success });
    }

    [HttpPost("inventory/assets/{id}/decommission")]
    [HttpDelete("inventory/assets/{id}")]
    public async Task<IActionResult> DecommissionAsset(string id, [FromBody] JsonObject? payload = null)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Sesión no válida." });
        }

        string targetSite = payload?["site_id"]?.ToString() ?? CurrentUserSiteId;
        string targetId = id;
        string customId = id;

        if (!Guid.TryParse(id, out _))
        {
            var siteAssets = await _supabase.GetAssetsAsync(targetSite);
            var match = siteAssets.FirstOrDefault(a => a is JsonObject aObj && string.Equals(aObj["custom_id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase)) as JsonObject;
            if (match != null && match["id"] != null)
            {
                targetId = match["id"]!.ToString();
                customId = match["custom_id"]?.ToString() ?? id;
            }
        }

        // SOFT DELETE / BAJA: Keep the asset and all historical measurements intact, set status to INACTIVE
        var updatePayload = new JsonObject
        {
            ["status"] = "INACTIVE"
        };

        bool success = await _supabase.UpdateAssetAsync(targetId, updatePayload);

        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "AssetDirectory",
            $"Baja de control normativo para activo '{customId}'. Estatus actualizado a INACTIVE (Trazabilidad preservada).",
            new { id = targetId, customId, status = "INACTIVE", ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        return Ok(new { success, message = "Activo dado de baja correctamente. Su historial de auditoría permanece protegido." });
    }

    [HttpPost("inventory/assets/{id}/reactivate")]
    public async Task<IActionResult> ReactivateAsset(string id, [FromBody] JsonObject? payload = null)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Sesión no válida." });
        }

        string targetSite = payload?["site_id"]?.ToString() ?? CurrentUserSiteId;
        string targetId = id;
        string customId = id;

        if (!Guid.TryParse(id, out _))
        {
            var siteAssets = await _supabase.GetAssetsAsync(targetSite);
            var match = siteAssets.FirstOrDefault(a => a is JsonObject aObj && string.Equals(aObj["custom_id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase)) as JsonObject;
            if (match != null && match["id"] != null)
            {
                targetId = match["id"]!.ToString();
                customId = match["custom_id"]?.ToString() ?? id;
            }
        }

        var updatePayload = new JsonObject
        {
            ["status"] = "ACTIVE"
        };

        bool success = await _supabase.UpdateAssetAsync(targetId, updatePayload);

        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "AssetDirectory",
            $"Reactivación de activo '{customId}'. Estatus actualizado a ACTIVE.",
            new { id = targetId, customId, status = "ACTIVE", ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        return Ok(new { success, message = "Activo reactivado correctamente." });
    }

    // --- SCHEDULE VALIDATIONS & DUE DATES ---
    [HttpGet("schedule/assets-due")]
    public async Task<IActionResult> GetScheduleAssetsDue([FromQuery] string? siteId = null, [FromQuery] string? area = null, [FromQuery] string? line = null, [FromQuery] string? search = null, [FromQuery] string? status = null)
    {
        string activeSiteId = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        
        var assets = await _supabase.GetAssetsAsync(activeSiteId);
        var measurements = await _supabase.GetMeasurementsForSiteAsync(activeSiteId);
        
        var assetMap = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        // 1. Populate registered assets from assets table
        foreach (var node in assets)
        {
            if (node is JsonObject aObj)
            {
                string customId = aObj["custom_id"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(customId)) continue;

                string rawClass = aObj["classification"]?.ToString() ?? "";
                string assetName = customId;
                string subtype = aObj["category"]?.ToString() ?? "Mobiliario ESD";
                string assetArea = "";
                string periodicity = "1m";
                string notes = "";

                if (rawClass.StartsWith("{") && rawClass.EndsWith("}"))
                {
                    try
                    {
                        var classObj = JsonNode.Parse(rawClass) as JsonObject;
                        if (classObj != null)
                        {
                            if (classObj["name"] != null && !string.IsNullOrWhiteSpace(classObj["name"]?.ToString())) assetName = classObj["name"]?.ToString()!;
                            if (classObj["subtype"] != null && !string.IsNullOrWhiteSpace(classObj["subtype"]?.ToString())) subtype = classObj["subtype"]?.ToString()!;
                            if (classObj["area"] != null && !string.IsNullOrWhiteSpace(classObj["area"]?.ToString())) assetArea = classObj["area"]?.ToString()!;
                            if (classObj["periodicity"] != null && !string.IsNullOrWhiteSpace(classObj["periodicity"]?.ToString())) periodicity = classObj["periodicity"]?.ToString()!;
                            if (classObj["notes"] != null) notes = classObj["notes"]?.ToString() ?? "";
                        }
                    }
                    catch { }
                }
                else if (!string.IsNullOrEmpty(rawClass))
                {
                    subtype = rawClass;
                }

                string loc = aObj["location"]?.ToString() ?? "N/A";
                if (string.IsNullOrEmpty(assetArea) && loc.Contains("->"))
                {
                    assetArea = loc.Split("->")[0].Trim();
                }
                else if (string.IsNullOrEmpty(assetArea))
                {
                    assetArea = "General";
                }

                string createdAt = aObj["created_at"]?.ToString() ?? DateTime.UtcNow.ToString("o");

                assetMap[customId] = new JsonObject
                {
                    ["id"] = aObj["id"]?.ToString(),
                    ["asset_id"] = customId,
                    ["custom_id"] = customId,
                    ["name"] = string.IsNullOrEmpty(assetName) ? customId : assetName,
                    ["category"] = aObj["category"]?.ToString() ?? "Mobiliario ESD",
                    ["sub_category"] = subtype,
                    ["location"] = loc,
                    ["area"] = assetArea,
                    ["periodicity"] = periodicity,
                    ["notes"] = notes,
                    ["created_at"] = createdAt,
                    ["last_verification"] = null,
                    ["next_verification"] = null,
                    ["auditor"] = null,
                    ["status_result"] = "PENDING",
                    ["status_schedule"] = "PENDING",
                    ["days_left"] = 0,
                    ["overdue_days"] = 0,
                    ["resistance_value"] = null,
                    ["static_field_value"] = null,
                    ["extra_points"] = new JsonArray(),
                    ["total_audits"] = 0
                };
            }
        }

        // 2. Populate measurements and index audit assets
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
                string loc = mObj["ubicacion"]?.ToString() ?? (mObj["extra_data"] as JsonObject)?["ubicacion"]?.ToString() ?? "N/A";
                string assetArea = loc.Contains("->") ? loc.Split("->")[0].Trim() : "General";
                entry = new JsonObject
                {
                    ["id"] = mObj["asset_id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    ["asset_id"] = idElem,
                    ["custom_id"] = idElem,
                    ["name"] = idElem,
                    ["category"] = "Mobiliario ESD",
                    ["sub_category"] = "Mobiliario ESD",
                    ["location"] = loc,
                    ["area"] = assetArea,
                    ["periodicity"] = "1m",
                    ["notes"] = "",
                    ["created_at"] = mObj["measured_at"]?.ToString() ?? DateTime.UtcNow.ToString("o"),
                    ["last_verification"] = null,
                    ["next_verification"] = null,
                    ["auditor"] = null,
                    ["status_result"] = "PASS",
                    ["status_schedule"] = "PENDING",
                    ["days_left"] = 0,
                    ["overdue_days"] = 0,
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
                entry["status_result"] = mObj["status_result"]?.ToString() ?? "PASS";
                
                if (mObj["auditor_id"] != null) entry["auditor"] = mObj["auditor_id"]?.ToString();
                if (mObj["resistance_value"] != null) entry["resistance_value"] = mObj["resistance_value"]?.GetValue<double>();
                if (mObj["static_field_value"] != null) entry["static_field_value"] = mObj["static_field_value"]?.GetValue<double>();

                if (mObj["extra_data"] is JsonObject edObj)
                {
                    if (edObj["tipo_equipo"] != null) entry["category"] = edObj["tipo_equipo"]?.ToString();
                    if (edObj["subtipo_elemento"] != null) entry["sub_category"] = edObj["subtipo_elemento"]?.ToString();
                    if (edObj["ubicacion"] != null) entry["location"] = edObj["ubicacion"]?.ToString();
                    if (edObj["auditor"] != null) entry["auditor"] = edObj["auditor"]?.ToString();
                    if (edObj["tiempo_descarga"] != null) entry["tiempo_descarga"] = edObj["tiempo_descarga"]?.GetValue<double>();
                    if (edObj["voltaje_balance"] != null) entry["voltaje_balance"] = edObj["voltaje_balance"]?.GetValue<int>();
                    if (edObj["mediciones_extra"] is JsonArray extraArr) entry["extra_points"] = extraArr.DeepClone();
                }
            }
        }

        // 3. Compute Schedule, Next Dates, and Compliance Status
        var now = DateTime.UtcNow.Date;
        var computedList = new List<JsonObject>();
        var distinctAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int overdueCount = 0;
        int dueSoonCount = 0;
        int compliantCount = 0;
        int permanentCount = 0;

        foreach (var entry in assetMap.Values)
        {
            string loc = entry["location"]?.ToString() ?? "N/A";
            string aArea = entry["area"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(aArea))
            {
                aArea = loc.Contains("->") ? loc.Split("->")[0].Trim() : "General";
                entry["area"] = aArea;
            }

            if (!string.IsNullOrEmpty(aArea)) distinctAreas.Add(aArea);
            if (!string.IsNullOrEmpty(loc) && loc != "N/A") distinctLines.Add(loc);

            string periodicity = entry["periodicity"]?.ToString() ?? "1m";
            string lastDateStr = entry["last_verification"]?.ToString() ?? "";
            string baseDateStr = !string.IsNullOrEmpty(lastDateStr) ? lastDateStr : entry["created_at"]?.ToString() ?? "";

            DateTime baseDate = DateTime.TryParse(baseDateStr, out var bdt) ? bdt.Date : now;
            DateTime? nextDate = CalculateNextDueDate(baseDate, periodicity);

            if (periodicity == "permanent" || nextDate == null)
            {
                entry["next_verification"] = null;
                entry["status_schedule"] = "PERMANENT";
                entry["days_left"] = 9999;
                entry["overdue_days"] = 0;
                permanentCount++;
            }
            else
            {
                entry["next_verification"] = nextDate.Value.ToString("yyyy-MM-dd");
                int diffDays = (int)(nextDate.Value.Date - now).TotalDays;
                entry["days_left"] = diffDays;

                if (diffDays < 0)
                {
                    entry["status_schedule"] = "OVERDUE";
                    entry["overdue_days"] = Math.Abs(diffDays);
                    overdueCount++;
                }
                else if (diffDays <= 7)
                {
                    entry["status_schedule"] = "DUE_SOON";
                    entry["overdue_days"] = 0;
                    dueSoonCount++;
                }
                else
                {
                    entry["status_schedule"] = "COMPLIANT";
                    entry["overdue_days"] = 0;
                    compliantCount++;
                }
            }

            computedList.Add(entry);
        }

        // Apply filters
        var filteredList = computedList.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string sLower = search.Trim().ToLower();
            filteredList = filteredList.Where(x =>
                (x["name"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["asset_id"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["category"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["sub_category"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["location"]?.ToString().ToLower().Contains(sLower) ?? false) ||
                (x["area"]?.ToString().ToLower().Contains(sLower) ?? false)
            );
        }

        if (!string.IsNullOrWhiteSpace(area) && area != "all")
        {
            filteredList = filteredList.Where(x => x["area"]?.ToString().Equals(area, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (!string.IsNullOrWhiteSpace(line) && line != "all")
        {
            filteredList = filteredList.Where(x => x["location"]?.ToString().Equals(line, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            filteredList = filteredList.Where(x => x["status_schedule"]?.ToString().Equals(status, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        var results = filteredList.OrderBy(x => x["days_left"]?.GetValue<int>() ?? 0).ToList();

        return Ok(new
        {
            summary = new
            {
                total_assets = computedList.Count,
                overdue_count = overdueCount,
                due_soon_count = dueSoonCount,
                compliant_count = compliantCount,
                permanent_count = permanentCount
            },
            areas = distinctAreas.OrderBy(x => x).ToList(),
            lines = distinctLines.OrderBy(x => x).ToList(),
            assets = results,
            overdue_assets = computedList.Where(x => x["status_schedule"]?.ToString() == "OVERDUE").OrderByDescending(x => x["overdue_days"]?.GetValue<int>() ?? 0).Take(10).ToList(),
            due_soon_assets = computedList.Where(x => x["status_schedule"]?.ToString() == "DUE_SOON").OrderBy(x => x["days_left"]?.GetValue<int>() ?? 0).Take(10).ToList()
        });
    }

    private static DateTime? CalculateNextDueDate(DateTime baseDate, string periodicity)
    {
        return periodicity?.ToLowerInvariant() switch
        {
            "1d" or "daily" or "diario" => baseDate.AddDays(1),
            "1w" or "weekly" or "semanal" => baseDate.AddDays(7),
            "2w" or "biweekly" or "quincenal" => baseDate.AddDays(14),
            "1m" or "monthly" or "mensual" => baseDate.AddMonths(1),
            "3m" or "quarterly" or "trimestral" => baseDate.AddMonths(3),
            "6m" or "semiannual" or "semestral" => baseDate.AddMonths(6),
            "1y" or "annual" or "anual" => baseDate.AddYears(1),
            "permanent" or "permanente" => null,
            _ => baseDate.AddMonths(1)
        };
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

    // --- MEASUREMENT EQUIPMENT & SITE PHOTO POLICIES (SUPABASE STORAGE VAULT) ---
    [HttpGet("equipment")]
    [HttpGet("catalogo-equipos")]
    public async Task<IActionResult> GetEquipment([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? "";
        if (!IsSuperAdmin && !IsCompanyAdmin)
        {
            targetSite = CurrentUserSiteId;
        }

        var data = await _supabase.GetCatalogoEquiposAsync(string.IsNullOrEmpty(targetSite) ? null : targetSite);

        // Merge PDF calibration certificates if available
        string certMapFile = Path.Combine(_env.WebRootPath, "data", "equipment_certificates.json");
        JsonObject? certMap = null;
        if (System.IO.File.Exists(certMapFile))
        {
            try
            {
                string json = await System.IO.File.ReadAllTextAsync(certMapFile);
                certMap = JsonNode.Parse(json) as JsonObject;
            }
            catch { }
        }

        if (data != null && certMap != null)
        {
            foreach (var item in data)
            {
                if (item is JsonObject obj)
                {
                    string id = obj["id"]?.ToString() ?? "";
                    string code = obj["codigo_equipo"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(id) && certMap[id] != null)
                    {
                        obj["certificado_url"] = $"/api/equipment/{id}/certificate";
                        obj["certificado_nombre"] = certMap[id]?["filename"]?.ToString() ?? "Certificado.pdf";
                    }
                    else if (!string.IsNullOrEmpty(code) && certMap[code] != null)
                    {
                        obj["certificado_url"] = $"/api/equipment/certificate-by-code/{Uri.EscapeDataString(code)}";
                        obj["certificado_nombre"] = certMap[code]?["filename"]?.ToString() ?? "Certificado.pdf";
                    }
                }
            }
        }

        return Ok(data);
    }

    [HttpPost("equipment/upload-certificate")]
    public async Task<IActionResult> UploadEquipmentCertificate(IFormFile? file, [FromForm] string? equipmentId, [FromForm] string? equipmentCode, [FromForm] string? siteId)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Sesión no válida o expirada." });
        }

        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para cargar certificados de calibración." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Por favor selecciona un archivo PDF válido." });
        }

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            return BadRequest(new { success = false, message = "Solo se permiten archivos en formato PDF (.pdf)." });
        }

        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        string safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.pdf";
        string storagePath = $"{targetSite}/{safeName}";

        // Upload to Supabase Storage Private Bucket "equipment-certificates"
        using var stream = file.OpenReadStream();
        var (uploadSuccess, storageKey, uploadMsg) = await _supabase.UploadStorageObjectAsync("equipment-certificates", storagePath, stream, "application/pdf");
        
        if (!uploadSuccess)
        {
            // Fallback to local protected storage if network issue
            string uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "certificates");
            Directory.CreateDirectory(uploadsDir);
            string localFilePath = Path.Combine(uploadsDir, safeName);
            using (var localStream = new FileStream(localFilePath, FileMode.Create))
            {
                await file.CopyToAsync(localStream);
            }
            storageKey = $"local/{safeName}";
        }

        string code = equipmentCode?.Trim() ?? "EQ-UNKNOWN";
        string eqId = equipmentId?.Trim() ?? "";

        // Save index in data/equipment_certificates.json
        string dataDir = Path.Combine(_env.WebRootPath, "data");
        Directory.CreateDirectory(dataDir);
        string certMapFile = Path.Combine(dataDir, "equipment_certificates.json");

        JsonObject certMap = new JsonObject();
        if (System.IO.File.Exists(certMapFile))
        {
            try
            {
                string json = await System.IO.File.ReadAllTextAsync(certMapFile);
                certMap = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch { }
        }

        JsonObject certEntry = new JsonObject
        {
            ["storage_key"] = storageKey,
            ["filename"] = file.FileName,
            ["site_id"] = targetSite,
            ["uploaded_by"] = currentUserId,
            ["uploaded_at"] = DateTime.UtcNow.ToString("o")
        };

        if (!string.IsNullOrEmpty(eqId)) certMap[eqId] = certEntry;
        if (!string.IsNullOrEmpty(code)) certMap[code] = certEntry;

        await System.IO.File.WriteAllTextAsync(certMapFile, certMap.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        // Log immutable audit event
        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "CertificatesVault", 
            $"Certificado de calibración '{file.FileName}' subido exitosamente para el equipo '{code}'.",
            new { equipmentCode = code, equipmentId = eqId, filename = file.FileName, storageKey, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        string certUrl = !string.IsNullOrEmpty(eqId) 
            ? $"/api/equipment/{eqId}/certificate" 
            : $"/api/equipment/certificate-by-code/{Uri.EscapeDataString(code)}";

        return Ok(new { success = true, certificate_url = certUrl, filename = file.FileName, storage_key = storageKey });
    }

    [HttpGet("equipment/{id}/certificate")]
    [HttpGet("equipment/certificate-by-code/{code}")]
    public async Task<IActionResult> DownloadEquipmentCertificate(string? id, string? code)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Debe iniciar sesión para acceder a los certificados de calibración." });
        }

        // Load mapping
        string certMapFile = Path.Combine(_env.WebRootPath, "data", "equipment_certificates.json");
        if (!System.IO.File.Exists(certMapFile))
        {
            return NotFound(new { success = false, message = "No se encontró el certificado solicitado." });
        }

        JsonObject? certMap = null;
        try
        {
            string json = await System.IO.File.ReadAllTextAsync(certMapFile);
            certMap = JsonNode.Parse(json) as JsonObject;
        }
        catch { }

        JsonObject? entry = null;
        if (!string.IsNullOrEmpty(id) && certMap?[id] is JsonObject e1) entry = e1;
        else if (!string.IsNullOrEmpty(code) && certMap?[code] is JsonObject e2) entry = e2;

        if (entry == null)
        {
            return NotFound(new { success = false, message = "Certificado no encontrado en el registro de equipos." });
        }

        string targetSite = entry["site_id"]?.ToString() ?? "";
        string filename = entry["filename"]?.ToString() ?? "Certificado_Calibracion.pdf";
        string storageKey = entry["storage_key"]?.ToString() ?? entry["url"]?.ToString() ?? "";

        // Multi-tenant authorization check
        if (!IsSuperAdmin)
        {
            if (!string.IsNullOrEmpty(targetSite) && targetSite != CurrentUserSiteId && !IsCompanyAdmin)
            {
                await _supabase.LogAuditEventAsync(currentUserId, targetSite, "SECURITY", "CertificatesVault", 
                    $"Intento de acceso NO AUTORIZADO al certificado '{filename}'. Permisos insuficientes.",
                    new { requestedSite = targetSite, userSite = CurrentUserSiteId, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
                
                return StatusCode(403, new { success = false, message = "Acceso denegado: este certificado pertenece a otra empresa o planta." });
            }
        }

        // Audit Trail: Log authorized download event
        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "CertificatesVault", 
            $"Descarga/consulta de certificado de calibración '{filename}' realizada por '{HttpContext.Session.GetString("user_name") ?? "Usuario"}'.",
            new { filename, targetSite, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        // Retrieve from Supabase Storage
        if (storageKey.StartsWith("equipment-certificates/"))
        {
            string path = storageKey.Substring("equipment-certificates/".Length);
            var (success, stream, contentType, msg) = await _supabase.DownloadStorageObjectAsync("equipment-certificates", path);
            if (success && stream != null)
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{filename}\"";
                return File(stream, contentType ?? "application/pdf");
            }
        }

        // Fallback to local files if present
        if (storageKey.StartsWith("/uploads/") || storageKey.StartsWith("local/"))
        {
            string localRel = storageKey.Replace("local/", "/uploads/certificates/").TrimStart('/');
            string localPath = Path.Combine(_env.WebRootPath, localRel);
            if (System.IO.File.Exists(localPath))
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{filename}\"";
                var bytes = await System.IO.File.ReadAllBytesAsync(localPath);
                return File(bytes, "application/pdf");
            }
        }

        return NotFound(new { success = false, message = "El archivo del certificado no se encuentra disponible en el almacenamiento seguro." });
    }

    [HttpPost("evidence/upload")]
    public async Task<IActionResult> UploadEvidencePhoto(IFormFile? file, [FromForm] string? section, [FromForm] string? siteId)
    {
        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { success = false, message = "Sesión no válida." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "No se recibió ninguna imagen." });
        }

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
        {
            return BadRequest(new { success = false, message = "Formato de imagen no soportado (se requiere JPG, PNG o WEBP)." });
        }

        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        string safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
        string storagePath = $"{targetSite}/{safeName}";
        string contentType = ext == ".png" ? "image/png" : (ext == ".webp" ? "image/webp" : "image/jpeg");

        // Upload to Supabase Storage Private Bucket "audit-evidence"
        using var stream = file.OpenReadStream();
        var (uploadSuccess, storageKey, uploadMsg) = await _supabase.UploadStorageObjectAsync("audit-evidence", storagePath, stream, contentType);

        if (!uploadSuccess)
        {
            string uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "evidence");
            Directory.CreateDirectory(uploadsDir);
            string localFilePath = Path.Combine(uploadsDir, safeName);
            using (var localStream = new FileStream(localFilePath, FileMode.Create))
            {
                await file.CopyToAsync(localStream);
            }
        }

        string evidenceUrl = $"/api/evidence/{targetSite}/{safeName}";

        await _supabase.LogAuditEventAsync(currentUserId, targetSite, "AUDIT", "AuditEvidence", 
            $"Evidencia fotográfica cargada para sección '{section ?? "General"}' (Archivo: {safeName}).",
            new { section, filename = file.FileName, evidenceUrl, ip = HttpContext.Connection.RemoteIpAddress?.ToString() });

        return Ok(new { success = true, evidence_url = evidenceUrl, storage_key = $"audit-evidence/{storagePath}" });
    }

    [HttpGet("evidence/validation/{siteId}/{fileName}")]
    [HttpGet("evidence/validation/{fileName}")]
    [HttpGet("evidence/{siteId}/{fileName}")]
    [HttpGet("evidence/{fileName}")]
    [HttpGet("uploads/isolated/{fileName}")]
    [HttpGet("uploads/evidence/{fileName}")]
    [HttpGet("uploads/checkers/{fileName}")]
    [HttpGet("uploads/photos/{fileName}")]
    [HttpGet("uploads/{fileName}")]
    public async Task<IActionResult> GetEvidencePhoto(string? siteId, string fileName)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        string storagePath = $"{targetSite}/{fileName}";

        // 1. Check Supabase Storage bucket 'validation-evidence'
        var (valSuccess, valStream, valCt, _) = await _supabase.DownloadStorageObjectAsync("validation-evidence", storagePath);
        if (valSuccess && valStream != null)
        {
            return File(valStream, valCt ?? "image/jpeg");
        }
        var (valDirSuccess, valDirStream, valDirCt, _) = await _supabase.DownloadStorageObjectAsync("validation-evidence", fileName);
        if (valDirSuccess && valDirStream != null)
        {
            return File(valDirStream, valDirCt ?? "image/jpeg");
        }

        // 2. Check Supabase Storage bucket 'audit-evidence' at {siteId}/{fileName}
        var (success, stream, contentType, msg) = await _supabase.DownloadStorageObjectAsync("audit-evidence", storagePath);
        if (success && stream != null)
        {
            return File(stream, contentType ?? "image/jpeg");
        }

        // 3. Check Supabase Storage bucket 'audit-evidence' at {fileName} directly
        var (successDirect, streamDirect, ctDirect, _) = await _supabase.DownloadStorageObjectAsync("audit-evidence", fileName);
        if (successDirect && streamDirect != null)
        {
            return File(streamDirect, ctDirect ?? "image/jpeg");
        }

        // 3. Fallback to local files if present
        string[] searchPaths = new[]
        {
            Path.Combine(_env.WebRootPath, "uploads", "evidence", fileName),
            Path.Combine(_env.WebRootPath, "uploads", "isolated", fileName),
            Path.Combine(_env.WebRootPath, "uploads", "checkers", fileName),
            Path.Combine(_env.WebRootPath, "uploads", "photos", fileName),
            Path.Combine(_env.WebRootPath, "uploads", fileName)
        };

        foreach (var p in searchPaths)
        {
            if (System.IO.File.Exists(p))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(p);
                string ext = Path.GetExtension(p).ToLowerInvariant();
                string ct = ext == ".png" ? "image/png" : (ext == ".webp" ? "image/webp" : "image/jpeg");
                return File(bytes, ct);
            }
        }

        return NotFound(new { success = false, message = "Evidencia no encontrada." });
    }

    [HttpGet("sites/{siteId}/photo-policy")]
    public async Task<IActionResult> GetSitePhotoPolicy(string siteId)
    {
        string filePath = Path.Combine(_env.WebRootPath, "data", "site_photo_policies.json");
        if (System.IO.File.Exists(filePath))
        {
            try
            {
                string json = await System.IO.File.ReadAllTextAsync(filePath);
                var root = JsonNode.Parse(json) as JsonObject;
                if (root != null && root[siteId] != null)
                {
                    return Ok(root[siteId]);
                }
            }
            catch { }
        }

        return Ok(new
        {
            audit_tr53 = true,
            event_meter = true,
            grounding = true,
            flooring = true,
            isolated = true,
            checkers = true,
            walking_test = true
        });
    }

    [HttpPost("sites/{siteId}/photo-policy")]
    public async Task<IActionResult> SaveSitePhotoPolicy(string siteId, [FromBody] JsonObject payload)
    {
        if (!IsSiteAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para modificar la política de fotos." });
        }

        string dataDir = Path.Combine(_env.WebRootPath, "data");
        Directory.CreateDirectory(dataDir);
        string filePath = Path.Combine(dataDir, "site_photo_policies.json");

        JsonObject root = new JsonObject();
        if (System.IO.File.Exists(filePath))
        {
            try
            {
                string json = await System.IO.File.ReadAllTextAsync(filePath);
                root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch { }
        }

        root[siteId] = payload;
        await System.IO.File.WriteAllTextAsync(filePath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return Ok(new { success = true, policy = payload });
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

        string certUrl = payload["certificado_url"]?.ToString() ?? "";
        string certFilename = payload["certificado_nombre"]?.ToString() ?? "Certificado.pdf";
        payload.Remove("certificado_url");
        payload.Remove("certificado_nombre");

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
        string newId = result?["id"]?.ToString() ?? "";

        // If certificate URL provided, link in equipment_certificates.json
        if (!string.IsNullOrEmpty(certUrl) && (!string.IsNullOrEmpty(newId) || !string.IsNullOrEmpty(code)))
        {
            try
            {
                string dataDir = Path.Combine(_env.WebRootPath, "data");
                Directory.CreateDirectory(dataDir);
                string certMapFile = Path.Combine(dataDir, "equipment_certificates.json");
                JsonObject certMap = new JsonObject();
                if (System.IO.File.Exists(certMapFile))
                {
                    string json = await System.IO.File.ReadAllTextAsync(certMapFile);
                    certMap = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
                }
                JsonObject certEntry = new JsonObject
                {
                    ["url"] = certUrl,
                    ["filename"] = certFilename,
                    ["uploaded_at"] = DateTime.UtcNow.ToString("o")
                };
                if (!string.IsNullOrEmpty(newId)) certMap[newId] = certEntry;
                if (!string.IsNullOrEmpty(code)) certMap[code] = certEntry;
                await System.IO.File.WriteAllTextAsync(certMapFile, certMap.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

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

        string mapId = !string.IsNullOrWhiteSpace(payload["id"]?.ToString()) ? payload["id"]!.ToString() : Guid.NewGuid().ToString();
        string siteId = !string.IsNullOrWhiteSpace(payload["siteId"]?.ToString()) ? payload["siteId"]!.ToString() : CurrentUserSiteId;
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

        // Immediate sync of point measurements to Supabase floor_validation_logs
        foreach (var p in points.Where(pt => pt.LastResistanceOhms.HasValue))
        {
            double ohms = p.LastResistanceOhms!.Value;
            var logPayload = new JsonObject
            {
                ["site_id"] = siteId,
                ["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId,
                ["room_name"] = areaName,
                ["location"] = areaName,
                ["point_number"] = int.TryParse(p.Code, out int pNum) ? pNum : 1,
                ["point_id"] = p.Label ?? $"Punto {p.Code}",
                ["ptp_resistance"] = FormatScientific(ohms),
                ["resistance_ohms"] = ohms,
                ["temp_hum"] = "23.5°C / 45%",
                ["status_result"] = ohms <= 1.0e9 ? "PASS" : "FAIL",
                ["measured_at"] = (p.MeasuredAt ?? DateTime.UtcNow).ToString("o")
            };
            await _supabase.InsertFloorValidationLogAsync(logPayload);
        }

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
                    ["ptp_resistance"] = FormatScientific(ohms),
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

    private static string FormatScientific(double ohms)
    {
        if (ohms < 1000) return ohms.ToString("0.###");
        int exp = (int)Math.Floor(Math.Log10(ohms));
        double mantissa = ohms / Math.Pow(10, exp);
        double roundedMantissa = Math.Round(mantissa, 2);
        return $"{roundedMantissa:0.##}e{exp}";
    }

    // --- COMPANY BRANDING & LOGO MANAGEMENT ---
    [HttpPost("settings/company-logo")]
    public async Task<IActionResult> UploadCompanyLogo(IFormFile? file, [FromForm] string? companyId, [FromForm] string? logoBase64)
    {
        if (!IsSuperAdmin && !IsCompanyAdmin)
        {
            return StatusCode(403, new { success = false, message = "No tienes permisos para modificar el logotipo de la empresa. Requiere rol CompanyAdmin o SuperAdmin." });
        }

        string targetCompanyId = IsSuperAdmin ? (companyId ?? CurrentUserCompanyId) : CurrentUserCompanyId;
        if (string.IsNullOrEmpty(targetCompanyId))
        {
            return BadRequest(new { success = false, message = "El ID de empresa es obligatorio." });
        }

        string storageKey = "";
        string ext = ".png";

        if (file != null && file.Length > 0)
        {
            ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".svg", ".webp" };
            if (!allowedExts.Contains(ext))
            {
                return BadRequest(new { success = false, message = "Formato de imagen no válido. Use PNG, JPG, SVG o WebP." });
            }

            string safeFileName = $"logos/{targetCompanyId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            using var stream = file.OpenReadStream();
            var (uploadOk, key, msg) = await _supabase.UploadStorageObjectAsync("audit-evidence", safeFileName, stream, file.ContentType);
            if (uploadOk)
            {
                storageKey = $"/storage/v1/object/authenticated/audit-evidence/{safeFileName}";
            }
            else
            {
                // Fallback to local
                string brandingDir = Path.Combine(_env.WebRootPath, "uploads", "branding");
                Directory.CreateDirectory(brandingDir);
                string localName = $"{targetCompanyId}{ext}";
                string localPath = Path.Combine(brandingDir, localName);
                using (var fs = new FileStream(localPath, FileMode.Create))
                {
                    await file.CopyToAsync(fs);
                }
                storageKey = $"/uploads/branding/{localName}";
            }
        }
        else if (!string.IsNullOrEmpty(logoBase64))
        {
            try
            {
                byte[] imgBytes = Convert.FromBase64String(logoBase64.Contains(",") ? logoBase64.Split(',')[1] : logoBase64);
                string safeFileName = $"logos/{targetCompanyId}_{DateTime.UtcNow:yyyyMMddHHmmss}.png";
                var (uploadOk, key, msg) = await _supabase.UploadStorageObjectAsync("audit-evidence", safeFileName, imgBytes, "image/png");
                if (uploadOk)
                {
                    storageKey = $"/storage/v1/object/authenticated/audit-evidence/{safeFileName}";
                }
                else
                {
                    string brandingDir = Path.Combine(_env.WebRootPath, "uploads", "branding");
                    Directory.CreateDirectory(brandingDir);
                    string localPath = Path.Combine(brandingDir, $"{targetCompanyId}.png");
                    await System.IO.File.WriteAllBytesAsync(localPath, imgBytes);
                    storageKey = $"/uploads/branding/{targetCompanyId}.png";
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error al procesar la imagen: {ex.Message}" });
            }
        }
        else
        {
            return BadRequest(new { success = false, message = "Por favor selecciona o proporciona un archivo de imagen." });
        }

        // Update company in Supabase
        await _supabase.UpdateCompanyAsync(targetCompanyId, new { logo_url = storageKey });

        // Update local branding file
        SaveLocalCompanyLogo(targetCompanyId, storageKey);

        return Ok(new { success = true, company_id = targetCompanyId, logo_url = storageKey, message = "Logotipo actualizado exitosamente." });
    }

    [HttpGet("settings/company-logo")]
    public async Task<IActionResult> GetCompanyLogo([FromQuery] string? companyId)
    {
        string targetCompanyId = IsSuperAdmin ? (companyId ?? CurrentUserCompanyId) : CurrentUserCompanyId;
        if (string.IsNullOrEmpty(targetCompanyId))
        {
            return Ok(new { success = true, logo_url = "/images/esd360-logo.png" });
        }

        var comp = await _supabase.GetCompanyByIdAsync(targetCompanyId);
        string logoUrl = comp?["logo_url"]?.ToString() ?? GetLocalCompanyLogo(targetCompanyId);
        if (string.IsNullOrEmpty(logoUrl)) logoUrl = "/images/esd360-logo.png";

        return Ok(new { success = true, company_id = targetCompanyId, logo_url = logoUrl });
    }

    private string GetLocalCompanyLogo(string companyId)
    {
        try
        {
            string dataDir = Path.Combine(_env.WebRootPath, "data");
            string path = Path.Combine(dataDir, "company_branding.json");
            if (System.IO.File.Exists(path))
            {
                var node = JsonNode.Parse(System.IO.File.ReadAllText(path));
                return node?[companyId]?.ToString() ?? "";
            }
        }
        catch { }
        return "";
    }

    private void SaveLocalCompanyLogo(string companyId, string logoUrl)
    {
        try
        {
            string dataDir = Path.Combine(_env.WebRootPath, "data");
            Directory.CreateDirectory(dataDir);
            string path = Path.Combine(dataDir, "company_branding.json");
            JsonObject map = new();
            if (System.IO.File.Exists(path))
            {
                map = JsonNode.Parse(System.IO.File.ReadAllText(path)) as JsonObject ?? new JsonObject();
            }
            map[companyId] = logoUrl;
            System.IO.File.WriteAllText(path, map.ToJsonString());
        }
        catch { }
    }

    private static string GetAbbreviation(string text, int length, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        var clean = System.Text.RegularExpressions.Regex.Replace(text, @"[^A-Za-z0-9]", "");
        if (clean.Length == 0) return fallback;
        return clean.Length <= length ? clean.ToUpper() : clean[..length].ToUpper();
    }

    private void SaveReportToIndex(JsonObject reportRecord)
    {
        try
        {
            string dataDir = Path.Combine(_env.WebRootPath, "data");
            Directory.CreateDirectory(dataDir);
            string path = Path.Combine(dataDir, "reports_history.json");
            JsonArray list = new();
            if (System.IO.File.Exists(path))
            {
                list = JsonNode.Parse(System.IO.File.ReadAllText(path)) as JsonArray ?? new JsonArray();
            }
            list.Insert(0, reportRecord);
            while (list.Count > 200) list.RemoveAt(list.Count - 1);
            System.IO.File.WriteAllText(path, list.ToJsonString());
        }
        catch { }
    }

    private JsonArray GetReportsFromIndex(string siteId, string? search, string? line, string? reportType = null, string? companyId = null)
    {
        var result = new JsonArray();
        try
        {
            string dataDir = Path.Combine(_env.WebRootPath, "data");
            string path = Path.Combine(dataDir, "reports_history.json");
            if (System.IO.File.Exists(path))
            {
                var list = JsonNode.Parse(System.IO.File.ReadAllText(path)) as JsonArray ?? new JsonArray();
                string q = (search ?? "").Trim().ToLower();
                string lFilter = (line ?? "").Trim().ToLower();
                string tFilter = (reportType ?? "").Trim().ToUpper();

                foreach (var item in list)
                {
                    if (item is JsonObject r)
                    {
                        string rSiteId = r["site_id"]?.ToString() ?? "";
                        string rCompanyId = r["company_id"]?.ToString() ?? "";

                        // MANDATORY MULTI-TENANT ISOLATION:
                        // Only reports belonging strictly to the currently selected siteId are displayed.
                        if (!string.IsNullOrEmpty(siteId))
                        {
                            if (!string.Equals(rSiteId, siteId, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        // If companyId is specified and report has company_id, ensure match
                        if (!string.IsNullOrEmpty(companyId) && !string.IsNullOrEmpty(rCompanyId))
                        {
                            if (!string.Equals(rCompanyId, companyId, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        string folio = r["folio"]?.ToString()?.ToLower() ?? "";
                        string auditor = r["auditor"]?.ToString()?.ToLower() ?? "";
                        string rLine = r["linea"]?.ToString()?.ToLower() ?? "";
                        string rType = (r["report_type"]?.ToString() ?? "LINE_VALIDATION").ToUpper();

                        if (!string.IsNullOrEmpty(tFilter) && tFilter != "ALL" && rType != tFilter) continue;
                        if (!string.IsNullOrEmpty(lFilter) && lFilter != "all" && !rLine.Contains(lFilter)) continue;
                        if (!string.IsNullOrEmpty(q) && !folio.Contains(q) && !auditor.Contains(q) && !rLine.Contains(q)) continue;

                        result.Add(r.DeepClone());
                    }
                }
            }
        }
        catch { }
        return result;
    }

    private bool IsFolioTaken(string folio, string siteId)
    {
        try
        {
            string dataDir = Path.Combine(_env.WebRootPath, "data");
            string path = Path.Combine(dataDir, "reports_history.json");
            if (System.IO.File.Exists(path))
            {
                var list = JsonNode.Parse(System.IO.File.ReadAllText(path)) as JsonArray;
                if (list != null && list.Any(x => string.Equals(x?["folio"]?.ToString(), folio, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            string reportsDir = Path.Combine(_env.WebRootPath, "uploads", "reports");
            if (System.IO.File.Exists(Path.Combine(reportsDir, $"{folio}.html")))
            {
                return true;
            }
        }
        catch { }
        return false;
    }

    // --- ESD CONTROL ELEMENT VALIDATION ENDPOINTS ---
    [HttpGet("validation/equipos")]
    public async Task<IActionResult> GetValidationEquipos([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var equipos = await _supabase.GetCatalogoEquiposAsync(targetSite);
        return Ok(equipos);
    }

    [HttpGet("validation/records")]
    public async Task<IActionResult> GetValidationRecords([FromQuery] string? siteId)
    {
        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        var records = await _supabase.GetValidacionesEsdAsync(targetSite);
        return Ok(records);
    }

    [HttpPost("validation/records")]
    public async Task<IActionResult> CreateValidationRecord([FromBody] JsonObject payload)
    {
        if (payload == null) return BadRequest(new { success = false, message = "Payload inválido." });

        string siteId = payload["site_id"]?.ToString() ?? CurrentUserSiteId;
        payload["site_id"] = siteId;

        string idElemento = payload["id_elemento"]?.ToString()?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(idElemento))
        {
            return BadRequest(new { success = false, message = "El ID del elemento es obligatorio." });
        }
        payload["id_elemento"] = idElemento;

        if (string.IsNullOrEmpty(payload["fecha_auditoria"]?.ToString()))
        {
            payload["fecha_auditoria"] = DateTime.UtcNow.ToString("o");
        }

        if (string.IsNullOrEmpty(payload["auditor"]?.ToString()))
        {
            payload["auditor"] = HttpContext.Session.GetString("user_name") ?? "Auditor ESD";
        }

        // Evaluate PASS/FAIL across all provided readings
        double? med1 = null;
        if (payload["medicion_1"] != null && double.TryParse(payload["medicion_1"]?.ToString(), out double parsedMed1))
        {
            med1 = parsedMed1;
        }

        double refLimit = 1.0e9;
        if (payload["limite_referencia"] != null && double.TryParse(payload["limite_referencia"]?.ToString(), out double parsedRef))
        {
            refLimit = parsedRef;
        }

        string calcResult = "CUMPLE (APROBADO)";
        
        // Evaluate med1
        if (med1.HasValue && med1.Value > refLimit)
        {
            calcResult = "NO CUMPLE (RECHAZADO)";
        }

        // Also evaluate any additional readings in readings array
        if (payload["readings"] is JsonArray readingsArr)
        {
            foreach (var rNode in readingsArr)
            {
                if (rNode is JsonObject rObj)
                {
                    if (rObj["value"] != null && double.TryParse(rObj["value"]?.ToString(), out double rVal))
                    {
                        if (rVal > refLimit)
                        {
                            calcResult = "NO CUMPLE (RECHAZADO)";
                            break;
                        }
                    }
                }
            }
        }

        payload["resultado"] = calcResult;

        string currentUserId = HttpContext.Session.GetString("user_id") ?? "";
        if (!string.IsNullOrEmpty(currentUserId) && !payload.ContainsKey("auditor_id"))
        {
            payload["auditor_id"] = currentUserId;
        }

        var inserted = await _supabase.InsertValidacionEsdAsync(payload);

        await _supabase.LogAuditEventAsync(currentUserId, siteId, "AUDIT", "ESDValidation",
            $"Validación de elemento '{idElemento}' ({payload["elemento_s20_20"]}) registrada con resultado: {calcResult}.",
            new { idElemento, resultado = calcResult, med1, refLimit });

        return Ok(new { success = true, resultado = calcResult, data = inserted });
    }

    [HttpPost("validation/upload-evidence")]
    public async Task<IActionResult> UploadValidationEvidence([FromForm] IFormFile file, [FromForm] string? siteId, [FromForm] string? elementId)
    {
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "No se recibió ningún archivo de imagen." });

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
        {
            return BadRequest(new { success = false, message = "Formato de imagen no soportado (se requiere JPG, PNG o WEBP)." });
        }

        string targetSite = !string.IsNullOrEmpty(siteId) ? siteId : CurrentUserSiteId;
        string elemPrefix = !string.IsNullOrEmpty(elementId) ? elementId.Trim().ToUpper() : "ELEM";
        string safeName = $"{elemPrefix}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 6)}{ext}";
        string storagePath = $"{targetSite}/{safeName}";
        string contentType = ext == ".png" ? "image/png" : (ext == ".webp" ? "image/webp" : "image/jpeg");

        using var stream = file.OpenReadStream();
        var (uploadSuccess, storageKey, uploadMsg) = await _supabase.UploadStorageObjectAsync("validation-evidence", storagePath, stream, contentType);

        if (!uploadSuccess)
        {
            string uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "evidence");
            Directory.CreateDirectory(uploadsDir);
            string localFilePath = Path.Combine(uploadsDir, safeName);
            using (var localStream = new FileStream(localFilePath, FileMode.Create))
            {
                await file.CopyToAsync(localStream);
            }
        }

        string evidenceUrl = $"/api/evidence/validation/{targetSite}/{safeName}";
        return Ok(new { success = true, evidence_url = evidenceUrl, storage_key = $"validation-evidence/{storagePath}" });
    }

    // --- TEMPERATURE UNIT PREFERENCE ---
    [HttpGet("settings/temp-unit")]
    public IActionResult GetTemperatureUnit()
    {
        string unit = HttpContext.Session.GetString("temp_unit") ?? "C";
        return Ok(new { success = true, unit });
    }

    // --- ESD ELEMENT VALIDATION REPORT EXPORT ---
    [HttpPost("validation/reports/generate")]
    public async Task<IActionResult> GenerateElementValidationReport([FromBody] JsonObject payload)
    {
        if (payload == null) return BadRequest(new { success = false, message = "Invalid payload." });

        string activeSiteId = payload["site_id"]?.ToString() ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        string activeCompanyId = payload["company_id"]?.ToString() ?? HttpContext.Session.GetString("company_id") ?? "";

        // 1. Resolve Company & Site Names
        string companyName = HttpContext.Session.GetString("company_name") ?? "BCS Automotive Interface Solutions";
        string siteName = HttpContext.Session.GetString("site_name") ?? "Queretaro Plant";
        string? logoUrl = null;

        if (!string.IsNullOrEmpty(activeCompanyId))
        {
            var compObj = await _supabase.GetCompanyByIdAsync(activeCompanyId);
            if (compObj != null)
            {
                companyName = compObj["name"]?.ToString() ?? companyName;
                logoUrl = compObj["logo_url"]?.ToString();
            }
        }

        if (string.IsNullOrEmpty(logoUrl) && !string.IsNullOrEmpty(activeCompanyId))
        {
            logoUrl = GetLocalCompanyLogo(activeCompanyId);
        }

        if (string.IsNullOrEmpty(logoUrl))
        {
            logoUrl = "https://github.com/aldoaoa/Visualizador-BCS-IDS/blob/main/BCS%20LOGO.png?raw=true";
        }

        // 2. Generate Unique Folio
        string compCode = GetAbbreviation(companyName, 3, "BCS");
        string siteCode = GetAbbreviation(siteName, 3, "QRO");
        string yearShort = DateTime.UtcNow.ToString("yy");

        string rawId = payload["id"]?.ToString() ?? payload["id_elemento"]?.ToString() ?? "001";
        int numericId = 1;
        var digitsOnly = new string(rawId.Where(char.IsDigit).ToArray());
        if (int.TryParse(digitsOnly, out int pId) && pId > 0)
        {
            numericId = pId;
        }

        string uniqueFolio;
        int attempts = 0;
        do
        {
            string hexSuffix = Guid.NewGuid().ToString("N")[..4].ToUpper();
            uniqueFolio = $"{compCode}-PV-{numericId:D3}-{yearShort}-{hexSuffix}";
            attempts++;
        } while (IsFolioTaken(uniqueFolio, activeSiteId) && attempts < 20);

        // 3. Generate HTML
        string html = ElementValidationReportGenerator.GenerateHtmlReport(
            payload,
            uniqueFolio,
            companyName,
            siteName,
            logoUrl,
            numericId
        );

        // 4. Save local cache AND upload to Supabase Storage 'audit-evidence'
        string storagePath = $"reports/{compCode}_{siteCode}/{uniqueFolio}.html";
        string downloadUrl = $"/api/schedule/reports/{uniqueFolio}/view";

        try
        {
            string reportsDir = Path.Combine(_env.WebRootPath, "uploads", "reports");
            Directory.CreateDirectory(reportsDir);
            string localPath = Path.Combine(reportsDir, $"{uniqueFolio}.html");
            await System.IO.File.WriteAllTextAsync(localPath, html, System.Text.Encoding.UTF8);

            var uploadBytes = System.Text.Encoding.UTF8.GetBytes(html);
            var (uploadOk, key, msg) = await _supabase.UploadStorageObjectAsync("audit-evidence", storagePath, uploadBytes, "text/html");
            if (uploadOk)
            {
                downloadUrl = $"/api/schedule/reports/{uniqueFolio}/view";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving element validation report: {ex.Message}");
        }

        // 5. Save structured record in reports index
        string elemId = payload["id_elemento"]?.ToString() ?? "ELEM";
        string ubicacion = payload["ubicacion"]?.ToString() ?? "";
        string auditor = payload["auditor"]?.ToString() ?? HttpContext.Session.GetString("user_name") ?? "Auditor ESD";

        SaveReportToIndex(new JsonObject
        {
            ["folio"] = uniqueFolio,
            ["report_type"] = "ELEMENT_VALIDATION",
            ["type_name"] = "ESD Control Element Validation Report",
            ["linea"] = $"{elemId} ({ubicacion})",
            ["auditor"] = auditor,
            ["fecha"] = DateTime.UtcNow.ToString("o"),
            ["storage_path"] = storagePath,
            ["download_url"] = downloadUrl,
            ["site_id"] = activeSiteId,
            ["company_id"] = activeCompanyId,
            ["site_name"] = siteName,
            ["company_name"] = companyName,
            ["id_elemento"] = elemId,
            ["elemento_s20_20"] = payload["elemento_s20_20"]?.ToString() ?? "",
            ["resultado"] = payload["resultado"]?.ToString() ?? "CUMPLE (APROBADO)"
        });

        return Ok(new
        {
            success = true,
            folio = uniqueFolio,
            download_url = downloadUrl,
            html
        });
    }
}

