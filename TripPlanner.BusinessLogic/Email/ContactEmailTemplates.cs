using System.Net;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Email;

/// <summary>Messages for the team inbox, not for users — plain and readable is enough.</summary>
internal static class ContactEmailTemplates
{
    public static EmailMessage ContactMessage(string inbox, User sender, string topic, string text)
    {
        var name = $"{sender.FirstName} {sender.LastName}".Trim();

        return new(
            ToAddress: inbox,
            Subject: $"[Contact] {topic} — {name}",
            HtmlBody: $"""
                <div style="font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:600px;color:#1f2430">
                  <p style="margin:0 0 12px">
                    <b>From:</b> {WebUtility.HtmlEncode(name)} &lt;{WebUtility.HtmlEncode(sender.Email)}&gt;<br>
                    <b>Topic:</b> {WebUtility.HtmlEncode(topic)}<br>
                    <b>User id:</b> {sender.Id}
                  </p>
                  <div style="white-space:pre-wrap;border-left:3px solid #ccc;padding-left:12px">{WebUtility.HtmlEncode(text)}</div>
                </div>
                """,
            PlainTextBody: $"From: {name} <{sender.Email}>\nTopic: {topic}\nUser id: {sender.Id}\n\n{text}",
            // Replying goes straight to the user, not back to the no-reply sender.
            ReplyTo: sender.Email);
    }
}
