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

    // ── Trip invitations ──────────────────────────────────────────────────────

    public static EmailMessage TripInvitation(
        string toAddress, string inviterName, string tripName, string location,
        string dateRange, string role, string link, int validDays) =>
        new(
            ToAddress: toAddress,
            Subject: $"{inviterName} invited you to {tripName}",
            HtmlBody: Layout(
                heading: $"{Escape(inviterName)} invited you along",
                body: $"<b>{Escape(tripName)}</b><br>{Escape(location)}<br>{Escape(dateRange)}" +
                      $"<br><br>You have been invited as <b>{Escape(role)}</b>.",
                buttonText: "View the invitation",
                buttonUrl: link,
                footer: $"This invitation is valid for {validDays} days. " +
                        "If you do not know the sender, you can ignore this email."),
            PlainTextBody:
                $"{inviterName} invited you to {tripName} ({location}, {dateRange}) as {role}.\n\n" +
                $"Respond here: {link}\n\nValid for {validDays} days.");

    /// <summary>
    /// Tells the organiser how an invitation was answered. They are the one waiting
    /// on it, and nothing else in the app would surface the reply.
    /// </summary>
    public static EmailMessage InvitationAnswered(
        string ownerEmail, string inviteeName, string tripName, bool accepted, string tripLink) =>
        new(
            ToAddress: ownerEmail,
            Subject: accepted
                ? $"{inviteeName} joined {tripName}"
                : $"{inviteeName} declined {tripName}",
            HtmlBody: Layout(
                heading: accepted ? "Someone joined your trip 🎉" : "An invitation was declined",
                body: accepted
                    ? $"<b>{Escape(inviteeName)}</b> accepted your invitation to <b>{Escape(tripName)}</b>."
                    : $"<b>{Escape(inviteeName)}</b> will not be joining <b>{Escape(tripName)}</b>.",
                buttonText: "Open the trip",
                buttonUrl: tripLink,
                footer: "You are receiving this because you organise this trip."),
            PlainTextBody: accepted
                ? $"{inviteeName} accepted your invitation to {tripName}. {tripLink}"
                : $"{inviteeName} declined your invitation to {tripName}. {tripLink}");

    /// <summary>
    /// Asks the next person in line to take over a trip whose owner left. Without it the
    /// offer would only be found by someone who happened to open the app in time.
    /// </summary>
    public static EmailMessage OwnershipOffered(
        string toAddress, string candidateName, string tripName, string link, int validDays) =>
        new(
            ToAddress: toAddress,
            Subject: $"Would you like to take over {tripName}?",
            HtmlBody: Layout(
                heading: "A trip needs a new organiser",
                body: $"Hi {Escape(candidateName)}, the organiser of <b>{Escape(tripName)}</b> has left " +
                      "TripPlanner, and you are next in line to take it over. As the owner you can " +
                      "edit the trip, decide who is on it and delete it.",
                buttonText: "Respond to the offer",
                buttonUrl: link,
                footer: $"The offer is open for {validDays} days. If you decline or do not answer, it " +
                        "passes to the next person on the trip; if nobody takes it, the trip is deleted."),
            PlainTextBody:
                $"The organiser of {tripName} has left TripPlanner and you are next in line to take it over.\n\n" +
                $"Respond here: {link}\n\nThe offer is open for {validDays} days.");

    // ── Security notifications ────────────────────────────────────────────────
    // These are not courtesy mail. If an attacker changes a password, an email
    // address or a 2FA setting, this notice is often the only signal the real owner
    // ever gets — so it goes to the address on file, not the new one.

    public static EmailMessage PasswordChanged(User user, string resetLink) =>
        new(
            ToAddress: user.Email,
            Subject: "Your TripPlanner password was changed",
            HtmlBody: Alert(
                heading: "Your password was changed",
                body: "The password on your TripPlanner account has just been changed, " +
                      "and every other device has been signed out.",
                footer: "If this was you, nothing more is needed. " +
                        "If it was not, reset your password immediately using the button below.",
                buttonText: "This wasn't me — reset it",
                buttonUrl: resetLink),
            PlainTextBody:
                "Your TripPlanner password was changed and other devices were signed out. " +
                $"If this was not you, reset it here: {resetLink}");

    public static EmailMessage EmailChangeRequested(User user, string newEmail, string link, int validHours) =>
        new(
            // Sent to the NEW address: following the link is how ownership is proved.
            ToAddress: newEmail,
            Subject: "Confirm your new TripPlanner email address",
            HtmlBody: Layout(
                heading: "Confirm this address",
                body: $"A request was made to move the TripPlanner account of " +
                      $"{Escape(user.Email)} to this address. Confirm it to complete the change.",
                buttonText: "Confirm this address",
                buttonUrl: link,
                footer: $"This link is valid for {validHours} hours. " +
                        "If you did not expect this email, simply ignore it — nothing changes."),
            PlainTextBody:
                $"Confirm your new TripPlanner address: {link} " +
                $"Valid for {validHours} hours.");

    public static EmailMessage EmailChangeNotice(string oldEmail, string newEmail) =>
        new(
            // Sent to the OLD address, which is the one an attacker is trying to cut off.
            ToAddress: oldEmail,
            Subject: "Your TripPlanner email address is being changed",
            HtmlBody: Alert(
                heading: "Email change requested",
                body: $"Someone asked to move this account to <b>{Escape(newEmail)}</b>. " +
                      "The change only takes effect once that address is confirmed.",
                footer: "If this was not you, change your password now — someone may have " +
                        "access to your account.",
                buttonText: null,
                buttonUrl: null),
            PlainTextBody:
                $"A request was made to change your TripPlanner address to {newEmail}. " +
                "If this was not you, change your password immediately.");

    public static EmailMessage TwoFactorChanged(User user, bool enabled) =>
        new(
            ToAddress: user.Email,
            Subject: enabled
                ? "Two-factor authentication is on"
                : "Two-factor authentication was switched off",
            HtmlBody: Alert(
                heading: enabled ? "Two-factor authentication enabled" : "Two-factor authentication disabled",
                body: enabled
                    ? "From now on you will need a code from your authenticator app to sign in."
                    : "Your account is now protected by your password alone.",
                footer: "If you did not do this, change your password immediately.",
                buttonText: null,
                buttonUrl: null),
            PlainTextBody: enabled
                ? "Two-factor authentication was enabled on your TripPlanner account."
                : "Two-factor authentication was disabled on your TripPlanner account.");

    /// <summary>Warning styling, and the button is optional — some notices only inform.</summary>
    private static string Alert(
        string heading, string body, string footer, string? buttonText, string? buttonUrl)
    {
        var button = buttonText is not null && buttonUrl is not null
            ? $"""
              <a href="{buttonUrl}"
                 style="display:inline-block;background:#b91c1c;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:8px;font-size:15px;font-weight:600">
                {buttonText}
              </a>
              """
            : string.Empty;

        return $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#1f2430">
              <div style="border-left:4px solid #d97706;padding-left:16px;margin-bottom:24px">
                <h1 style="margin:0 0 8px;font-size:20px;font-weight:600">{heading}</h1>
                <p style="margin:0;font-size:15px;line-height:1.55;color:#4a5160">{body}</p>
              </div>
              {button}
              <p style="margin:24px 0 0;font-size:13px;line-height:1.5;color:#8a90a0">{footer}</p>
            </div>
            """;
    }

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
