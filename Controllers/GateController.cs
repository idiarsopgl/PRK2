using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class GateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<GateController> _logger;

        public GateController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, ILogger<GateController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // GET: Gate/Entry
        public IActionResult Entry()
        {
            return View();
        }

        // POST: Gate/ProcessEntry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessEntry([FromForm] VehicleEntryModel model, [FromForm] string base64Image)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Save entry photo
                string entryPhotoPath = null;
                if (!string.IsNullOrEmpty(base64Image))
                {
                    entryPhotoPath = await SaveBase64Image(base64Image, "entry_photos");
                }

                // Get current active shift
                var currentShift = await GetCurrentShiftAsync();
                if (currentShift == null)
                {
                    return BadRequest(new { error = "Tidak ada shift aktif saat ini" });
                }

                // Create vehicle record
                var vehicle = new Vehicle
                {
                    VehicleNumber = model.VehicleNumber.ToUpper(),
                    VehicleType = model.VehicleType,
                    DriverName = model.DriverName,
                    PhoneNumber = model.PhoneNumber,
                    EntryTime = DateTime.Now,
                    IsParked = true,
                    EntryPhotoPath = entryPhotoPath,
                    ShiftId = currentShift.Id
                };

                // Generate barcode/QR code
                string barcodeData = GenerateBarcodeData(vehicle);
                string barcodeImagePath = await GenerateAndSaveQRCode(barcodeData);
                
                // Create parking ticket
                var ticket = new ParkingTicket
                {
                    TicketNumber = GenerateTicketNumber(),
                    BarcodeData = barcodeData,
                    BarcodeImagePath = barcodeImagePath ?? string.Empty,
                    IssueTime = DateTime.Now,
                    IsUsed = false,
                    Vehicle = vehicle,
                    ShiftId = currentShift.Id,
                    OperatorId = User.Identity?.Name ?? "system"
                };

                // Create journal entry
                var journal = new Journal
                {
                    Action = "Check In",
                    Description = $"Vehicle {vehicle.VehicleNumber} checked in",
                    Timestamp = DateTime.UtcNow,
                    OperatorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system"
                };

                _context.Vehicles.Add(vehicle);
                _context.ParkingTickets.Add(ticket);
                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                // Open the gate (implement your gate control logic here)
                await OpenGateAsync();

                return Ok(new
                {
                    message = "Kendaraan berhasil masuk",
                    ticketNumber = ticket.TicketNumber,
                    barcodeImageUrl = $"/uploads/barcodes/{Path.GetFileName(barcodeImagePath)}",
                    entryTime = vehicle.EntryTime
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Terjadi kesalahan: {ex.Message}" });
            }
        }

        // GET: Gate/Exit
        public IActionResult Exit()
        {
            return View();
        }

        // POST: Gate/ProcessExit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessExit([FromForm] string ticketNumber, [FromForm] string base64Image)
        {
            try
            {
                var ticket = await _context.ParkingTickets
                    .Include(t => t.Vehicle)
                    .FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber);

                if (ticket == null)
                {
                    return NotFound(new { error = "Tiket tidak ditemukan" });
                }

                if (ticket.IsUsed)
                {
                    return BadRequest(new { error = "Tiket sudah digunakan" });
                }

                // Save exit photo
                string exitPhotoPath = null;
                if (!string.IsNullOrEmpty(base64Image))
                {
                    exitPhotoPath = await SaveBase64Image(base64Image, "exit_photos") ?? string.Empty;
                }

                // Update vehicle status
                var vehicle = ticket.Vehicle;
                if (vehicle != null)
                {
                    vehicle.IsParked = false;
                    vehicle.ExitTime = DateTime.Now;
                    vehicle.ExitPhotoPath = exitPhotoPath;
                }
                else
                {
                    return BadRequest(new { error = "Vehicle data not found" });
                }

                // Mark ticket as used
                ticket.IsUsed = true;
                ticket.ScanTime = DateTime.Now;

                // Get current shift
                var currentShift = await GetCurrentShiftAsync();
                if (currentShift == null)
                {
                    return BadRequest(new { error = "Tidak ada shift aktif saat ini" });
                }

                // Create journal entry
                var journal = new Journal
                {
                    Action = "Check Out",
                    Description = $"Vehicle {vehicle.VehicleNumber} checked out",
                    Timestamp = DateTime.UtcNow,
                    OperatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                };

                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                // Open the gate (implement your gate control logic here)
                await OpenGateAsync();

                return Ok(new
                {
                    message = "Kendaraan berhasil keluar",
                    vehicleNumber = vehicle.VehicleNumber,
                    entryTime = vehicle.EntryTime,
                    exitTime = vehicle.ExitTime,
                    duration = (vehicle.ExitTime - vehicle.EntryTime)?.ToString(@"hh\:mm")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Terjadi kesalahan: {ex.Message}" });
            }
        }

        private async Task<string> SaveBase64Image(string base64Image, string folder)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64Image.Split(',')[1]);
                var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.jpg";
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", folder);
                
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                
                return $"/uploads/{folder}/{fileName}";
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string GenerateBarcodeData(Vehicle vehicle)
        {
            return $"PARK-{vehicle.VehicleNumber}-{DateTime.Now:yyyyMMddHHmmss}";
        }

        private async Task<string> GenerateAndSaveQRCode(string data)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new BitmapByteQRCode(qrCodeData);
                var qrCodeBytes = qrCode.GetGraphic(20);
                
                var fileName = $"qr_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.png";
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "barcodes");
                
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, qrCodeBytes);
                
                return $"/uploads/barcodes/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code");
                return null;
            }
        }

        private string GenerateTicketNumber()
        {
            return $"TIK-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        private string GenerateJournalNumber()
        {
            return $"JRN-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        private async Task<Shift> GetCurrentShiftAsync()
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            return await _context.Shifts
                .FirstOrDefaultAsync(s => 
                    s.Date.Date == now.Date && 
                    s.StartTime <= currentTime && 
                    s.EndTime >= currentTime &&
                    s.IsActive);
        }

        private async Task OpenGateAsync()
        {
            // Implement your gate control logic here
            // This is just a placeholder
            await Task.Delay(1000);
        }
    }
} 