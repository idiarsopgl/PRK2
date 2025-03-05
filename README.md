# Parking Management System

A modern parking management system built with ASP.NET Core that allows for efficient management of parking spaces, vehicle entry and exit tracking, payment processing, and reporting.

## Features

- **Real-time Dashboard**: View current parking space occupancy and recent parking activities
- **Vehicle Management**: Track vehicle entry and exit, assign parking spaces
- **Payment Processing**: Calculate fees based on parking duration and process payments
- **Reporting**: Generate reports on occupancy rates, revenue, and parking patterns
- **User Authentication**: Secure access to management features

## Technical Details

- Built with ASP.NET Core 8.0
- Uses Entity Framework Core for data access
- In-memory database for development and testing
- SignalR for real-time updates
- MVC architecture with Razor views

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later

### Running the Application

1. Clone the repository
2. Navigate to the project directory
3. Run the application:
   ```
   dotnet run
   ```
4. Access the application in your browser at `http://localhost:5126`

## Configuration

The application is currently configured to use an in-memory database for development and testing purposes. For production use, you should configure a persistent database like SQL Server, PostgreSQL, or SQLite.

To configure a different database provider:

1. Install the appropriate NuGet package for your database provider
2. Update the connection string in `appsettings.json`
3. Modify the database configuration in `Program.cs`

## Default Credentials

For testing purposes, the application comes with pre-seeded data including parking spaces and sample vehicles.

## License

This project is licensed under the MIT License.
