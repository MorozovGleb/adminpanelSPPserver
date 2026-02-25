using Microsoft.AspNetCore.Mvc;

namespace SPP.Serever.Controllers
{
    [ApiController]
    [Route("api/roles")]
    public class RoleController(SppDbContext context) : ControllerBase
    {
        private readonly SppDbContext _context = context;
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = _context.Roles.ToList();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
