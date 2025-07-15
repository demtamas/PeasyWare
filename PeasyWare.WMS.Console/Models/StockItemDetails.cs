using System;

namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a single, physical stock item in the warehouse, typically a pallet.
    /// This class holds all relevant details about the item, including its identity, product information,
    /// status, and current location.
    /// </summary>
    public class StockItemDetails
    {
        // --- Identity Properties ---

        /// <summary>
        /// Gets or sets the unique internal identifier for this specific stock item record.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable external identifier, such as a pallet license plate or SSCC.
        /// This is the primary ID scanned by operators.
        /// </summary>
        public string ExternalId { get; set; } = string.Empty;


        // --- Product (SKU) Properties ---

        /// <summary>
        /// Gets or sets the internal ID of the SKU (Stock Keeping Unit) for this product.
        /// </summary>
        public int InternalId { get; set; }

        /// <summary>
        /// Gets or sets the customer-facing SKU identifier for this product.
        /// </summary>
        public string CustomerSkuId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description of the product.
        /// </summary>
        public string SkuDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the standard quantity for a full unit (e.g., a full pallet) of this SKU.
        /// </summary>
        public int FullUnitQty { get; set; }

        /// <summary>
        /// Gets or sets the product's batch or lot number.
        /// </summary>
        public string BatchNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Best Before End (BBE) date for this specific batch.
        /// </summary>
        public DateTime? BestBeforeDate { get; set; }


        // --- Quantity and Status Properties ---

        /// <summary>
        /// Gets or sets the current physical quantity of the product on this stock item.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Gets or sets the quantity of this stock item that is allocated to an outbound order.
        /// </summary>
        public int AllocatedQuantity { get; set; }

        /// <summary>
        /// Gets or sets the current status code of the stock (e.g., "AV", "BL", "MV").
        /// </summary>
        public string StatusCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description of the current status.
        /// </summary>
        public string StatusDescription { get; set; } = string.Empty;


        // --- Location and Reference Properties ---

        /// <summary>
        /// Gets or sets the internal ID of the stock item's current location.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name of the stock item's current location.
        /// </summary>
        public string CurrentLocation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the inbound document reference number associated with this stock item's arrival.
        /// This is typically cleared after the initial putaway is complete.
        /// </summary>
        public string DocumentRef { get; set; } = string.Empty;


        // --- Putaway Preference Properties ---

        /// <summary>
        /// Gets or sets the ID of the preferred storage type for this product (e.g., Racking, Bulk).
        /// </summary>
        public int PreferredStorageType { get; set; }

        /// <summary>
        /// Gets or sets the ID of the preferred storage section for this product (e.g., Top, Floor). Can be null.
        /// </summary>
        public int? PreferredSectionId { get; set; }
    }
}