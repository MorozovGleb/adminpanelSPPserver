using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPP.Serever.DTOs;
using SPP.Serever.Models;
using SPP.Server.DTOs;

namespace SPP.Serever.Controllers
{
    public class UserView(Models.User user,Models.Role role)
    {
        public int ID { get; set; } = user.ID;
        public string Name { get; set; } = user.Name;
        public string Surname { get; set; } = user.Surname;
        public string Phone_number { get; set; } = user.Phone_number;
        public string Login { get; set; } = user.Login;
        public string Role { get; set; } = role.Name;
    }
    [ApiController]
    [Route("api/users")]
    public class UsersController(SppDbContext context) : ControllerBase
    {
        private readonly SppDbContext _context = context;
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users =await _context.Users.ToListAsync();
                List<UserView> usersView = new List<UserView>();
                var rols = await _context.Roles.ToListAsync();
                foreach (var user in users)
                {

                    usersView.Add(new(user, rols.FirstOrDefault(r => r.ID == user.ID_Role)));
                }
                return Ok(usersView);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost]
        public IActionResult Create([FromBody] User user)
        {
            try
            {
                user.Role = null;
                _context.Users.Add(user);
                _context.SaveChanges();
                return Ok();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest(ex.Message);
            }
        }
    }
}
