using System;
using System.Text;
using System.Text.Json.Nodes;

namespace ESDSuite.Core.Helpers;

public static class LineReportGenerator
{
    public static string GenerateLineReportHtml(
        string linea,
        JsonArray rows,
        string auditor,
        string comentarios,
        string uniqueFolio,
        string companyName,
        string siteName,
        string? logoUrl = null,
        DateTime? emissionDate = null)
    {
        var emDate = emissionDate ?? DateTime.UtcNow;
        string fechaEmision = emDate.ToString("dd-MMM-yyyy HH:mm UTC");
        string fechaPie = emDate.ToString("yyyy/MM/dd HH:mm");

        string effectiveLogo = !string.IsNullOrWhiteSpace(logoUrl)
            ? logoUrl
            : "/images/esd360-logo.png";

        var sbRows = new StringBuilder();
        int idx = 1;

        foreach (var node in rows)
        {
            if (node is not JsonObject r) continue;

            string categoria = r["Categoría"]?.ToString() ?? r["category"]?.ToString() ?? "N/D";
            string idElem = r["ID / Nombre"]?.ToString() ?? r["custom_id"]?.ToString() ?? r["name"]?.ToString() ?? "N/D";
            string clasif = r["Clasificación"]?.ToString() ?? r["sub_category"]?.ToString() ?? r["subtype"]?.ToString() ?? r["classification"]?.ToString() ?? "";
            string ultimaVal = r["Última Medición"]?.ToString() ?? r["last_verification"]?.ToString() ?? r["last_tested"]?.ToString() ?? "N/D";
            if (ultimaVal.Contains('T')) ultimaVal = ultimaVal.Split('T')[0];
            
            string vencimiento = r["Próximo Vencimiento"]?.ToString() ?? r["next_verification"]?.ToString() ?? "N/D";
            if (vencimiento.Contains('T')) vencimiento = vencimiento.Split('T')[0];

            string estatusRaw = (r["Estatus"]?.ToString() ?? r["status_schedule"]?.ToString() ?? r["status"]?.ToString() ?? "PENDIENTE").ToUpper();

            string estatusLimpio = estatusRaw.Replace("🟢", "").Replace("🔴", "").Replace("🟡", "").Trim();
            string colorTxt = (estatusLimpio.Contains("VIGENTE") || estatusLimpio.Contains("PASS") || estatusLimpio.Contains("PASA") || estatusLimpio.Contains("COMPLIANT"))
                ? "text-emerald-600 font-bold"
                : (estatusLimpio.Contains("VENCIDO") || estatusLimpio.Contains("FAIL") || estatusLimpio.Contains("FALLA") || estatusLimpio.Contains("OVERDUE"))
                ? "text-red-600 font-bold"
                : "text-amber-600 font-bold";

            string tipoDisplay = string.IsNullOrWhiteSpace(clasif) || clasif == categoria
                ? categoria
                : $"{categoria} - {clasif}";

            sbRows.Append($@"
            <tr class=""text-center border-b border-gray-300 print:border-black hover:bg-gray-50"">
                <td class=""border-r border-gray-300 p-2 print:border-black font-medium"">{idx++}</td>
                <td class=""border-r border-gray-300 p-2 font-bold text-left print:border-black text-blue-900"">{idElem}</td>
                <td class=""border-r border-gray-300 p-2 text-left print:border-black"">{tipoDisplay}</td>
                <td class=""border-r border-gray-300 p-2 font-mono text-gray-700 print:border-black text-xs"">{ultimaVal}</td>
                <td class=""border-r border-gray-300 p-2 font-mono print:border-black text-xs"">{vencimiento}</td>
                <td class=""p-2 {colorTxt}"">{estatusLimpio}</td>
            </tr>");
        }

        if (rows.Count == 0)
        {
            sbRows.Append(@"
            <tr class=""text-center border-b border-gray-300"">
                <td colspan=""6"" class=""p-4 text-gray-500 italic"">No se encontraron activos inventariados directamente en esta línea o no se registraron filas adicionales.</td>
            </tr>");
        }

        string html = $@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{uniqueFolio} - Reporte Oficial de Validación</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <style>
        @media print {{
            body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
            .no-print {{ display: none !important; }}
        }}
    </style>
</head>
<body class=""bg-gray-100 p-4 md:p-8 font-sans text-sm text-gray-800 print:bg-white print:p-0"">
    <div class=""max-w-5xl mx-auto mb-6 bg-white p-4 rounded-xl shadow flex justify-between items-center no-print border border-gray-200"">
        <div class=""flex items-center gap-3"">
            <span class=""text-xl"">📋</span>
            <div>
                <h3 class=""font-bold text-gray-800 text-base"">Reporte Oficial de Validación de Línea</h3>
                <span class=""text-xs text-gray-500 font-mono"">Folio: {uniqueFolio}</span>
            </div>
        </div>
        <div class=""flex gap-2"">
            <button onclick=""window.print()"" class=""bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-bold shadow-sm flex items-center gap-2 text-sm transition-all"">
                <span>🖨️</span> Imprimir / Guardar PDF
            </button>
        </div>
    </div>

    <div class=""max-w-5xl mx-auto bg-white shadow-xl rounded-lg overflow-hidden print:shadow-none print:w-full print:border print:border-black"">
        <!-- HEADER WITH DYNAMIC LOGO -->
        <div class=""border-b-2 border-gray-800 p-6 flex items-center justify-between print:border-black bg-slate-50 print:bg-transparent"">
            <div class=""w-1/3 flex items-center"">
                <img src=""{effectiveLogo}"" alt=""Logo Empresa"" class=""max-h-16 max-w-full object-contain"" onerror=""this.src='/images/esd360-logo.png'"" />
            </div>
            <div class=""w-1/3 text-center"">
                <h1 class=""text-base md:text-lg font-extrabold text-gray-900 tracking-tight uppercase"">Reporte de Validación de Línea (ESD)</h1>
                <p class=""text-xs text-gray-600 font-semibold"">Cumplimiento Integral ANSI/ESD S20.20 &bull; IEC 61340-5-1</p>
                <div class=""text-[11px] text-gray-500 mt-1"">{companyName} &bull; {siteName}</div>
            </div>
            <div class=""w-1/3 text-right text-xs"">
                <div class=""font-extrabold text-red-700 text-base font-mono mb-1"">{uniqueFolio}</div>
                <div class=""flex justify-end gap-1 text-gray-600 mb-1"">
                    <span class=""font-bold"">Fecha de Emisión:</span>
                    <span>{fechaEmision}</span>
                </div>
            </div>
        </div>

        <div class=""p-6 space-y-6"">
            <!-- GENERAL INFO PANEL -->
            <div class=""bg-gray-50 p-4 border border-gray-300 rounded-lg print:border-black print:bg-transparent"">
                <div class=""grid grid-cols-2 gap-4 text-xs md:text-sm"">
                    <div><span class=""font-bold text-[#003366]"">Línea / Operación Evaluada:</span> <span class=""text-base font-extrabold text-gray-900 ml-1"">{linea}</span></div>
                    <div><span class=""font-bold text-[#003366]"">Auditor Responsable:</span> <span class=""font-bold text-gray-800 ml-1"">{auditor}</span></div>
                    <div><span class=""font-bold text-[#003366]"">Empresa / Site:</span> <span class=""text-gray-700 ml-1"">{companyName} ({siteName})</span></div>
                    <div><span class=""font-bold text-[#003366]"">Normativa Aplicable:</span> <span class=""text-gray-700 ml-1"">ANSI/ESD S20.20-2021 / TR53</span></div>
                </div>
            </div>

            <!-- ASSETS TABLE -->
            <div>
                <div class=""bg-[#003366] text-white font-bold px-3 py-1.5 uppercase text-xs rounded-t print:bg-black flex justify-between items-center"">
                    <span>Desglose de Activos y Puntos de Control Operativos</span>
                    <span class=""text-[11px] font-normal lowercase opacity-90"">{rows.Count} activos evaluados</span>
                </div>
                <table class=""w-full text-xs md:text-sm border-collapse border border-gray-300 print:border-black"">
                    <thead>
                        <tr class=""bg-gray-200 border-b border-gray-300 print:bg-transparent print:border-black font-bold text-gray-700 text-xs"">
                            <th class=""p-2 border-r border-gray-300 print:border-black w-10 text-center"">No.</th>
                            <th class=""p-2 border-r border-gray-300 print:border-black text-left"">ID Elemento</th>
                            <th class=""p-2 border-r border-gray-300 print:border-black text-left"">Tipo / Subtipo de Equipo</th>
                            <th class=""p-2 border-r border-gray-300 print:border-black text-center"">Última Validación</th>
                            <th class=""p-2 border-r border-gray-300 print:border-black text-center"">Próx. Vencimiento</th>
                            <th class=""p-2 text-center"">Estatus</th>
                        </tr>
                    </thead>
                    <tbody>
                        {sbRows}
                    </tbody>
                </table>
            </div>

            <!-- COMMENTS & CONCLUSIONS -->
            <div class=""border border-gray-300 p-4 bg-gray-50 rounded-lg print:border-black print:bg-transparent"">
                <div class=""font-bold text-[#003366] text-xs uppercase mb-1.5 print:text-black flex items-center gap-1.5"">
                    <span>📝</span> Comentarios / Conclusiones del Auditor:
                </div>
                <div class=""text-xs md:text-sm text-gray-700 leading-relaxed italic whitespace-pre-line"">{(string.IsNullOrWhiteSpace(comentarios) ? "La línea evaluada cumple satisfactoriamente con los límites de resistencia y disipación electrostática de conformidad con la norma ANSI/ESD S20.20." : comentarios)}</div>
            </div>

            <!-- SIGNATURE SECTION -->
            <div class=""mt-12 mb-6 pt-6 [page-break-inside:avoid]"">
                <div class=""w-64 mx-auto text-center border-t-2 border-gray-800 pt-2 print:border-black"">
                    <div class=""font-bold uppercase text-xs text-gray-500 mb-1"">CERTIFICADO Y AUDITADO POR:</div>
                    <div class=""text-center font-extrabold text-sm text-gray-800 print:text-black"">{auditor}</div>
                    <div class=""text-xs text-gray-500 mt-0.5"">Coordinador / Auditor ESD Calificado</div>
                </div>
            </div>
            
            <!-- MANDATORY CORPORATE FOOTER WITH UNIQUE ID -->
            <div class=""border-t-[2px] border-b-[2px] border-black mt-12 py-2 text-[11px] font-sans [page-break-inside:avoid] bg-slate-50 print:bg-transparent"">
                <div class=""flex justify-between items-center px-2"">
                    <div class=""text-left leading-tight"">
                        <div class=""font-bold text-gray-800"">{companyName} &bull; {siteName}</div>
                        <div class=""text-[10px] text-gray-500"">Sistema de Control y Trazabilidad ESD 360</div>
                    </div>
                    <div class=""text-center leading-tight font-mono text-[10px] text-gray-600"">
                        <div>Fecha de Generación: {fechaPie}</div>
                    </div>
                    <div class=""text-right leading-tight"">
                        <div class=""font-bold font-mono text-red-800 text-[11px]"">ID: {uniqueFolio}</div>
                        <div class=""text-[10px] text-gray-500"">Documento Oficial de Auditoría</div>
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
