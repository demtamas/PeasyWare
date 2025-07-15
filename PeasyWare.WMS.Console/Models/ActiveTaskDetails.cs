namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents the details of an in-progress putaway task that is being resumed.
    /// This class is used to hold the information retrieved from the database when an operator
    /// scans a pallet that is already in 'MV' (In Transit) status.
    /// </summary>
    public class ActiveTaskDetails
    {
        /// <summary>
        /// Gets or sets the unique identifier of the active reservation associated with this task.
        /// </summary>
        public int ReservationId { get; set; }

        /// <summary>
        /// Gets or sets the details of the destination location that was originally suggested for this task.
        /// This ensures the operator is directed to the correct, already-reserved location.
        /// </summary>
        public SuggestedLocation Suggestion { get; set; } = new SuggestedLocation();
    }
}