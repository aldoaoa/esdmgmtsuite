using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace ESDSuite.Core.Helpers;

public class ReportLocale
{
    public string Title { get; set; } = "Reporte de Validación de Línea (ESD)";
    public string Subtitle { get; set; } = "Cumplimiento Integral ANSI/ESD S20.20 • IEC 61340-5-1";
    public string EmissionDate { get; set; } = "Fecha de Emisión:";
    public string LineOperation { get; set; } = "Línea / Operación:";
    public string AuditorResponsible { get; set; } = "Auditor Responsable:";
    public string CompanySite { get; set; } = "Empresa / Site:";
    public string ApplicableStandard { get; set; } = "Normativa Aplicable:";
    public string OverallStatus { get; set; } = "Estatus General:";
    public string StatusApproved { get; set; } = "APROBADA (PASS)";
    public string StatusNonCompliant { get; set; } = "NO APROBADA (FAIL)";
    public string TableHeader { get; set; } = "Desglose de Activos y Mediciones Actuales de Línea";
    public string TotalAssetsEvaluated { get; set; } = "{0} activos evaluados";
    public string ColNo { get; set; } = "No.";
    public string ColAssetId { get; set; } = "ID Elemento";
    public string ColType { get; set; } = "Tipo / Subtipo de Equipo";
    public string ColTestPoint { get; set; } = "Punto de Prueba";
    public string ColMeasurements { get; set; } = "Mediciones";
    public string ColLastTest { get; set; } = "Última Medición";
    public string ColNextDue { get; set; } = "Próx. Vencimiento";
    public string ColResult { get; set; } = "Resultado";
    public string PassBadge { get; set; } = "PASS";
    public string FailBadge { get; set; } = "FAIL";
    public string PendingBadge { get; set; } = "PENDIENTE";
    public string MainLabel { get; set; } = "Ppal";
    public string NoMeasurements { get; set; } = "Sin medición previa";
    public string NoAssets { get; set; } = "No se encontraron activos inventariados directamente en esta línea o no se registraron filas de medición.";
    public string CommentsTitle { get; set; } = "Comentarios / Conclusiones del Auditor:";
    public string DefaultCompliantComment { get; set; } = "La línea evaluada CUMPLE satisfactoriamente con los límites de resistencia y disipación electrostática de conformidad con la norma ANSI/ESD S20.20.";
    public string DefaultNonCompliantComment { get; set; } = "ATENCIÓN / NO APROBADA: Se detectaron activos fuera de los límites normativos establecidos por la norma ANSI/ESD S20.20 / TR53. Se requiere acción correctiva y re-certificación inmediata.";
    public string CertifiedBy { get; set; } = "CERTIFICADO Y AUDITADO POR:";
    public string AuditorTitle { get; set; } = "Coordinador / Auditor ESD Calificado";
    public string FooterSystem { get; set; } = "Sistema de Control y Trazabilidad ESD 360";
    public string FooterGenDate { get; set; } = "Fecha de Generación:";
    public string FooterOfficialDoc { get; set; } = "Documento Oficial de Auditoría";
    public string PrintButton { get; set; } = "Imprimir / Guardar PDF";
    public string FolioLabel { get; set; } = "Folio:";
}

