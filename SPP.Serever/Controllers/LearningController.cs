using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/learning")]
public class LearningController : ControllerBase
{
    private readonly SppDbContext _db;

    public LearningController(SppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{verificationId}")]
    public IActionResult Get(int verificationId)
    {
        return Ok(_db.Learning
            .Where(x => x.ID_Verification == verificationId)
            .ToList());
    }
}
