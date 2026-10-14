using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.WEB;

/// <summary>
/// Passes on ownership offers nobody answered in time.
///
/// <para>
/// Without it an unanswered offer would hold its trip ownerless forever. Offers are also
/// lapsed the moment someone tries to answer one late, so this only has to catch the
/// ones nobody touches — hourly is plenty against a seven-day window.
/// </para>
/// </summary>
public sealed class OwnershipOfferExpiryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OwnershipOfferExpiryService> _logger;

    public OwnershipOfferExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<OwnershipOfferExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var ownership = scope.ServiceProvider.GetRequiredService<ITripOwnershipService>();
                var lapsed = await ownership.ExpireOverdueOffersAsync(stoppingToken);

                if (lapsed > 0)
                    _logger.LogInformation("Passed on {Count} unanswered ownership offer(s).", lapsed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let a failed sweep take the host down; try again next tick.
                _logger.LogError(ex, "Ownership offer expiry sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
