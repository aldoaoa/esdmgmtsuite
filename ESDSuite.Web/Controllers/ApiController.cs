using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using ESDSuite.Core.Constants;
using ESDSuite.Core.Helpers;
using ESDSuite.Core.Models;
using ESDSuite.Services.Auth;
using ESDSuite.Services.Supabase;

namespace ESDSuite.Web.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private const string DefaultSiteId = "eff70028-0759-4033-9c2b-41e1c1cc6efd";
    private const string DefaultAuditorId = "84d85bea-272c-42d1-ad14-35eb702f1e56";

    public ApiController(SupabaseService supabase)
    {
        _supabase = supabase;
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
            if (!string.IsNullOrEmpty(session.SiteId)) HttpContext.Session.SetString("site_id", session.SiteId);
            if (!string.IsNullOrEmpty(session.CompanyId)) HttpContext.Session.SetString("company_id", session.CompanyId);
            HttpContext.Session.SetString("site_name", session.SiteName);
            HttpContext.Session.SetString("company_name", session.CompanyName);
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
            user_role = HttpContext.Session.GetString("user_role"),
            site_id = HttpContext.Session.GetString("site_id") ?? DefaultSiteId,
            company_id = HttpContext.Session.GetString("company_id"),
            site_name = HttpContext.Session.GetString("site_name"),
            company_name = HttpContext.Session.GetString("company_name"),
            lang = HttpContext.Session.GetString("lang") ?? "es",
            version = EsdConstants.SystemVersion
        });
    }

    [HttpPost("set-site")]
    public IActionResult SetActiveSite([FromBody] JsonObject payload)
    {
        string siteId = payload["site_id"]?.ToString() ?? "";
        string siteName = payload["site_name"]?.ToString() ?? "";
        if (!string.IsNullOrEmpty(siteId)) HttpContext.Session.SetString("site_id", siteId);
        if (!string.IsNullOrEmpty(siteName)) HttpContext.Session.SetString("site_name", siteName);
        return Ok(new { success = true });
    }

    [HttpPost("set-lang")]
    public IActionResult SetLanguage([FromBody] JsonObject payload)
    {
        string lang = payload["lang"]?.ToString() ?? "es";
        HttpContext.Session.SetString("lang", lang);
        return Ok(new { success = true, lang });
    }

    [HttpGet("dashboard-metrics")]
    public async Task<IActionResult> GetDashboardMetrics([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        
        var assets = await _supabase.GetAssetsAsync(targetSite);
        var floors = await _supabase.GetFloorValidationLogsAsync(targetSite);
        var grounding = await _supabase.GetGroundingLogsAsync(targetSite);
        var entrance = await _supabase.GetEntranceCheckersLogsAsync(targetSite);

        return Ok(new
        {
            totalAssets = assets.Count,
            totalFloors = floors.Count,
            totalGrounding = grounding.Count,
            totalEntrance = entrance.Count,
            assetsData = assets
        });
    }

    private async Task<string> GetOrCreateAssetIdAsync(string customId, string siteId, string category, string location)
    {
        string cleanCustomId = customId.Trim().ToUpper();
        var existingAssets = await _supabase.GetAssetsAsync(siteId);

        foreach (var node in existingAssets)
        {
            if (node is JsonObject aObj && aObj["custom_id"]?.ToString().Trim().ToUpper() == cleanCustomId)
            {
                return aObj["id"]?.ToString() ?? Guid.NewGuid().ToString();
            }
        }

        // Insert new asset if not existing
        var newAsset = await _supabase.InsertAssetAsync(new
        {
            site_id = siteId,
            custom_id = cleanCustomId,
            category = category,
            classification = category,
            location = location.Trim().ToUpper(),
            status = "ACTIVE"
        });

        return newAsset?["id"]?.ToString() ?? Guid.NewGuid().ToString();
    }

    // --- UNIFIED AUDIT SUBMISSION (VENCIDOS.PY FORM PARITY WITH MEASUREMENTS TABLE) ---
    [HttpGet("audit/last-measurement/{id}")]
    public async Task<IActionResult> GetLastMeasurement(string id)
    {
        var result = await _supabase.GetUltimaMedicionAsync(id);
        return Ok(new { found = result != null, data = result });
    }

    [HttpPost("audit/submit-form")]
    public async Task<IActionResult> SubmitAuditForm([FromBody] JsonObject payload)
    {
        string idElemento = payload["id_elemento"]?.ToString() ?? "";
        string tipoEquipo = payload["tipo_equipo"]?.ToString() ?? "Mobiliario";
        string ubicacion = payload["ubicacion"]?.ToString() ?? "N/A";
        string auditor = payload["auditor"]?.ToString() ?? HttpContext.Session.GetString("user_name") ?? "Auditor ESD";
        string comentarios = payload["comentarios"]?.ToString() ?? "";
        string siteId = HttpContext.Session.GetString("site_id") ?? payload["site_id"]?.ToString() ?? DefaultSiteId;
        string auditorId = HttpContext.Session.GetString("user_id") ?? payload["auditor_id"]?.ToString() ?? DefaultAuditorId;
        string fechaActual = DateTime.Now.ToString("o");

        string assetId = await GetOrCreateAssetIdAsync(idElemento, siteId, tipoEquipo, ubicacion);

        string estatusEval = "PENDIENTE";
        JsonObject? resInsert = null;

        if (tipoEquipo.Trim().ToLower() == "ionizador")
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
                    tipo_equipo = "Ionizador",
                    ubicacion = ubicacion,
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
                    ubicacion = ubicacion,
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
    public async Task<IActionResult> GetEventMeterLogs()
    {
        var data = await _supabase.GetEventMeterLogsAsync();
        return Ok(data);
    }

    [HttpPost("event-meter")]
    public async Task<IActionResult> AddEventMeterLog([FromBody] JsonObject payload)
    {
        string idOp = payload["id_operacion"]?.ToString() ?? "OP-01";
        string tipoContacto = payload["tipo_contacto"]?.ToString() ?? "Maquinaria";
        int cantEventos = payload["cantidad_eventos"]?.GetValue<int>() ?? 0;
        double voltMax = payload["voltaje_maximo"]?.GetValue<double>() ?? 0;
        string notas = payload["notas"]?.ToString() ?? "";
        string siteId = HttpContext.Session.GetString("site_id") ?? payload["site_id"]?.ToString() ?? DefaultSiteId;
        string auditorId = HttpContext.Session.GetString("user_id") ?? payload["auditor_id"]?.ToString() ?? DefaultAuditorId;

        string assetId = await GetOrCreateAssetIdAsync(idOp, siteId, "Event Meter", "LINEA");

        string estatus = AuditEvaluationEngine.EvaluateEventMeter(voltMax);

        var dataToInsert = new
        {
            site_id = siteId,
            asset_id = assetId,
            auditor_id = auditorId,
            static_field_value = voltMax,
            status_result = estatus == "APROBADO" ? "PASS" : "FAIL",
            observaciones = notas,
            extra_data = new
            {
                id_operacion = idOp,
                tipo_contacto = tipoContacto,
                cantidad_eventos = cantEventos,
                type = "event_meter"
            },
            measured_at = DateTime.Now.ToString("o")
        };

        var result = await _supabase.InsertMeasurementAsync(dataToInsert);
        return Ok(new { success = result != null, estatus = estatus, data = result });
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
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetGroundingLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/grounding")]
    public async Task<IActionResult> AddGroundingLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null) payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        if (payload["auditor_id"] == null) payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;
        
        double ohms = payload["resistance_ohms"]?.GetValue<double>() ?? 0;
        string type = payload["point_type"]?.ToString() ?? "";
        double limit = type.Contains("Auxiliary") ? 25.0 : 2.0;
        payload["status_result"] = ohms < limit ? "PASS" : "FAIL";

        var result = await _supabase.InsertGroundingLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("infra/floors")]
    public async Task<IActionResult> GetFloorLogs([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetFloorValidationLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/floors")]
    public async Task<IActionResult> AddFloorLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null) payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        if (payload["auditor_id"] == null) payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;

        double ohms = payload["resistance_ohms"]?.GetValue<double>() ?? 0;
        payload["status_result"] = ohms <= 1.0e9 ? "PASS" : "FAIL";

        var result = await _supabase.InsertFloorValidationLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("infra/isolated")]
    public async Task<IActionResult> GetIsolatedLogs([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetIsolatedConductorsLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/isolated")]
    public async Task<IActionResult> AddIsolatedLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null) payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        if (payload["auditor_id"] == null) payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;

        double volts = payload["max_voltage"]?.GetValue<double>() ?? 0;
        payload["status_result"] = volts <= 35.0 ? "PASS" : "FAIL";

        var result = await _supabase.InsertIsolatedConductorsLogAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("infra/checkers")]
    public async Task<IActionResult> GetCheckersLogs([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetEntranceCheckersLogsAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("infra/checkers")]
    public async Task<IActionResult> AddCheckersLog([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null) payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        if (payload["auditor_id"] == null) payload["auditor_id"] = HttpContext.Session.GetString("user_id") ?? DefaultAuditorId;

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

    [HttpGet("routes/lines")]
    public async Task<IActionResult> GetLines([FromQuery] string? siteId)
    {
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetCatalogoLineasAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("routes/lines")]
    public async Task<IActionResult> AddLine([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null)
        {
            payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        }
        var result = await _supabase.InsertCatalogoLineaAsync(payload);
        return Ok(new { success = result != null, data = result });
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
        string targetSite = siteId ?? HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        var data = await _supabase.GetCatalogoEquiposAsync(targetSite);
        return Ok(data);
    }

    [HttpPost("equipment")]
    public async Task<IActionResult> AddEquipment([FromBody] JsonObject payload)
    {
        if (payload["site_id"] == null)
        {
            payload["site_id"] = HttpContext.Session.GetString("site_id") ?? DefaultSiteId;
        }
        var result = await _supabase.InsertCatalogoEquipoAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpDelete("equipment/{id}")]
    public async Task<IActionResult> DeleteEquipment(string id)
    {
        bool success = await _supabase.DeleteCatalogoEquipoAsync(id);
        return Ok(new { success });
    }

    // --- SETTINGS, TENANTS & USERS ---
    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var data = await _supabase.GetCompaniesAsync();
        return Ok(data);
    }

    [HttpGet("sites")]
    public async Task<IActionResult> GetSites([FromQuery] string? companyId)
    {
        var data = await _supabase.GetSitesAsync(companyId);
        return Ok(data);
    }

    [HttpPost("sites")]
    public async Task<IActionResult> AddSite([FromBody] JsonObject payload)
    {
        var result = await _supabase.InsertSiteAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? companyId, [FromQuery] string? siteId)
    {
        var data = await _supabase.GetUsersAsync(companyId, siteId);
        return Ok(data);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] JsonObject payload)
    {
        string pwd = payload["password"]?.ToString() ?? "123456";
        payload["password_hash"] = PasswordHasher.HashPassword(pwd);
        payload.Remove("password");

        var result = await _supabase.InsertUserAsync(payload);
        return Ok(new { success = result != null, data = result });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        bool success = await _supabase.DeleteUserAsync(id);
        return Ok(new { success });
    }
}
