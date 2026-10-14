namespace TripPlanner.Core.Interfaces.IServices
{
    public sealed record EmailMessage(
        string ToAddress,
        string Subject,
        string HtmlBody,
        string? PlainTextBody = null,
        string? ReplyTo = null);

    /// <summary>
    /// Delivers transactional email. Implementations are swapped by configuration
    /// (<c>Email:Provider</c>), so no caller ever knows which provider is in use.
    /// </summary>
    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken ct = default);
    }
}
