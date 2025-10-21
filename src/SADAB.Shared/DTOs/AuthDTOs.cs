namespace SADAB.Shared.DTOs;

public class RegisterRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }

    public override string ToString()
    {
        return $"Username={Username}, Email={Email}, Password=***";
    }
}

public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }

    public override string ToString()
    {
        return $"Username={Username}, Password=***";
    }
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public DateTime ExpiresAt { get; set; }

    public override string ToString()
    {
        return $"Token={Token.Substring(0, Math.Min(20, Token.Length))}..., Username={Username}, Email={Email}, ExpiresAt={ExpiresAt:yyyy-MM-dd HH:mm:ss}";
    }
}
