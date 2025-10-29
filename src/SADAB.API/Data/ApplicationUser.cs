using Microsoft.AspNetCore.Identity;

namespace SADAB.API.Data;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public override string ToString()
    {
        return $"Id={Id}, UserName={UserName ?? "null"}, Email={Email ?? "null"}, " +
               $"CreatedAt={CreatedAt:yyyy-MM-dd HH:mm:ss}, LastLoginAt={LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}";
    }
}
