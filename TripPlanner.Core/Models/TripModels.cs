using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class TripDto
    {
        public Guid Id { get; set; }

        /// <summary>Null while the trip waits for a new owner.</summary>
        public Guid? OwnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>"yyyy-MM-dd" — a calendar day, deliberately not a timestamp.</summary>
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;

        public decimal Budget { get; set; }

        /// <summary>ISO 4217, so the client never has to guess what the amount means.</summary>
        public string Currency { get; set; } = "EUR";

        public string TransportType { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string? Description { get; set; }

        /// <summary>"upcoming" | "active" | "completed" — derived from the dates.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>What the calling user is allowed to do, so the UI can hide dead controls.</summary>
        public bool CanManage { get; set; }
        public bool IsOwner { get; set; }

        /// <summary>The owner left and the trip is being offered to the next person in line.</summary>
        public bool AwaitingOwner { get; set; }

        /// <summary>The offer currently in progress, when <see cref="AwaitingOwner"/> is true.</summary>
        public OwnershipTransferDto? OwnershipTransfer { get; set; }

        public List<TripParticipantDto> Participants { get; set; } = new();
    }

    /// <summary>A trip in the caller's deleted list, on its way out.</summary>
    public class DeletedTripDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>"yyyy-MM-dd"</summary>
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;

        public DateTime DeletedAt { get; set; }

        /// <summary>When it is removed from the database for good.</summary>
        public DateTime PurgeAt { get; set; }
    }

    public class CreateTripRequest
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(300)]
        public string Location { get; set; } = string.Empty;

        /// <summary>"yyyy-MM-dd". A plain date, so no timezone conversion can shift it.</summary>
        [Required]
        [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "StartDate must be in yyyy-MM-dd format.")]
        public string StartDate { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "EndDate must be in yyyy-MM-dd format.")]
        public string EndDate { get; set; } = string.Empty;

        [Range(0, 1_000_000)]
        public decimal Budget { get; set; }

        [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code.")]
        public string Currency { get; set; } = "EUR";

        [Required, MaxLength(50)]
        public string TransportType { get; set; } = "plane";

        [MaxLength(500)]
        public string? Image { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public List<AddParticipantRequest> Participants { get; set; } = new();
    }

    public class UpdateTripRequest : CreateTripRequest
    {
        /// <summary>Null leaves the roster untouched; a list replaces the non-owner rows.</summary>
        public new List<AddParticipantRequest>? Participants { get; set; }
    }
}
