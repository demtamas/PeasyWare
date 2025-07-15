using System;

namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a reservation placed on a specific warehouse location for a putaway task.
    /// This prevents other processes from using the location while a pallet is in transit to it.
    /// </summary>
    public class LocationReservation
    {
        /// <summary>
        /// Gets or sets the unique identifier for the reservation record.
        /// </summary>
        public int ReservationId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the location that is being reserved.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the external ID of the pallet for which the reservation was made.
        /// </summary>
        public string ReservedByPalletId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the user who initiated the putaway task and created the reservation.
        /// </summary>
        public int ReservedByUserId { get; set; }

        /// <summary>
        /// Gets or sets the type of reservation (e.g., "PUTAWAY").
        /// </summary>
        public string ReservationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the reservation was created.
        /// </summary>
        public DateTime ReservedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the reservation will automatically expire
        /// if the task is not completed.
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}
