using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ParkIRC.Models
{
    public class Shift
    {
        public Shift()
        {
            Vehicles = new List<Vehicle>();
            Operators = new List<Operator>();
        }

        public int Id { get; set; }
        
        [Required]
        public string ShiftName { get; set; } = string.Empty;
        
        [Required]
        public TimeSpan StartTime { get; set; }
        
        [Required]
        public TimeSpan EndTime { get; set; }
        
        public DateTime Date { get; set; }
        
        public bool IsActive { get; set; }
        
        public virtual ICollection<Vehicle> Vehicles { get; set; }
        public virtual ICollection<Operator> Operators { get; set; }
    }
} 