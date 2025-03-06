using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using ParkIRC.Models;

namespace ParkIRC.Hubs
{
    public class ParkingHub : Hub
    {
        /// <summary>
        /// Called by clients to update dashboard data for all clients
        /// </summary>
        /// <param name="data">Dashboard data to be broadcasted</param>
        public async Task UpdateDashboard(DashboardViewModel data)
        {
            await Clients.All.SendAsync("ReceiveDashboardUpdate", data);
        }

        /// <summary>
        /// Called by clients to notify about new vehicle entry
        /// </summary>
        /// <param name="vehicleNumber">The vehicle number that entered</param>
        public async Task NotifyVehicleEntry(string vehicleNumber)
        {
            await Clients.All.SendAsync("VehicleEntryNotification", vehicleNumber);
        }

        /// <summary>
        /// Called by clients to notify about vehicle exit
        /// </summary>
        /// <param name="vehicleNumber">The vehicle number that exited</param>
        public async Task NotifyVehicleExit(string vehicleNumber)
        {
            await Clients.All.SendAsync("VehicleExitNotification", vehicleNumber);
        }
    }
} 