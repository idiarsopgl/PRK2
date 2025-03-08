using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class ManagementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ManagementController(ApplicationDbContext context)
        {
            _context = context;
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

        public async Task<IActionResult> EditOperator(int? id)
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
                    existingOperator.Email = @operator.Email;
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
                .ThenBy(s => s.StartTime.TotalMinutes)
                .ToList();
                
            return View(shifts);
        }

        public IActionResult CreateShift()
        {
            var shift = new Shift
            {
                Date = DateTime.Today,
                IsActive = true
            };
            return View(shift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShift(Shift shift)
        {
            if (ModelState.IsValid)
            {
                _context.Add(shift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Shifts));
            }
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
        public async Task<IActionResult> EditShift(int id, Shift shift)
        {
            if (id != shift.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shift);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShiftExists(shift.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Shifts));
            }
            return View(shift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift != null)
            {
                _context.Shifts.Remove(shift);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Shifts));
        }

        private bool ShiftExists(int id)
        {
            return _context.Shifts.Any(e => e.Id == id);
        }
    }
} 