using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Models;
using ParkIRC.Data;
using System.Collections.Generic;

namespace ParkIRC.Controllers
{
    public class ReportingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.ParkingTransactions.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(t => t.EntryTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.ExitTime <= endDate.Value);
            }

            var transactions = await query.ToListAsync();
            var report = new
            {
                TotalRevenue = transactions.Sum(t => t.Amount),
                AverageOccupancy = await CalculateAverageOccupancy(startDate, endDate),
                PeakHours = await CalculatePeakHours(startDate, endDate)
            };

            return View(report);
        }

        private async Task<double> CalculateAverageOccupancy(DateTime? startDate, DateTime? endDate)
        {
            var totalSpaces = await _context.ParkingSpaces.CountAsync();
            var occupiedSpacesCount = await _context.ParkingSpaces
                .CountAsync(s => s.IsOccupied);

            return (double)occupiedSpacesCount / totalSpaces * 100;
        }

        private async Task<string> CalculatePeakHours(DateTime? startDate, DateTime? endDate)
        {
            var transactions = await _context.ParkingTransactions
                .Where(t => (!startDate.HasValue || t.EntryTime >= startDate.Value) &&
                           (!endDate.HasValue || t.ExitTime <= endDate.Value))
                .ToListAsync();

            var hourlyCount = transactions
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            return hourlyCount != null
                ? $"{hourlyCount.Hour:D2}:00 - {hourlyCount.Hour:D2}:59"
                : "No data available";
        }

        public async Task<IActionResult> DailyRevenue(DateTime? date = null)
        {
            var queryDate = date ?? DateTime.Today;
            var nextDay = queryDate.AddDays(1);

            var transactions = await _context.ParkingTransactions
                .Where(t => t.ExitTime >= queryDate && t.ExitTime < nextDay)
                .ToListAsync();

            var report = new
            {
                Date = queryDate.ToString("yyyy-MM-dd"),
                TotalRevenue = transactions.Sum(t => t.TotalAmount),
                TransactionCount = transactions.Count,
                AverageAmount = transactions.Any() ? transactions.Average(t => t.TotalAmount) : 0
            };

            return View(report);
        }

        public async Task<IActionResult> OccupancyReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.Today.AddDays(-7);
            var end = endDate ?? DateTime.Today;

            var dailyOccupancy = await _context.ParkingSpaces
                .GroupBy(s => s.LastOccupiedTime.HasValue && s.LastOccupiedTime.Value.Date >= start && s.LastOccupiedTime.Value.Date <= end 
                    ? s.LastOccupiedTime.Value.Date 
                    : DateTime.Today)
                .Select(g => new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    OccupancyRate = g.Count(s => s.IsOccupied) * 100.0 / g.Count()
                })
                .OrderBy(r => r.Date)
                .ToListAsync();

            return View(dailyOccupancy);
        }

        public async Task<IActionResult> VehicleTypeStatistics(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var vehicleTypes = await _context.Vehicles
                .Where(v => v.EntryTime.Date >= start.Date && v.EntryTime.Date <= end.Date)
                .GroupBy(v => v.VehicleType)
                .Select(g => new
                {
                    VehicleType = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(v => v.Transactions.Sum(t => t.TotalAmount))
                })
                .ToListAsync();

            return View(vehicleTypes);
        }

        public async Task<IActionResult> PeakHourAnalysis(DateTime? date = null)
        {
            var queryDate = date ?? DateTime.Today;
            var nextDay = queryDate.AddDays(1);

            var hourlyData = await _context.ParkingTransactions
                .Where(t => t.EntryTime >= queryDate && t.EntryTime < nextDay)
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(t => t.TotalAmount)
                })
                .OrderBy(h => h.Hour)
                .ToListAsync();

            return View(hourlyData);
        }
    }
}