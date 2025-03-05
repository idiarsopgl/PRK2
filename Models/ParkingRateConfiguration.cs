using System;

namespace Geex.Models
{
    public class ParkingRateConfiguration
    {
        public int Id { get; set; }
        public required string VehicleType { get; set; }
        public decimal BaseRate { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal DailyRate { get; set; }
        public decimal WeeklyRate { get; set; }
        public decimal MonthlyRate { get; set; }
        public decimal PenaltyRate { get; set; }
        public bool IsActive { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public required string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public required string LastModifiedBy { get; set; }
        public DateTime? LastModifiedAt { get; set; }
    }
}