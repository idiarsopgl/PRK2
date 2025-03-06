using System;

namespace ParkIRC.Models
{
    public class ParkingTransaction
    {
        public int Id { get; set; }
        public required string TransactionNumber { get; set; }
        public int VehicleId { get; set; }
        public int ParkingSpaceId { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalAmount { get; set; }
        public required string PaymentStatus { get; set; }
        public required string PaymentMethod { get; set; }
        public DateTime PaymentTime { get; set; }
        
        // Navigation properties
        public Vehicle Vehicle { get; set; } = null!;
        public ParkingSpace ParkingSpace { get; set; } = null!;
    }
}