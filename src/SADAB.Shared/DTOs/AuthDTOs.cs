using SADAB.Shared.Extensions;

namespace SADAB.Shared.DTOs;

public class RegisterRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Sensitive data (Password) is masked.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Sensitive data (Password) is masked.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Token is partially masked for security.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}
