using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.WEB;

/// <summary>
/// Removes trips from the database once their restore window has passed.
///
/// <para>
/// Soft deletion is what makes undo possible; keeping the rows forever afterwards only
/// grows the tables and keeps personal data nobody asked us to keep.
/// </para>
/// </summary>
public sealed class DeletedTripPurgeService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<DeletedTripPurgeService> _logger;

    public DeletedTripPurgeService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<DeletedTripPurgeService> logger)
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
                var cutoff = _clock.GetUtcNow().UtcDateTime - TripRetention.DeletedTripWindow;

                await using var scope = _scopeFactory.CreateAsyncScope();

                var trips = scope.ServiceProvider.GetRequiredService<ITripRepository>();
                var covers = await trips.PurgeDeletedBeforeAsync(cutoff, stoppingToken);

                // After the rows are gone, so a failed purge never leaves a trip without its cover.
                var storage = scope.ServiceProvider.GetRequiredService<IImageStorage>();
                foreach (var cover in covers)
                    await storage.DeleteAsync(cover, stoppingToken);

                if (covers.Count > 0)
                    _logger.LogInformation(
                        "Purged {Count} trip(s) deleted before {Cutoff:u}.", covers.Count, cutoff);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let a failed sweep take the host down; try again next tick.
                _logger.LogError(ex, "Deleted-trip purge failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
