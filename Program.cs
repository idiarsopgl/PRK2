using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Geex.Data;
using Geex;
using Microsoft.Extensions.DependencyInjection;
using Geex.Hubs;
using Geex.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("GeexParkingDb"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add SignalR support
builder.Services.AddSignalR();

// Add controller support with JSON options for AJAX requests
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// Add response caching
builder.Services.AddResponseCaching();

var app = builder.Build();

// Ensure database is created and seeded with sample data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Seed parking spaces
        if (!context.ParkingSpaces.Any())
        {
            var parkingSpaces = new List<ParkingSpace>
            {
                new ParkingSpace { SpaceNumber = "A1", SpaceType = "Standard", IsOccupied = false, HourlyRate = 5.00m },
                new ParkingSpace { SpaceNumber = "A2", SpaceType = "Standard", IsOccupied = false, HourlyRate = 5.00m },
                new ParkingSpace { SpaceNumber = "A3", SpaceType = "Standard", IsOccupied = false, HourlyRate = 5.00m },
                new ParkingSpace { SpaceNumber = "B1", SpaceType = "Compact", IsOccupied = false, HourlyRate = 3.50m },
                new ParkingSpace { SpaceNumber = "B2", SpaceType = "Compact", IsOccupied = false, HourlyRate = 3.50m },
                new ParkingSpace { SpaceNumber = "C1", SpaceType = "Premium", IsOccupied = false, HourlyRate = 8.00m },
                new ParkingSpace { SpaceNumber = "D1", SpaceType = "Handicap", IsOccupied = false, HourlyRate = 4.00m },
            };
            context.ParkingSpaces.AddRange(parkingSpaces);
            
            // Create some vehicles and transactions for demonstration
            var vehicle1 = new Vehicle 
            { 
                VehicleNumber = "ABC123", 
                VehicleType = "Sedan", 
                DriverName = "John Doe", 
                ContactNumber = "555-1234",
                EntryTime = DateTime.Now.AddHours(-3),
                IsParked = true
            };
            
            var vehicle2 = new Vehicle 
            { 
                VehicleNumber = "XYZ789", 
                VehicleType = "SUV", 
                DriverName = "Jane Smith", 
                ContactNumber = "555-5678",
                EntryTime = DateTime.Now.AddHours(-5),
                ExitTime = DateTime.Now.AddHours(-2),
                IsParked = false
            };
            
            var vehicle3 = new Vehicle 
            { 
                VehicleNumber = "DEF456", 
                VehicleType = "Compact", 
                DriverName = "Bob Johnson", 
                ContactNumber = "555-9012",
                EntryTime = DateTime.Now.AddHours(-1),
                IsParked = true
            };
            
            context.Vehicles.AddRange(vehicle1, vehicle2, vehicle3);
            
            // Assign vehicles to spaces
            parkingSpaces[0].IsOccupied = true;
            parkingSpaces[0].CurrentVehicle = vehicle1;
            vehicle1.AssignedSpace = parkingSpaces[0];
            vehicle1.AssignedSpaceId = parkingSpaces[0].Id;
            
            parkingSpaces[3].IsOccupied = true;
            parkingSpaces[3].CurrentVehicle = vehicle3;
            vehicle3.AssignedSpace = parkingSpaces[3];
            vehicle3.AssignedSpaceId = parkingSpaces[3].Id;
            
            // Create some completed transactions
            var transaction1 = new ParkingTransaction
            {
                VehicleId = vehicle2.Id,
                ParkingSpaceId = parkingSpaces[1].Id,
                EntryTime = DateTime.Now.AddHours(-5),
                ExitTime = DateTime.Now.AddHours(-2),
                HourlyRate = parkingSpaces[1].HourlyRate,
                TotalAmount = parkingSpaces[1].HourlyRate * 3, // 3 hours
                PaymentStatus = "Paid",
                PaymentMethod = "Credit Card",
                PaymentTime = DateTime.Now.AddHours(-2),
                TransactionNumber = GenerateTransactionNumber(),
                Vehicle = vehicle2,
                ParkingSpace = parkingSpaces[1]
            };
            
            context.ParkingTransactions.Add(transaction1);
            
            // Save changes
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Add response caching middleware
app.UseResponseCaching();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Parking}/{action=Dashboard}/{id?}");

// Map SignalR hub
app.MapHub<ParkingHub>("/parkingHub");

app.Run();

// Helper method to generate transaction numbers
static string GenerateTransactionNumber()
{
    return "TRX-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
}
