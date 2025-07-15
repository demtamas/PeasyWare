namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a comprehensive set of details for a single warehouse location.
    /// This class is used to hold the results from stored procedures that query location properties,
    /// such as its status, capacity, and any active reservations.
    /// </summary>
    public class LocationDetails
    {
        /// <summary>
        /// Gets or sets the unique internal identifier for the location.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name of the location (e.g., "A1-01-04").
        /// </summary>
        public string LocationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the location's type (e.g., Racking, Bulk).
        /// </summary>
        public int TypeId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the location's type allows for
        /// multiple putaway tasks to be assigned to it simultaneously.
        /// </summary>
        public bool AllowConcurrentPutaway { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the location is active and available for use.
        /// If false, the location is blocked for operational reasons.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets any operational notes associated with the location (e.g., "Damaged racking").
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of pallets the location can hold.
        /// </summary>
        public int CapacityTotal { get; set; }

        /// <summary>
        /// Gets or sets the number of pallets currently physically stored in the location.
        /// </summary>
        public int CapacityUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of active putaway reservations for this location.
        /// This represents stock that is "in-flight" to this location.
        /// </summary>
        public int ReservedCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there is at least one active putaway reservation for this location.
        /// </summary>
        public bool IsReservedForPutaway { get; set; }
    }
}
