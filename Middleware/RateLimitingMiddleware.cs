using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ParkIRC.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();
        private static readonly TimeSpan _requestInterval = TimeSpan.FromSeconds(1);

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var currentTime = DateTime.UtcNow;

            if (_lastRequestTimes.TryGetValue(ipAddress, out var lastRequestTime))
            {
                var timeSinceLastRequest = currentTime - lastRequestTime;
                if (timeSinceLastRequest < _requestInterval)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
                    return;
                }
            }

            _lastRequestTimes.AddOrUpdate(ipAddress, currentTime, (_, _) => currentTime);
            await _next(context);
        }
    }
} 