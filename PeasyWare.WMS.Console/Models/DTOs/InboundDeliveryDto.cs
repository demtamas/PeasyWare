namespace PeasyWare.WMS.Console.Models.DTOs
{
    public class InboundDeliveryDto
    {
        public string? DeliveryNumber { get; set; }
        public string? SupplierCode { get; set; }
        public DateTime? EstimatedArrival { get; set; }
        public List<InboundLineDto> Lines { get; set; } = new();
    }

    public class InboundLineDto
    {
        public string? SKU { get; set; }
        public int Quantity { get; set; }
        public string? Status { get; set; } = "AV"; // Default to 'AV' if not specified
        public string? ExternalId { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? BestBeforeDate { get; set; }
    }
}
