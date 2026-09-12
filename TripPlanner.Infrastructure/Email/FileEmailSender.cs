using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.Infrastructure.Email;

/// <summary>
/// Development sender: writes each message to disk as .html instead of delivering it,
/// and logs the path. Open the file in a browser to see exactly what the user would get
/// — including the verification or reset link, so no mailbox is needed to test a flow.
/// </summary>
public sealed class FileEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<FileEmailSender> _logger;

    public FileEmailSender(IOptions<EmailOptions> options, ILogger<FileEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var directory = Path.GetFullPath(_options.File.Directory);
        Directory.CreateDirectory(directory);

        var fileName = $"{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}_{Sanitize(message.ToAddress)}.html";
        var path = Path.Combine(directory, fileName);

        var contents = $"""
            <!doctype html>
            <meta charset="utf-8">
            <title>{message.Subject}</title>
            <div style="font:14px system-ui;background:#f6f6f6;padding:12px;margin-bottom:16px">
              <b>To:</b> {message.ToAddress}<br>
              <b>From:</b> {_options.FromName} &lt;{_options.FromAddress}&gt;<br>
              <b>Subject:</b> {message.Subject}<br>
              <b>Sent:</b> {DateTime.UtcNow:u}
            </div>
            {message.HtmlBody}
            """;

        await File.WriteAllTextAsync(path, contents, ct);

        _logger.LogInformation(
            "Email not sent (Provider=File). Written to {Path} — open it to follow the link.",
            path);
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
}
