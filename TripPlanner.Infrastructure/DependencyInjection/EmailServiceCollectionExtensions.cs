using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Infrastructure.Email;

namespace TripPlanner.Infrastructure.DependencyInjection;

public static class EmailServiceCollectionExtensions
{
    private const string ResendApiBase = "https://api.resend.com/";

    public static IServiceCollection AddEmailSending(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(section);

        var options = section.Get<EmailOptions>() ?? new EmailOptions();

        if (!string.Equals(options.Provider, "Resend", StringComparison.OrdinalIgnoreCase))
        {
            // Development default: write messages to disk instead of sending them.
            services.AddSingleton<IEmailSender, FileEmailSender>();
            return services;
        }

        if (string.IsNullOrWhiteSpace(options.Resend.ApiKey))
            throw new InvalidOperationException(
                "Email:Provider is 'Resend' but Email:Resend:ApiKey is empty. Set it with: " +
                "dotnet user-secrets set \"Email:Resend:ApiKey\" \"re_...\"");

        services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
        {
            client.BaseAddress = new Uri(ResendApiBase);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Resend.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
