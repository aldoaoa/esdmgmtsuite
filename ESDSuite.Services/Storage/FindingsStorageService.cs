using System.Text.Json;
using System.Text.Json.Nodes;

namespace ESDSuite.Services.Storage;

public class FindingsStorageService
{
    private readonly string _findingsFilePath;
    private readonly string _limitsFilePath;
    private readonly string _uploadsDirectory;
    private readonly object _lock = new();

    public FindingsStorageService(string? contentRootPath = null, string? webRootPath = null)
    {
        string root = !string.IsNullOrEmpty(contentRootPath) ? contentRootPath : AppDomain.CurrentDomain.BaseDirectory;
        string wwwroot = !string.IsNullOrEmpty(webRootPath) ? webRootPath : Path.Combine(root, "wwwroot");
        string dataDir = Path.Combine(wwwroot, "data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        _findingsFilePath = Path.Combine(dataDir, "findings.json");
        _limitsFilePath = Path.Combine(dataDir, "normative_limits.json");

        _uploadsDirectory = Path.Combine(wwwroot, "uploads", "findings");
        if (!Directory.Exists(_uploadsDirectory))
        {
            Directory.CreateDirectory(_uploadsDirectory);
        }
    }

    public string UploadsDirectory => _uploadsDirectory;

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

    public JsonArray GetNormativeLimits(string siteId)
    {
        lock (_lock)
        {
            if (!File.Exists(_limitsFilePath))
            {
                var defaults = GetDefaultNormativeLimits();
                var siteLimitsMap = new JsonObject
                {
                    [siteId] = defaults.DeepClone(),
                    ["default"] = defaults.DeepClone()
                };
                File.WriteAllText(_limitsFilePath, siteLimitsMap.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(_limitsFilePath);
                var root = JsonNode.Parse(json) as JsonObject;
                if (root != null)
                {
                    if (root[siteId] is JsonArray siteArr && siteArr.Count > 0)
                    {
                        return siteArr.DeepClone().AsArray();
                    }
                    if (root["default"] is JsonArray defArr && defArr.Count > 0)
                    {
                        return defArr.DeepClone().AsArray();
                    }
                }
            }
            catch { }

            var defFallback = GetDefaultNormativeLimits();
            return defFallback;
        }
    }

    public void SaveNormativeLimits(string siteId, JsonArray limits)
    {
        lock (_lock)
        {
            JsonObject root = new();
            if (File.Exists(_limitsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_limitsFilePath);
                    root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
                }
                catch { }
            }

            root[siteId] = limits.DeepClone();
            if (!root.ContainsKey("default"))
            {
                root["default"] = GetDefaultNormativeLimits();
            }

            File.WriteAllText(_limitsFilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public JsonArray ResetNormativeLimits(string siteId)
    {
        var defaults = GetDefaultNormativeLimits();
        SaveNormativeLimits(siteId, defaults);
        return defaults;
    }

    public JsonArray GetAllFindings(string? siteId = null)
    {
        lock (_lock)
        {
            if (!File.Exists(_findingsFilePath))
            {
                return new JsonArray();
            }

            try
            {
                string json = File.ReadAllText(_findingsFilePath);
                var arr = JsonNode.Parse(json) as JsonArray ?? new JsonArray();
                if (string.IsNullOrWhiteSpace(siteId) || siteId.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    return arr;
                }

                var filtered = new JsonArray();
                foreach (var item in arr)
                {
                    if (item is JsonObject obj)
                    {
                        string sId = obj["site_id"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(sId) || string.Equals(sId, siteId, StringComparison.OrdinalIgnoreCase))
                        {
                            filtered.Add(obj.DeepClone());
                        }
                    }
                }
                return filtered;
            }
            catch
            {
                return new JsonArray();
            }
        }
    }

    public JsonObject? GetFindingById(string id)
    {
        lock (_lock)
        {
            if (!File.Exists(_findingsFilePath)) return null;

            try
            {
                string json = File.ReadAllText(_findingsFilePath);
                var arr = JsonNode.Parse(json) as JsonArray;
                if (arr == null) return null;

                foreach (var item in arr)
                {
                    if (item is JsonObject obj && string.Equals(obj["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    {
                        return obj.DeepClone() as JsonObject;
                    }
                }
            }
            catch { }
            return null;
        }
    }

    public string GenerateTrackingCode(string siteId)
    {
        lock (_lock)
        {
            int currentYear = DateTime.UtcNow.Year;
            string prefix = $"FND-{currentYear}-";
            int maxNum = 0;

            if (File.Exists(_findingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_findingsFilePath);
                    var arr = JsonNode.Parse(json) as JsonArray;
                    if (arr != null)
                    {
                        foreach (var item in arr)
                        {
                            if (item is JsonObject obj)
                            {
                                string code = obj["tracking_code"]?.ToString() ?? "";
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
                }
                catch { }
            }

            return $"{prefix}{(maxNum + 1):D4}";
        }
    }

    public JsonObject SaveFinding(JsonObject finding)
    {
        lock (_lock)
        {
            JsonArray arr = new();
            if (File.Exists(_findingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_findingsFilePath);
                    arr = JsonNode.Parse(json) as JsonArray ?? new JsonArray();
                }
                catch { }
            }

            string id = finding["id"]?.ToString() ?? Guid.NewGuid().ToString();
            finding["id"] = id;

            if (finding["tracking_code"] == null || string.IsNullOrWhiteSpace(finding["tracking_code"]?.ToString()))
            {
                string sId = finding["site_id"]?.ToString() ?? "";
                finding["tracking_code"] = GenerateTrackingCode(sId);
            }

            if (finding["opened_at"] == null || string.IsNullOrWhiteSpace(finding["opened_at"]?.ToString()))
            {
                finding["opened_at"] = DateTime.UtcNow.ToString("o");
            }

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
                        ["notes"] = "Hallazgo levantado y registrado en el tracker de acciones correctivas."
                    }
                };
            }

            int existingIndex = -1;
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is JsonObject obj && string.Equals(obj["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                arr[existingIndex] = finding.DeepClone();
            }
            else
            {
                arr.Insert(0, finding.DeepClone());
            }

            File.WriteAllText(_findingsFilePath, arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return finding;
        }
    }

    public bool DeleteFinding(string id)
    {
        lock (_lock)
        {
            if (!File.Exists(_findingsFilePath)) return false;

            try
            {
                string json = File.ReadAllText(_findingsFilePath);
                var arr = JsonNode.Parse(json) as JsonArray;
                if (arr == null) return false;

                int foundIndex = -1;
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonObject obj && string.Equals(obj["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (foundIndex >= 0)
                {
                    arr.RemoveAt(foundIndex);
                    File.WriteAllText(_findingsFilePath, arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    return true;
                }
            }
            catch { }
            return false;
        }
    }

    public JsonObject GetDashboardMetrics(string siteId)
    {
        var all = GetAllFindings(siteId);

        int total = all.Count;
        int open = 0;
        int inProgress = 0;
        int pendingVerification = 0;
        int closed = 0;
        int critical = 0;
        int major = 0;
        int minor = 0;

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

                // Lessons Learned
                string lesson = obj["lessons_learned"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(lesson))
                {
                    lessonsLearned.Add(new JsonObject
                    {
                        ["id"] = obj["id"]?.ToString(),
                        ["tracking_code"] = obj["tracking_code"]?.ToString(),
                        ["category"] = cat,
                        ["element_id"] = obj["element_id"]?.ToString(),
                        ["area"] = area,
                        ["root_cause"] = obj["root_cause"]?.ToString() ?? "N/A",
                        ["corrective_action"] = obj["corrective_action"]?.ToString() ?? "N/A",
                        ["lessons_learned"] = lesson,
                        ["closed_at"] = obj["closed_at"]?.ToString() ?? obj["opened_at"]?.ToString()
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
            ["lessonsLearned"] = lessonsLearned
        };
    }
}
