using Microsoft.AspNetCore.Identity;

namespace Geex
{
    public class ApplicationUser : IdentityUser
    {
        // Add additional profile data for application users here
        public string FullName { get; set; } = string.Empty;
    }
}
