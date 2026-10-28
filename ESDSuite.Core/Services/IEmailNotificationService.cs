using ESDSuite.Core.Models;

namespace ESDSuite.Core.Services;

public interface IEmailNotificationService
{
    Task<(bool success, string messageId, string? error)> SendEmailAsync(
        string toEmail, 
        string? toName, 
        string subject, 
        string htmlContent, 
        string? plainText = null,
        string emailType = "GENERIC",
        string? relatedId = null,
        string? siteId = null);

    Task<(bool success, string messageId, string? error)> SendFindingAssignedNotificationAsync(FindingEmailNotificationModel model);

    Task<(bool success, string messageId, string? error)> SendFindingReminderAsync(FindingReminderEmailModel model);

    Task<(bool success, string messageId, string? error)> SendUserWelcomeAsync(UserWelcomeEmailModel model);

    Task<(bool success, string messageId, string? error)> SendPasswordResetAsync(string toEmail, string toName, string resetUrl);

    Task<(bool success, string messageId, string? error)> SendGenericNotificationAsync(
        string toEmail, 
        string toName, 
        string subject, 
        string title, 
        string message, 
        string? ctaText = null, 
        string? ctaUrl = null,
        string? siteId = null);
}
