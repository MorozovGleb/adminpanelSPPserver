using Microsoft.AspNetCore.Mvc;
using SPP.Serever.Models;

[ApiController]
[Route("api/schedule")]
public class ScheduleController : ControllerBase
{
    private readonly SppDbContext _db;

    public ScheduleController(SppDbContext db)
    {
        _db = db;
    }

    [HttpGet("user/{userId}")]
    public IActionResult GetUserSchedule(int userId)
    {
        var data = _db.Schedule
            .Where(x => x.ID_User == userId)
            .OrderBy(x => x._Date)
            .ToList();

        return Ok(data);
    }

    [HttpPost]
    public IActionResult Create(Schedule model)
    {
        _db.Schedule.Add(model);
        _db.SaveChanges();
        return Ok();
    }
}
