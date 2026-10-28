using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ESDSuite.Core.Models;
using ESDSuite.Core.Services;
using ESDSuite.Services.Supabase;

namespace ESDSuite.Services.Email;

public class ResendEmailService : IEmailNotificationService
{
    private readonly HttpClient _http;
    private readonly ResendConfig _config;
    private readonly SupabaseService _supabase;

    public ResendEmailService(HttpClient http, ResendConfig config, SupabaseService supabase)
    {
        _http = http;
        _config = config;
        _supabase = supabase;
    }

    public async Task<(bool success, string messageId, string? error)> SendEmailAsync(
        string toEmail, 
        string? toName, 
        string subject, 
        string htmlContent, 
        string? plainText = null,
        string emailType = "GENERIC",
        string? relatedId = null,
        string? siteId = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return (false, "", "El correo destinatario es requerido.");
        }

        string fromHeader = string.IsNullOrWhiteSpace(_config.FromName)
            ? _config.FromEmail
            : $"{_config.FromName} <{_config.FromEmail}>";

        var payload = new JsonObject
        {
            ["from"] = fromHeader,
            ["to"] = new JsonArray { toEmail.Trim() },
            ["subject"] = subject,
            ["html"] = htmlContent
        };

        if (!string.IsNullOrWhiteSpace(plainText))
        {
            payload["text"] = plainText;
        }

