using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC.Models;

namespace ParkIRC.Services
{
    public interface IParkingService
    {
        Task<ParkingSpace> AssignParkingSpaceAsync(Vehicle vehicle);
        Task<ParkingTransaction> ProcessExitAsync(string vehicleNumber, string paymentMethod);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
    }

    public class ParkingService : IParkingService
    {
        private readonly ApplicationDbContext _context;

        public ParkingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ParkingSpace> AssignParkingSpaceAsync(Vehicle vehicle)
        {
            // Get all available parking spaces based on vehicle type
            var availableSpaces = await _context.ParkingSpaces
                .Where(s => !s.IsOccupied)
                .ToListAsync();

            // Filter spaces based on vehicle type
            var suitableSpaces = vehicle.VehicleType.ToLower() switch
            {
                "compact" => availableSpaces.Where(s => s.SpaceType == "Compact" || s.SpaceType == "Standard"),
                "sedan" => availableSpaces.Where(s => s.SpaceType == "Standard"),
                "suv" => availableSpaces.Where(s => s.SpaceType == "Standard" || s.SpaceType == "Premium"),
                "handicap" => availableSpaces.Where(s => s.SpaceType == "Handicap"),
                _ => availableSpaces.Where(s => s.SpaceType == "Standard")
            };

            // Get the most suitable space (prefer spaces specifically designed for the vehicle type)
            var assignedSpace = suitableSpaces
                .OrderBy(s => s.SpaceType != (vehicle.VehicleType == "Compact" ? "Compact" : "Standard"))
                .ThenBy(s => s.SpaceNumber)
                .FirstOrDefault();

            if (assignedSpace == null)
                throw new InvalidOperationException("No suitable parking spaces available.");

            // Assign the space
            assignedSpace.IsOccupied = true;
            assignedSpace.CurrentVehicle = vehicle;
            vehicle.AssignedSpace = assignedSpace;
            vehicle.AssignedSpaceId = assignedSpace.Id;
            vehicle.EntryTime = DateTime.Now;
            vehicle.IsParked = true;

            await _context.SaveChangesAsync();

            return assignedSpace;
        }

        public async Task<ParkingTransaction> ProcessExitAsync(string vehicleNumber, string paymentMethod)
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.AssignedSpace)
                .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);

            if (vehicle == null)
                throw new InvalidOperationException("Vehicle not found or not currently parked.");

            var exitTime = DateTime.Now;
            var duration = exitTime - vehicle.EntryTime;
            var hours = Math.Ceiling(duration.TotalHours);
            var totalAmount = hours * vehicle.AssignedSpace.HourlyRate;

            var transaction = new ParkingTransaction
            {
                VehicleId = vehicle.Id,
                ParkingSpaceId = vehicle.AssignedSpace.Id,
                EntryTime = vehicle.EntryTime,
                ExitTime = exitTime,
                HourlyRate = vehicle.AssignedSpace.HourlyRate,
                TotalAmount = totalAmount,
                PaymentStatus = "Paid",
                PaymentMethod = paymentMethod,
                PaymentTime = exitTime,
                TransactionNumber = GenerateTransactionNumber(),
                Vehicle = vehicle,
                ParkingSpace = vehicle.AssignedSpace
            };

            // Update vehicle and space status
            vehicle.IsParked = false;
            vehicle.ExitTime = exitTime;
            vehicle.AssignedSpace.IsOccupied = false;
            vehicle.AssignedSpace.CurrentVehicle = null;
            vehicle.AssignedSpace = null;
            vehicle.AssignedSpaceId = null;

            _context.ParkingTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return transaction;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return false;

            // Note: This is a placeholder. The actual implementation should use ASP.NET Core Identity's
            // password reset functionality. This will be implemented in the AuthController.
            return true;
        }

        private static string GenerateTransactionNumber()
        {
            return "TRX-" + DateTime.Now.ToString("yyyyMMdd") + "-" + 
                   Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
        }
    }
} 