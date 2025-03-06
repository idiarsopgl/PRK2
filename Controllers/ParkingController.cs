using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Models;
using ParkIRC.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ParkIRC.Hubs;

namespace ParkIRC.Controllers
{
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ParkingController> _logger;
        private readonly IHubContext<ParkingHub>? _hubContext;

        public ParkingController(
            ApplicationDbContext context, 
            ILogger<ParkingController> logger,
            IHubContext<ParkingHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }
        
        [ResponseCache(Duration = 60)]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboardData = await GetDashboardData();
                
                // For backward compatibility with the view
                ViewData["TotalSpaces"] = dashboardData.TotalSpaces;
                ViewData["AvailableSpaces"] = dashboardData.AvailableSpaces;
                ViewData["OccupiedSpaces"] = dashboardData.TotalSpaces - dashboardData.AvailableSpaces;
                ViewData["TodayRevenue"] = dashboardData.DailyRevenue;
                ViewData["HourlyOccupancy"] = await GetHourlyOccupancy();
                ViewData["VehicleTypeDistribution"] = await GetVehicleTypeDistribution();
                ViewData["RecentActivity"] = await GetRecentParkingActivity();
                
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                ViewData["Error"] = "Unable to load dashboard data. Please try again later.";
                return View(new DashboardViewModel());
            }
        }

        private async Task<DashboardViewModel> GetDashboardData()
        {
            var totalSpaces = await _context.ParkingSpaces.CountAsync();
            var occupiedSpaces = await _context.ParkingSpaces.CountAsync(s => s.IsOccupied);
            var availableSpaces = totalSpaces - occupiedSpaces;
            
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var dailyRevenue = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.HasValue && t.PaymentTime.Value.Date == today)
                .SumAsync(t => t.TotalAmount);

            var weeklyRevenue = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.HasValue && t.PaymentTime.Value.Date >= weekStart && t.PaymentTime.Value.Date <= today)
                .SumAsync(t => t.TotalAmount);

            var monthlyRevenue = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.HasValue && t.PaymentTime.Value.Date >= monthStart && t.PaymentTime.Value.Date <= today)
                .SumAsync(t => t.TotalAmount);

            var recentActivity = await _context.ParkingTransactions
                .Where(t => t.Vehicle != null)
                .OrderByDescending(t => t.EntryTime)
                .Take(10)
                .Select(t => new ParkingActivity
                {
                    VehicleType = t.Vehicle != null ? t.Vehicle.VehicleType : "Unknown",
                    LicensePlate = t.Vehicle != null ? t.Vehicle.VehicleNumber : "Unknown",
                    Timestamp = t.EntryTime,
                    ActionType = t.ExitTime.HasValue ? "Exit" : "Entry",
                    Fee = t.TotalAmount,
                    ParkingType = "Unknown"  // We'll set this later
                })
                .ToListAsync();

            // Load vehicle details and update parking type if needed
            foreach(var activity in recentActivity)
            {
                if (activity is ParkingActivity pa)
                {
                    var transaction = await _context.ParkingTransactions
                        .Include(t => t.Vehicle)
                        .ThenInclude(v => v != null ? v.AssignedSpace : null)
                        .FirstOrDefaultAsync(t => t.Vehicle != null && 
                                               t.Vehicle.VehicleNumber == pa.LicensePlate && 
                                               t.EntryTime == pa.Timestamp);
                    
                    if (transaction?.Vehicle?.AssignedSpace != null)
                    {
                        pa.ParkingType = transaction.Vehicle.AssignedSpace.SpaceType;
                    }
                }
            }

            var hourlyData = await _context.ParkingTransactions
                .Where(t => t.EntryTime.Date == today)
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new OccupancyData
                {
                    Hour = $"{g.Key}:00",
                    OccupancyPercentage = (double)g.Count() / totalSpaces * 100
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            return new DashboardViewModel
            {
                TotalSpaces = totalSpaces,
                AvailableSpaces = availableSpaces,
                DailyRevenue = dailyRevenue,
                WeeklyRevenue = weeklyRevenue,
                MonthlyRevenue = monthlyRevenue,
                RecentActivity = recentActivity,
                HourlyOccupancy = hourlyData
            };
        }

        public IActionResult VehicleEntry()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RecordEntry([FromBody] VehicleEntryModel entryModel)
        {
            if (entryModel == null)
                return BadRequest("Vehicle information is required.");
            try
            {
                // Assign parking space automatically
                var availableSpace = await GetAvailableParkingSpace(entryModel.VehicleType);
                if (availableSpace == null)
                    return BadRequest("No available parking spaces");

                // Create and save the vehicle
                var newVehicle = new Vehicle
                {
                    VehicleNumber = entryModel.VehicleNumber,
                    VehicleType = entryModel.VehicleType,
                    DriverName = entryModel.DriverName,
                    ContactNumber = entryModel.ContactNumber,
                    EntryTime = DateTime.Now,
                    AssignedSpaceId = availableSpace.Id,
                    IsParked = true
                };

                _context.Vehicles.Add(newVehicle);
                await _context.SaveChangesAsync();

                // Update parking space
                availableSpace.IsOccupied = true;
                availableSpace.LastOccupiedTime = DateTime.Now;
                _context.ParkingSpaces.Update(availableSpace);

                // Create parking transaction
                var transaction = new ParkingTransaction
                {
                    VehicleId = newVehicle.Id,
                    ParkingSpaceId = availableSpace.Id,
                    EntryTime = newVehicle.EntryTime,
                    HourlyRate = availableSpace.HourlyRate,
                    PaymentStatus = "Pending",
                    TransactionNumber = GenerateTransactionNumber()
                };

                _context.ParkingTransactions.Add(transaction);
                await _context.SaveChangesAsync();
                
                // Notify dashboard clients about data update
                if (_hubContext != null)
                {
                    var dashboardData = await GetDashboardData();
                    await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate", dashboardData);
                }
                
                return Ok(new { vehicle = newVehicle, transaction });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording vehicle entry");
                return StatusCode(500, ex.Message);
            }
        }

        private async Task<object> GetHourlyOccupancy()
        {
            var today = DateTime.Today;
            var hourlyData = await _context.ParkingTransactions
                .Where(t => t.EntryTime.Date == today)
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            return new
            {
                Labels = hourlyData.Select(x => $"{x.Hour}:00").ToList(),
                Data = hourlyData.Select(x => x.Count).ToList()
            };
        }

        private async Task<object> GetVehicleTypeDistribution()
        {
            var distribution = await _context.Vehicles
                .Where(v => v.IsParked)
                .GroupBy(v => v.VehicleType)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return new
            {
                Labels = distribution.Select(x => x.Type).ToList(),
                Data = distribution.Select(x => x.Count).ToList()
            };
        }

        private async Task<List<object>> GetRecentParkingActivity()
        {
            var activities = await _context.ParkingTransactions
                .Where(t => t.Vehicle != null)
                .OrderByDescending(t => t.EntryTime)
                .Take(10)
                .Select(t => new ParkingActivityViewModel
                {
                    VehicleNumber = t.Vehicle != null ? t.Vehicle.VehicleNumber : "Unknown",
                    EntryTime = t.EntryTime,
                    ExitTime = t.ExitTime,
                    Duration = t.ExitTime.HasValue ? 
                        (t.ExitTime.Value - t.EntryTime).TotalHours.ToString("F1") + " hours" : 
                        "In Progress",
                    Amount = t.TotalAmount,
                    Status = t.ExitTime.HasValue ? "Completed" : "In Progress"
                })
                .ToListAsync();
                
            return activities.Cast<object>().ToList();
        }

        public IActionResult VehicleExit()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessExit([FromBody] string vehicleNumber)
        {
            if (string.IsNullOrEmpty(vehicleNumber))
                return BadRequest("Vehicle number is required.");
            try
            {
                var vehicle = await _context.Vehicles
                    .Include(v => v.AssignedSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);

                if (vehicle == null)
                    return NotFound("Vehicle not found");

                var transaction = await _context.ParkingTransactions
                    .Where(t => t.VehicleId == vehicle.Id && t.ExitTime == null)
                    .OrderByDescending(t => t.EntryTime)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                    return NotFound("No active parking transaction found");

                // Update transaction
                transaction.ExitTime = DateTime.Now;
                transaction.TotalAmount = CalculateParkingFee(transaction.EntryTime, transaction.ExitTime.Value, transaction.HourlyRate);
                _context.ParkingTransactions.Update(transaction);

                // Update vehicle and parking space
                vehicle.IsParked = false;
                vehicle.ExitTime = DateTime.Now;
                if (vehicle.AssignedSpace != null)
                {
                    vehicle.AssignedSpace.IsOccupied = false;
                }
                _context.Vehicles.Update(vehicle);

                await _context.SaveChangesAsync();
                
                // Notify dashboard clients about data update
                if (_hubContext != null)
                {
                    var dashboardData = await GetDashboardData();
                    await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate", dashboardData);
                }
                
                return Ok(new { vehicle, transaction });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vehicle exit");
                return StatusCode(500, ex.Message);
            }
        }

        private async Task<ParkingSpace?> GetAvailableParkingSpace(string type)
        {
            return await _context.ParkingSpaces
                .Where(ps => !ps.IsOccupied && ps.SpaceType == type)
                .FirstOrDefaultAsync();
        }

        private static decimal CalculateParkingFee(DateTime entryTime, DateTime exitTime, decimal hourlyRate)
        {
            var duration = exitTime - entryTime;
            var hours = (decimal)Math.Ceiling(duration.TotalHours);
            return hours * hourlyRate;
        }

        private static string GenerateTransactionNumber()
        {
            return $"TRX-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}".ToUpper();
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        // New method for exporting dashboard data
        public async Task<IActionResult> ExportDashboardData()
        {
            try
            {
                var dashboardData = await GetDashboardData();
                // In a real implementation, you would use a library like EPPlus to create an Excel file
                // For now, we'll return a CSV
                var csv = "TotalSpaces,AvailableSpaces,DailyRevenue,WeeklyRevenue,MonthlyRevenue\n";
                csv += $"{dashboardData.TotalSpaces},{dashboardData.AvailableSpaces},{dashboardData.DailyRevenue},{dashboardData.WeeklyRevenue},{dashboardData.MonthlyRevenue}";
                
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "dashboard-report.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting dashboard data");
                return StatusCode(500, "Error exporting dashboard data");
            }
        }
    }
}