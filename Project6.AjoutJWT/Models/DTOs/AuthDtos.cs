using System.ComponentModel.DataAnnotations;

namespace Project6.AjoutJWT.Models;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, Length(4, 20)] string UserName,
    [Required, MinLength(6)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password
);

public record AuthResponse(
    string Token,
    DateTime ExpirationUtc
);