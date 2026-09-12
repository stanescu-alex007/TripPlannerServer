using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Email;

/// <summary>
/// Plain inline-styled HTML on purpose: mail clients strip &lt;style&gt; blocks and
/// ignore most modern CSS, so anything fancier would render unpredictably.
/// </summary>
internal static class AuthEmailTemplates
{
    public static EmailMessage VerifyEmail(User user, string link, int validHours) =>
        new(
            ToAddress: user.Email,
            Subject: "Confirm your TripPlanner account",
            HtmlBody: Layout(
                heading: $"Welcome, {Escape(user.FirstName)}",
                body: "Confirm your email address to finish setting up your account.",
                buttonText: "Confirm my email",
                buttonUrl: link,
                footer: $"This link is valid for {validHours} hours. " +
                        "If you did not create a TripPlanner account you can ignore this message."),
            PlainTextBody:
                $"Welcome, {user.FirstName}.\n\n" +
                $"Confirm your email address: {link}\n\n" +
                $"The link is valid for {validHours} hours.");

    public static EmailMessage PasswordReset(User user, string link, int validMinutes) =>
        new(
            ToAddress: user.Email,
            Subject: "Reset your TripPlanner password",
            HtmlBody: Layout(
                heading: "Password reset",
                body: "Use the button below to choose a new password.",
                buttonText: "Choose a new password",
                buttonUrl: link,
                footer: $"This link is valid for {validMinutes} minutes and can be used once. " +
                        "If you did not request a reset, ignore this email — your password stays unchanged."),
            PlainTextBody:
                $"Reset your TripPlanner password: {link}\n\n" +
                $"The link is valid for {validMinutes} minutes and can be used once.\n" +
                "If you did not request this, ignore the email.");

    private static string Layout(
        string heading, string body, string buttonText, string buttonUrl, string footer) => $"""
        <div style="font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#1f2430">
          <h1 style="margin:0 0 16px;font-size:22px;font-weight:600">{heading}</h1>
          <p style="margin:0 0 24px;font-size:15px;line-height:1.55;color:#4a5160">{body}</p>
          <a href="{buttonUrl}"
             style="display:inline-block;background:#7b2cbf;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:8px;font-size:15px;font-weight:600">
            {buttonText}
          </a>
          <p style="margin:28px 0 0;font-size:13px;line-height:1.5;color:#8a90a0">{footer}</p>
          <p style="margin:16px 0 0;font-size:12px;line-height:1.5;color:#a4a9b6;word-break:break-all">
            If the button does not work, paste this into your browser:<br>{buttonUrl}
          </p>
        </div>
        """;

    /// <summary>User-supplied text goes into HTML, so angle brackets must not survive.</summary>
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
