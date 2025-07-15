namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents an authenticated user of the WMS application.
    /// This class holds the essential details of the user's identity and role.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Gets or sets the unique internal identifier for the user.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Gets or sets the user's login name.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's full name for display purposes.
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the primary role assigned to the user (e.g., "Operator", "Admin").
        /// </summary>
        public string RoleName { get; set; } = string.Empty;
    }
}
