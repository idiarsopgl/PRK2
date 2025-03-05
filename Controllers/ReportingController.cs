using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Geex.Models;
using Geex.Data;
using System.Collections.Generic;

namespace Geex.Controllers
{
    public class ReportingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> DailyRevenue(DateTime? date)
        {
            date ??= DateTime.Today;
            var dailyTransactions = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.HasValue && t.PaymentTime.Value.Date == date.Value.Date)
                .ToListAsync();

            var dailyRevenue = new
            {
                TotalAmount = dailyTransactions.Sum(t => t.TotalAmount),
                TransactionCount = dailyTransactions.Count,
                Date = date.Value.Date,
                HourlyBreakdown = dailyTransactions
                    .GroupBy(t => t.PaymentTime?.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Amount = g.Sum(t => t.TotalAmount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Hour)
                    .ToList()
            };

            return View(dailyRevenue);
        }

        public async Task<IActionResult> OccupancyReport(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Today.AddDays(-7);
            endDate ??= DateTime.Today;

            var occupancyData = await _context.ParkingTransactions
                .Where(t => t.EntryTime.Date >= startDate.Value.Date && 
                           (t.ExitTime == null || t.ExitTime.Value.Date <= endDate.Value.Date))
                .GroupBy(t => t.EntryTime.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalVehicles = g.Count(),
                    AverageStayDuration = g.Average(t => 
                        ((t.ExitTime ?? DateTime.Now) - t.EntryTime).TotalHours)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return View(occupancyData);
        }

        public async Task<IActionResult> VehicleTypeStatistics(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today;

            var vehicleStats = await _context.ParkingTransactions
                .Include(t => t.Vehicle)
                .Where(t => t.EntryTime.Date >= startDate.Value.Date && 
                           t.EntryTime.Date <= endDate.Value.Date)
                .GroupBy(t => t.Vehicle.VehicleType)
                .Select(g => new
                {
                    VehicleType = g.Key,
                    Count = g.Count(),
                    TotalRevenue = g.Sum(t => t.TotalAmount),
                    AverageStayDuration = g.Average(t => 
                        ((t.ExitTime ?? DateTime.Now) - t.EntryTime).TotalHours)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return View(vehicleStats);
        }

        public async Task<IActionResult> PeakHourAnalysis(DateTime? date)
        {
            date ??= DateTime.Today;

            var peakHours = await _context.ParkingTransactions
                .Where(t => t.EntryTime.Date == date.Value.Date)
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    VehicleCount = g.Count(),
                    Revenue = g.Sum(t => t.TotalAmount)
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            return View(peakHours);
        }
    }
}