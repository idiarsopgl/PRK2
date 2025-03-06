# ParkIRC - Parking Management System

A modern parking management system built with ASP.NET Core that helps manage parking spaces, vehicle entries/exits, and payments.

## Features

- User Authentication & Authorization
  - Role-based access control (Admin, Staff)
  - Secure password reset functionality
  - User profile management

- Parking Management
  - Automatic space assignment
  - Vehicle entry/exit tracking
  - Real-time space availability
  - Payment processing
  - Transaction history

- Dashboard
  - Real-time occupancy status
  - Revenue statistics
  - Recent activities
  - Space utilization metrics

## Technology Stack

- ASP.NET Core 9.0
- Entity Framework Core
- SQL Server
- Identity Framework
- SignalR for real-time updates
- Bootstrap 5 for UI

## Prerequisites

- .NET 9.0 SDK
- SQL Server
- Visual Studio 2022 or VS Code

## Getting Started

1. Clone the repository:
```bash
git clone https://github.com/yourusername/ParkIRC.git
```

2. Navigate to the project directory:
```bash
cd ParkIRC
```

3. Update the connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=GeexParkingDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

4. Apply database migrations:
```bash
dotnet ef database update
```

5. Run the application:
```bash
dotnet run
```

6. Access the application:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

## Default Admin Account

- Email: admin@parkingsystem.com
- Password: Admin@123

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
