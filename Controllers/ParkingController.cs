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
using ParkIRC.Extensions;
using System.Text.Json;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ParkingController> _logger;
        private readonly IHubContext<ParkingHub> _hubContext;
        private readonly IParkingService _parkingService;

        public ParkingController(
            ApplicationDbContext context,
            ILogger<ParkingController> logger,
            IParkingService parkingService,
            IHubContext<ParkingHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _parkingService = parkingService;
            _hubContext = hubContext;
        }
        
        [ResponseCache(Duration = 60)]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                var monthStart = new DateTime(today.Year, today.Month, 1);

                // Get all required data in parallel for better performance
                var totalSpacesTask = _context.ParkingSpaces.CountAsync();
                var availableSpacesTask = _context.ParkingSpaces.CountAsync(s => !s.IsOccupied);
                var dailyRevenueTask = _context.ParkingTransactions
                    .Where(t => t.PaymentTime.Date == today)
                    .SumDecimalAsync(t => t.TotalAmount);
                var weeklyRevenueTask = _context.ParkingTransactions
                    .Where(t => t.PaymentTime.Date >= weekStart && t.PaymentTime.Date <= today)
                    .SumDecimalAsync(t => t.TotalAmount);
                var monthlyRevenueTask = _context.ParkingTransactions
                    .Where(t => t.PaymentTime.Date >= monthStart && t.PaymentTime.Date <= today)
                    .SumDecimalAsync(t => t.TotalAmount);
                var recentActivityTask = GetRecentActivity();
                var hourlyOccupancyTask = GetHourlyOccupancyData();
                var vehicleDistributionTask = GetVehicleTypeDistribution();

                await Task.WhenAll(
                    totalSpacesTask, availableSpacesTask, dailyRevenueTask,
                    weeklyRevenueTask, monthlyRevenueTask, recentActivityTask,
                    hourlyOccupancyTask, vehicleDistributionTask
                );

                var dashboardData = new DashboardViewModel
                {
                    TotalSpaces = await totalSpacesTask,
                    AvailableSpaces = await availableSpacesTask,
                    DailyRevenue = await dailyRevenueTask,
                    WeeklyRevenue = await weeklyRevenueTask,
                    MonthlyRevenue = await monthlyRevenueTask,
                    RecentActivity = await recentActivityTask,
                    HourlyOccupancy = await hourlyOccupancyTask,
                    VehicleDistribution = await vehicleDistributionTask
                };
                
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                return View("Error", new ParkIRC.Models.ErrorViewModel 
                { 
                    Message = "Terjadi kesalahan saat memuat dashboard. Silakan coba lagi nanti.",
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }

        private async Task<List<ParkingActivity>> GetRecentActivity()
        {
            return await _context.ParkingTransactions
                .Include(t => t.Vehicle)
                .Include(t => t.ParkingSpace)
                .Where(t => t.Vehicle != null)
                    .OrderByDescending(t => t.EntryTime)
                    .Take(10)
                    .Select(t => new ParkingActivity
                    {
                    VehicleType = t.Vehicle != null ? t.Vehicle.VehicleType ?? "Unknown" : "Unknown",
                    LicensePlate = t.Vehicle != null ? t.Vehicle.VehicleNumber ?? "Unknown" : "Unknown",
                        Timestamp = t.EntryTime,
                        ActionType = t.ExitTime != default(DateTime) ? "Exit" : "Entry",
                        Fee = t.TotalAmount,
                    ParkingType = t.ParkingSpace != null ? t.ParkingSpace.SpaceType ?? "Unknown" : "Unknown"
                    })
                    .ToListAsync();
        }

        private async Task<List<OccupancyData>> GetHourlyOccupancyData()
        {
            var today = DateTime.Today;
            var totalSpaces = await _context.ParkingSpaces.CountAsync();
            
            var hourlyData = await _context.ParkingTransactions
                    .Where(t => t.EntryTime.Date == today)
                    .GroupBy(t => t.EntryTime.Hour)
                    .Select(g => new OccupancyData
                    {
                    Hour = $"{g.Key:D2}:00",
                    Count = g.Count(),
                        OccupancyPercentage = totalSpaces > 0 ? (double)g.Count() / totalSpaces * 100 : 0
                    })
                .ToListAsync();

            // Fill in missing hours with zero values
            var allHours = Enumerable.Range(0, 24)
                .Select(h => new OccupancyData
                {
                    Hour = $"{h:D2}:00",
                    Count = 0,
                    OccupancyPercentage = 0
                });

            return allHours.Union(hourlyData, new OccupancyDataComparer())
                    .OrderBy(x => x.Hour)
                    .ToList();
        }

        private async Task<List<VehicleDistributionData>> GetVehicleTypeDistribution()
        {
            return await _context.Vehicles
                    .Where(v => v.IsParked)
                .GroupBy(v => v.VehicleType ?? "Unknown")
                .Select(g => new VehicleDistributionData
                    {
                        Type = g.Key,
                        Count = g.Count()
                    })
                .ToListAsync();
        }

        public IActionResult VehicleEntry()
        {
            return View();
        }

        private async Task<ParkingSpace?> FindOptimalParkingSpace(string vehicleType)
        {
            try
            {
                // Get all available spaces that match the vehicle type
                var availableSpaces = await _context.ParkingSpaces
                    .Where(s => !s.IsOccupied && s.SpaceType == vehicleType)
                    .ToListAsync();

                if (!availableSpaces.Any())
                {
                    _logger.LogWarning("No available parking spaces for vehicle type: {VehicleType}", vehicleType);
                    return null;
                }

                // For now, we'll use a simple strategy: pick the first available space
                // TODO: Implement more sophisticated space selection based on:
                // 1. Proximity to entrance/exit
                // 2. Space size optimization
                // 3. Traffic flow optimization
                return availableSpaces.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding optimal parking space for vehicle type: {VehicleType}", vehicleType);
                return null;
            }
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

                // Check if vehicle already exists and is parked
                var existingVehicle = await _context.Vehicles
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == entryModel.VehicleNumber);
                
                if (existingVehicle != null && existingVehicle.IsParked)
                {
                    _logger.LogWarning("Vehicle {VehicleNumber} is already parked", entryModel.VehicleNumber);
                    return BadRequest(new { error = "Kendaraan sudah terparkir" });
                }

                // Find optimal parking space automatically
                var optimalSpace = await FindOptimalParkingSpace(entryModel.VehicleType);
                if (optimalSpace == null)
                {
                    _logger.LogWarning("No available parking space for vehicle type: {VehicleType}", entryModel.VehicleType);
                    return BadRequest(new { error = $"Tidak ada ruang parkir tersedia untuk kendaraan tipe {GetVehicleTypeName(entryModel.VehicleType)}" });
                }

                // Create or update vehicle record
                if (existingVehicle == null)
                {
                    existingVehicle = new Vehicle
                    {
                        VehicleNumber = entryModel.VehicleNumber,
                        VehicleType = entryModel.VehicleType,
                        DriverName = entryModel.DriverName,
                        PhoneNumber = entryModel.PhoneNumber,
                        IsParked = true,
                        ParkingSpace = optimalSpace
                    };
                    _context.Vehicles.Add(existingVehicle);
                }
                else
                {
                    existingVehicle.VehicleType = entryModel.VehicleType;
                    existingVehicle.DriverName = entryModel.DriverName;
                    existingVehicle.PhoneNumber = entryModel.PhoneNumber;
                    existingVehicle.IsParked = true;
                    existingVehicle.ParkingSpace = optimalSpace;
                }

                // Create parking transaction
                var transaction = new ParkingTransaction
                {
                    Vehicle = existingVehicle,
                    EntryTime = DateTime.Now,
                        TransactionNumber = GenerateTransactionNumber(),
                    Status = "Active"
                    };
                _context.ParkingTransactions.Add(transaction);

                // Update parking space status
                optimalSpace.IsOccupied = true;
                optimalSpace.LastOccupiedTime = DateTime.Now;

                    await _context.SaveChangesAsync();
                    
                // Notify clients about the update via SignalR
                    if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("UpdateParkingStatus", new
                    {
                        Action = "Entry",
                        VehicleNumber = entryModel.VehicleNumber,
                        SpaceNumber = optimalSpace.SpaceNumber,
                        SpaceType = optimalSpace.SpaceType,
                        Timestamp = DateTime.Now
                    });
                }

                return Ok(new
                {
                    message = "Kendaraan berhasil masuk",
                    spaceNumber = optimalSpace.SpaceNumber,
                    spaceType = optimalSpace.SpaceType,
                    transactionNumber = transaction.TransactionNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vehicle entry for {VehicleNumber}", entryModel.VehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat memproses masuk kendaraan" });
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

        private async Task<object> GetVehicleDistributionForDashboard()
        {
            var distribution = await GetVehicleTypeDistribution();
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessExit([FromBody] string vehicleNumber)
        {
            if (string.IsNullOrEmpty(vehicleNumber))
                return BadRequest(new { error = "Nomor kendaraan harus diisi." });

            try
            {
                // Normalize vehicle number
                vehicleNumber = vehicleNumber.ToUpper().Trim();

                // Find the vehicle and include necessary related data
                var vehicle = await _context.Vehicles
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);

                if (vehicle == null)
                    return NotFound(new { error = "Kendaraan tidak ditemukan atau sudah keluar dari parkir." });

                // Find active transaction
                var transaction = await _context.ParkingTransactions
                    .Where(t => t.VehicleId == vehicle.Id && t.ExitTime == default(DateTime))
                    .OrderByDescending(t => t.EntryTime)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                    return NotFound(new { error = "Tidak ditemukan transaksi parkir yang aktif untuk kendaraan ini." });

                // Get applicable parking rate
                var parkingRate = await _context.Set<ParkingRateConfiguration>()
                    .Where(r => r.VehicleType == vehicle.VehicleType 
                            && r.IsActive 
                            && r.EffectiveFrom <= DateTime.Now 
                            && (!r.EffectiveTo.HasValue || r.EffectiveTo >= DateTime.Now))
                    .OrderByDescending(r => r.EffectiveFrom)
                    .FirstOrDefaultAsync();

                if (parkingRate == null)
                    return BadRequest(new { error = "Tidak dapat menemukan tarif parkir yang sesuai." });

                // Calculate duration and fee
                var exitTime = DateTime.Now;
                var duration = exitTime - transaction.EntryTime;
                var hours = Math.Ceiling(duration.TotalHours);
                
                decimal totalAmount;
                if (hours <= 1)
                {
                    totalAmount = parkingRate.BaseRate;
                }
                else if (hours <= 24)
                {
                    totalAmount = parkingRate.BaseRate + (parkingRate.HourlyRate * (decimal)(hours - 1));
                }
                else if (hours <= 168) // 7 days
                {
                    var days = Math.Ceiling(hours / 24);
                    totalAmount = parkingRate.DailyRate * (decimal)days;
                }
                else // more than 7 days
                {
                    var weeks = Math.Ceiling(hours / 168);
                    totalAmount = parkingRate.WeeklyRate * (decimal)weeks;
                }

                // Update transaction
                transaction.ExitTime = exitTime;
                transaction.TotalAmount = totalAmount;
                transaction.Status = "Completed";
                _context.ParkingTransactions.Update(transaction);

                // Update vehicle and parking space
                vehicle.ExitTime = exitTime;
                vehicle.IsParked = false;
                vehicle.ParkingSpace.IsOccupied = false;
                vehicle.ParkingSpace.LastOccupiedTime = exitTime;
                _context.Vehicles.Update(vehicle);

                await _context.SaveChangesAsync();
                
                // Create response data
                var response = new
                {
                    message = "Kendaraan berhasil keluar",
                    vehicleNumber = vehicle.VehicleNumber,
                    entryTime = transaction.EntryTime,
                    exitTime = exitTime,
                    duration = $"{hours:0} jam",
                    totalAmount = totalAmount,
                    spaceNumber = vehicle.ParkingSpace?.SpaceNumber
                };

                // Notify clients if SignalR hub is available
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("UpdateParkingStatus", new
                    {
                        Action = "Exit",
                        VehicleNumber = vehicle.VehicleNumber,
                        SpaceNumber = vehicle.ParkingSpace?.SpaceNumber,
                        Timestamp = exitTime
                    });

                    var dashboardData = await GetDashboardData();
                    await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate", dashboardData);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vehicle exit for {VehicleNumber}", vehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat memproses keluar kendaraan." });
            }
        }

        private async Task<ParkingSpace?> GetAvailableParkingSpace(string type)
        {
            return await _context.ParkingSpaces
                .Where(ps => !ps.IsOccupied && ps.SpaceType.ToLower() == type.ToLower())
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

        public async Task<IActionResult> Transaction()
        {
            var currentShift = await GetCurrentShiftAsync();
            ViewBag.CurrentShift = currentShift;
            
            var parkingRates = await _context.ParkingRates
                .Where(r => r.IsActive)
                .OrderBy(r => r.VehicleType)
                .ToListAsync();
            ViewBag.ParkingRates = parkingRates;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessTransaction([FromBody] TransactionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var vehicle = await _context.Vehicles
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == request.VehicleNumber && v.IsParked);

                if (vehicle == null)
                {
                    return NotFound("Vehicle not found or not currently parked");
                }

                var parkingSpace = vehicle.ParkingSpace;
                if (parkingSpace == null)
                {
                    return NotFound("Parking space not found");
                }

                var rate = await _context.ParkingRates
                    .FirstOrDefaultAsync(r => r.VehicleType == vehicle.VehicleType && r.IsActive);

                if (rate == null)
                {
                    return NotFound("Parking rate not found for this vehicle type");
                }

                // Calculate duration and amount
                var duration = DateTime.Now - vehicle.EntryTime;
                var amount = CalculateParkingFee(vehicle.EntryTime, DateTime.Now, rate.HourlyRate);

                // Create transaction
                var transaction = new ParkingTransaction
                {
                    VehicleId = vehicle.Id,
                    ParkingSpaceId = parkingSpace.Id,
                    TransactionNumber = GenerateTransactionNumber(),
                    EntryTime = vehicle.EntryTime,
                    ExitTime = DateTime.Now,
                    HourlyRate = rate.HourlyRate,
                    Amount = amount,
                    TotalAmount = amount,
                    PaymentStatus = "Completed",
                    PaymentMethod = request.PaymentMethod,
                    PaymentTime = DateTime.Now,
                    Status = "Completed"
                };

                _context.ParkingTransactions.Add(transaction);

                // Update vehicle and parking space
                vehicle.ExitTime = DateTime.Now;
                vehicle.IsParked = false;
                parkingSpace.IsOccupied = false;
                parkingSpace.CurrentVehicle = null;

                await _context.SaveChangesAsync();

                // Notify clients of the update
                await _hubContext.Clients.All.SendAsync("UpdateParkingStatus");

                return Json(new
                {
                    success = true,
                    transactionNumber = transaction.TransactionNumber,
                    vehicleNumber = vehicle.VehicleNumber,
                    entryTime = vehicle.EntryTime,
                    exitTime = transaction.ExitTime,
                    duration = duration.ToString(@"hh\:mm"),
                    amount = transaction.TotalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing parking transaction");
                return StatusCode(500, "An error occurred while processing the transaction");
            }
        }

        private async Task<Shift> GetCurrentShiftAsync()
        {
            var now = DateTime.Now.TimeOfDay;
            // First get all active shifts
            var activeShifts = await _context.Shifts
                .Where(s => s.IsActive)
                .ToListAsync();
            
            // Then perform the TimeSpan comparison on the client side
            var currentShift = activeShifts
                .FirstOrDefault(s => s.StartTime <= now && s.EndTime >= now);
        
            if (currentShift == null)
                throw new InvalidOperationException("No active shift found");
        
            return currentShift;
        }

        private async Task<DashboardViewModel> GetDashboardData()
        {
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var totalSpaces = await _context.ParkingSpaces.CountAsync();
            var availableSpaces = await _context.ParkingSpaces.CountAsync(s => !s.IsOccupied);
            var dailyRevenue = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.Date == today)
                .SumDecimalAsync(t => t.TotalAmount);
            var weeklyRevenue = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.Date >= weekStart && t.PaymentTime.Date <= today)
                .SumDecimalAsync(t => t.TotalAmount);
            var monthlyRevenue = await _context.ParkingTransactions
                .Where(t => t.PaymentTime.Date >= monthStart && t.PaymentTime.Date <= today)
                .SumDecimalAsync(t => t.TotalAmount);

            return new DashboardViewModel
            {
                TotalSpaces = totalSpaces,
                AvailableSpaces = availableSpaces,
                DailyRevenue = dailyRevenue,
                WeeklyRevenue = weeklyRevenue,
                MonthlyRevenue = monthlyRevenue,
                RecentActivity = await GetRecentActivity(),
                HourlyOccupancy = await GetHourlyOccupancyData(),
                VehicleDistribution = await GetVehicleTypeDistribution()
            };
        }
    }

    public class TransactionRequest
    {
        public string VehicleNumber { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Cash";
    }
}