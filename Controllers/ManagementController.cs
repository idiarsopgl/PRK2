using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class ManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ManagementController> _logger;

        public ManagementController(ApplicationDbContext context, ILogger<ManagementController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Parking Slots Management
        public async Task<IActionResult> ParkingSlots()
        {
            var parkingSpaces = await _context.ParkingSpaces.ToListAsync();
            return View(parkingSpaces);
        }

        public IActionResult CreateParkingSlot()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateParkingSlot(ParkingSpace parkingSpace)
        {
            if (ModelState.IsValid)
            {
                _context.Add(parkingSpace);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ParkingSlots));
            }
            return View(parkingSpace);
        }

        public async Task<IActionResult> EditParkingSlot(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var parkingSpace = await _context.ParkingSpaces.FindAsync(id);
            if (parkingSpace == null)
            {
                return NotFound();
            }
            return View(parkingSpace);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditParkingSlot(int id, ParkingSpace parkingSpace)
        {
            if (id != parkingSpace.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(parkingSpace);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ParkingSpaceExists(parkingSpace.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(ParkingSlots));
            }
            return View(parkingSpace);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteParkingSlot(int id)
        {
            var parkingSpace = await _context.ParkingSpaces.FindAsync(id);
            if (parkingSpace != null)
            {
                _context.ParkingSpaces.Remove(parkingSpace);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ParkingSlots));
        }

        private bool ParkingSpaceExists(int id)
        {
            return _context.ParkingSpaces.Any(e => e.Id == id);
        }

        // Operators Management
        public async Task<IActionResult> Operators()
        {
            var operators = await _context.Operators.ToListAsync();
            return View(operators);
        }

        public IActionResult CreateOperator()
        {
            return View(new Operator
            {
                IsActive = true,
                JoinDate = DateTime.Today,
                CreatedAt = DateTime.UtcNow
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOperator(Operator @operator)
        {
            if (ModelState.IsValid)
            {
                @operator.CreatedAt = DateTime.UtcNow;
                _context.Add(@operator);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Operators));
            }
            return View(@operator);
        }

        public async Task<IActionResult> EditOperator(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @operator = await _context.Operators.FindAsync(id);
            if (@operator == null)
            {
                return NotFound();
            }
            return View(@operator);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOperator(string id, Operator @operator)
        {
            if (id != @operator.Id.ToString())
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingOperator = await _context.Operators.FindAsync(@operator.Id);
                    if (existingOperator == null)
                    {
                        return NotFound();
                    }

                    existingOperator.FullName = @operator.FullName;
                    existingOperator.BadgeNumber = @operator.BadgeNumber;
                    existingOperator.PhoneNumber = @operator.PhoneNumber;
                    existingOperator.IsActive = @operator.IsActive;
                    existingOperator.LastModifiedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OperatorExists(@operator.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Operators));
            }
            return View(@operator);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOperator(string id)
        {
            var @operator = await _context.Operators.FindAsync(id);
            if (@operator == null)
            {
                return Json(new { success = false, message = "Operator tidak ditemukan" });
            }

            try
            {
                _context.Operators.Remove(@operator);
                await _context.SaveChangesAsync();

                // Log the action
                var journal = new Journal
                {
                    Action = "DELETE_OPERATOR",
                    Description = $"Operator dihapus: {@operator.FullName}",
                    OperatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system",
                    Timestamp = DateTime.UtcNow
                };
                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Gagal menghapus operator: " + ex.Message });
            }
        }

        private bool OperatorExists(string id)
        {
            return _context.Operators.Any(e => e.Id == id);
        }

        // Shifts Management
        public async Task<IActionResult> Shifts()
        {
            // First get the data without ordering by TimeSpan
            var shifts = await _context.Shifts
                .Include(s => s.Operators)
                .Include(s => s.Vehicles)
                .OrderBy(s => s.Date)
                .ToListAsync();
                
            // Then order by StartTime on the client side
            shifts = shifts
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime.TimeOfDay.TotalMinutes)
                .ToList();
                
            return View(shifts);
        }

        public IActionResult CreateShift()
        {
            var shift = new Shift
            {
                Date = DateTime.Today,
                IsActive = true,
                StartTime = DateTime.Today, // Set default time
                EndTime = DateTime.Today,   // Set default time
                Name = "",                  // Initialize with empty string
                ShiftName = "",             // Initialize with empty string
                Description = "",           // Initialize with empty string
                MaxOperators = 1            // Set default value
            };
            return View(shift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShift(Shift shift, string startTime, string endTime, string[] WorkDays)
        {
            // Log incoming data for debugging
            _logger.LogInformation($"CreateShift POST method called");
            _logger.LogInformation($"Received shift data - Name: '{shift.Name}', IsActive: {shift.IsActive}");
            _logger.LogInformation($"WorkDays count: {(WorkDays?.Length ?? 0)}");
            _logger.LogInformation($"StartTime: '{startTime}', EndTime: '{endTime}'");
            
            // Remove ShiftName validation error if Name is valid
            if (!string.IsNullOrEmpty(shift.Name))
            {
                _logger.LogInformation($"Name is valid: '{shift.Name}'");
                // Remove ShiftName validation error if it exists
                ModelState.Remove("ShiftName");
            }
            else
            {
                ModelState.AddModelError("Name", "Nama shift wajib diisi");
                _logger.LogWarning("Name is null or empty");
            }

            if (WorkDays == null || WorkDays.Length == 0)
            {
                ModelState.AddModelError("WorkDays", "Pilih minimal satu hari kerja");
                _logger.LogWarning("No workdays selected");
            }
            else
            {
                _logger.LogInformation($"Selected workdays: {string.Join(", ", WorkDays)}");
            }

            if (string.IsNullOrEmpty(startTime))
            {
                ModelState.AddModelError("StartTime", "Waktu mulai wajib diisi");
                _logger.LogWarning("StartTime is null or empty");
            }

            if (string.IsNullOrEmpty(endTime))
            {
                ModelState.AddModelError("EndTime", "Waktu selesai wajib diisi");
                _logger.LogWarning("EndTime is null or empty");
            }

            _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");

            if (ModelState.IsValid)
            {
                try
                {
                    // Parse time strings to TimeSpan
                    if (TimeSpan.TryParse(startTime, out TimeSpan parsedStartTime) && 
                        TimeSpan.TryParse(endTime, out TimeSpan parsedEndTime))
                    {
                        var baseDate = DateTime.Today;
                        shift.StartTime = baseDate.Add(parsedStartTime);
                        shift.EndTime = baseDate.Add(parsedEndTime);
                        shift.Date = baseDate;
                        // Set ShiftName from Name
                        shift.ShiftName = shift.Name;
                        _logger.LogInformation($"ShiftName set to: '{shift.ShiftName}'");
                        shift.WorkDaysString = string.Join(",", WorkDays ?? Array.Empty<string>());
                        shift.CreatedAt = DateTime.Now;
                        
                        // Explicitly set IsActive to true for new shifts
                        shift.IsActive = true;

                        _logger.LogInformation($"Adding shift to database: Name='{shift.Name}', ShiftName='{shift.ShiftName}', WorkDaysString='{shift.WorkDaysString}'");
                        
                        _context.Add(shift);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Shift added successfully with ID: {shift.Id}");

                        // Log the action
                        var journal = new Journal
                        {
                            Action = "CREATE_SHIFT",
                            Description = $"Shift baru dibuat: {shift.Name}",
                            OperatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system",
                            Timestamp = DateTime.UtcNow
                        };
                        _context.Journals.Add(journal);
                        await _context.SaveChangesAsync();

                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            _logger.LogInformation("Returning JSON success response");
                            return Json(new { success = true, message = "Shift berhasil dibuat!" });
                        }

                        _logger.LogInformation("Redirecting to Shifts page");
                        TempData["SuccessMessage"] = "Shift berhasil dibuat!";
                        return RedirectToAction(nameof(Shifts));
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid time format: StartTime='{startTime}', EndTime='{endTime}'");
                        ModelState.AddModelError("", "Format waktu tidak valid");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating shift");
                    ModelState.AddModelError("", "Terjadi kesalahan saat menyimpan shift. Silakan coba lagi.");
                }
            }
            else
            {
                // Log all model state errors for debugging
                _logger.LogWarning("Model validation failed. Errors:");
                foreach (var modelState in ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        _logger.LogWarning($"Field: {modelState.Key}, Error: {error.ErrorMessage}");
                    }
                }
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning($"Returning JSON error response: {string.Join(", ", errors)}");
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            _logger.LogInformation("Returning to CreateShift view with model");
            return View(shift);
        }

        public async Task<IActionResult> EditShift(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shift = await _context.Shifts
                .Include(s => s.Operators)
                .Include(s => s.Vehicles)
                .FirstOrDefaultAsync(s => s.Id == id);
                
            if (shift == null)
            {
                return NotFound();
            }
            return View(shift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShift(int id, Shift shift, List<string> WorkDays)
        {
            if (id != shift.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingShift = await _context.Shifts
                        .Include(s => s.Operators)
                        .Include(s => s.Vehicles)
                        .FirstOrDefaultAsync(s => s.Id == id);

                    if (existingShift == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingShift.Name = shift.Name;
                    existingShift.ShiftName = shift.Name;
                    existingShift.StartTime = shift.StartTime;
                    existingShift.EndTime = shift.EndTime;
                    existingShift.Description = shift.Description;
                    existingShift.MaxOperators = shift.MaxOperators;
                    existingShift.IsActive = shift.IsActive;
                    existingShift.WorkDaysString = string.Join(",", WorkDays ?? new List<string>());

                    await _context.SaveChangesAsync();

                    // Log the action
                    var journal = new Journal
                    {
                        Action = "EDIT_SHIFT",
                        Description = $"Shift diperbarui: {shift.Name}",
                        OperatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system",
                        Timestamp = DateTime.UtcNow
                    };
                    _context.Journals.Add(journal);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Shift berhasil diperbarui!";
                    return RedirectToAction(nameof(Shifts));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShiftExists(shift.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }
            return View(shift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShift(int id)
        {
            try
            {
                var shift = await _context.Shifts.FindAsync(id);
                if (shift == null)
                {
                    return Json(new { success = false, message = "Shift tidak ditemukan" });
                }

                _context.Shifts.Remove(shift);

                // Log the action
                var journal = new Journal
                {
                    Action = "DELETE_SHIFT",
                    Description = $"Shift dihapus: {shift.Name}",
                    OperatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system",
                    Timestamp = DateTime.UtcNow
                };
                _context.Journals.Add(journal);
                
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Shift berhasil dihapus" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Gagal menghapus shift: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleShiftStatus(int id, bool isActive)
        {
            try
            {
                var shift = await _context.Shifts.FindAsync(id);
                if (shift == null)
                {
                    return Json(new { success = false, message = "Shift tidak ditemukan" });
                }

                shift.IsActive = isActive;
                await _context.SaveChangesAsync();

                // Log the action
                var journal = new Journal
                {
                    Action = isActive ? "ACTIVATE_SHIFT" : "DEACTIVATE_SHIFT",
                    Description = $"Shift {shift.Name} {(isActive ? "diaktifkan" : "dinonaktifkan")}",
                    OperatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system",
                    Timestamp = DateTime.UtcNow
                };
                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Gagal memperbarui status shift: " + ex.Message });
            }
        }

        private bool ShiftExists(int id)
        {
            return _context.Shifts.Any(e => e.Id == id);
        }
    }
} 