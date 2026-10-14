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

    /// <summary>Absent optional fields are left out rather than sent as null.</summary>
    private static readonly JsonSerializerOptions OmitNulls = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var payload = new
        {
            from = $"{_options.FromName} <{_options.FromAddress}>",
            to = new[] { message.ToAddress },
            subject = message.Subject,
            html = message.HtmlBody,
            text = message.PlainTextBody,
            reply_to = message.ReplyTo
        };

        using var response = await _http.PostAsJsonAsync("emails", payload, OmitNulls, ct);

        if (response.IsSuccessStatusCode)
        {
            // Log the provider's id: it is the only way to trace a "my email never
            // arrived" report back to a specific record in the Resend dashboard.
            var id = await ReadMessageIdAsync(response, ct);

            _logger.LogInformation(
                "Sent '{Subject}' to {Recipient} via Resend (id: {MessageId}).",
                message.Subject, message.ToAddress, id ?? "unknown");
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        _logger.LogError(
            "Resend rejected the message to {Recipient}: {Status} {Body}",
            message.ToAddress, (int)response.StatusCode, body);

        throw new InvalidOperationException(
            $"Resend returned {(int)response.StatusCode} when sending to {message.ToAddress}.");
    }

    /// <summary>
    /// Pulls the "id" out of Resend's response. Never throws — failing to read the
    /// id must not turn a delivered message into a reported failure.
    /// </summary>
    private static async Task<string?> ReadMessageIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            return document.RootElement.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
