using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace ESDSuite.Core.Helpers;

public static class ElementValidationReportGenerator
{
    private static readonly Dictionary<string, string> ElementTranslationsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Pulsera antiestática", "Wrist strap" },
        { "Calzado", "Footwear" },
        { "Piso ESD", "ESD Floor" },
        { "Superficie de trabajo", "Work Surface" },
        { "Monitor Continuo", "Continuous Monitor" },
        { "Ionizador", "Ionizer" },
        { "Bolsa disipativa", "Dissipative bag" },
        { "Cautín / Estación de soldar", "Soldering Iron / Station" },
        { "Caja Disipativa", "Dissipative Box" },
        { "Caja conductiva", "Conductive Box" },
        { "Charola conductiva", "Conductive Tray" },
        { "Charola Disipativa", "Dissipative Tray" },
        { "Magazine", "Magazine" },
        { "Bata", "Smock / Garment" },
        { "Gorra", "Cap / Headwear" },
        { "Rack", "Storage Rack" },
        { "Carrito", "Cart / Trolley" },
        { "Silla ESD", "ESD Chair" },
        { "Guantes Nitrilo", "Nitrile Gloves" },
        { "Guantes Tela", "Fabric Gloves" },
        { "Tapete de piso", "Floor Mat" },
        { "Aislantes - EPA (General)", "Insulators - EPA (General)" },
        { "Aislantes - Conductores Aislados", "Insulators - Isolated Conductors" },
        { "Aislantes - Contacto directo", "Insulators - Direct Contact" },
        { "Bolsas blindadas", "Shielding Bags" }
    };

    private static readonly Dictionary<string, string> MagnitudeTranslationsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Resistencia", "Resistance" },
        { "Voltaje", "Voltage" },
        { "Tiempo", "Time / Decay" },
        { "Longitud", "Length / Distance" },
        { "Otro", "Other / Visual" }
    };

    public static string GenerateHtmlReport(
        JsonObject record,
        string folio,
        string companyName,
        string siteName,
        string? logoUrl,
        int reportIndex = 1)
    {
        string GetVal(string key, string fallback = "N/D")
        {
            var val = record[key]?.ToString();
            if (string.IsNullOrWhiteSpace(val) || val.Equals("null", StringComparison.OrdinalIgnoreCase))
                return fallback;
            return val.Trim();
        }

        // 1. Resolve Company Logo
        string effectiveLogo = logoUrl ?? "";
        if (string.IsNullOrEmpty(effectiveLogo))
        {
            effectiveLogo = "https://github.com/aldoaoa/Visualizador-BCS-IDS/blob/main/BCS%20LOGO.png?raw=true";
        }

        // 2. Resolve Year and Execution Date in English (e.g. 22-Aug-2026)
        string fechaRaw = GetVal("fecha_auditoria", DateTime.UtcNow.ToString("o"));
        DateTime execDate = DateTime.UtcNow;
        if (DateTime.TryParse(fechaRaw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedDate))
        {
            execDate = parsedDate;
        }
        else if (DateTime.TryParse(fechaRaw, out var localParsed))
        {
            execDate = localParsed;
        }

        string executionDateFormatted = execDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
        string footerDateStr = execDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        string shortYear = execDate.ToString("yy", CultureInfo.InvariantCulture);

        // 3. Extract and Translate Control Element Metadata
        string rawElement = GetVal("elemento_s20_20", "Work Surface");
        string elementEn = ElementTranslationsEn.TryGetValue(rawElement, out var elemTrans) ? elemTrans : rawElement;

        string idElemento = GetVal("id_elemento", "N/D");
        string fabElem = GetVal("fabricante_elem", "N/D");
        string modElem = GetVal("modelo_elem", "N/D");
        string snElem = GetVal("sn_elem", "N/D");

        // 4. General Information & Environmental
        string temperatura = GetVal("temperatura", "N/D");
        string tempUnit = GetVal("temp_unit", "");
        if (!string.IsNullOrEmpty(tempUnit) && !temperatura.Contains("°"))
        {
            temperatura = $"{temperatura} °{tempUnit}";
        }

        string humedad = GetVal("humedad", "N/D");
        if (!humedad.Contains("%") && humedad != "N/D")
        {
            humedad = $"{humedad} %";
        }

        string ubicacion = GetVal("ubicacion", "N/D");
        string rawMagnitud = GetVal("magnitud", "Resistencia");
        string magnitudEn = MagnitudeTranslationsEn.TryGetValue(rawMagnitud, out var magTrans) ? magTrans : rawMagnitud;

        // 5. Traceability & Measurement Equipment
        string idEquipo = GetVal("id_equipo_utilizado", "N/D");
        string tipoEquipo = GetVal("tipo_equipo", "Digital Megohmmeter TR53");
        string reporteCal = GetVal("reporte_cal", "CERT-2026-ESD");
        string resolucion = GetVal("resolucion", "0.01");
        string fabEq = GetVal("fabricante_eq", "Desco 19787");
        string modEq = GetVal("modelo_eq", "Surface Resistance Meter");
        string snEq = GetVal("sn_eq", "SN-982310");
        string fechaProxCal = GetVal("fecha_prox_cal", "2027-01-15");

        // 6. Test Method, Unit & Limits
        string testMethod = GetVal("metodo", "ANSI/ESD TR53");
        string unitStr = GetVal("unidad", "ohms");
        string refRaw = GetVal("limite_referencia", "");
        string refStr = refRaw;
        if (double.TryParse(refRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var refNumVal))
        {
            refStr = (refNumVal > 1000 || (refNumVal < 0.01 && refNumVal > 0))
                ? refNumVal.ToString("0.00E+00", CultureInfo.InvariantCulture)
                : refNumVal.ToString("G", CultureInfo.InvariantCulture);
        }

        // 7. Parse Dynamic Readings (Multi-Readings Array or medicion_1..5)
        var validNums = new List<double>();
        var tableRowsHtml = new StringBuilder();

        // Check if "readings" array is provided
        if (record["readings"] is JsonArray readingsArray && readingsArray.Count > 0)
        {
            int rIdx = 1;
            foreach (var node in readingsArray)
            {
                if (node is JsonObject rObj)
                {
                    string rawVal = rObj["value"]?.ToString() ?? "";
                    string locPoint = rObj["location_point"]?.ToString() ?? "";

                    if (double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                    {
                        validNums.Add(num);
                        string valDisplay = (num > 1000 || (num < 0.01 && num > 0))
                            ? num.ToString("0.00E+00", CultureInfo.InvariantCulture)
                            : num.ToString("G", CultureInfo.InvariantCulture);

                        string rowPointLabel = string.IsNullOrEmpty(locPoint) ? "" : $" <span class=\"text-xs text-gray-500 font-normal\">({locPoint})</span>";

                        tableRowsHtml.Append($@"
                        <tr class=""border-b border-gray-200 hover:bg-blue-50 print:hover:bg-transparent text-center"">
                            <td class=""p-1 border-r border-gray-300 font-bold"">{rIdx}</td>
                            <td class=""p-1 border-r border-gray-300"">{refStr}</td>
                            <td class=""p-1 border-r border-gray-300"">0.0</td>
                            <td class=""p-1 border-r border-gray-300 bg-yellow-50 print:bg-transparent font-mono font-bold"">{valDisplay}{rowPointLabel}</td>
                            <td class=""p-1 border-r border-gray-300"">{testMethod}</td>
                            <td class=""p-1 border-r border-gray-300"">{unitStr}</td>
                        </tr>");
                        rIdx++;
                    }
                }
            }
        }
        else
        {
            // Fallback to reading discrete fields (medicion_1 ... medicion_5)
            var rawList = new List<string>
            {
                GetVal("medicion_1", ""),
                GetVal("medicion_2", ""),
                GetVal("medicion_3", ""),
                GetVal("medicion_4", ""),
                GetVal("medicion_5", "")
            };

            int rIdx = 1;
            foreach (var m in rawList)
            {
                if (!string.IsNullOrWhiteSpace(m) && !m.Equals("n/d", StringComparison.OrdinalIgnoreCase) && !m.Equals("nan", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(m, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                    {
                        validNums.Add(num);
                        string valDisplay = (num > 1000 || (num < 0.01 && num > 0))
                            ? num.ToString("0.00E+00", CultureInfo.InvariantCulture)
                            : num.ToString("G", CultureInfo.InvariantCulture);

                        tableRowsHtml.Append($@"
                        <tr class=""border-b border-gray-200 hover:bg-blue-50 print:hover:bg-transparent text-center"">
                            <td class=""p-1 border-r border-gray-300 font-bold"">{rIdx}</td>
                            <td class=""p-1 border-r border-gray-300"">{refStr}</td>
                            <td class=""p-1 border-r border-gray-300"">0.0</td>
                            <td class=""p-1 border-r border-gray-300 bg-yellow-50 print:bg-transparent font-mono font-bold"">{valDisplay}</td>
                            <td class=""p-1 border-r border-gray-300"">{testMethod}</td>
                            <td class=""p-1 border-r border-gray-300"">{unitStr}</td>
                        </tr>");
                        rIdx++;
                    }
                }
            }
        }

        // If no readings could be parsed, provide a single row
        if (validNums.Count == 0)
        {
            tableRowsHtml.Append($@"
            <tr class=""border-b border-gray-200 text-center"">
                <td class=""p-1 border-r border-gray-300 font-bold"">1</td>
                <td class=""p-1 border-r border-gray-300"">{refStr}</td>
                <td class=""p-1 border-r border-gray-300"">0.0</td>
                <td class=""p-1 border-r border-gray-300 bg-yellow-50 font-mono font-bold"">N/D</td>
                <td class=""p-1 border-r border-gray-300"">{testMethod}</td>
                <td class=""p-1 border-r border-gray-300"">{unitStr}</td>
            </tr>");
        }

        // 8. Calculate Average
        double averageVal = validNums.Count > 0 ? (validNums.Sum() / validNums.Count) : 0;
        string averageStr = averageVal > 0
            ? ((averageVal > 1000 || (averageVal < 0.01 && averageVal > 0))
                ? averageVal.ToString("0.00E+00", CultureInfo.InvariantCulture)
                : averageVal.ToString("G", CultureInfo.InvariantCulture))
            : "N/A";

        // 9. Photographic Evidence Tag
        string imgUrl = GetVal("imagen_url", "");
        if (string.IsNullOrEmpty(imgUrl) || imgUrl == "N/D" || imgUrl.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            imgUrl = GetVal("evidence_url", "");
        }

        string imgTag;
        if (string.IsNullOrEmpty(imgUrl) || imgUrl == "N/D" || imgUrl.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            imgTag = "<span class=\"text-gray-400 flex flex-col items-center\"><br><br>No photographic evidence attached</span>";
        }
        else
        {
            imgTag = $"<img src=\"{imgUrl}\" alt=\"Validation Evidence\" style=\"height: 190px; width: auto; max-width: 100%; object-fit: contain; margin: 0 auto; border-radius: 4px;\" />";
        }

        // 10. Result & Color
        string rawResultado = GetVal("resultado", "CUMPLE (APROBADO)");
        string resUpper = rawResultado.ToUpperInvariant();
        bool isCompliant = !resUpper.Contains("NO CUMPLE") && !resUpper.Contains("RECHAZADO") && !resUpper.Contains("FAIL") && !resUpper.Contains("FALLA");

        string resultTextEn = isCompliant ? "COMPLIANT (APPROVED)" : "NON-COMPLIANT (REJECTED)";
        string resultColorClass = isCompliant ? "text-green-600" : "text-red-600";

        string auditor = GetVal("auditor", "ESD Certified Auditor");
        string notas = GetVal("notas", "No additional remarks.");

        // 11. HTML Template Generation (100% English, fully compatible with modern browsers and print)
        string html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>{folio}</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <style>
        @media print {{
            body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
            .print\:hidden {{ display: none !important; }}
            .print\:shadow-none {{ box-shadow: none !important; }}
            .print\:w-full {{ width: 100% !important; max-width: 100% !important; }}
            .print\:bg-white {{ background-color: #FFFFFF !important; }}
            .print\:p-0 {{ padding: 0 !important; }}
            .print\:bg-transparent {{ background-color: transparent !important; }}
        }}
    </style>
</head>
<body class=""bg-gray-100 p-4 md:p-8 font-sans text-sm print:bg-white print:p-0"">
    <!-- Top Action Bar (Hidden in Print) -->
    <div class=""max-w-5xl mx-auto mb-6 bg-white p-4 rounded-lg shadow flex justify-end print:hidden"">
        <button onclick=""window.print()"" class=""bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded font-bold shadow-sm flex items-center gap-2 cursor-pointer transition-colors"">
            🖨️ Print / Save PDF
        </button>
    </div>
    
    <div class=""max-w-5xl mx-auto bg-white shadow-xl print:shadow-none print:w-full border border-gray-200"">
        <!-- HEADER -->
        <div class=""border-b-2 border-gray-800 p-6 flex items-start justify-between"">
            <div class=""w-1/3 flex items-center"">
                <img src=""{effectiveLogo}"" alt=""{companyName} Logo"" class=""h-16 object-contain max-w-[200px]"" onerror=""this.style.display='none'"" />
            </div>
            <div class=""w-1/3 text-center"">
                <h1 class=""text-lg font-bold text-gray-800 uppercase"">ESD Control Element Validation Report</h1>
                <p class=""text-xs text-gray-600 font-semibold mt-0.5"">ANSI/ESD S20.20-2021 &bull; ESD TR53</p>
                <p class=""text-xs text-gray-500 font-medium"">{siteName}</p>
            </div>
            <div class=""w-1/3 text-right text-sm"">
                <div class=""font-bold text-red-700 text-lg mb-1"">Report: {folio}</div>
                <div class=""flex justify-end gap-2 text-xs"">
                    <span class=""font-bold text-gray-700"">Execution Date:</span>
                    <span class=""text-gray-900"">{executionDateFormatted}</span>
                </div>
            </div>
        </div>

        <div class=""p-6 space-y-6"">
            <!-- SECTION 1: ELEMENT DATA & GENERAL INFO (2 COLUMNS) -->
            <div class=""grid grid-cols-2 gap-6"">
                <div>
                    <div class=""bg-gray-800 text-white font-bold px-2.5 py-1 uppercase text-xs tracking-wider"">Control Element Data</div>
                    <table class=""w-full text-sm border-collapse border border-gray-300"">
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">ID:</td><td class=""p-1.5 font-semibold"">{idElemento}</td></tr>
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Element:</td><td class=""p-1.5"">{elementEn}</td></tr>
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Manufacturer:</td><td class=""p-1.5"">{fabElem}</td></tr>
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Model:</td><td class=""p-1.5"">{modElem}</td></tr>
                        <tr><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Serial No.:</td><td class=""p-1.5 font-mono"">{snElem}</td></tr>
                    </table>
                </div>
                <div>
                    <div class=""bg-gray-800 text-white font-bold px-2.5 py-1 uppercase text-xs tracking-wider"">General Information</div>
                    <table class=""w-full text-sm border-collapse border border-gray-300 h-full"">
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Temperature:</td><td class=""p-1.5"">{temperatura}</td></tr>
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Humidity:</td><td class=""p-1.5"">{humedad}</td></tr>
                        <tr class=""border-b border-gray-300""><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Location:</td><td class=""p-1.5"">{ubicacion}</td></tr>
                        <tr><td class=""w-1/3 font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Magnitude:</td><td class=""p-1.5 font-semibold"">{magnitudEn}</td></tr>
                    </table>
                </div>
            </div>

            <!-- SECTION 2: TRACEABILITY (MEASUREMENT EQUIPMENT) -->
            <div>
                <div class=""bg-gray-800 text-white font-bold px-2.5 py-1 uppercase text-xs tracking-wider"">Traceability (Measurement Equipment)</div>
                <div class=""grid grid-cols-2 border-l border-t border-gray-300"">
                    <div class=""border-r border-b border-gray-300"">
                        <table class=""w-full text-sm"">
                            <tr class=""border-b border-gray-300""><td class=""font-bold bg-gray-100 p-1.5 w-1/3 border-r border-gray-300"">ID:</td><td class=""p-1.5 font-semibold"">{idEquipo}</td></tr>
                            <tr class=""border-b border-gray-300""><td class=""font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Equipment:</td><td class=""p-1.5"">{tipoEquipo}</td></tr>
                            <tr class=""border-b border-gray-300""><td class=""font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Cal. Report:</td><td class=""p-1.5 font-mono"">{reporteCal}</td></tr>
                            <tr><td class=""font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Resolution:</td><td class=""p-1.5"">{resolucion}</td></tr>
                        </table>
                    </div>
                    <div class=""border-b border-gray-300"">
                        <table class=""w-full text-sm"">
                            <tr class=""border-b border-gray-300""><td class=""font-bold bg-gray-100 p-1.5 w-1/3 border-r border-gray-300"">Manufacturer:</td><td class=""p-1.5"">{fabEq}</td></tr>
                            <tr class=""border-b border-gray-300""><td class=""font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Model:</td><td class=""p-1.5"">{modEq}</td></tr>
                            <tr class=""border-b border-gray-300""><td class=""font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Serial No.:</td><td class=""p-1.5 font-mono"">{snEq}</td></tr>
                            <tr><td class=""font-bold bg-gray-100 p-1.5 border-r border-gray-300"">Cal. Expiration:</td><td class=""p-1.5 font-semibold text-blue-800"">{fechaProxCal}</td></tr>
                        </table>
                    </div>
                </div>
            </div>

            <!-- SECTION 3: RESULTS (ANSI/ESD S20.20) -->
            <div>
                <div class=""bg-gray-800 text-white font-bold px-2.5 py-1 uppercase text-xs tracking-wider"">Results (ANSI/ESD S20.20)</div>
                <table class=""w-full text-sm border-collapse border border-gray-300 text-center"">
                    <tr class=""bg-gray-100 border-b border-gray-300 font-bold text-gray-800"">
                        <th class=""p-2 border-r border-gray-300 w-12"">No.</th>
                        <th class=""p-2 border-r border-gray-300"">Reference Limit</th>
                        <th class=""p-2 border-r border-gray-300 w-24"">Tolerance</th>
                        <th class=""p-2 border-r border-gray-300"">Obtained Result</th>
                        <th class=""p-2 border-r border-gray-300"">Test Method</th>
                        <th class=""p-2 border-r border-gray-300 w-24"">Unit</th>
                    </tr>
                    {tableRowsHtml}
                    <tr class=""border-t-2 border-gray-400 bg-gray-50"">
                        <td colspan=""3"" class=""p-2 font-bold text-right border-r border-gray-300"">Average / Final Result:</td>
                        <td class=""p-2 font-mono font-bold text-center border-r border-gray-300 bg-yellow-100"">{averageStr}</td>
                        <td colspan=""2"" class=""p-2""></td>
                    </tr>
                </table>
            </div>

            <!-- SECTION 4: PRODUCT IMAGE & COMMENTS (2 COLUMNS) -->
            <div class=""grid grid-cols-2 gap-6 min-h-64"">
                <div class=""border border-gray-300 flex flex-col items-center justify-center bg-gray-50 overflow-hidden relative rounded-sm"">
                    <div class=""absolute top-0 left-0 bg-gray-800 text-white font-bold px-2.5 py-1 uppercase text-xs w-full text-left z-10"">Product / Evidence Image</div>
                    <div class=""mt-8 flex-1 flex items-center justify-center p-3"">
                        {imgTag}
                    </div>
                </div>
                <div class=""border border-gray-300 flex flex-col relative rounded-sm bg-white"">
                    <div class=""bg-gray-800 text-white font-bold px-2.5 py-1 uppercase text-xs w-full"">Comments / Observations</div>
                    <div class=""p-3 text-sm text-gray-800 leading-relaxed"">{notas}</div>
                    <div class=""absolute bottom-3 right-3 text-xl font-bold {resultColorClass} px-3 py-1 bg-gray-50 rounded border border-gray-200"">
                        {resultTextEn}
                    </div>
                </div>
            </div>

            <!-- SECTION 5: SIGNATURE & APPROVAL -->
            <div class=""mt-10 mb-6 pt-6 [page-break-inside:avoid]"">
                <div class=""w-1/2 mx-auto text-center border-t border-gray-800 pt-2"">
                    <div class=""font-bold uppercase text-xs text-gray-600 mb-1"">APPROVED AND CERTIFIED BY:</div>
                    <div class=""text-center font-bold text-gray-900 text-sm"">{auditor}</div>
                    <div class=""text-center text-xs text-gray-500 mt-0.5"">Qualified ESD Program Auditor</div>
                </div>
            </div>
            
            <!-- DOCUMENT FOOTER CONTROL BAR -->
            <div class=""border-t-[3px] border-b-[3px] border-black mt-12 py-1.5 text-[11px] font-sans [page-break-inside:avoid]"">
                <div class=""flex justify-between items-end"">
                    <div class=""text-left leading-tight"">
                        <div class=""font-bold"">B_010_4_018_QRO_SP_Rev. A</div>
                        <div class=""text-gray-600"">ESD Control Element Validation Report Form</div>
                    </div>
                    <div class=""text-center leading-tight"">
                        <div>Date: {footerDateStr}</div>
                    </div>
                    <div class=""text-right leading-tight"">
                        <div>Ref. B_010_3_002_QRO_SP</div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</body>
</html>";

        return html;
    }
}
