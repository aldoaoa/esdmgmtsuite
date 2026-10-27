using System.Text.Json;
using System.Text.Json.Nodes;
using ESDSuite.Services.Supabase;

namespace ESDSuite.Services.Storage;

public class FindingsStorageService
{
    private readonly SupabaseService _supabase;

    public FindingsStorageService(SupabaseService supabase)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
    }

    public FindingsStorageService(string? contentRootPath, string? webRootPath, SupabaseService supabase)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
    }

    public string UploadsDirectory => "";

    public JsonArray GetDefaultNormativeLimits()
    {
        return new JsonArray
        {
            // --- TABLA 1: Sistemas de Puesta a Tierra y Conexión Equipotencial ---
            new JsonObject
            {
                ["id"] = "t1_equip_ground",
                ["table_number"] = "Tabla 1",
                ["table_title"] = "Sistemas de Puesta a Tierra y Conexión Equipotencial",
                ["item_name"] = "Conductor de Puesta a Tierra del Equipo",
                ["test_type"] = "Resistencia Continua a Tierra de CA",
                ["operator"] = "<",
                ["limit_value"] = 1.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0 Ohm",
                ["test_method"] = "ANSI/ESD S6.1 / ESD TR53",
                ["frequency"] = "Anual",
                ["severity"] = "CRITICAL",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t1_ac_ground",
                ["table_number"] = "Tabla 1",
                ["table_title"] = "Sistemas de Puesta a Tierra y Conexión Equipotencial",
                ["item_name"] = "Tierra de CA del Equipo",
                ["test_type"] = "Resistencia de Conexión Eléctrica",
                ["operator"] = "<",
                ["limit_value"] = 1.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0 Ohm",
                ["test_method"] = "ANSI/ESD S6.1 / ESD TR53",
                ["frequency"] = "Anual",
                ["severity"] = "CRITICAL",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t1_aux_ground",
                ["table_number"] = "Tabla 1",
                ["table_title"] = "Sistemas de Puesta a Tierra y Conexión Equipotencial",
                ["item_name"] = "Tierra Auxiliar",
                ["test_type"] = "Resistencia a Tierra Principal",
                ["operator"] = "<",
                ["limit_value"] = 25.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 25.0 Ohms",
                ["test_method"] = "ANSI/ESD S6.1",
                ["frequency"] = "Anual",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t1_cpg",
                ["table_number"] = "Tabla 1",
                ["table_title"] = "Sistemas de Puesta a Tierra y Conexión Equipotencial",
                ["item_name"] = "Punto Común de Tierra (CPG)",
                ["test_type"] = "Resistencia a Conductor Principal",
                ["operator"] = "<",
                ["limit_value"] = 1.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0 Ohm",
                ["test_method"] = "ANSI/ESD S6.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "CRITICAL",
                ["auto_finding"] = true
            },

            // --- TABLA 2: Sistemas de Conexión a Tierra del Personal ---
            new JsonObject
            {
                ["id"] = "t2_wrist_strap",
                ["table_number"] = "Tabla 2",
                ["table_title"] = "Sistemas de Conexión a Tierra del Personal",
                ["item_name"] = "Sistema de Pulsera Antiestática (Operador + Banda)",
                ["test_type"] = "Resistencia del Sistema",
                ["operator"] = "<",
                ["limit_value"] = 35000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 3.5x10^7 Ohms (35 Megohms)",
                ["test_method"] = "ANSI/ESD SP9.2 / ESD TR53",
                ["frequency"] = "Diario / Continuo",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t2_footwear_flooring_r",
                ["table_number"] = "Tabla 2",
                ["table_title"] = "Sistemas de Conexión a Tierra del Personal",
                ["item_name"] = "Sistema Calzado / Piso ESD (Método 1)",
                ["test_type"] = "Resistencia del Sistema",
                ["operator"] = "<",
                ["limit_value"] = 1000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^9 Ohms (1 Gigohm)",
                ["test_method"] = "ANSI/ESD STM97.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t2_walking_test",
                ["table_number"] = "Tabla 2",
                ["table_title"] = "Sistemas de Conexión a Tierra del Personal",
                ["item_name"] = "Generación de Carga Corporal (Walking Test - Método 2)",
                ["test_type"] = "Tensión Pico Máxima",
                ["operator"] = "<",
                ["limit_value"] = 100.0,
                ["unit"] = "Volts",
                ["limit_display"] = "< 100 Volts",
                ["test_method"] = "ANSI/ESD STM97.2",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },

            // --- TABLA 3: Elementos de Control ESD en Áreas EPA ---
            new JsonObject
            {
                ["id"] = "t3_worksurface",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Superficies de Trabajo ESD (Tapetes / Mesas)",
                ["test_type"] = "Resistencia Punto a Tierra (Rtg)",
                ["operator"] = "<",
                ["limit_value"] = 1000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^9 Ohms",
                ["test_method"] = "ANSI/ESD STM4.1 / ESD TR53",
                ["frequency"] = "Semestral / Anual",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_flooring",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Pisos ESD (Epóxico / Vinílico)",
                ["test_type"] = "Resistencia Punto a Tierra (Rtg)",
                ["operator"] = "<",
                ["limit_value"] = 1000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^9 Ohms",
                ["test_method"] = "ANSI/ESD STM7.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_seating",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Sillas y Asientos ESD",
                ["test_type"] = "Resistencia Punto a Tierra (Rtg)",
                ["operator"] = "<",
                ["limit_value"] = 1000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^9 Ohms",
                ["test_method"] = "ANSI/ESD STM12.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MINOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_garments_rpp",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Prendas de Control Estático (Batas ESD)",
                ["test_type"] = "Resistencia Punto a Punto (Rpp)",
                ["operator"] = "<",
                ["limit_value"] = 100000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^11 Ohms",
                ["test_method"] = "ANSI/ESD STM2.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_garments_rptgp",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Prendas Conectables a Tierra (Batas con Broche)",
                ["test_type"] = "Resistencia Punto a Broche (Rptgp)",
                ["operator"] = "<",
                ["limit_value"] = 1000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^9 Ohms",
                ["test_method"] = "ANSI/ESD STM2.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_garment_system",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Sistema Prenda + Operador a Tierra",
                ["test_type"] = "Resistencia del Sistema (Rsys)",
                ["operator"] = "<",
                ["limit_value"] = 35000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 3.5x10^7 Ohms",
                ["test_method"] = "ANSI/ESD STM2.1 / S20.20",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_carts",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Carritos, Racks y Estanterías Móviles",
                ["test_type"] = "Resistencia Punto a Tierra (Rtg)",
                ["operator"] = "<",
                ["limit_value"] = 1000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^9 Ohms",
                ["test_method"] = "ANSI/ESD STM4.1 / ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MINOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t3_soldering_irons",
                ["table_number"] = "Tabla 3",
                ["table_title"] = "Elementos de Control ESD en Áreas EPA",
                ["item_name"] = "Cautines y Estaciones de Soldadura",
                ["test_type"] = "Resistencia Punta a Tierra de CA",
                ["operator"] = "<",
                ["limit_value"] = 10.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 10.0 Ohms",
                ["test_method"] = "ESD TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },

            // --- TABLA 4: Empaques, Materiales e Ionización ---
            new JsonObject
            {
                ["id"] = "t4_ionizer_offset",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Ionizadores (Offset Voltage / Balance)",
                ["test_type"] = "Tensión Residual de Balance",
                ["operator"] = "<=",
                ["limit_value"] = 35.0,
                ["unit"] = "Volts",
                ["limit_display"] = "<= ±35 Volts",
                ["test_method"] = "ANSI/ESD STM3.1 / ESD SP3.3 / ESD TR53",
                ["frequency"] = "Trimestral",
                ["severity"] = "CRITICAL",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t4_ionizer_decay",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Ionizadores (Tiempo de Descarga)",
                ["test_type"] = "Tiempo de neutralización 1000V a 100V",
                ["operator"] = "<=",
                ["limit_value"] = 10.0,
                ["unit"] = "Segundos",
                ["limit_display"] = "<= 10.0 Segundos",
                ["test_method"] = "ANSI/ESD STM3.1 / ESD SP3.3 / ESD TR53",
                ["frequency"] = "Trimestral",
                ["severity"] = "CRITICAL",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t4_isolated_conductors",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Conductores Aislados en EPA",
                ["test_type"] = "Tensión Electrostática",
                ["operator"] = "<",
                ["limit_value"] = 35.0,
                ["unit"] = "Volts",
                ["limit_display"] = "< 35 Volts",
                ["test_method"] = "ANSI/ESD S20.20 Sec. 8.3.1",
                ["frequency"] = "Semestral",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t4_insulators_general",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Aislantes en EPA (>30 cm de ESDS)",
                ["test_type"] = "Potencial Electrostático de Campo",
                ["operator"] = "<",
                ["limit_value"] = 2000.0,
                ["unit"] = "Volts",
                ["limit_display"] = "< 2,000 Volts",
                ["test_method"] = "ANSI/ESD S20.20 Sec. 8.3.2 / TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "MINOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t4_insulators_direct",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Aislantes Contacto Directo (<2.5 cm de ESDS)",
                ["test_type"] = "Campo Electrostático Superficial",
                ["operator"] = "<=",
                ["limit_value"] = 125.0,
                ["unit"] = "Volts",
                ["limit_display"] = "<= 125 Volts/in",
                ["test_method"] = "ANSI/ESD S20.20 Sec. 8.3.2 / TR53",
                ["frequency"] = "Semestral",
                ["severity"] = "CRITICAL",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t4_packaging_dissipative",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Empaques y Materiales Disipativos",
                ["test_type"] = "Resistencia Superficial",
                ["operator"] = "<",
                ["limit_value"] = 100000000000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^11 Ohms",
                ["test_method"] = "ANSI/ESD STM11.11 / STM11.12",
                ["frequency"] = "Recepción / Anual",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            },
            new JsonObject
            {
                ["id"] = "t4_packaging_conductive",
                ["table_number"] = "Tabla 4",
                ["table_title"] = "Empaques, Materiales e Ionización",
                ["item_name"] = "Empaques y Materiales Conductivos",
                ["test_type"] = "Resistencia Superficial",
                ["operator"] = "<",
                ["limit_value"] = 10000.0,
                ["unit"] = "Ohms",
                ["limit_display"] = "< 1.0x10^4 Ohms",
                ["test_method"] = "ANSI/ESD STM11.11 / STM11.12",
                ["frequency"] = "Recepción / Anual",
                ["severity"] = "MAJOR",
                ["auto_finding"] = true
            }
        };
    }

    public async Task<JsonArray> GetNormativeLimitsAsync(string siteId)
    {
        try
        {
            var sbLimits = await _supabase.GetNormativeLimitsFromSupabaseAsync(siteId);
            if (sbLimits != null && sbLimits.Count > 0) return sbLimits;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindingsStorageService] GetNormativeLimitsAsync error: {ex.Message}");
        }

        var defaults = GetDefaultNormativeLimits();
        _ = Task.Run(async () =>
        {
            try { await _supabase.SaveNormativeLimitsToSupabaseAsync(siteId, defaults); } catch { }
        });
        return defaults;
    }

    public async Task<bool> SaveNormativeLimitsAsync(string siteId, JsonArray limits)
    {
        try
        {
            return await _supabase.SaveNormativeLimitsToSupabaseAsync(siteId, limits);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindingsStorageService] SaveNormativeLimitsAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<JsonArray> ResetNormativeLimitsAsync(string siteId)
    {
        var defaults = GetDefaultNormativeLimits();
        await SaveNormativeLimitsAsync(siteId, defaults);
        return defaults;
    }

    public async Task<JsonArray> GetAllFindingsAsync(string? siteId = null, string? status = null)
    {
        try
        {
            var list = await _supabase.GetFindingsFromSupabaseAsync(siteId, status);
            if (list != null) return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindingsStorageService] GetAllFindingsAsync error: {ex.Message}");
        }
        return new JsonArray();
    }

    public async Task<JsonObject?> GetFindingByIdAsync(string id)
    {
        try
        {
            return await _supabase.GetFindingByIdFromSupabaseAsync(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindingsStorageService] GetFindingByIdAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GenerateTrackingCodeAsync(string siteId, string prefixType = "FND")
    {
        int currentYear = DateTime.UtcNow.Year;
        string prefix = $"{prefixType}-{currentYear}-";
        int maxNum = 0;

        try
        {
            var all = await GetAllFindingsAsync(siteId);
            foreach (var item in all)
            {
                if (item is JsonObject obj)
                {
                    string code = obj["tracking_code"]?.ToString() ?? obj["tracker_code"]?.ToString() ?? "";
                    if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string numPart = code.Substring(prefix.Length);
                        if (int.TryParse(numPart, out int num) && num > maxNum)
                        {
                            maxNum = num;
                        }
                    }
                }
            }
        }
        catch { }

        return $"{prefix}{(maxNum + 1):D4}";
    }

    public async Task<(bool Success, JsonObject? Finding, string? Error)> SaveFindingAsync(JsonObject finding)
    {
        string id = finding["id"]?.ToString() ?? Guid.NewGuid().ToString();
        if (!Guid.TryParse(id, out var validGuid))
        {
            id = Guid.NewGuid().ToString();
        }
        finding["id"] = id;

        if (finding["tracking_code"] == null || string.IsNullOrWhiteSpace(finding["tracking_code"]?.ToString()))
        {
            string sId = finding["site_id"]?.ToString() ?? "";
            finding["tracking_code"] = await GenerateTrackingCodeAsync(sId);
        }
        finding["tracker_code"] = finding["tracking_code"];

        if (finding["opened_at"] == null || string.IsNullOrWhiteSpace(finding["opened_at"]?.ToString()))
        {
            finding["opened_at"] = DateTime.UtcNow.ToString("o");
        }
        finding["created_at"] = finding["opened_at"];

        if (finding["status"] == null || string.IsNullOrWhiteSpace(finding["status"]?.ToString()))
        {
            finding["status"] = "OPEN";
        }

        if (finding["timeline"] == null)
        {
            finding["timeline"] = new JsonArray
            {
                new JsonObject
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["user"] = finding["auditor_name"]?.ToString() ?? "Sistema",
                    ["action"] = "Apertura de Hallazgo",
                    ["notes"] = "Hallazgo registrado en el sistema."
                }
            };
        }

        try
        {
            var payload = BuildSupabasePayload(finding);
            var (ok, data, error) = await _supabase.SaveFindingToSupabaseAsync(payload);
            if (ok)
            {
                return (true, data ?? finding, null);
            }
            return (false, null, error ?? "Error al persistir en Supabase");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindingsStorageService] SaveFindingAsync error: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteFindingAsync(string id)
    {
        try
        {
            return await _supabase.DeleteFindingFromSupabaseAsync(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindingsStorageService] DeleteFindingAsync error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // Synchronous compatibility wrappers
    public JsonArray GetNormativeLimits(string siteId) => GetNormativeLimitsAsync(siteId).GetAwaiter().GetResult();
    public void SaveNormativeLimits(string siteId, JsonArray limits) => SaveNormativeLimitsAsync(siteId, limits).GetAwaiter().GetResult();
    public JsonArray ResetNormativeLimits(string siteId) => ResetNormativeLimitsAsync(siteId).GetAwaiter().GetResult();
    public JsonArray GetAllFindings(string? siteId = null) => GetAllFindingsAsync(siteId).GetAwaiter().GetResult();
    public JsonObject? GetFindingById(string id) => GetFindingByIdAsync(id).GetAwaiter().GetResult();
    public string GenerateTrackingCode(string siteId, string prefixType = "FND") => GenerateTrackingCodeAsync(siteId, prefixType).GetAwaiter().GetResult();
    public JsonObject SaveFinding(JsonObject finding) => SaveFindingAsync(finding).GetAwaiter().GetResult().Finding ?? finding;
    public bool DeleteFinding(string id) => DeleteFindingAsync(id).GetAwaiter().GetResult().Success;
    public JsonObject GetDashboardMetrics(string siteId) => GetDashboardMetricsAsync(siteId).GetAwaiter().GetResult();

    private JsonObject BuildSupabasePayload(JsonObject f)
    {
        var sb = new JsonObject();
        string id = f["id"]?.ToString() ?? Guid.NewGuid().ToString();
        if (!Guid.TryParse(id, out _)) id = Guid.NewGuid().ToString();
        sb["id"] = id;
        sb["tracking_code"] = f["tracking_code"]?.ToString() ?? f["tracker_code"]?.ToString() ?? id;

        string? siteId = f["site_id"]?.ToString();
        if (!string.IsNullOrWhiteSpace(siteId)) sb["site_id"] = siteId; else sb["site_id"] = null;

        string? compId = f["company_id"]?.ToString();
        if (!string.IsNullOrWhiteSpace(compId)) sb["company_id"] = compId; else sb["company_id"] = null;

        sb["source_module"] = f["source_module"]?.ToString() ?? "Levantamiento Manual";
        sb["title"] = f["title"]?.ToString() ?? "Hallazgo Normativo";
        sb["category"] = f["category"]?.ToString() ?? "General";
        sb["normative_table"] = f["normative_table"]?.ToString() ?? f["reference_table"]?.ToString() ?? "Tabla 2: Superficies de Trabajo / Mobiliario";
        sb["element_id"] = f["element_id"]?.ToString() ?? "";
        sb["area"] = f["area"]?.ToString() ?? "";
        sb["location"] = f["location"]?.ToString() ?? "";
        sb["measured_value"] = f["measured_value"]?.ToString() ?? "";
        sb["required_limit"] = f["required_limit"]?.ToString() ?? f["limit_applied"]?.ToString() ?? "";
        sb["severity"] = f["severity"]?.ToString() ?? "MAJOR";
        sb["status"] = f["status"]?.ToString() ?? "OPEN";
        sb["auditor_id"] = f["auditor_id"]?.ToString();
        sb["auditor_name"] = f["auditor_name"]?.ToString() ?? f["opened_by"]?.ToString() ?? "Auditor";
        sb["opened_by"] = f["opened_by"]?.ToString() ?? f["auditor_name"]?.ToString() ?? "Auditor";
        sb["assignee_name"] = f["assignee_name"]?.ToString() ?? f["assigned_to"]?.ToString();
        sb["assignee_email"] = f["assignee_email"]?.ToString();

        string? openedAt = f["opened_at"]?.ToString() ?? f["created_at"]?.ToString();
        if (!string.IsNullOrWhiteSpace(openedAt) && DateTime.TryParse(openedAt, out var dtOpened))
            sb["opened_at"] = DateTime.SpecifyKind(dtOpened, DateTimeKind.Utc).ToString("o");
        else
            sb["opened_at"] = DateTime.UtcNow.ToString("o");

        string? due = f["due_date"]?.ToString();
        if (!string.IsNullOrWhiteSpace(due) && DateTime.TryParse(due, out var dtDue))
            sb["due_date"] = DateTime.SpecifyKind(dtDue, DateTimeKind.Utc).ToString("o");
        else
            sb["due_date"] = null;

        string? closedAt = f["closed_at"]?.ToString();
        if (!string.IsNullOrWhiteSpace(closedAt) && DateTime.TryParse(closedAt, out var dtClosed))
            sb["closed_at"] = DateTime.SpecifyKind(dtClosed, DateTimeKind.Utc).ToString("o");
        else
            sb["closed_at"] = null;

        sb["closed_by"] = f["closed_by"]?.ToString();
        sb["description"] = f["description"]?.ToString();
        sb["containment_action"] = f["containment_action"]?.ToString() ?? f["immediate_containment"]?.ToString();
        sb["root_cause"] = f["root_cause"]?.ToString();
        sb["corrective_action"] = f["corrective_action"]?.ToString();
        sb["preventive_action"] = f["preventive_action"]?.ToString();
        sb["lessons_learned"] = f["lessons_learned"]?.ToString();
        sb["evidence_open_url"] = f["evidence_open_url"]?.ToString();
        sb["evidence_close_url"] = f["evidence_close_url"]?.ToString();

        if (f["timeline"] != null)
            sb["timeline"] = f["timeline"]!.DeepClone();
        else
            sb["timeline"] = new JsonArray();

        sb["created_at"] = sb["opened_at"];
        sb["updated_at"] = DateTime.UtcNow.ToString("o");
        return sb;
    }

    public async Task<JsonObject> GetDashboardMetricsAsync(string siteId)
    {
        var all = await GetAllFindingsAsync(siteId);

        int total = 0;
        int open = 0;
        int inProgress = 0;
        int pendingVerification = 0;
        int closed = 0;
        int critical = 0;
        int major = 0;
        int minor = 0;
        int manualLessonsCount = 0;
        int auditLessonsCount = 0;

        var typeDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var responsibleDistribution = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var areaDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lessonsLearned = new JsonArray();

        double totalClosureDays = 0;
        int closedWithDurationCount = 0;
        int onTimeClosedCount = 0;

        foreach (var item in all)
        {
            if (item is JsonObject obj)
            {
                string status = obj["status"]?.ToString().ToUpper() ?? "OPEN";
                string sev = obj["severity"]?.ToString().ToUpper() ?? "MAJOR";
                string cat = obj["category"]?.ToString() ?? "General";
                string area = obj["area"]?.ToString() ?? "General";
                string resp = obj["assignee_name"]?.ToString() ?? "Sin Asignar";
                string srcModule = obj["source_module"]?.ToString() ?? "";
                string tCode = obj["tracking_code"]?.ToString() ?? obj["tracker_code"]?.ToString() ?? "";
                bool isManualLesson = srcModule.Equals("Lección Aprendida Manual", StringComparison.OrdinalIgnoreCase)
                    || srcModule.Equals("Lección Manual", StringComparison.OrdinalIgnoreCase)
                    || tCode.StartsWith("LRN-", StringComparison.OrdinalIgnoreCase);

                if (!isManualLesson)
                {
                    total++;
                    if (status == "OPEN") open++;
                    else if (status == "IN_PROGRESS") inProgress++;
                    else if (status == "PENDING_VERIFICATION") pendingVerification++;
                    else if (status == "CLOSED") closed++;

                    if (sev == "CRITICAL") critical++;
                    else if (sev == "MAJOR") major++;
                    else if (sev == "MINOR") minor++;

                    // By Type / Category
                    if (!typeDistribution.ContainsKey(cat)) typeDistribution[cat] = 0;
                    typeDistribution[cat]++;

                    // By Area
                    if (!areaDistribution.ContainsKey(area)) areaDistribution[area] = 0;
                    areaDistribution[area]++;

                    // By Responsible
                    if (!responsibleDistribution.ContainsKey(resp))
                    {
                        responsibleDistribution[resp] = new JsonObject
                        {
                            ["name"] = resp,
                            ["total"] = 0,
                            ["open"] = 0,
                            ["closed"] = 0
                        };
                    }
                    var rObj = responsibleDistribution[resp];
                    rObj["total"] = (rObj["total"]?.GetValue<int>() ?? 0) + 1;
                    if (status == "CLOSED")
                    {
                        rObj["closed"] = (rObj["closed"]?.GetValue<int>() ?? 0) + 1;
                    }
                    else
                    {
                        rObj["open"] = (rObj["open"]?.GetValue<int>() ?? 0) + 1;
                    }

                    // MTTR & On-time calculation
                    if (status == "CLOSED" && DateTime.TryParse(obj["opened_at"]?.ToString(), out DateTime openDt))
                    {
                        DateTime closeDt = DateTime.TryParse(obj["closed_at"]?.ToString(), out DateTime cDt) ? cDt : DateTime.UtcNow;
                        double days = (closeDt - openDt).TotalDays;
                        if (days >= 0)
                        {
                            totalClosureDays += days;
                            closedWithDurationCount++;
                        }

                        if (DateTime.TryParse(obj["due_date"]?.ToString(), out DateTime dueDt))
                        {
                            if (closeDt <= dueDt.AddDays(1)) onTimeClosedCount++;
                        }
                        else
                        {
                            onTimeClosedCount++;
                        }
                    }
                }

                // Lessons Learned (both manual continuous improvement and closed audit findings)
                string lesson = obj["lessons_learned"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(lesson))
                {
                    if (isManualLesson) manualLessonsCount++; else auditLessonsCount++;
                    string title = obj["title"]?.ToString() ?? "Lección Aprendida";
                    string loc = obj["location"]?.ToString() ?? area;
                    string evUrl = obj["evidence_open_url"]?.ToString() ?? obj["evidence_close_url"]?.ToString() ?? "";
                    string author = obj["assignee_name"]?.ToString() ?? obj["assigned_to"]?.ToString() ?? obj["closed_by"]?.ToString() ?? obj["auditor_name"]?.ToString() ?? "N/A";

                    lessonsLearned.Add(new JsonObject
                    {
                        ["id"] = obj["id"]?.ToString(),
                        ["tracking_code"] = tCode,
                        ["tracker_code"] = tCode,
                        ["title"] = title,
                        ["category"] = cat,
                        ["normative_table"] = obj["normative_table"]?.ToString() ?? obj["reference_table"]?.ToString() ?? "Buenas Prácticas",
                        ["element_id"] = obj["element_id"]?.ToString() ?? "",
                        ["area"] = area,
                        ["location"] = loc,
                        ["root_cause"] = obj["root_cause"]?.ToString() ?? "N/A",
                        ["corrective_action"] = obj["corrective_action"]?.ToString() ?? "N/A",
                        ["preventive_action"] = obj["preventive_action"]?.ToString() ?? "",
                        ["lessons_learned"] = lesson,
                        ["lesson"] = lesson,
                        ["description"] = obj["description"]?.ToString() ?? "",
                        ["evidence_url"] = evUrl,
                        ["closed_at"] = obj["closed_at"]?.ToString() ?? obj["opened_at"]?.ToString() ?? obj["created_at"]?.ToString(),
                        ["assigned_to"] = author,
                        ["author"] = author,
                        ["source_module"] = srcModule,
                        ["is_manual"] = isManualLesson
                    });
                }
            }
        }

        double mttr = closedWithDurationCount > 0 ? Math.Round(totalClosureDays / closedWithDurationCount, 1) : 0.0;
        double onTimePct = closed > 0 ? Math.Round((double)onTimeClosedCount / closed * 100.0, 1) : 100.0;

        var typeArr = new JsonArray();
        foreach (var kvp in typeDistribution.OrderByDescending(x => x.Value))
        {
            typeArr.Add(new JsonObject { ["name"] = kvp.Key, ["count"] = kvp.Value });
        }

        var areaArr = new JsonArray();
        foreach (var kvp in areaDistribution.OrderByDescending(x => x.Value))
        {
            areaArr.Add(new JsonObject { ["name"] = kvp.Key, ["count"] = kvp.Value });
        }

        var respArr = new JsonArray();
        foreach (var r in responsibleDistribution.Values.OrderByDescending(x => x["total"]?.GetValue<int>() ?? 0))
        {
            respArr.Add(r.DeepClone());
        }

        return new JsonObject
        {
            ["totalFindings"] = total,
            ["openFindings"] = open,
            ["inProgressFindings"] = inProgress,
            ["pendingVerificationFindings"] = pendingVerification,
            ["closedFindings"] = closed,
            ["criticalFindings"] = critical,
            ["majorFindings"] = major,
            ["minorFindings"] = minor,
            ["mttrDays"] = mttr,
            ["onTimeRate"] = onTimePct,
            ["typeDistribution"] = typeArr,
            ["areaDistribution"] = areaArr,
            ["responsibleDistribution"] = respArr,
            ["lessonsLearned"] = lessonsLearned,
            ["lessons_learned"] = lessonsLearned,
            ["manualLessonsCount"] = manualLessonsCount,
            ["auditLessonsCount"] = auditLessonsCount,
            ["totalLessonsCount"] = lessonsLearned.Count
        };
    }
}
