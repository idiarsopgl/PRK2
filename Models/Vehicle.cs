using System;

namespace ParkIRC.Models
{
    public class Vehicle
    {
        public int Id { get; set; }
        public string VehicleNumber { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public int? AssignedSpaceId { get; set; }
        public bool IsParked { get; set; }
        
        // Navigation property
        public virtual ParkingSpace? AssignedSpace { get; set; }
    }
}