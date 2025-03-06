using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ParkIRC.Models
{
    public class Vehicle
    {
        public Vehicle()
        {
            // Initialize collections
            Transactions = new List<ParkingTransaction>();
            
            // Initialize required string properties
            VehicleNumber = string.Empty;
            VehicleType = string.Empty;
            DriverName = string.Empty;
            ContactNumber = string.Empty;
        }
        
        public int Id { get; set; }
        
        [Required]
        public string VehicleNumber { get; set; }
        
        [Required]
        public string VehicleType { get; set; }
        
        public string DriverName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public bool IsParked { get; set; }
        
        public int? AssignedSpaceId { get; set; }
        public virtual ParkingSpace? AssignedSpace { get; set; }
        
        public virtual ICollection<ParkingTransaction> Transactions { get; set; }
    }
}