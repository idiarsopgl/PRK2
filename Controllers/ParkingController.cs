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
using ParkIRC.Services;
using Microsoft.AspNetCore.Authorization;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ParkingController> _logger;
        private readonly IHubContext<ParkingHub>? _hubContext;
        private readonly IParkingService _parkingService;

        public ParkingController(
            ApplicationDbContext context,
            ILogger<ParkingController> logger,
            IParkingService parkingService,
            IHubContext<ParkingHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _parkingService = parkingService;
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
                
                try
                {
                    ViewData["HourlyOccupancy"] = await GetHourlyOccupancy();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading hourly occupancy data");
                    ViewData["HourlyOccupancy"] = new List<object>();
                }
                
                try
                {
                    ViewData["VehicleTypeDistribution"] = await GetVehicleTypeDistribution();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading vehicle type distribution");
                    ViewData["VehicleTypeDistribution"] = new List<object>();
                }
                
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                // Create a minimal dashboard model to avoid empty UI
                var fallbackModel = new DashboardViewModel
                {
                    TotalSpaces = 0,
                    AvailableSpaces = 0,
                    DailyRevenue = 0,
                    WeeklyRevenue = 0,
                    MonthlyRevenue = 0,
                    RecentActivity = new List<ParkingActivity>(),
                    HourlyOccupancy = new List<OccupancyData>()
                };
                
                // Add error message to be displayed in the view
                ViewData["ErrorMessage"] = "Unable to load dashboard data. Please try again later.";
                
                return View(fallbackModel);
            }
        }

        private async Task<DashboardViewModel> GetDashboardData()
        {
            try
            {
                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek); // Start of current week (Sunday)
                var monthStart = new DateTime(today.Year, today.Month, 1); // Start of current month

                // Get space data using separate queries first
            var totalSpaces = await _context.ParkingSpaces.CountAsync();
                var availableSpaces = await _context.ParkingSpaces.CountAsync(s => !s.IsOccupied);

                // Use client-side evaluation for aggregations
                // Daily revenue
                var dailyTransactions = await _context.ParkingTransactions
                    .Where(t => t.PaymentTime.Date == today)
                    .ToListAsync();  // Get the data to client first
                decimal dailySum = dailyTransactions.Sum(t => t.TotalAmount);

                // Weekly revenue
                var weeklyTransactions = await _context.ParkingTransactions
                    .Where(t => t.PaymentTime.Date >= weekStart && t.PaymentTime.Date <= today)
                    .ToListAsync();  // Get the data to client first
                decimal weeklySum = weeklyTransactions.Sum(t => t.TotalAmount);

                // Monthly revenue
                var monthlyTransactions = await _context.ParkingTransactions
                    .Where(t => t.PaymentTime.Date >= monthStart && t.PaymentTime.Date <= today)
                    .ToListAsync();  // Get the data to client first
                decimal monthlySum = monthlyTransactions.Sum(t => t.TotalAmount);

                // Recent activity
                var recentActivity = await _context.ParkingTransactions
                    .Include(t => t.Vehicle)  // Include vehicle in the initial query
                    .Include(t => t.Vehicle.ParkingSpace)  // Include assigned space
                    .OrderByDescending(t => t.EntryTime)
                    .Take(10)
                    .Select(t => new ParkingActivity
                    {
                        VehicleType = t.Vehicle != null ? t.Vehicle.VehicleType : "Unknown",
                        LicensePlate = t.Vehicle != null ? t.Vehicle.VehicleNumber : "Unknown",
                        Timestamp = t.EntryTime,
                        ActionType = t.ExitTime != default(DateTime) ? "Exit" : "Entry",
                        Fee = t.TotalAmount,
                        ParkingType = t.Vehicle != null && t.Vehicle.ParkingSpace != null ? t.Vehicle.ParkingSpace.SpaceType : "Unknown"
                    })
                    .ToListAsync();

                // Get hourly occupancy data
                // Use raw SQLite query for hourly data aggregation
                var hourlyRawData = await _context.ParkingTransactions
                    .Where(t => t.EntryTime.Date == today)
                    .AsNoTracking()
                    .ToListAsync();

                // Client-side grouping and processing
                var hourlyData = hourlyRawData
                    .GroupBy(t => t.EntryTime.Hour)
                    .Select(g => new OccupancyData
                    {
                        Hour = $"{g.Key}:00",
                        OccupancyPercentage = totalSpaces > 0 ? (double)g.Count() / totalSpaces * 100 : 0
                    })
                    .OrderBy(x => x.Hour)
                    .ToList();

                // Vehicle type distribution using client-side processing
                var vehicleTypes = await _context.Vehicles
                    .Where(v => v.IsParked)
                    .AsNoTracking()
                    .ToListAsync();
                
                var typeDistribution = vehicleTypes
                    .GroupBy(v => v.VehicleType)
                    .Select(g => new
                    {
                        Type = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                return new DashboardViewModel
                {
                    TotalSpaces = totalSpaces,
                    AvailableSpaces = availableSpaces,
                    DailyRevenue = dailySum,
                    WeeklyRevenue = weeklySum,
                    MonthlyRevenue = monthlySum,
                    RecentActivity = recentActivity,
                    HourlyOccupancy = hourlyData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                
                // Return a basic fallback model with zeros
                return new DashboardViewModel
                {
                    TotalSpaces = 0,
                    AvailableSpaces = 0,
                    DailyRevenue = 0,
                    WeeklyRevenue = 0,
                    MonthlyRevenue = 0,
                    RecentActivity = new List<ParkingActivity>(),
                    HourlyOccupancy = new List<OccupancyData>()
                };
            }
        }

        public IActionResult VehicleEntry()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordEntry([FromBody] VehicleEntryModel entryModel)
        {
            if (entryModel == null)
            {
                _logger.LogWarning("Vehicle entry model is null");
                return BadRequest(new { error = "Data kendaraan tidak valid" });
            }

            try
            {
                _logger.LogInformation("Processing vehicle entry for {VehicleNumber}, Type: {VehicleType}", 
                    entryModel.VehicleNumber, entryModel.VehicleType);
                    
                // Validate the model
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value != null && kvp.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                        );
                    
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors.Values.SelectMany(v => v)));
                    return BadRequest(new { errors });
                }

                // Normalize vehicle number format
                entryModel.VehicleNumber = entryModel.VehicleNumber.ToUpper().Trim();
                
                // Check vehicle format separately
                var vehicleNumberRegex = new System.Text.RegularExpressions.Regex(@"^[A-Z]{1,2}\s\d{1,4}\s[A-Z]{1,3}$");
                if (!vehicleNumberRegex.IsMatch(entryModel.VehicleNumber))
                {
                    _logger.LogWarning("Invalid vehicle number format: {VehicleNumber}", entryModel.VehicleNumber);
                    return BadRequest(new { error = "Format nomor kendaraan tidak valid. Contoh: B 1234 ABC" });
                }

                // Check if vehicle already exists in the system
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.VehicleNumber == entryModel.VehicleNumber && v.IsParked);
                
                if (existingVehicle != null)
                {
                    _logger.LogWarning("Vehicle {VehicleNumber} is already parked", entryModel.VehicleNumber);
                    return BadRequest(new { error = "Kendaraan sudah terparkir di fasilitas" });
                }

                // Find available parking space based on vehicle type
                var availableSpaces = await _context.ParkingSpaces
                    .Where(ps => !ps.IsOccupied && ps.SpaceType.ToLower() == entryModel.VehicleType.ToLower())
                    .ToListAsync();
                    
                _logger.LogInformation("Found {Count} available spaces for {VehicleType}", 
                    availableSpaces.Count, entryModel.VehicleType);
                    
                if (!availableSpaces.Any())
                {
                    _logger.LogWarning("No parking spaces available for {VehicleType}", entryModel.VehicleType);
                    return BadRequest(new { error = $"Tidak ada slot parkir tersedia untuk {GetVehicleTypeName(entryModel.VehicleType)}" });
                }
                
                // Select the first available space
                var parkingSpace = availableSpaces.First();

                // Use a transaction to ensure data consistency
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    // Create new vehicle record
                    var vehicle = new Vehicle
                    {
                        VehicleNumber = entryModel.VehicleNumber,
                        VehicleType = entryModel.VehicleType,
                        DriverName = entryModel.DriverName,
                        ContactNumber = entryModel.ContactNumber,
                        EntryTime = DateTime.Now,
                        IsParked = true,
                        ParkingSpaceId = parkingSpace.Id
                    };

                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Created vehicle record with ID {VehicleId}", vehicle.Id);

                    // Mark space as occupied and update its properties
                    parkingSpace.IsOccupied = true;
                    parkingSpace.LastOccupiedTime = DateTime.Now;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Updated parking space {SpaceNumber} to occupied", parkingSpace.SpaceNumber);

                    // Create entry transaction record
                    var transactionRecord = new ParkingTransaction
                    {
                        VehicleId = vehicle.Id,
                        ParkingSpaceId = parkingSpace.Id,
                        EntryTime = vehicle.EntryTime,
                        TransactionNumber = GenerateTransactionNumber(),
                        HourlyRate = parkingSpace.HourlyRate,
                        PaymentStatus = "Pending",
                        PaymentMethod = "Cash"
                    };

                    _context.ParkingTransactions.Add(transactionRecord);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Created transaction record with number {TransactionNumber}", 
                        transactionRecord.TransactionNumber);
                
                    // Commit the transaction
                    await transaction.CommitAsync();
                    _logger.LogInformation("Transaction committed successfully for vehicle {VehicleNumber}", 
                        vehicle.VehicleNumber);

                    // Notify clients about the new entry
                    if (_hubContext != null)
                    {
                        try
                        {
                            await _hubContext.Clients.All.SendAsync("NotifyVehicleEntry", vehicle.VehicleNumber);
                            var dashboardData = await GetDashboardData();
                            await _hubContext.Clients.All.SendAsync("UpdateDashboard", dashboardData);
                            _logger.LogInformation("SignalR notifications sent for vehicle {VehicleNumber}", vehicle.VehicleNumber);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error notifying clients about vehicle entry");
                        }
                    }

                    return Ok(new { 
                        message = "Data kendaraan berhasil disimpan",
                        vehicle = new {
                            id = vehicle.Id,
                            vehicleNumber = vehicle.VehicleNumber,
                            entryTime = vehicle.EntryTime
                        },
                        parkingSpace = new {
                            id = parkingSpace.Id,
                            spaceNumber = parkingSpace.SpaceNumber
                        }
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during transaction for vehicle entry: {VehicleNumber}", entryModel.VehicleNumber);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording vehicle entry for {VehicleNumber}", entryModel?.VehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat menyimpan data kendaraan. Silakan coba lagi." });
            }
        }

        private string GetVehicleTypeName(string type)
        {
            return type.ToLower() switch
            {
                "car" => "mobil",
                "motorcycle" => "motor",
                "truck" => "truk",
                _ => type
            };
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
                    Duration = t.ExitTime != default(DateTime) ? 
                        (t.ExitTime - t.EntryTime).TotalHours.ToString("F1") + " hours" : 
                        "In Progress",
                    Amount = t.TotalAmount,
                    Status = t.ExitTime != default(DateTime) ? "Completed" : "In Progress"
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
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);

                if (vehicle == null)
                    return NotFound("Vehicle not found");

                var transaction = await _context.ParkingTransactions
                    .Where(t => t.VehicleId == vehicle.Id && t.ExitTime == default(DateTime))
                    .OrderByDescending(t => t.EntryTime)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                    return NotFound("No active parking transaction found");

                // Update transaction
                transaction.ExitTime = DateTime.Now;
                transaction.TotalAmount = CalculateParkingFee(transaction.EntryTime, transaction.ExitTime, transaction.HourlyRate);
                _context.ParkingTransactions.Update(transaction);

                // Update vehicle and parking space
                vehicle.IsParked = false;
                vehicle.ExitTime = DateTime.Now;
                if (vehicle.ParkingSpace != null)
                {
                vehicle.ParkingSpace.IsOccupied = false;
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
                .Where(ps => !ps.IsOccupied && ps.CurrentVehicle == null && ps.SpaceType.ToLower() == type.ToLower())
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

        // Get available parking spaces by vehicle type
        [HttpGet]
        public async Task<IActionResult> GetAvailableSpaces()
        {
            try
            {
                var spaces = await _context.ParkingSpaces
                    .Where(ps => !ps.IsOccupied)
                    .GroupBy(ps => ps.SpaceType.ToLower())
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);

                return Json(new
                {
                    car = spaces.GetValueOrDefault("car", 0),
                    motorcycle = spaces.GetValueOrDefault("motorcycle", 0),
                    truck = spaces.GetValueOrDefault("truck", 0)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available parking spaces");
                return StatusCode(500, new { error = "Gagal mengambil data slot parkir" });
            }
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

        public async Task<IActionResult> GetParkingTransactions(DateTime? startDate = null, DateTime? endDate = null)
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
            return Json(transactions);
        }

        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.ParkingSpaces.Include(p => p.CurrentVehicle).AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(p => p.CurrentVehicle != null && p.CurrentVehicle.EntryTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(p => p.CurrentVehicle != null && p.CurrentVehicle.EntryTime <= endDate.Value);
            }

            var spaces = await query.ToListAsync();
            return View(spaces);
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(int id)
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.ParkingSpace)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                return NotFound();
            }

            var transaction = await _parkingService.ProcessCheckout(vehicle);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CheckVehicleAvailability(string vehicleNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vehicleNumber))
                {
                    return BadRequest(new { error = "Nomor kendaraan tidak valid" });
                }

                vehicleNumber = vehicleNumber.ToUpper().Trim();
                
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);
                    
                return Ok(new { 
                    isAvailable = existingVehicle == null,
                    message = existingVehicle != null 
                        ? "Kendaraan sudah terparkir di fasilitas" 
                        : "Nomor kendaraan tersedia untuk digunakan"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking vehicle availability for {VehicleNumber}", vehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat memeriksa ketersediaan kendaraan." });
            }
        }
    }
}