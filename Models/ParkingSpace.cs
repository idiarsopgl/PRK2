using System;
using System.Collections.Generic;

namespace ParkIRC.Models
{
    public class ParkingSpace
    {
        public int Id { get; set; }
        public required string SpaceNumber { get; set; }
        public required string SpaceType { get; set; }
        public bool IsOccupied { get; set; }
        public DateTime? LastOccupiedTime { get; set; }
        public decimal HourlyRate { get; set; }
        
        // Navigation properties
        public Vehicle? CurrentVehicle { get; set; }
        public ICollection<ParkingTransaction> Transactions { get; set; } = new List<ParkingTransaction>();
    }
}