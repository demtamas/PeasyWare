using System;

namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a summary view of an inbound delivery for display in lists.
    /// </summary>
    public class InboundDeliverySummary
    {
        public int InboundId { get; set; }
        public string DocumentRef { get; set; } = string.Empty;
        public DateTime ExpectedArrivalDate { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusDescription { get; set; } = string.Empty;
    }
}
