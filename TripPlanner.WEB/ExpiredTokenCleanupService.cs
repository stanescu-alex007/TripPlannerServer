using TripPlanner.Core.Interfaces.IRepositories;

namespace TripPlanner.WEB;

/// <summary>
/// Prunes expired refresh and one-time tokens.
///
/// <para>
/// Without this the tables only ever grow — the previous implementation had no cleanup
/// at all. Revoked rows are kept past their expiry deliberately: reuse detection needs
/// to still find a revoked token, so only rows whose lifetime has fully lapsed are
/// removed, and then only after a grace period.
/// </para>
/// </summary>
public sealed class ExpiredTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>Expired rows linger this long so replays still hit a row, not a blank.</summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiredTokenCleanupService> _logger;

    public ExpiredTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<ExpiredTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let a failed sweep take the host down; try again next tick.
                _logger.LogError(ex, "Token cleanup failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime - GracePeriod;

        using var scope = _scopeFactory.CreateScope();

        var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var userTokens = scope.ServiceProvider.GetRequiredService<IUserTokenRepository>();

        var removedRefresh = await refreshTokens.DeleteExpiredAsync(cutoff, ct);
        var removedUser = await userTokens.DeleteExpiredAsync(cutoff, ct);

        if (removedRefresh + removedUser > 0)
        {
            _logger.LogInformation(
                "Pruned {RefreshCount} refresh tokens and {UserCount} one-time tokens expired before {Cutoff:u}.",
                removedRefresh, removedUser, cutoff);
        }
    }
}
