namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a system-suggested destination location for a putaway task.
    /// This class holds the key details needed to direct an operator to the correct location.
    /// </summary>
    public class SuggestedLocation
    {
        /// <summary>
        /// Gets or sets the unique internal identifier for the suggested location.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name of the suggested location (e.g., "A1-01-04").
        /// </summary>
        public string LocationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the aisle where the suggested location is found. Can be null for non-racking locations.
        /// </summary>
        public string Aisle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the section where the suggested location is found (e.g., "Top", "Floor").
        /// </summary>
        public string SectionName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the location's type (e.g., "Racking", "Bulk").
        /// </summary>
        public string TypeName { get; set; } = string.Empty;
    }
}