        string messageId = "";
        string? error = null;
        bool isSuccess = false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey.Trim());
            request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            string respContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var resNode = JsonNode.Parse(respContent);
                messageId = resNode?["id"]?.ToString() ?? "";
                isSuccess = true;
            }
            else
            {
                error = $"Resend HTTP {response.StatusCode}: {respContent}";
                Console.WriteLine($"[Resend API Error]: {error}");
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Console.WriteLine($"[Resend API Exception]: {ex.Message}");
        }

        // Persist audit log 100% in Supabase
        _ = Task.Run(async () =>
        {
            try
            {
                var log = new EmailLogRecord
                {
                    ToEmail = toEmail.Trim(),
                    ToName = toName,
                    Subject = subject,
                    EmailType = emailType,
                    RelatedId = relatedId,
                    Status = isSuccess ? "SENT" : "ERROR",
                    MessageId = messageId,
                    ErrorMessage = error,
                    SiteId = siteId,
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };
                await _supabase.InsertEmailLogAsync(log);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Email Log Error]: {ex.Message}");
            }
        });

        return (isSuccess, messageId, error);
    }

    public async Task<(bool success, string messageId, string? error)> SendFindingAssignedNotificationAsync(FindingEmailNotificationModel model)
    {
        if (string.IsNullOrWhiteSpace(model.AssigneeEmail))
        {
            return (false, "", "No se especificó correo para el responsable asignado.");
        }

        string subject = $"🔔 [Nuevo Hallazgo {model.TrackingCode}] {model.Title} - Severidad {model.Severity}";
        string html = BuildFindingAssignedHtml(model);
        string plain = $"esd360 - Nuevo Hallazgo Asignado: {model.TrackingCode}\nTítulo: {model.Title}\nSeveridad: {model.Severity}\nUbicación: {model.Location}\nFecha Límite: {model.DueDate}\nAccede a https://esd360.app para gestionarlo.";

        return await SendEmailAsync(
            model.AssigneeEmail, 
            model.AssigneeName, 
            subject, 
            html, 
            plain, 
            "FINDING_ASSIGNED", 
            model.TrackingCode, 
            model.SiteId);
    }

    public async Task<(bool success, string messageId, string? error)> SendFindingReminderAsync(FindingReminderEmailModel model)
    {
        if (string.IsNullOrWhiteSpace(model.AssigneeEmail))
        {
            return (false, "", "No se especificó correo para el recordatorio.");
        }

        string prefix = model.IsOverdue ? "⚠️ [URGENTE - HALLAZGO VENCIDO]" : "⏰ [RECORDATORIO]";
        string subject = $"{prefix} {model.TrackingCode} - {model.DaysRemainingText}";
        string html = BuildFindingReminderHtml(model);
        string plain = $"esd360 - Recordatorio de Hallazgo: {model.TrackingCode}\nEstado: {model.DaysRemainingText}\nFecha Límite: {model.DueDate}\nAccede a https://esd360.app para registrar tus acciones correctivas.";

        string type = model.IsOverdue ? "FINDING_OVERDUE" : "FINDING_REMINDER_DUE";
        return await SendEmailAsync(
            model.AssigneeEmail, 
            model.AssigneeName, 
            subject, 
            html, 
            plain, 
            type, 
            model.TrackingCode, 
            model.SiteId);
    }

    public async Task<(bool success, string messageId, string? error)> SendUserWelcomeAsync(UserWelcomeEmailModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            return (false, "", "Correo no proporcionado.");
        }

        string subject = "👋 Bienvenido a la Plataforma esd360 Enterprise";
        string html = BuildUserWelcomeHtml(model);
        string plain = $"Bienvenido a esd360 Enterprise.\nUsuario: {model.Email}\nRol: {model.Role}\nContraseña Temporal: {model.TemporaryPassword}\nAccede en https://esd360.app";

        return await SendEmailAsync(
            model.Email, 
            model.FullName, 
            subject, 
            html, 
            plain, 
            "USER_WELCOME", 
            model.Email, 
            null);
    }

    public async Task<(bool success, string messageId, string? error)> SendPasswordResetAsync(string toEmail, string toName, string resetUrl)
    {
        string subject = "🔐 Solicitud de Restablecimiento de Contraseña - esd360";
        string html = BuildPasswordResetHtml(toName, resetUrl);
        string plain = $"Has solicitado restablecer tu contraseña en esd360. Ingresa al siguiente enlace para actualizarla: {resetUrl}";

        return await SendEmailAsync(
            toEmail, 
            toName, 
            subject, 
            html, 
            plain, 
            "PASSWORD_RESET", 
            toEmail, 
            null);
    }

    public async Task<(bool success, string messageId, string? error)> SendGenericNotificationAsync(
        string toEmail, 
        string toName, 
        string subject, 
        string title, 
        string message, 
        string? ctaText = null, 
        string? ctaUrl = null,
        string? siteId = null)
    {
        string html = BuildGenericHtml(toName, title, message, ctaText, ctaUrl);
        return await SendEmailAsync(
            toEmail, 
            toName, 
            subject, 
            html, 
            message, 
            "GENERIC", 
            null, 
            siteId);
    }

    // --- HTML TEMPLATE GENERATORS ---

    private string BuildFindingAssignedHtml(FindingEmailNotificationModel m)
    {
        var (sevBadge, sevColor, sevBg) = GetSeverityBadges(m.Severity);
        string safeCode = HtmlEncoder.Default.Encode(m.TrackingCode);
        string safeTitle = HtmlEncoder.Default.Encode(m.Title);
        string safeElem = HtmlEncoder.Default.Encode(string.IsNullOrEmpty(m.AuditedElement) ? "General / No especificado" : m.AuditedElement);
        string safeLoc = HtmlEncoder.Default.Encode(m.Location);
        string safeTable = HtmlEncoder.Default.Encode(m.NormativeTable);
        string safeLimit = HtmlEncoder.Default.Encode(m.RequiredLimit);
        string safeMeas = HtmlEncoder.Default.Encode(m.MeasuredValue);
        string safeCont = HtmlEncoder.Default.Encode(string.IsNullOrEmpty(m.ImmediateContainment) ? "Pendiente por definir" : m.ImmediateContainment);
        string safeDue = HtmlEncoder.Default.Encode(m.DueDate);
        string safeAuditor = HtmlEncoder.Default.Encode(m.AuditorName);
        string safeAssignee = HtmlEncoder.Default.Encode(m.AssigneeName);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Nuevo Hallazgo Asignado</title>
</head>
<body style='margin:0; padding:0; background-color:#F1F5F9; font-family:-apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; color:#0F172A;'>
    <div style='max-width:620px; margin:30px auto; background:#FFFFFF; border-radius:14px; overflow:hidden; border:1px solid #E2E8F0; box-shadow:0 10px 25px rgba(15,23,42,0.08);'>
        
        <!-- HEADER -->
        <div style='background:linear-gradient(135deg, #0F172A 0%, #1E1B4B 100%); padding:28px 32px; color:#FFFFFF;'>
            <table style='width:100%; border-collapse:collapse;'>
                <tr>
                    <td>
                        <span style='font-size:20px; font-weight:800; letter-spacing:-0.5px;'>esd360</span>
                        <span style='font-size:11px; background:rgba(255,255,255,0.18); padding:3px 8px; border-radius:12px; margin-left:8px; font-weight:700;'>ENTERPRISE</span>
                    </td>
                    <td style='text-align:right;'>
                        <span style='font-size:11px; color:#94A3B8;'>ANSI/ESD S20.20-2021</span>
                    </td>
                </tr>
            </table>
            <h1 style='margin:18px 0 6px 0; font-size:22px; font-weight:800; color:#FFFFFF; line-height:1.25;'>Nuevo Hallazgo Normativo Asignado</h1>
            <p style='margin:0; font-size:13px; color:#CBD5E1;'>Has sido asignado como responsable técnico para la resolución de este hallazgo.</p>
        </div>

        <!-- MAIN BODY -->
        <div style='padding:28px 32px;'>
            
            <!-- SEVERITY CARD -->
            <div style='display:flex; align-items:center; justify-content:space-between; background:{sevBg}; border:1px solid {sevColor}; border-radius:10px; padding:12px 18px; margin-bottom:22px;'>
                <div>
                    <span style='font-size:11px; font-weight:800; text-transform:uppercase; color:{sevColor}; letter-spacing:0.5px;'>Código de Rastreo</span>
                    <div style='font-size:18px; font-weight:800; color:#0F172A; font-family:monospace;'>{safeCode}</div>
                </div>
                <div style='text-align:right;'>
                    <span style='display:inline-block; background:{sevColor}; color:#FFFFFF; font-weight:800; font-size:12px; padding:5px 12px; border-radius:20px;'>
                        {sevBadge}
                    </span>
                </div>
            </div>

            <!-- DETAILS TABLE -->
            <h3 style='font-size:14px; font-weight:700; color:#334155; margin:0 0 12px 0; text-transform:uppercase; letter-spacing:0.5px;'>Detalles del Levantamiento</h3>
            
            <table style='width:100%; border-collapse:collapse; margin-bottom:24px; font-size:13px;'>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; width:38%; font-weight:600;'>Título / Condición:</td>
                    <td style='padding:8px 0; color:#0F172A; font-weight:700;'>{safeTitle}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Elemento Auditado:</td>
                    <td style='padding:8px 0; color:#0F172A; font-weight:600;'>{safeElem}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Ubicación / Área:</td>
                    <td style='padding:8px 0; color:#0F172A; font-weight:600;'>{safeLoc}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Tabla Normativa:</td>
                    <td style='padding:8px 0; color:#0F172A;'>{safeTable}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Límite Requerido:</td>
                    <td style='padding:8px 0; color:#059669; font-weight:700;'>{safeLimit}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Valor Medido:</td>
                    <td style='padding:8px 0; color:#DC2626; font-weight:800; font-family:monospace;'>{safeMeas}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Contención Inmediata:</td>
                    <td style='padding:8px 0; color:#475569;'>{safeCont}</td>
                </tr>
                <tr style='border-bottom:1px solid #F1F5F9;'>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Fecha Límite Compromiso:</td>
                    <td style='padding:8px 0; color:#DC2626; font-weight:800;'>📅 {safeDue}</td>
                </tr>
                <tr>
                    <td style='padding:8px 0; color:#64748B; font-weight:600;'>Auditor que levantó:</td>
                    <td style='padding:8px 0; color:#475569;'>{safeAuditor}</td>
                </tr>
            </table>

            <!-- ACTION BUTTON -->
            <div style='text-align:center; margin:32px 0 20px 0;'>
                <a href='https://esd360.app' target='_blank' style='background:#2563EB; color:#FFFFFF; text-decoration:none; padding:14px 28px; border-radius:8px; font-weight:800; font-size:14px; display:inline-block; box-shadow:0 4px 12px rgba(37,99,235,0.3);'>
                    🚀 Gestionar Hallazgo en esd360.app &rarr;
                </a>
            </div>

            <p style='font-size:12px; color:#94A3B8; text-align:center; margin:0;'>
                Ingresa al sistema para documentar la causa raíz (5 Por qués / Ishikawa) y subir tu evidencia fotográfica de cierre.
            </p>
        </div>

        <!-- FOOTER -->
        <div style='background:#F8FAFC; padding:18px 32px; border-top:1px solid #E2E8F0; font-size:11px; color:#94A3B8; text-align:center;'>
            <div>esd360 Enterprise Platform &bull; Compliance ANSI/ESD S20.20 &bull; IEC 61340-5-1 &bull; ESD TR53</div>
            <div style='margin-top:4px;'>Notificación enviada automáticamente a <strong>{HtmlEncoder.Default.Encode(m.AssigneeEmail)}</strong> desde <strong>notification@esd360.app</strong></div>
        </div>
    </div>
</body>
</html>";
    }

    private string BuildFindingReminderHtml(FindingReminderEmailModel m)
    {
        string headerBg = m.IsOverdue ? "linear-gradient(135deg, #7F1D1D 0%, #991B1B 100%)" : "linear-gradient(135deg, #9A3412 0%, #C2410C 100%)";
        string titleText = m.IsOverdue ? "⚠️ Hallazgo Vencido Pendiente de Cierre" : "⏰ Recordatorio: Hallazgo Próximo a Vencer";
        string statusColor = m.IsOverdue ? "#DC2626" : "#EA580C";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Recordatorio de Hallazgo</title>
</head>
<body style='margin:0; padding:0; background-color:#F1F5F9; font-family:-apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
    <div style='max-width:620px; margin:30px auto; background:#FFFFFF; border-radius:14px; overflow:hidden; border:1px solid #E2E8F0; box-shadow:0 10px 25px rgba(15,23,42,0.08);'>
        
        <div style='background:{headerBg}; padding:26px 32px; color:#FFFFFF;'>
            <div style='font-size:18px; font-weight:800;'>esd360 Platform</div>
            <h2 style='margin:12px 0 4px 0; font-size:20px; color:#FFFFFF;'>{titleText}</h2>
            <p style='margin:0; font-size:13px; color:#FED7AA;'>Hola {HtmlEncoder.Default.Encode(m.AssigneeName)}, tienes un hallazgo que requiere tu atención inmediata.</p>
        </div>

        <div style='padding:26px 32px;'>
            <div style='background:#FFF7ED; border-left:4px solid {statusColor}; padding:14px 18px; border-radius:6px; margin-bottom:20px;'>
                <div style='font-size:16px; font-weight:800; color:{statusColor};'>{HtmlEncoder.Default.Encode(m.DaysRemainingText)}</div>
                <div style='font-size:13px; color:#475569; margin-top:4px;'>Código: <strong>{HtmlEncoder.Default.Encode(m.TrackingCode)}</strong> &bull; Ubicación: {HtmlEncoder.Default.Encode(m.Location)}</div>
                <div style='font-size:13px; color:#475569;'>Fecha Límite: <strong>{HtmlEncoder.Default.Encode(m.DueDate)}</strong></div>
            </div>

            <p style='font-size:13.5px; color:#334155; line-height:1.5;'>
                El hallazgo normativo <strong>{HtmlEncoder.Default.Encode(m.Title)}</strong> requiere la implementación de acciones correctivas para cumplir con el estándar ANSI/ESD S20.20 de la planta.
            </p>

            <div style='text-align:center; margin:28px 0 16px 0;'>
                <a href='https://esd360.app' target='_blank' style='background:#DC2626; color:#FFFFFF; text-decoration:none; padding:13px 26px; border-radius:8px; font-weight:800; font-size:14px; display:inline-block;'>
                    🛠️ Registrar Cierre o Extensión en esd360 &rarr;
                </a>
            </div>
        </div>

        <div style='background:#F8FAFC; padding:16px 32px; border-top:1px solid #E2E8F0; font-size:11px; color:#94A3B8; text-align:center;'>
            esd360 Enterprise &bull; ANSI/ESD S20.20-2021 &bull; Enviado automáticamente desde notification@esd360.app
        </div>
    </div>
</body>
</html>";
    }

    private string BuildUserWelcomeHtml(UserWelcomeEmailModel m)
    {
        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Bienvenido a esd360</title></head>
<body style='margin:0; padding:0; background-color:#F1F5F9; font-family:-apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
    <div style='max-width:620px; margin:30px auto; background:#FFFFFF; border-radius:14px; overflow:hidden; border:1px solid #E2E8F0; box-shadow:0 10px 25px rgba(15,23,42,0.08);'>
        <div style='background:linear-gradient(135deg, #0F172A 0%, #312E81 100%); padding:28px 32px; color:#FFFFFF;'>
            <div style='font-size:20px; font-weight:800;'>esd360 Enterprise Platform</div>
            <h2 style='margin:14px 0 4px 0; font-size:22px; color:#FFFFFF;'>¡Bienvenido al Sistema!</h2>
            <p style='margin:0; font-size:13px; color:#C7D2FE;'>Se ha generado tu cuenta de acceso a la plataforma ESD.</p>
        </div>

        <div style='padding:28px 32px;'>
            <p style='font-size:14px; color:#334155;'>Hola <strong>{HtmlEncoder.Default.Encode(m.FullName)}</strong>,</p>
            <p style='font-size:13.5px; color:#475569;'>Has sido dado de alta en la plataforma corporativa esd360 para la gestión de auditorías y cumplimiento normativo ANSI/ESD S20.20.</p>

            <div style='background:#F8FAFC; border:1px solid #E2E8F0; border-radius:10px; padding:18px; margin:22px 0;'>
                <div style='font-size:12px; font-weight:800; color:#64748B; text-transform:uppercase; margin-bottom:10px;'>Credenciales de Acceso</div>
                <div style='font-size:13px; margin-bottom:6px;'><strong>Plataforma:</strong> <a href='https://esd360.app' style='color:#2563EB;'>https://esd360.app</a></div>
                <div style='font-size:13px; margin-bottom:6px;'><strong>Usuario / Correo:</strong> {HtmlEncoder.Default.Encode(m.Email)}</div>
                <div style='font-size:13px; margin-bottom:6px;'><strong>Rol Asignado:</strong> <span style='background:#EEF2FF; color:#4F46E5; padding:2px 8px; border-radius:10px; font-weight:700;'>{HtmlEncoder.Default.Encode(m.Role)}</span></div>
                <div style='font-size:13px; margin-bottom:6px;'><strong>Planta:</strong> {HtmlEncoder.Default.Encode(m.SiteName)}</div>
                <div style='font-size:13px;'><strong>Contraseña Temporal:</strong> <code style='background:#E2E8F0; padding:3px 6px; border-radius:4px; font-weight:700; color:#0F172A;'>{HtmlEncoder.Default.Encode(m.TemporaryPassword)}</code></div>
            </div>

            <div style='text-align:center; margin:28px 0 16px 0;'>
                <a href='https://esd360.app' target='_blank' style='background:#4F46E5; color:#FFFFFF; text-decoration:none; padding:14px 30px; border-radius:8px; font-weight:800; font-size:14px; display:inline-block;'>
                    🔒 Iniciar Sesión en esd360 &rarr;
                </a>
            </div>
            
            <p style='font-size:12px; color:#94A3B8; text-align:center;'>Te recomendamos cambiar tu contraseña una vez que ingreses al sistema desde tu perfil.</p>
        </div>

        <div style='background:#F8FAFC; padding:16px 32px; border-top:1px solid #E2E8F0; font-size:11px; color:#94A3B8; text-align:center;'>
            esd360 Enterprise &bull; Soporte: notification@esd360.app
        </div>
    </div>
</body>
</html>";
    }

    private string BuildPasswordResetHtml(string toName, string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Restablecer Contraseña</title></head>
<body style='margin:0; padding:0; background-color:#F1F5F9; font-family:-apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
    <div style='max-width:580px; margin:30px auto; background:#FFFFFF; border-radius:14px; overflow:hidden; border:1px solid #E2E8F0;'>
        <div style='background:#0F172A; padding:24px 32px; color:#FFFFFF;'>
            <h2 style='margin:0; font-size:20px;'>esd360 Enterprise</h2>
        </div>
        <div style='padding:28px 32px;'>
            <h3 style='margin:0 0 12px 0; color:#0F172A;'>Restablecimiento de Contraseña</h3>
            <p style='font-size:13.5px; color:#475569;'>Hola {HtmlEncoder.Default.Encode(toName)}, hemos recibido una solicitud para cambiar tu contraseña.</p>
            <div style='text-align:center; margin:26px 0;'>
                <a href='{resetUrl}' target='_blank' style='background:#2563EB; color:#FFFFFF; text-decoration:none; padding:12px 24px; border-radius:6px; font-weight:700; display:inline-block;'>
                    🔑 Actualizar mi Contraseña
                </a>
            </div>
            <p style='font-size:12px; color:#94A3B8;'>Si tú no solicitaste este cambio, puedes ignorar este mensaje.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string BuildGenericHtml(string toName, string title, string message, string? ctaText, string? ctaUrl)
    {
        string ctaBlock = (!string.IsNullOrEmpty(ctaText) && !string.IsNullOrEmpty(ctaUrl))
            ? $"<div style='text-align:center; margin:28px 0;'><a href='{ctaUrl}' target='_blank' style='background:#2563EB; color:#FFFFFF; text-decoration:none; padding:12px 24px; border-radius:6px; font-weight:700; display:inline-block;'>{HtmlEncoder.Default.Encode(ctaText)} &rarr;</a></div>"
            : "";

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>{HtmlEncoder.Default.Encode(title)}</title></head>
<body style='margin:0; padding:0; background-color:#F1F5F9; font-family:-apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
    <div style='max-width:600px; margin:30px auto; background:#FFFFFF; border-radius:12px; overflow:hidden; border:1px solid #E2E8F0;'>
        <div style='background:#0F172A; padding:22px 28px; color:#FFFFFF;'>
            <div style='font-size:18px; font-weight:800;'>esd360 Enterprise</div>
        </div>
        <div style='padding:28px;'>
            <h2 style='margin:0 0 12px 0; font-size:18px; color:#0F172A;'>{HtmlEncoder.Default.Encode(title)}</h2>
            <p style='font-size:14px; color:#334155; line-height:1.6;'>{HtmlEncoder.Default.Encode(message)}</p>
            {ctaBlock}
        </div>
        <div style='background:#F8FAFC; padding:14px 28px; border-top:1px solid #E2E8F0; font-size:11px; color:#94A3B8; text-align:center;'>
            esd360 Platform &bull; notification@esd360.app
        </div>
    </div>
</body>
</html>";
    }

    private static (string badge, string color, string bg) GetSeverityBadges(string severity)
    {
        return (severity?.ToUpperInvariant()) switch
        {
            "CRITICAL" or "F1" or "F1_CRITICAL" => ("F1 - Crítica", "#DC2626", "#FEF2F2"),
            "MAJOR" or "F2" or "F2_MAJOR" => ("F2 - Mayor", "#EA580C", "#FFF7ED"),
            "MINOR" or "F3" or "F3_MINOR" => ("F3 - Menor", "#2563EB", "#EFF6FF"),
            "OBSERVATION" or "F4" or "F4_OBSERVATION" => ("F4 - Observación", "#7C3AED", "#FAF5FF"),
            _ => ("F2 - Mayor", "#EA580C", "#FFF7ED")
        };
    }
}
