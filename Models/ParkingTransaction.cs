using System;

namespace Geex.Models
{
    public class ParkingTransaction
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public int ParkingSpaceId { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? PaymentTime { get; set; }
        public string TransactionNumber { get; set; }
        
        // Navigation properties
        public virtual Vehicle Vehicle { get; set; }
        public virtual ParkingSpace ParkingSpace { get; set; }
    }
}