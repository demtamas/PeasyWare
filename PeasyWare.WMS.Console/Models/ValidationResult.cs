namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Encapsulates the result of a validation operation.
    /// This class provides a standard way to return both a success/failure status
    /// and a corresponding message or data payload from any validation method.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the validation was successful.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets a message providing details about the validation result,
        /// typically used to explain why a validation failed.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stock item that was validated.
        /// This property will typically be populated only on a successful validation.
        /// </summary>
        public StockItemDetails? StockItem { get; set; }

        /// <summary>
        /// Creates a new ValidationResult instance representing a successful validation.
        /// </summary>
        /// <param name="stockItem">The stock item that passed validation.</param>
        /// <returns>A new ValidationResult object with IsValid set to true.</returns>
        public static ValidationResult Success(StockItemDetails stockItem)
        {
            return new ValidationResult { IsValid = true, StockItem = stockItem };
        }

        /// <summary>
        /// Creates a new ValidationResult instance representing a failed validation.
        /// </summary>
        /// <param name="message">The reason why the validation failed.</param>
        /// <returns>A new ValidationResult object with IsValid set to false and a descriptive message.</returns>
        public static ValidationResult Fail(string message)
        {
            return new ValidationResult { IsValid = false, Message = message };
        }
    }
}
