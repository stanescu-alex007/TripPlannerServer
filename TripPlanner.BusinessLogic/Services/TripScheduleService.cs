using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class TripScheduleService : ITripScheduleService
    {
        private readonly ITripScheduleRepository _scheduleRepo;
        private readonly ITripRepository _tripRepo;

        private static readonly HashSet<string> ValidStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "Pending", "InProgress", "Done", "Canceled" };

        public TripScheduleService(ITripScheduleRepository scheduleRepo, ITripRepository tripRepo)
        {
            _scheduleRepo = scheduleRepo;
            _tripRepo = tripRepo;
        }

        public async Task<List<TripScheduleItemDto>> GetScheduleAsync(Guid tripId)
        {
            var items = await _scheduleRepo.GetByTripIdAsync(tripId);
            return items.Select(MapToDto).ToList();
        }

        public async Task<Result<TripScheduleItemDto>> AddItemAsync(
            Guid tripId, Guid requesterId, CreateScheduleItemRequest request)
        {
            var trip = await _tripRepo.GetByIdAsync(tripId);
            if (trip is null)
                return Result<TripScheduleItemDto>.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result<TripScheduleItemDto>.Fail("Only the trip owner can manage the schedule.");

            // Date must be within trip range (strip time, compare UTC date only)
            var requestDate = request.Date.Date;
            var startDate = trip.StartDate.ToUniversalTime().Date;
            var endDate = trip.EndDate.ToUniversalTime().Date;

            if (requestDate < startDate || requestDate > endDate)
                return Result<TripScheduleItemDto>.Fail(
                    $"Date must be between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}.");

            var item = new TripScheduleItem
            {
                TripId = tripId,
                Date = DateTime.SpecifyKind(requestDate, DateTimeKind.Utc),
                StartTime = string.IsNullOrWhiteSpace(request.StartTime) ? null : request.StartTime.Trim(),
                EndTime = string.IsNullOrWhiteSpace(request.EndTime) ? null : request.EndTime.Trim(),
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Status = "Pending",
                Order = request.Order,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _scheduleRepo.AddAsync(item);
            return Result<TripScheduleItemDto>.Ok(MapToDto(created));
        }

        public async Task<Result> UpdateItemAsync(
            Guid itemId, Guid requesterId, UpdateScheduleItemRequest request)
        {
            var item = await _scheduleRepo.GetByIdAsync(itemId);
            if (item is null)
                return Result.Fail("Schedule item not found.");

            var trip = await _tripRepo.GetByIdAsync(item.TripId);
            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("Only the trip owner can manage the schedule.");

            var requestDate = request.Date.Date;
            var startDate = trip.StartDate.ToUniversalTime().Date;
            var endDate = trip.EndDate.ToUniversalTime().Date;

            if (requestDate < startDate || requestDate > endDate)
                return Result.Fail(
                    $"Date must be between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}.");

            item.Date = DateTime.SpecifyKind(requestDate, DateTimeKind.Utc);
            item.StartTime = string.IsNullOrWhiteSpace(request.StartTime) ? null : request.StartTime.Trim();
            item.EndTime = string.IsNullOrWhiteSpace(request.EndTime) ? null : request.EndTime.Trim();
            item.Title = request.Title.Trim();
            item.Description = request.Description?.Trim();
            item.Order = request.Order;

            await _scheduleRepo.UpdateAsync(item);
            return Result.Ok();
        }

        public async Task<Result> UpdateStatusAsync(Guid itemId, Guid requesterId, string status)
        {
            if (!ValidStatuses.Contains(status))
                return Result.Fail($"Invalid status '{status}'. Valid values: Pending, InProgress, Done, Canceled.");

            var item = await _scheduleRepo.GetByIdAsync(itemId);
            if (item is null)
                return Result.Fail("Schedule item not found.");

            var trip = await _tripRepo.GetByIdAsync(item.TripId);
            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("Only the trip owner can update schedule status.");

            // Normalize casing
            item.Status = char.ToUpper(status[0]) + status[1..];
            await _scheduleRepo.UpdateAsync(item);
            return Result.Ok();
        }

        public async Task<Result> DeleteItemAsync(Guid itemId, Guid requesterId)
        {
            var item = await _scheduleRepo.GetByIdAsync(itemId);
            if (item is null)
                return Result.Fail("Schedule item not found.");

            var trip = await _tripRepo.GetByIdAsync(item.TripId);
            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("Only the trip owner can manage the schedule.");

            await _scheduleRepo.DeleteAsync(item);
            return Result.Ok();
        }

        private static TripScheduleItemDto MapToDto(TripScheduleItem item) => new()
        {
            Id = item.Id,
            TripId = item.TripId,
            Date = item.Date,
            StartTime = item.StartTime,
            EndTime = item.EndTime,
            Title = item.Title,
            Description = item.Description,
            Status = item.Status,
            Order = item.Order,
            CreatedAt = item.CreatedAt
        };
    }
}
