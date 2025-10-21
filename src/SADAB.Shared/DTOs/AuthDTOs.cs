namespace SADAB.Shared.DTOs;

public class RegisterRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public DateTime ExpiresAt { get; set; }
}
