using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.WEB;

/// <summary>
/// Encrypts two-factor secrets that were stored in plaintext before encryption at rest
/// existed.
///
/// <para>
/// A migration cannot do this — encrypting needs the application's key, which the
/// database never sees. Runs once per start and is idempotent: once every environment
/// has started on this version it finds nothing, and can be deleted.
/// </para>
/// </summary>
public sealed class TwoFactorSecretProtectionBackfill : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TwoFactorSecretProtectionBackfill> _logger;

    public TwoFactorSecretProtectionBackfill(
        IServiceScopeFactory scopeFactory,
        ILogger<TwoFactorSecretProtectionBackfill> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var credentials = scope.ServiceProvider.GetRequiredService<ITwoFactorCredentialService>();
            var converted = await credentials.ProtectLegacySecretsAsync(stoppingToken);

            if (converted > 0)
                _logger.LogWarning("Encrypted {Count} two-factor secret(s) found stored in plaintext.", converted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Not fatal: until it succeeds, affected users can still sign in with a
            // recovery code, and the next start tries again.
            _logger.LogError(ex, "Two-factor secret backfill failed.");
        }
    }
}
