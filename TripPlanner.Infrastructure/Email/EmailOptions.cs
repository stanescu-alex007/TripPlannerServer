namespace TripPlanner.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Resend" to send for real, "File" to write messages to disk in dev.</summary>
    public string Provider { get; set; } = "File";

    public string FromAddress { get; set; } = "onboarding@resend.dev";
    public string FromName { get; set; } = "TripPlanner";

    public ResendOptions Resend { get; set; } = new();
    public FileOptions File { get; set; } = new();

    public sealed class ResendOptions
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    public sealed class FileOptions
    {
        public string Directory { get; set; } = "../_sent-emails";
    }
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Base URL the links inside emails point at.</summary>
    public string BaseUrl { get; set; } = "http://localhost:4200";
}
