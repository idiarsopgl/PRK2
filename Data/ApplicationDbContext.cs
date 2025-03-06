using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Models;
using ParkIRC;

namespace ParkIRC.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public required DbSet<ParkingSpace> ParkingSpaces { get; set; }
        public required DbSet<Vehicle> Vehicles { get; set; }
        public required DbSet<ParkingTransaction> ParkingTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.AssignedSpace)
                .WithOne(ps => ps.CurrentVehicle)
                .HasForeignKey<Vehicle>(v => v.AssignedSpaceId);

            modelBuilder.Entity<ParkingTransaction>()
                .HasOne(pt => pt.Vehicle)
                .WithMany()
                .HasForeignKey(pt => pt.VehicleId);

            modelBuilder.Entity<ParkingTransaction>()
                .HasOne(pt => pt.ParkingSpace)
                .WithMany()
                .HasForeignKey(pt => pt.ParkingSpaceId);
        }
    }
}