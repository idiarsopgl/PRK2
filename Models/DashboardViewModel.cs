using System;
using System.Collections.Generic;

namespace ParkIRC.Models
{
    public class DashboardViewModel
    {
        public int TotalSpaces { get; set; }
        public int AvailableSpaces { get; set; }
        public List<ParkingActivity> RecentActivity { get; set; } = new List<ParkingActivity>();
        public decimal DailyRevenue { get; set; }
        public decimal WeeklyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public List<OccupancyData> HourlyOccupancy { get; set; } = new List<OccupancyData>();
    }

    public class OccupancyData
    {
        public string Hour { get; set; } = string.Empty;
        public double OccupancyPercentage { get; set; }
    }

    public class ParkingActivity
    {
        public string VehicleType { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string ParkingType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; } = string.Empty; // "Entry" or "Exit"
        public decimal? Fee { get; set; } // Null for entry, has value for exit
    }
} 