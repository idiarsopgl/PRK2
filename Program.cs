using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC;
using Microsoft.Extensions.DependencyInjection;
using ParkIRC.Hubs;
using ParkIRC.Models;
using ParkIRC.Services;
using ParkIRC.Middleware;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure HTTPS
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 5127; // Set your HTTPS port
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});

// Register ParkingService
builder.Services.AddScoped<IParkingService, ParkingService>();

// Register EmailService and EmailTemplateService
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();

builder.Services.AddIdentity<Operator, IdentityRole>(options => {
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    
    // User settings
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options => {
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.SlidingExpiration = true;
});

// Add SignalR support
builder.Services.AddSignalR();

// Add controller support with JSON options for AJAX requests
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// Add response caching
builder.Services.AddResponseCaching();

// Konfigurasi logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// Tambahkan file logging
builder.Logging.AddFile("logs/parkirc-{Date}.txt", LogLevel.Information);

var app = builder.Build();

// Ensure database is created and seeded with sample data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<Operator>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
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
                PhoneNumber = "555-1234",
                EntryTime = DateTime.Now.AddHours(-3),
                IsParked = true
            };
            
            var vehicle2 = new Vehicle 
            { 
                VehicleNumber = "XYZ789", 
                VehicleType = "SUV", 
                DriverName = "Jane Smith", 
                PhoneNumber = "555-5678",
                EntryTime = DateTime.Now.AddHours(-5),
                ExitTime = DateTime.Now.AddHours(-2),
                IsParked = false
            };
            
            var vehicle3 = new Vehicle 
            { 
                VehicleNumber = "DEF456", 
                VehicleType = "Compact", 
                DriverName = "Bob Johnson", 
                PhoneNumber = "555-9012",
                EntryTime = DateTime.Now.AddHours(-1),
                IsParked = true
            };
            
            context.Vehicles.AddRange(vehicle1, vehicle2, vehicle3);
            
            // Assign vehicles to spaces
            parkingSpaces[0].IsOccupied = true;
            parkingSpaces[0].CurrentVehicle = vehicle1;
            vehicle1.ParkingSpace = parkingSpaces[0];
            vehicle1.ParkingSpaceId = parkingSpaces[0].Id;
            
            parkingSpaces[3].IsOccupied = true;
            parkingSpaces[3].CurrentVehicle = vehicle3;
            vehicle3.ParkingSpace = parkingSpaces[3];
            vehicle3.ParkingSpaceId = parkingSpaces[3].Id;
            
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
            
            // Create roles if they don't exist
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            
            if (!await roleManager.RoleExistsAsync("Staff"))
            {
                await roleManager.CreateAsync(new IdentityRole("Staff"));
            }
            
            // Create a default admin user
            if (await userManager.FindByEmailAsync("admin@parkingsystem.com") == null)
            {
                var adminUser = new Operator
                {
                    UserName = "admin@parkingsystem.com",
                    Email = "admin@parkingsystem.com",
                    FullName = "System Administrator",
                    Name = "System Administrator",
                    EmailConfirmed = true,
                    IsActive = true,
                    JoinDate = DateTime.Today,
                    CreatedAt = DateTime.UtcNow
                };
                
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            
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

// Configure HTTPS redirection
app.UseHttpsRedirection();
app.UseStaticFiles();

// Add rate limiting middleware
app.UseMiddleware<RateLimitingMiddleware>();

// Add response caching middleware
app.UseResponseCaching();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// Map SignalR hub
app.MapHub<ParkingHub>("/parkingHub");

app.Run();

// Helper method to generate transaction numbers
static string GenerateTransactionNumber()
{
    return "TRX-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
}
