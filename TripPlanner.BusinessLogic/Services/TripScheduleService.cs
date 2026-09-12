using System.Globalization;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class TripScheduleService : ITripScheduleService
    {
        private readonly ITripScheduleRepository _schedule;
        private readonly ITripRepository _trips;
        private readonly TimeProvider _clock;

        public TripScheduleService(
            ITripScheduleRepository schedule, ITripRepository trips, TimeProvider clock)
        {
            _schedule = schedule;
            _trips = trips;
            _clock = clock;
        }

        public async Task<Result<List<TripScheduleItemDto>>> GetScheduleAsync(
            Guid tripId, Guid requesterId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null)
                return Result<List<TripScheduleItemDto>>.Fail(ErrorCodes.TripNotFound);

            // Anyone on the trip may read the schedule; only managers may change it.
            if (!TripAccess.CanView(trip, requesterId))
                return Result<List<TripScheduleItemDto>>.Fail(ErrorCodes.TripForbidden);

            var items = await _schedule.GetByTripIdAsync(tripId, ct);

            return Result<List<TripScheduleItemDto>>.Ok(items.Select(MapToDto).ToList());
        }

        public async Task<Result<TripScheduleItemDto>> AddItemAsync(
            Guid tripId, Guid requesterId, CreateScheduleItemRequest request, CancellationToken ct = default)
        {
            var (trip, error) = await LoadForManageAsync(tripId, requesterId, ct);
            if (error is not null) return Result<TripScheduleItemDto>.Fail(error);

            if (!TryParseDay(request.Date, out var date))
                return Result<TripScheduleItemDto>.Fail(ErrorCodes.ValidationFailed);

            if (date < trip!.StartDate || date > trip.EndDate)
                return Result<TripScheduleItemDto>.Fail(ErrorCodes.ScheduleDateOutOfRange);

            var times = ParseTimes(request.StartTime, request.EndTime);
            if (times is null)
                return Result<TripScheduleItemDto>.Fail(ErrorCodes.ValidationFailed);

            var item = new TripScheduleItem
            {
                TripId = tripId,
                Date = date,
                StartTime = times.Value.Start,
                EndTime = times.Value.End,
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Status = TripScheduleItemStatus.Pending,
                Order = request.Order
            };

            var created = await _schedule.AddAsync(item, ct);

            return Result<TripScheduleItemDto>.Ok(MapToDto(created));
        }

        public async Task<Result> UpdateItemAsync(
            Guid tripId, Guid itemId, Guid requesterId, UpdateScheduleItemRequest request, CancellationToken ct = default)
        {
            var (trip, error) = await LoadForManageAsync(tripId, requesterId, ct);
            if (error is not null) return Result.Fail(error);

            var item = await _schedule.GetByIdAsync(itemId, ct);

            // Checking TripId too stops an item being reached through a trip the caller
            // happens to own.
            if (item is null || item.TripId != tripId)
                return Result.Fail(ErrorCodes.ScheduleItemNotFound);

            if (!TryParseDay(request.Date, out var date))
                return Result.Fail(ErrorCodes.ValidationFailed);

            if (date < trip!.StartDate || date > trip.EndDate)
                return Result.Fail(ErrorCodes.ScheduleDateOutOfRange);

            var times = ParseTimes(request.StartTime, request.EndTime);
            if (times is null) return Result.Fail(ErrorCodes.ValidationFailed);

            item.Date = date;
            item.StartTime = times.Value.Start;
            item.EndTime = times.Value.End;
            item.Title = request.Title.Trim();
            item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            item.Order = request.Order;

            await _schedule.UpdateAsync(item, ct);
            return Result.Ok();
        }

        public async Task<Result> UpdateStatusAsync(
            Guid tripId, Guid itemId, Guid requesterId, string status, CancellationToken ct = default)
        {
            // Map whatever casing the client sent onto the canonical value. The old code
            // upper-cased only the first letter, turning "inprogress" into "Inprogress" —
            // a value no consumer recognised.
            var normalized = TripScheduleItemStatus.Normalize(status);
            if (normalized is null)
                return Result.Fail(ErrorCodes.ScheduleStatusInvalid);

            var (_, error) = await LoadForManageAsync(tripId, requesterId, ct);
            if (error is not null) return Result.Fail(error);

            var item = await _schedule.GetByIdAsync(itemId, ct);
            if (item is null || item.TripId != tripId)
                return Result.Fail(ErrorCodes.ScheduleItemNotFound);

            item.Status = normalized;
            await _schedule.UpdateAsync(item, ct);

            return Result.Ok();
        }

        public async Task<Result> DeleteItemAsync(
            Guid tripId, Guid itemId, Guid requesterId, CancellationToken ct = default)
        {
            var (_, error) = await LoadForManageAsync(tripId, requesterId, ct);
            if (error is not null) return Result.Fail(error);

            var item = await _schedule.GetByIdAsync(itemId, ct);
            if (item is null || item.TripId != tripId)
                return Result.Fail(ErrorCodes.ScheduleItemNotFound);

            await _schedule.DeleteAsync(item, ct);
            return Result.Ok();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task<(Trip? Trip, Error? Error)> LoadForManageAsync(
            Guid tripId, Guid requesterId, CancellationToken ct)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return (null, ErrorCodes.TripNotFound);

            // Hide existence from outsiders; tell insiders they lack the rights.
            if (!TripAccess.CanView(trip, requesterId)) return (null, ErrorCodes.TripNotFound);
            if (!TripAccess.CanManage(trip, requesterId)) return (null, ErrorCodes.TripForbidden);

            return (trip, null);
        }

        /// <summary>
        /// Parses "yyyy-MM-dd" into UTC midnight. Building the DateTime with
        /// <c>SpecifyKind</c> instead of converting it is what keeps the stored day equal
        /// to the day the user picked.
        /// </summary>
        private static bool TryParseDay(string value, out DateTime utcMidnight)
        {
            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day))
            {
                utcMidnight = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                return true;
            }

            utcMidnight = default;
            return false;
        }

        /// <summary>Returns null when a value is unparseable or the range is inverted.</summary>
        private static (TimeOnly? Start, TimeOnly? End)? ParseTimes(string? start, string? end)
        {
            TimeOnly? parsedStart = null;
            TimeOnly? parsedEnd = null;

            if (!string.IsNullOrWhiteSpace(start))
            {
                if (!TimeOnly.TryParseExact(start.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var s)) return null;
                parsedStart = s;
            }

            if (!string.IsNullOrWhiteSpace(end))
            {
                if (!TimeOnly.TryParseExact(end.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var e)) return null;
                parsedEnd = e;
            }

            if (parsedStart.HasValue && parsedEnd.HasValue && parsedEnd < parsedStart)
                return null;

            return (parsedStart, parsedEnd);
        }

        private static TripScheduleItemDto MapToDto(TripScheduleItem item) => new()
        {
            Id = item.Id,
            TripId = item.TripId,
            Date = item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StartTime = item.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            EndTime = item.EndTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Title = item.Title,
            Description = item.Description,
            Status = item.Status,
            Order = item.Order,
            CreatedAt = item.CreatedAt
        };
    }
}
