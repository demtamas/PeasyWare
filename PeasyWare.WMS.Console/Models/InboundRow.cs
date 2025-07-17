using System;

namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a single line item on an inbound delivery.
    /// </summary>
    public class InboundRow
    {
        public int SkuId { get; set; }
        public string SkuName { get; set; } = string.Empty;
        public string SkuDescription { get; set; } = string.Empty;
        public int ExpectedQty { get; set; }
        public int ReceivedQty { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime? BestBeforeDate { get; set; }
    }
}