public static class LineReportGenerator
{
    public static ReportLocale GetLocale(string? langCode)
    {
        string l = (langCode ?? "es").Trim().ToLower();
        return l switch
        {
            "en" => new ReportLocale
            {
                Title = "Line Validation Audit Report (ESD)",
                Subtitle = "Comprehensive Compliance ANSI/ESD S20.20 • IEC 61340-5-1",
                EmissionDate = "Emission Date:",
                LineOperation = "Line / Operation:",
                AuditorResponsible = "Responsible Auditor:",
                CompanySite = "Company / Facility:",
                ApplicableStandard = "Applicable Standard:",
                OverallStatus = "Overall Line Status:",
                StatusApproved = "APPROVED (PASS)",
                StatusNonCompliant = "NON-COMPLIANT (FAIL)",
                TableHeader = "Asset Breakdown & Current Line Measurements",
                TotalAssetsEvaluated = "{0} assets evaluated",
                ColNo = "No.",
                ColAssetId = "Asset ID",
                ColType = "Equipment Type / Subtype",
                ColTestPoint = "Test Point",
                ColMeasurements = "Measurements",
                ColLastTest = "Last Verification",
                ColNextDue = "Next Due Date",
                ColResult = "Result",
                PassBadge = "PASS",
                FailBadge = "FAIL",
                PendingBadge = "PENDING",
                MainLabel = "Main",
                NoMeasurements = "No prior measurement",
                NoAssets = "No inventory assets or test measurements found directly in this production line.",
                CommentsTitle = "Auditor Comments & Conclusions:",
                DefaultCompliantComment = "The evaluated line COMPLIES satisfactorily with the resistance and electrostatic dissipation requirements in accordance with ANSI/ESD S20.20.",
                DefaultNonCompliantComment = "WARNING / NON-COMPLIANT: Assets exceeding normative limits under ANSI/ESD S20.20 / TR53 were detected. Immediate corrective action and re-certification are required.",
                CertifiedBy = "CERTIFIED AND AUDITED BY:",
                AuditorTitle = "Qualified ESD Coordinator / Auditor",
                FooterSystem = "ESD 360 Control & Traceability System",
                FooterGenDate = "Generation Date:",
                FooterOfficialDoc = "Official Audit Document",
                PrintButton = "Print / Save PDF",
                FolioLabel = "Folio:"
            },
            "de" => new ReportLocale
            {
                Title = "Linienvalidierungs-Auditbericht (ESD)",
                Subtitle = "Vollständige Konformität ANSI/ESD S20.20 • IEC 61340-5-1",
                EmissionDate = "Ausstellungsdatum:",
                LineOperation = "Linie / Vorgang:",
                AuditorResponsible = "Verantwortlicher Auditor:",
                CompanySite = "Unternehmen / Standort:",
                ApplicableStandard = "Anwendbare Norm:",
                OverallStatus = "Gesamtstatus:",
                StatusApproved = "BESTANDEN (PASS)",
                StatusNonCompliant = "NICHT BESTANDEN (FAIL)",
                TableHeader = "Aufschlüsselung der Betriebsmittel und aktuellen Messwerte",
                TotalAssetsEvaluated = "{0} bewertete Betriebsmittel",
                ColNo = "Nr.",
                ColAssetId = "Betriebsmittel-ID",
                ColType = "Gerätetyp / Unterkategorie",
                ColTestPoint = "Prüfpunkt",
                ColMeasurements = "Messungen",
                ColLastTest = "Letzte Prüfung",
                ColNextDue = "Nächste Fälligkeit",
                ColResult = "Ergebnis",
                PassBadge = "PASS",
                FailBadge = "FAIL",
                PendingBadge = "AUSSTEHEND",
                MainLabel = "Haupt",
                NoMeasurements = "Keine vorherige Messung",
                NoAssets = "Keine Betriebsmittel oder Messungen für diese Linie gefunden.",
                CommentsTitle = "Kommentare und Schlussfolgerungen des Auditors:",
                DefaultCompliantComment = "Die geprüfte Fertigungslinie ERFÜLLT die elektrostatischen Ableitgrenzwerte gemäß der Norm ANSI/ESD S20.20 zufriedenstellend.",
                DefaultNonCompliantComment = "ACHTUNG / NICHT BESTANDEN: Es wurden Betriebsmittel außerhalb der durch ANSI/ESD S20.20 / TR53 festgelegten Grenzwerte festgestellt. Sofortige Korrekturmaßnahmen erforderlich.",
                CertifiedBy = "ZERTIFIZIERT UND GEPRÜFT DURCH:",
                AuditorTitle = "Qualifizierter ESD-Koordinator / Auditor",
                FooterSystem = "ESD 360 Kontroll- und Rückverfolgbarkeitssystem",
                FooterGenDate = "Erstellungsdatum:",
                FooterOfficialDoc = "Offizielles Auditdokument",
                PrintButton = "Drucken / Als PDF speichern",
                FolioLabel = "Folio:"
            },
            "it" => new ReportLocale
            {
                Title = "Rapporto Ufficiale di Validazione Linea (ESD)",
                Subtitle = "Conformità Integrale ANSI/ESD S20.20 • IEC 61340-5-1",
                EmissionDate = "Data di Emissione:",
                LineOperation = "Linea / Operazione:",
                AuditorResponsible = "Auditor Responsabile:",
                CompanySite = "Azienda / Stabilimento:",
                ApplicableStandard = "Normativa Applicabile:",
                OverallStatus = "Stato Generale:",
                StatusApproved = "APPROVATA (PASS)",
                StatusNonCompliant = "NON APPROVATA (FAIL)",
                TableHeader = "Dettaglio Asset e Misurazioni Attuali di Linea",
                TotalAssetsEvaluated = "{0} asset valutati",
                ColNo = "N.",
                ColAssetId = "ID Elemento",
                ColType = "Tipo / Sottotipo di Apparecchiatura",
                ColTestPoint = "Punto di Prova",
                ColMeasurements = "Misurazioni",
                ColLastTest = "Ultima Misurazione",
                ColNextDue = "Prossima Scadenza",
                ColResult = "Risultato",
                PassBadge = "PASS",
                FailBadge = "FAIL",
                PendingBadge = "IN ATTESA",
                MainLabel = "Princ",
                NoMeasurements = "Nessuna misurazione precedente",
                NoAssets = "Nessun asset inventariato o misurazione trovata per questa linea.",
                CommentsTitle = "Commenti e Conclusioni dell'Auditor:",
                DefaultCompliantComment = "La linea valutata È CONFORME ai limiti di resistenza e dissipazione elettrostatica secondo la norma ANSI/ESD S20.20.",
                DefaultNonCompliantComment = "ATTENZIONE / NON CONFORME: Sono stati rilevati asset che superano i limiti normativi stabiliti da ANSI/ESD S20.20 / TR53. Sono necessarie azioni correttive immediate.",
                CertifiedBy = "CERTIFICATO E VERIFICATO DA:",
                AuditorTitle = "Coordinatore / Auditor ESD Qualificato",
                FooterSystem = "Sistema di Controllo e Tracciabilità ESD 360",
                FooterGenDate = "Data di Generazione:",
                FooterOfficialDoc = "Documento Ufficiale di Audit",
                PrintButton = "Stampa / Salva PDF",
                FolioLabel = "Folio:"
            },
            "ro" => new ReportLocale
            {
                Title = "Raport Oficial de Validare a Liniei (ESD)",
                Subtitle = "Conformitate Integrală ANSI/ESD S20.20 • IEC 61340-5-1",
                EmissionDate = "Data Emiterii:",
                LineOperation = "Linie / Operațiune:",
                AuditorResponsible = "Auditor Responsabil:",
                CompanySite = "Companie / Fabrică:",
                ApplicableStandard = "Standard Aplicabil:",
                OverallStatus = "Statut General:",
                StatusApproved = "APROBAT (PASS)",
                StatusNonCompliant = "NEAPROBAT (FAIL)",
                TableHeader = "Defalcare Active și Măsurători Actuale ale Liniei",
                TotalAssetsEvaluated = "{0} active evaluate",
                ColNo = "Nr.",
                ColAssetId = "ID Element",
                ColType = "Tip / Subtip Echipament",
                ColTestPoint = "Punct de Testare",
                ColMeasurements = "Măsurători",
                ColLastTest = "Ultima Măsurare",
                ColNextDue = "Următoarea Scadență",
                ColResult = "Rezultat",
                PassBadge = "PASS",
                FailBadge = "FAIL",
                PendingBadge = "ÎN AȘTEPTARE",
                MainLabel = "Ppal",
                NoMeasurements = "Fără măsurare anterioară",
                NoAssets = "Nu s-au găsit active inventariate sau măsurători înregistrate pentru această linie.",
                CommentsTitle = "Comentarii și Concluzii ale Auditorului:",
                DefaultCompliantComment = "Linia evaluată RESPECTĂ cerințele și limitele de disipare electrostatică în conformitate cu standardul ANSI/ESD S20.20.",
                DefaultNonCompliantComment = "ATENȚIE / NECONFORM: Au fost detectate active în afara limitelor normative stabilite de ANSI/ESD S20.20 / TR53. Sunt necesare acțiuni corective imediate.",
                CertifiedBy = "CERTIFICAT ȘI AUDITAT DE:",
                AuditorTitle = "Coordonator / Auditor ESD Calificat",
                FooterSystem = "Sistem de Control și Trasabilitate ESD 360",
                FooterGenDate = "Data Generării:",
                FooterOfficialDoc = "Document Oficial de Audit",
                PrintButton = "Imprimare / Salvare PDF",
                FolioLabel = "Folio:"
            },
            "zh" => new ReportLocale
            {
                Title = "生产线防静电官方验证报告 (ESD)",
                Subtitle = "全面符合 ANSI/ESD S20.20 • IEC 61340-5-1 标准",
                EmissionDate = "签发日期:",
                LineOperation = "生产线 / 工序:",
                AuditorResponsible = "责任审核员:",
                CompanySite = "公司 / 厂区:",
                ApplicableStandard = "适用标准:",
                OverallStatus = "综合状态:",
                StatusApproved = "合格通过 (PASS)",
                StatusNonCompliant = "不合格 (FAIL)",
                TableHeader = "受检设备资产与实时测量数据清单",
                TotalAssetsEvaluated = "已评估 {0} 项资产",
                ColNo = "序号",
                ColAssetId = "设备编号",
                ColType = "设备类型 / 子分类",
                ColTestPoint = "测试点位置",
                ColMeasurements = "测量数据",
                ColLastTest = "最近测试日期",
                ColNextDue = "下次到期日期",
                ColResult = "判定结果",
                PassBadge = "PASS",
                FailBadge = "FAIL",
                PendingBadge = "待检",
                MainLabel = "主测点",
                NoMeasurements = "无历史测量数据",
                NoAssets = "未在该生产线上检索到直接关联的设备资产或测试记录。",
                CommentsTitle = "审核员评语与结论:",
                DefaultCompliantComment = "评估的生产线完全符合 ANSI/ESD S20.20 标准所规定的静电消除与电阻限值要求。",
                DefaultNonCompliantComment = "警告 / 不合格：检测到存在超出 ANSI/ESD S20.20 / TR53 标准限值的设备资产。需立即采取纠正措施并重新认证。",
                CertifiedBy = "审核与认证人:",
                AuditorTitle = "注册 ESD 协调员 / 资深审核员",
                FooterSystem = "ESD 360 智能静电管控与追溯系统",
                FooterGenDate = "生成时间:",
                FooterOfficialDoc = "官方防静电审核合规文件",
                PrintButton = "打印 / 保存为 PDF",
                FolioLabel = "报告单号:"
            },
            _ => new ReportLocale()
        };
    }

