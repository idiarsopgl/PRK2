using System;

namespace Geex.Models
{
    public class ParkingSpace
    {
        public int Id { get; set; }
        public string SpaceNumber { get; set; } = string.Empty;
        public string SpaceType { get; set; } = string.Empty;
        public bool IsOccupied { get; set; }
        public DateTime? LastOccupiedTime { get; set; }
        public decimal HourlyRate { get; set; }
        
        // Navigation property
        public virtual Vehicle? CurrentVehicle { get; set; }
    }
}