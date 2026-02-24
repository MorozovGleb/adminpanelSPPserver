using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPP.Server.DTOs;


[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SppDbContext _db;

    public AuthController(SppDbContext db)
    {
        _db = db;
    }

    [HttpPost("login")]
    public IActionResult Login(LoginRequestDto request)
    {
        var user = _db.Users
            .Include(x => x.Role)
            .FirstOrDefault(x => x.Login == request.Login
                              && x.Password == request.Password);

        if (user == null)
            return Unauthorized();

        return Ok(new
        {
            user.ID,
            user.Name,
            user.Surname,
            Role = user.Role.Name
        });
    }
}
