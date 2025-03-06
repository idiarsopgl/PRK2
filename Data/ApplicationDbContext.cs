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

        public DbSet<ParkingSpace> ParkingSpaces { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<ParkingTransaction> ParkingTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ParkingSpace>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SpaceNumber).IsRequired();
                entity.Property(e => e.SpaceType).IsRequired();
                entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.CurrentVehicle)
                    .WithOne(v => v.AssignedSpace)
                    .HasForeignKey<Vehicle>(v => v.AssignedSpaceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Vehicle>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.VehicleNumber).IsRequired();
                entity.Property(e => e.VehicleType).IsRequired();
                entity.Property(e => e.DriverName).IsRequired();
            });

            builder.Entity<ParkingTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionNumber).IsRequired();
                entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Vehicle)
                    .WithMany(v => v.Transactions)
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ParkingSpace)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(e => e.ParkingSpaceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}