    public static string GenerateLineReportHtml(
        string linea,
        JsonArray rows,
        string auditor,
        string comentarios,
        string uniqueFolio,
        string companyName,
        string siteName,
        string? logoUrl = null,
        DateTime? emissionDate = null,
        string? lang = "es")
    {
        var t = GetLocale(lang);
        var emDate = emissionDate ?? DateTime.UtcNow;
        string fechaEmision = emDate.ToString("dd-MMM-yyyy HH:mm UTC");
        string fechaPie = emDate.ToString("yyyy/MM/dd HH:mm");

        string effectiveLogo = !string.IsNullOrWhiteSpace(logoUrl)
            ? logoUrl
            : "/images/esd360-logo.png";

        bool hasNonCompliant = false;
        var sbRows = new StringBuilder();
        int idx = 1;

        foreach (var node in rows)
        {
            if (node is not JsonObject r) continue;

            string categoria = r["Categoría"]?.ToString() ?? r["category"]?.ToString() ?? "N/D";
            string idElem = r["ID / Nombre"]?.ToString() ?? r["custom_id"]?.ToString() ?? r["asset_id"]?.ToString() ?? r["name"]?.ToString() ?? "N/D";
            string clasif = r["Clasificación"]?.ToString() ?? r["sub_category"]?.ToString() ?? r["subtype"]?.ToString() ?? r["classification"]?.ToString() ?? "";
            string puntoContacto = r["punto_contacto"]?.ToString() ?? r["point"]?.ToString() ?? "";
            
            string ultimaVal = r["Última Medición"]?.ToString() ?? r["last_verification"]?.ToString() ?? r["last_tested"]?.ToString() ?? "N/D";
            if (ultimaVal.Contains('T')) ultimaVal = ultimaVal.Split('T')[0];
            
            string vencimiento = r["Próximo Vencimiento"]?.ToString() ?? r["next_verification"]?.ToString() ?? "N/D";
            if (vencimiento.Contains('T')) vencimiento = vencimiento.Split('T')[0];

            string estatusRaw = (r["Estatus"]?.ToString() ?? r["status_schedule"]?.ToString() ?? r["status"]?.ToString() ?? "PENDIENTE").ToUpper();
            string estatusLimpio = estatusRaw.Replace("🟢", "").Replace("🔴", "").Replace("🟡", "").Trim();

            bool isPass = estatusLimpio.Contains("VIGENTE") || estatusLimpio.Contains("PASS") || estatusLimpio.Contains("PASA") || estatusLimpio.Contains("COMPLIANT");
            bool isFail = estatusLimpio.Contains("VENCIDO") || estatusLimpio.Contains("FAIL") || estatusLimpio.Contains("FALLA") || estatusLimpio.Contains("OVERDUE") || estatusLimpio.Contains("NON-COMPLIANT");

            if (isFail)
            {
                hasNonCompliant = true;
            }

            string colorBadge = isPass
                ? "bg-emerald-100 text-emerald-800 border-emerald-300"
                : isFail
                ? "bg-red-100 text-red-800 border-red-300"
                : "bg-amber-100 text-amber-800 border-amber-300";

            string badgeText = isPass ? t.PassBadge : isFail ? t.FailBadge : t.PendingBadge;

            string tipoDisplay = string.IsNullOrWhiteSpace(clasif) || clasif == categoria
                ? categoria
                : $"{categoria} &bull; <span class=\"text-gray-500 text-[11px]\">{clasif}</span>";

            string medicionDisplay = FormatDetailedMeasurements(r, t);

            string puntoDisplay = string.IsNullOrWhiteSpace(puntoContacto)
                ? "<span class=\"text-gray-400 italic text-[11px]\">General</span>"
                : $"<span class=\"text-gray-700 text-xs font-medium\">{puntoContacto}</span>";

            sbRows.Append($@"
            <tr class=""border-b border-gray-200 print:border-black hover:bg-blue-50/40 transition-colors"">
                <td class=""border-r border-gray-200 p-2.5 print:border-black font-semibold text-center text-gray-500 text-xs align-top"">{idx++}</td>
                <td class=""border-r border-gray-200 p-2.5 font-bold text-left print:border-black text-blue-950 font-mono text-xs align-top"">{idElem}</td>
                <td class=""border-r border-gray-200 p-2.5 text-left print:border-black text-xs align-top"">{tipoDisplay}</td>
                <td class=""border-r border-gray-200 p-2.5 text-left print:border-black align-top"">{puntoDisplay}</td>
                <td class=""border-r border-gray-200 p-2.5 text-left print:border-black text-xs font-mono align-top"">{medicionDisplay}</td>
                <td class=""border-r border-gray-200 p-2.5 font-mono text-gray-700 text-center print:border-black text-xs align-top"">{ultimaVal}</td>
                <td class=""border-r border-gray-200 p-2.5 font-mono text-center print:border-black text-xs align-top"">{vencimiento}</td>
                <td class=""p-2.5 text-center align-top"">
                    <span class=""inline-block px-2.5 py-0.5 rounded-full text-[11px] font-bold border {colorBadge} print:border-black"">
                        {badgeText}
                    </span>
                </td>
            </tr>");
        }

        if (rows.Count == 0)
        {
            sbRows.Append($@"
            <tr class=""text-center border-b border-gray-300"">
                <td colspan=""8"" class=""p-6 text-gray-500 italic bg-gray-50"">{t.NoAssets}</td>
            </tr>");
        }

        // Overall Line Status
        string overallBadgeText = hasNonCompliant ? t.StatusNonCompliant : t.StatusApproved;
        string overallBadgeClass = hasNonCompliant ? "bg-red-100 text-red-800 border-red-300" : "bg-emerald-100 text-emerald-800 border-emerald-300";

        // Effective Conclusions
        string conclusionText;
        if (!string.IsNullOrWhiteSpace(comentarios))
        {
            conclusionText = comentarios;
        }
        else
        {
            conclusionText = hasNonCompliant ? t.DefaultNonCompliantComment : t.DefaultCompliantComment;
        }

        string totalEvaluatedText = string.Format(t.TotalAssetsEvaluated, rows.Count);

        string html = $@"<!DOCTYPE html>
<html lang=""{lang}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{uniqueFolio} - {t.Title}</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <style>
        @media print {{
            body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
            .no-print {{ display: none !important; }}
        }}
    </style>
</head>
<body class=""bg-gray-100 p-4 md:p-8 font-sans text-sm text-gray-800 print:bg-white print:p-0"">
    <div class=""max-w-6xl mx-auto mb-6 bg-white p-4 rounded-xl shadow flex justify-between items-center no-print border border-gray-200"">
        <div class=""flex items-center gap-3"">
            <span class=""text-2xl"">📋</span>
            <div>
                <h3 class=""font-bold text-gray-800 text-base"">{t.Title}</h3>
                <span class=""text-xs text-gray-500 font-mono"">{t.FolioLabel} {uniqueFolio}</span>
            </div>
        </div>
        <div class=""flex gap-2"">
            <button onclick=""window.print()"" class=""bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-bold shadow-sm flex items-center gap-2 text-sm transition-all"">
                <span>🖨️</span> {t.PrintButton}
            </button>
        </div>
    </div>

    <div class=""max-w-6xl mx-auto bg-white shadow-xl rounded-lg overflow-hidden print:shadow-none print:w-full print:border print:border-black"">
        <!-- HEADER WITH DYNAMIC CORPORATE LOGO (CENTERED IN UPPER-LEFT 1/3) -->
        <div class=""border-b-2 border-gray-800 p-6 flex items-center justify-between print:border-black bg-slate-50 print:bg-transparent min-h-[110px]"">
            <div class=""w-1/3 flex items-center justify-center p-2 h-24"">
                <img src=""{effectiveLogo}"" alt=""Logo Empresa"" class=""max-h-20 max-w-full w-auto object-contain mx-auto"" onerror=""this.src='/images/esd360-logo.png'"" />
            </div>
            <div class=""w-1/3 text-center px-2"">
                <h1 class=""text-base md:text-lg font-extrabold text-gray-900 tracking-tight uppercase"">{t.Title}</h1>
                <p class=""text-xs text-gray-600 font-semibold"">{t.Subtitle}</p>
                <div class=""text-[11px] text-gray-500 mt-1 font-medium"">{companyName} &bull; {siteName}</div>
            </div>
            <div class=""w-1/3 text-right text-xs pl-2"">
                <div class=""font-extrabold text-red-700 text-base font-mono mb-1"">{uniqueFolio}</div>
                <div class=""flex justify-end gap-1 text-gray-600 mb-1"">
                    <span class=""font-bold"">{t.EmissionDate}</span>
                    <span>{fechaEmision}</span>
                </div>
            </div>
        </div>

        <div class=""p-6 space-y-6"">
            <!-- GENERAL INFO PANEL WITH OVERALL LINE STATUS -->
            <div class=""bg-gray-50 p-4 border border-gray-300 rounded-lg print:border-black print:bg-transparent"">
                <div class=""grid grid-cols-2 md:grid-cols-5 gap-3 text-xs md:text-sm items-center"">
                    <div><span class=""font-bold text-[#003366] block text-[11px] uppercase tracking-wider"">{t.LineOperation}</span> <span class=""text-sm font-extrabold text-gray-900"">{linea}</span></div>
                    <div><span class=""font-bold text-[#003366] block text-[11px] uppercase tracking-wider"">{t.AuditorResponsible}</span> <span class=""font-bold text-gray-800"">{auditor}</span></div>
                    <div><span class=""font-bold text-[#003366] block text-[11px] uppercase tracking-wider"">{t.CompanySite}</span> <span class=""text-gray-700"">{companyName} ({siteName})</span></div>
                    <div><span class=""font-bold text-[#003366] block text-[11px] uppercase tracking-wider"">{t.ApplicableStandard}</span> <span class=""text-gray-700 font-semibold"">ANSI/ESD S20.20 / TR53</span></div>
                    <div>
                        <span class=""font-bold text-[#003366] block text-[11px] uppercase tracking-wider"">{t.OverallStatus}</span>
                        <span class=""inline-block px-2.5 py-1 rounded-md text-xs font-extrabold border {overallBadgeClass} print:border-black mt-0.5"">
                            {overallBadgeText}
                        </span>
                    </div>
                </div>
            </div>

            <!-- ASSETS TABLE WITH DETAILED MEDICIONES (MAIN & ADDITIONAL) -->
            <div>
                <div class=""bg-[#003366] text-white font-bold px-3.5 py-2 uppercase text-xs rounded-t print:bg-black flex justify-between items-center"">
                    <span>{t.TableHeader}</span>
                    <span class=""text-[11px] font-normal lowercase opacity-90"">{totalEvaluatedText}</span>
                </div>
                <table class=""w-full text-xs md:text-sm border-collapse border border-gray-300 print:border-black"">
                    <thead>
                        <tr class=""bg-gray-200 border-b border-gray-300 print:bg-transparent print:border-black font-bold text-gray-700 text-xs"">
                            <th class=""p-2.5 border-r border-gray-300 print:border-black w-10 text-center"">{t.ColNo}</th>
                            <th class=""p-2.5 border-r border-gray-300 print:border-black text-left"">{t.ColAssetId}</th>
                            <th class=""p-2.5 border-r border-gray-300 print:border-black text-left"">{t.ColType}</th>
                            <th class=""p-2.5 border-r border-gray-300 print:border-black text-left"">{t.ColTestPoint}</th>
                            <th class=""p-2.5 border-r border-gray-300 print:border-black text-left"">{t.ColMeasurements}</th>
                            <th class=""p-2.5 border-r border-gray-300 print:border-black text-center"">{t.ColLastTest}</th>
                            <th class=""p-2.5 border-r border-gray-300 print:border-black text-center"">{t.ColNextDue}</th>
                            <th class=""p-2.5 text-center"">{t.ColResult}</th>
                        </tr>
                    </thead>
                    <tbody>
                        {sbRows}
                    </tbody>
                </table>
            </div>

            <!-- COMMENTS & CONCLUSIONS (DYNAMICALLY MATCHING RESULTS) -->
            <div class=""border border-gray-300 p-4 bg-gray-50 rounded-lg print:border-black print:bg-transparent"">
                <div class=""font-bold text-[#003366] text-xs uppercase mb-1.5 print:text-black flex items-center gap-1.5"">
                    <span>📝</span> {t.CommentsTitle}
                </div>
                <div class=""text-xs md:text-sm text-gray-800 leading-relaxed italic whitespace-pre-line"">{conclusionText}</div>
            </div>

            <!-- SIGNATURE SECTION -->
            <div class=""mt-10 mb-4 pt-4 [page-break-inside:avoid]"">
                <div class=""w-64 mx-auto text-center border-t-2 border-gray-800 pt-2 print:border-black"">
                    <div class=""font-bold uppercase text-xs text-gray-500 mb-1"">{t.CertifiedBy}</div>
                    <div class=""text-center font-extrabold text-sm text-gray-800 print:text-black"">{auditor}</div>
                    <div class=""text-xs text-gray-500 mt-0.5"">{t.AuditorTitle}</div>
                </div>
            </div>
            
            <!-- MANDATORY CORPORATE FOOTER WITH UNIQUE ID -->
            <div class=""border-t-[2px] border-b-[2px] border-black mt-8 py-2 text-[11px] font-sans [page-break-inside:avoid] bg-slate-50 print:bg-transparent"">
                <div class=""flex justify-between items-center px-2"">
                    <div class=""text-left leading-tight"">
                        <div class=""font-bold text-gray-800"">{companyName} &bull; {siteName}</div>
                        <div class=""text-[10px] text-gray-500"">{t.FooterSystem}</div>
                    </div>
                    <div class=""text-center leading-tight font-mono text-[10px] text-gray-600"">
                        <div>{t.FooterGenDate} {fechaPie}</div>
                    </div>
                    <div class=""text-right leading-tight"">
                        <div class=""font-bold font-mono text-red-800 text-[11px]"">ID: {uniqueFolio}</div>
                        <div class=""text-[10px] text-gray-500"">{t.FooterOfficialDoc}</div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    private static string FormatDetailedMeasurements(JsonObject r, ReportLocale t)
    {
        var lines = new List<string>();

        // 1. Medición Principal (Resistencia, Voltaje, Ionizador)
        var mainParts = new List<string>();
        if (r["resistance_value"] != null && double.TryParse(r["resistance_value"]?.ToString(), out double resVal))
        {
            mainParts.Add($"<span class=\"font-bold text-blue-950\">{FormatResistance(resVal)}</span>");
        }
        else if (r["resistencia"] != null && double.TryParse(r["resistencia"]?.ToString(), out double resVal2))
        {
            mainParts.Add($"<span class=\"font-bold text-blue-950\">{FormatResistance(resVal2)}</span>");
        }

        if (r["static_field_value"] != null && double.TryParse(r["static_field_value"]?.ToString(), out double voltVal))
        {
            mainParts.Add($"<span class=\"text-purple-800 font-semibold\">{voltVal:0.#} V</span>");
        }
        else if (r["voltaje"] != null && double.TryParse(r["voltaje"]?.ToString(), out double voltVal2))
        {
            mainParts.Add($"<span class=\"text-purple-800 font-semibold\">{voltVal2:0.#} V</span>");
        }

        if (r["tiempo_descarga"] != null && double.TryParse(r["tiempo_descarga"]?.ToString(), out double decay))
        {
            string balStr = r["voltaje_balance"] != null ? $" / {r["voltaje_balance"]}V" : "";
            mainParts.Add($"<span class=\"text-cyan-800 font-semibold\">{decay:0.#}s{balStr}</span>");
        }

        if (mainParts.Count > 0)
        {
            lines.Add($"<div><span class=\"font-semibold text-slate-700\">{t.MainLabel}:</span> {string.Join(" &bull; ", mainParts)}</div>");
        }

        // 2. Mediciones Adicionales / Puntos Extra Detallados
        if (r["extra_points"] is JsonArray extra && extra.Count > 0)
        {
            int pNum = 2;
            foreach (var pNode in extra)
            {
                if (pNode is JsonObject pObj)
                {
                    string tipo = pObj["tipo"]?.ToString()?.ToLower() ?? "resistencia";
                    string valorRaw = pObj["valor"]?.ToString() ?? "";
                    string coment = pObj["comentario"]?.ToString()?.Trim() ?? "";

                    string valFormatted = valorRaw;
                    if (tipo == "resistencia" && double.TryParse(valorRaw, out double extraRes))
                    {
                        valFormatted = $"<span class=\"font-semibold text-blue-900\">{FormatResistance(extraRes)}</span>";
                    }
                    else if (tipo == "voltaje" && double.TryParse(valorRaw, out double extraVolt))
                    {
                        valFormatted = $"<span class=\"font-semibold text-purple-800\">{extraVolt:0.#} V</span>";
                    }

                    string labelDesc = !string.IsNullOrEmpty(coment) ? $"P{pNum} ({coment})" : $"P{pNum}";
                    lines.Add($"<div class=\"text-[11px] text-slate-600 pl-1 border-l-2 border-slate-300\">{labelDesc}: {valFormatted}</div>");
                    pNum++;
                }
            }
        }

        if (lines.Count > 0)
        {
            return $"<div class=\"space-y-0.5\">{string.Join("", lines)}</div>";
        }

        string rawVal = r["valor_medido"]?.ToString() ?? r["last_value"]?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(rawVal))
        {
            return $"<span class=\"font-medium text-gray-800\">{rawVal}</span>";
        }

        return $"<span class=\"text-gray-400 italic text-[11px]\">{t.NoMeasurements}</span>";
    }

    private static string FormatResistance(double res)
    {
        if (res >= 1e9) return $"{(res / 1e9):0.##} GΩ";
        if (res >= 1e6) return $"{(res / 1e6):0.##} MΩ";
        if (res >= 1e3) return $"{(res / 1e3):0.##} kΩ";
        if (res < 1) return $"{res:0.###} Ω";
        return $"{res:0.##} Ω";
    }
}

