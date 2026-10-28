namespace ESDSuite.Core.Models;

public class FindingEmailNotificationModel
{
    public string TrackingCode { get; set; } = "";
    public string Title { get; set; } = "Hallazgo Normativo";
    public string Severity { get; set; } = "MAJOR";
    public string AuditedElement { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public string Area { get; set; } = "";
    public string NormativeTable { get; set; } = "";
    public string RequiredLimit { get; set; } = "";
    public string MeasuredValue { get; set; } = "";
    public string ImmediateContainment { get; set; } = "";
    public string AuditorName { get; set; } = "";
    public string AssigneeName { get; set; } = "";
    public string AssigneeEmail { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string SiteId { get; set; } = "";
}

public class FindingReminderEmailModel
{
    public string TrackingCode { get; set; } = "";
    public string Title { get; set; } = "Recordatorio de Hallazgo";
    public string Severity { get; set; } = "MAJOR";
    public string Location { get; set; } = "";
    public string AssigneeName { get; set; } = "";
    public string AssigneeEmail { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string DaysRemainingText { get; set; } = "";
    public bool IsOverdue { get; set; } = false;
    public string SiteId { get; set; } = "";
}

public class UserWelcomeEmailModel
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Auditor";
    public string SiteName { get; set; } = "";
    public string TemporaryPassword { get; set; } = "";
    public string LoginUrl { get; set; } = "https://esd360.app";
}

public class EmailLogRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ToEmail { get; set; } = "";
    public string? ToName { get; set; }
    public string Subject { get; set; } = "";
    public string EmailType { get; set; } = "";
    public string? RelatedId { get; set; }
    public string Status { get; set; } = "SENT";
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SiteId { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
