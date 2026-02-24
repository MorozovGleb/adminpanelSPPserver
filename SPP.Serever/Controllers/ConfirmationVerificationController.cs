using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Добавлено для ToListAsync()
using SPP.Serever.Models;
using SPP.Serever.DTOs;

namespace SPP.Serever.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfirmationVerificationController : ControllerBase
    {
        private readonly SppDbContext _context;

        public ConfirmationVerificationController(SppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(await _context.ConfirmationVerifications.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ConfirmationVerification model)
        {
            _context.ConfirmationVerifications.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.ConfirmationVerifications.FindAsync(id);
            if (item == null) return NotFound();

            _context.ConfirmationVerifications.Remove(item);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}