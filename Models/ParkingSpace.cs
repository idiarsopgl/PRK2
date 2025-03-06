using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ParkIRC.Models
{
    public class ParkingSpace
    {
        public int Id { get; set; }
        
        [Required]
        public string SpaceNumber { get; set; }
        
        [Required]
        public string SpaceType { get; set; }
        
        public bool IsOccupied { get; set; }
        public DateTime? LastOccupiedTime { get; set; }
        
        public decimal HourlyRate { get; set; }
        
        public int? CurrentVehicleId { get; set; }
        
        public virtual Vehicle CurrentVehicle { get; set; }
        
        public virtual ICollection<ParkingTransaction> Transactions { get; set; }
    }
}