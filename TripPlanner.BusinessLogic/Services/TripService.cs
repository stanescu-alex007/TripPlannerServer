using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class TripService : ITripService
    {
        private readonly ITripRepository _tripRepository;
        private readonly IUserRepository _userRepository;

        public TripService(ITripRepository tripRepository, IUserRepository userRepository)
        {
            _tripRepository = tripRepository;
            _userRepository = userRepository;
        }

        public async Task<List<TripDto>> GetMyTripsAsync(Guid userId)
        {
            var trips = await _tripRepository.GetAllByOwnerIdAsync(userId);
            return trips.Select(MapToDto).ToList();
        }

        public async Task<TripDto?> GetTripByIdAsync(Guid tripId)
        {
            var trip = await _tripRepository.GetByIdWithDetailsAsync(tripId);
            return trip is null ? null : MapToDto(trip);
        }

        public async Task<TripDto> CreateTripAsync(Guid ownerId, CreateTripRequest request)
        {
            var owner = await _userRepository.GetByIdAsync(ownerId);

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = request.Name.Trim(),
                Location = request.Location.Trim(),
                StartDate = request.StartDate.ToUniversalTime(),
                EndDate = request.EndDate.ToUniversalTime(),
                Budget = request.Budget,
                TransportType = request.TransportType,
                Image = request.Image ?? string.Empty,
                Description = request.Description ?? string.Empty,
            };

            var createdTrip = await _tripRepository.CreateAsync(trip);

            // Always add the owner as the first participant
            await _tripRepository.AddParticipantAsync(new TripParticipant
            {
                TripId = createdTrip.Id,
                UserId = ownerId,
                Email = owner?.Email ?? string.Empty,
                Nickname = owner is not null ? owner.FirstName : "Owner",
                Role = "Owner",
                Status = "Confirmed"
            });

            // Add additional participants — no registered account required
            foreach (var p in request.Participants ?? new List<AddParticipantRequest>())
            {
                if (string.IsNullOrWhiteSpace(p.Email)) continue;

                var email = p.Email.Trim().ToLowerInvariant();

                // Skip if this is the owner
                if (owner?.Email?.ToLowerInvariant() == email) continue;

                // Skip duplicates
                var existing = await _tripRepository.GetParticipantByEmailAsync(createdTrip.Id, email);
                if (existing is not null) continue;

                // Optionally link to a registered user
                var user = await _userRepository.GetByEmailAsync(email);

                await _tripRepository.AddParticipantAsync(new TripParticipant
                {
                    TripId = createdTrip.Id,
                    Email = email,
                    UserId = user?.Id,
                    Nickname = p.Nickname.Trim(),
                    Role = p.Role.Trim(),
                    Status = "NotConfirmed"
                });
            }

            var full = await _tripRepository.GetByIdWithDetailsAsync(createdTrip.Id);
            return MapToDto(full!);
        }

        public async Task<Result> UpdateTripAsync(Guid tripId, Guid requesterId, UpdateTripRequest request)
        {
            var trip = await _tripRepository.GetByIdWithDetailsAsync(tripId);

            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("You do not have permission to update this trip.");

            trip.Name = request.Name.Trim();
            trip.Location = request.Location.Trim();
            trip.StartDate = request.StartDate.ToUniversalTime();
            trip.EndDate = request.EndDate.ToUniversalTime();
            trip.Budget = request.Budget;
            trip.TransportType = request.TransportType;
            trip.Description = request.Description ?? trip.Description;

            if (request.Image is not null)
                trip.Image = request.Image;

            await _tripRepository.UpdateAsync(trip);

            // Sync participants if provided
            if (request.Participants is not null)
            {
                // Remove all non-owner participants, then re-add
                var currentParticipants = trip.Participants.ToList();
                foreach (var existing in currentParticipants)
                {
                    if (existing.UserId.HasValue && existing.UserId.Value == trip.OwnerId) continue;
                    await _tripRepository.RemoveParticipantAsync(existing.Id);
                }

                var ownerEntity = await _userRepository.GetByIdAsync(trip.OwnerId);
                var ownerEmail = ownerEntity?.Email?.ToLowerInvariant() ?? string.Empty;

                foreach (var p in request.Participants)
                {
                    if (string.IsNullOrWhiteSpace(p.Email)) continue;

                    var email = p.Email.Trim().ToLowerInvariant();
                    if (email == ownerEmail) continue;

                    var user = await _userRepository.GetByEmailAsync(email);

                    await _tripRepository.AddParticipantAsync(new TripParticipant
                    {
                        TripId = tripId,
                        Email = email,
                        UserId = user?.Id,
                        Nickname = p.Nickname.Trim(),
                        Role = p.Role.Trim(),
                        Status = "NotConfirmed"
                    });
                }
            }

            return Result.Ok();
        }

        public async Task<Result> DeleteTripAsync(Guid tripId, Guid requesterId)
        {
            var trip = await _tripRepository.GetByIdAsync(tripId);

            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("You do not have permission to delete this trip.");

            await _tripRepository.DeleteAsync(trip);
            return Result.Ok();
        }

        public async Task<Result> AddParticipantAsync(Guid tripId, Guid requesterId, AddParticipantRequest request)
        {
            var trip = await _tripRepository.GetByIdAsync(tripId);

            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("Only the trip owner can add participants.");

            if (string.IsNullOrWhiteSpace(request.Email))
                return Result.Fail("Email is required.");

            var email = request.Email.Trim().ToLowerInvariant();

            var existing = await _tripRepository.GetParticipantByEmailAsync(tripId, email);
            if (existing is not null)
                return Result.Fail("This participant is already in the trip.");

            // Optionally link to a registered user
            var user = await _userRepository.GetByEmailAsync(email);

            await _tripRepository.AddParticipantAsync(new TripParticipant
            {
                TripId = tripId,
                Email = email,
                UserId = user?.Id,
                Nickname = request.Nickname.Trim(),
                Role = request.Role.Trim(),
                Status = "NotConfirmed"
            });

            return Result.Ok();
        }

        public async Task<Result> RemoveParticipantAsync(Guid tripId, Guid requesterId, Guid participantId)
        {
            var trip = await _tripRepository.GetByIdWithDetailsAsync(tripId);

            if (trip is null)
                return Result.Fail("Trip not found.");

            if (trip.OwnerId != requesterId)
                return Result.Fail("Only the trip owner can remove participants.");

            var participant = trip.Participants.FirstOrDefault(p => p.Id == participantId);
            if (participant is null)
                return Result.Fail("Participant not found.");

            if (participant.UserId == trip.OwnerId)
                return Result.Fail("The trip owner cannot be removed.");

            await _tripRepository.RemoveParticipantAsync(participantId);
            return Result.Ok();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static TripDto MapToDto(Trip trip)
        {
            return new TripDto
            {
                Id = trip.Id,
                OwnerId = trip.OwnerId,
                Name = trip.Name,
                Location = trip.Location,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                Budget = trip.Budget,
                TransportType = trip.TransportType,
                Image = trip.Image,
                Description = trip.Description,
                Status = CalculateStatus(trip.StartDate, trip.EndDate),
                Participants = (trip.Participants ?? Enumerable.Empty<TripParticipant>())
                    .Select(p => new TripParticipantDto
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Email = p.Email,
                        Nickname = p.Nickname,
                        Role = p.Role,
                        Status = p.Status
                    }).ToList()
            };
        }

        private static string CalculateStatus(DateTime startDate, DateTime endDate)
        {
            var now = DateTime.UtcNow;
            if (now < startDate) return "upcoming";
            if (now > endDate) return "completed";
            return "active";
        }
    }
}

