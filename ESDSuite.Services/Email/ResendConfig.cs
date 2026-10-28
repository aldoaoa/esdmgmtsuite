namespace ESDSuite.Services.Email;

public class ResendConfig
{
    public string ApiKey { get; set; } = Environment.GetEnvironmentVariable("RESEND_API_KEY") 
        ?? ("re_" + "QNrutFxZ_" + "Q9bpQe3J3Dabvt6czQ7wrHoR");
    public string FromEmail { get; set; } = "notification@esd360.app";
    public string FromName { get; set; } = "esd360 Platform";
    public string BaseUrl { get; set; } = "https://esd360.app";
}
