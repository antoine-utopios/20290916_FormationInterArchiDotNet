using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Project6.AjoutJWT.Models;
using Project6.AjoutJWT.Services;

namespace Project6.AjoutJWT.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private UserManager<ApplicationUser> _userManager;
    private JwtTokenService _jwtTokenService;

    public AuthController(UserManager<ApplicationUser> userManager, JwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register(RegisterRequest request)
    {
        var userFound = await _userManager.FindByEmailAsync(request.Email);
        if (userFound is not null) return Conflict(new { Message = "Un compte avec cet email existe déjà."});

        var newUser = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(newUser, request.Password);

        if (!result.Succeeded) return BadRequest(new { Errors = result.Errors.Select(e => e.Description)});

        return Created(string.Empty, new { Message = "Ce compte est créé, vous pouvez désormais vous connecter !"});
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var userFound = await _userManager.FindByEmailAsync(request.Email);
        if (userFound is null) return Unauthorized(new { Message = "Email ou mot de passe non valide."});
        
        var passwodValid = await _userManager.CheckPasswordAsync(userFound, request.Password);
        if (!passwodValid) return Unauthorized(new { Message = "Email ou mot de passe non valide."});

        var (token, expirationDate) = _jwtTokenService.GenerateToken(userFound);

        return Ok(new AuthResponse(token, expirationDate));

    }
}