using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.Infrastructure.Email;

/// <summary>
/// Sends through Resend's HTTP API (https://api.resend.com/emails).
///
/// <para>
/// Note on the sender address: until a domain is verified in the Resend dashboard and
/// its SPF/DKIM records are published, Resend only accepts <c>onboarding@resend.dev</c>
/// as the from-address and only delivers to the account owner's own address. That is
/// enough for development; production needs a verified domain.
/// </para>
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient http,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var payload = new
        {
            from = $"{_options.FromName} <{_options.FromAddress}>",
            to = new[] { message.ToAddress },
            subject = message.Subject,
            html = message.HtmlBody,
            text = message.PlainTextBody
        };

        using var response = await _http.PostAsJsonAsync("emails", payload, ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Sent {Subject} to {Recipient} via Resend.",
                message.Subject, message.ToAddress);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        _logger.LogError(
            "Resend rejected the message to {Recipient}: {Status} {Body}",
            message.ToAddress, (int)response.StatusCode, body);

        throw new InvalidOperationException(
            $"Resend returned {(int)response.StatusCode} when sending to {message.ToAddress}.");
    }
}
