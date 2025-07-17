using System;

namespace PeasyWare.WMS.Console.Models
{
    /// <summary>
    /// Represents a summary of an inbound delivery that is ready for the receiving process.
    /// </summary>
    public class ReceivableDelivery
    {
        public int InboundId { get; set; }
        public string DocumentRef { get; set; } = string.Empty;
        public DateTime ExpectedArrivalDate { get; set; }
        public string StatusDescription { get; set; } = string.Empty;
    }
}