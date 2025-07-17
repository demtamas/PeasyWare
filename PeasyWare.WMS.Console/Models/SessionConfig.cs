namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents the application-wide configuration settings loaded from the database.
    /// This object holds operational parameters that can be changed without recompiling the application.
    /// </summary>
    public class SessionConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether the application should allow users to log in.
        /// If set to false, the application will display a maintenance message and exit.
        /// </summary>
        public bool EnableLogin { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether verbose debugging information should be logged.
        /// </summary>
        public bool EnableDebugging { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether operators are allowed to manually modify
        /// a system-suggested putaway location.
        /// </summary>
        public bool AllowPutawayModification { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the receiving screen should display the list
        /// of expected items. If false, it enables a "blind" receiving workflow.
        /// </summary>
        public bool ShowExpectedOnReceive { get; set; } // Add this line

        /// <summary>
        /// Gets or sets the duration in minutes that a location reservation remains active
        /// before being considered expired by the system. Defaults to 15 minutes.
        /// </summary>
        public int ReservationTimeoutMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets the duration in minutes after which a pallet in 'MV' (In Transit) status
        /// is considered "stuck" and is automatically reset by the maintenance job. Defaults to 15 minutes.
        /// </summary>
        public int UnlockTimeoutMinutes { get; set; } = 15;
    }
}
