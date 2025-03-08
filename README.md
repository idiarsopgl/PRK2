# ParkIRC - Parking Management System

A modern parking management system built with ASP.NET Core that helps manage parking spaces, vehicle entries/exits, shift scheduling, and payments.

## Features

- User Authentication & Authorization
  - Role-based access control (Admin, Operator)
  - Secure password reset functionality
  - User profile management

- Parking Management
  - Automatic space assignment
  - Vehicle entry/exit tracking
  - Real-time space availability
  - Payment processing
  - Transaction history

- Shift Management
  - Shift scheduling functionality

- Dashboard
  - Real-time occupancy status
  - Revenue statistics
  - Recent activities
  - Space utilization metrics

## Technology Stack

- ASP.NET Core 6.0
- Entity Framework Core
- SQLite
- Identity Framework
- SweetAlert2
- jQuery
- Bootstrap 5 for UI

## Prerequisites

- .NET 6.0 SDK
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
    "DefaultConnection": "Data Source=GeexParkingDB.db;"
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
- HTTP: http://localhost:5126
- HTTPS: https://localhost:5127

## Default Admin Account

- Email: admin@parkingsystem.com
- Password: Admin@123

## Project Structure

- `/src`: Source code
- `/test`: Tests
- `/docs`: Documentation
- `/build`: Build artifacts

### Key Controllers

- `AuthController`: Manages user authentication and authorization.
- `ParkingController`: Handles vehicle entry and exit tracking.
- `ShiftController`: Manages shift scheduling functionality.

### Key Models

- `User`: Represents the user entity.
- `ParkingSpace`: Represents a parking space in the system.
- `Shift`: Represents a shift in the shift management system.

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
