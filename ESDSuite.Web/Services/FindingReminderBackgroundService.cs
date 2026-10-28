using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Nodes;
using ESDSuite.Core.Models;
using ESDSuite.Core.Services;
using ESDSuite.Services.Storage;
using ESDSuite.Services.Supabase;

namespace ESDSuite.Web.Services;

public class FindingReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);

    public FindingReminderBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay after server boot before running the first scan
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunReminderScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FindingReminderBackgroundService Scan Error]: {ex.Message}");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> RunReminderScanAsync(CancellationToken stoppingToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<FindingsStorageService>();
        var supabase = scope.ServiceProvider.GetRequiredService<SupabaseService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

        var allFindings = await storage.GetAllFindingsAsync();
        var plantUsers = await supabase.GetUsersAsync();

        // Build email lookup by user name/id
        var userEmailMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in plantUsers)
        {
            if (u is JsonObject uObj)
            {
                string uEmail = uObj["email"]?.ToString()?.Trim() ?? "";
                string uName = uObj["full_name"]?.ToString()?.Trim() ?? "";
                string uId = uObj["id"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(uEmail))
                {
                    if (!string.IsNullOrEmpty(uName)) userEmailMap[uName] = uEmail;
                    if (!string.IsNullOrEmpty(uId)) userEmailMap[uId] = uEmail;
                    userEmailMap[uEmail] = uEmail;
                }
            }
        }

        // Fetch today's email logs from Supabase to prevent duplicate emails on the same day
        var recentLogs = await supabase.GetEmailLogsAsync(null, 200);
        var sentTodayTrackingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

        foreach (var l in recentLogs)
        {
            if (l is JsonObject lObj)
            {
                string cAt = lObj["created_at"]?.ToString() ?? "";
                string relId = lObj["related_id"]?.ToString() ?? "";
                string type = lObj["email_type"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(relId) && cAt.StartsWith(todayStr) && (type == "FINDING_OVERDUE" || type == "FINDING_REMINDER_DUE"))
                {
                    sentTodayTrackingCodes.Add(relId);
                }
            }
        }

        int remindersSent = 0;
        DateTime today = DateTime.UtcNow.Date;

        foreach (var f in allFindings)
        {
            if (stoppingToken.IsCancellationRequested) break;
            if (f is not JsonObject fObj) continue;

            string status = (fObj["status"]?.ToString() ?? "").ToUpperInvariant();
            if (status == "CLOSED" || status == "RESOLVED" || status == "CANCELLED") continue;

            string trackingCode = fObj["tracking_code"]?.ToString() ?? fObj["tracker_code"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(trackingCode)) continue;

            // Skip if already alerted today
            if (sentTodayTrackingCodes.Contains(trackingCode)) continue;

            string dueDateStr = fObj["due_date"]?.ToString()?.Trim() ?? "";
            if (!DateTime.TryParse(dueDateStr, out DateTime dueDate)) continue;

            DateTime targetDate = dueDate.Date;
            int daysDiff = (int)(targetDate - today).TotalDays;

            bool isOverdue = daysDiff < 0;
            bool isDueSoon = daysDiff >= 0 && daysDiff <= 2;

            if (!isOverdue && !isDueSoon) continue;

            // Identify assignee email
            string assignee = fObj["assignee_name"]?.ToString() ?? fObj["assigned_to"]?.ToString() ?? "";
            string assigneeEmail = fObj["assignee_email"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(assigneeEmail) && !string.IsNullOrEmpty(assignee) && userEmailMap.TryGetValue(assignee, out string? mapped))
            {
                assigneeEmail = mapped;
            }

            if (string.IsNullOrWhiteSpace(assigneeEmail)) continue;

            string daysText = isOverdue
                ? $"⚠️ Vencido hace {Math.Abs(daysDiff)} día(s) (Fecha Límite: {dueDate:yyyy-MM-dd})"
                : (daysDiff == 0 ? "⏰ Vence HOY" : $"⏰ Vence en {daysDiff} día(s) ({dueDate:yyyy-MM-dd})");

            var reminder = new FindingReminderEmailModel
            {
                TrackingCode = trackingCode,
                Title = fObj["title"]?.ToString() ?? "Hallazgo Normativo",
                Severity = fObj["severity"]?.ToString() ?? "MAJOR",
                Location = fObj["location"]?.ToString() ?? "Ubicación Planta",
                AssigneeName = assignee,
                AssigneeEmail = assigneeEmail,
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                DaysRemainingText = daysText,
                IsOverdue = isOverdue,
                SiteId = fObj["site_id"]?.ToString() ?? ""
            };

            var (ok, msgId, err) = await emailService.SendFindingReminderAsync(reminder);
            if (ok)
            {
                sentTodayTrackingCodes.Add(trackingCode);
                remindersSent++;
            }
        }

        Console.WriteLine($"[FindingReminderBackgroundService] Scan completed. Sent {remindersSent} reminders.");
        return remindersSent;
    }
}
