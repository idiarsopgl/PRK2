using System;
using System.Collections.Generic;

namespace ParkIRC.Models
{
    public class Vehicle
    {
        public int Id { get; set; }
        public required string VehicleNumber { get; set; }
        public required string VehicleType { get; set; }
        public required string DriverName { get; set; }
        public string? ContactNumber { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public bool IsParked { get; set; }
        
        // Navigation properties
        public int? AssignedSpaceId { get; set; }
        public ParkingSpace? AssignedSpace { get; set; }
        public ICollection<ParkingTransaction> Transactions { get; set; } = new List<ParkingTransaction>();
    }
}