using Microsoft.AspNetCore.Identity;

namespace SADAB.Server.Data;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
