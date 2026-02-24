using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPP.Serever.DTOs;
using SPP.Serever.Models;
using System;
[ApiController]
[Route("api/[controller]")]
public class VerificationController : ControllerBase
{
    private readonly SppDbContext _context;

    public VerificationController(SppDbContext context)
    {
        _context = context;
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserVerifications([FromRoute] int userId)
    {
        try
        {
            Console.WriteLine("v/user/id");
            Console.WriteLine(userId);
            var verifications = await _context.Verifications.ToListAsync();

            var confirmations = await _context.ConfirmationVerifications
                .Where(c => c.ID_User == userId)
                .ToListAsync();

            var result = verifications.Select(v => new VerificationStatusDto
            {
                Name = v.Name,
                Date = confirmations
                    .FirstOrDefault(c => c.ID_Verification == v.ID)?._Date
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"500{ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }
    [HttpGet("f")]
    public async Task<IActionResult> GetVerifications()
    {
        try
        {
            var confirmations = await _context.ConfirmationVerifications
            .ToListAsync();

            var result = confirmations;

            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"500{ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateVerification(int ID, [FromBody] ConfirmationVerification dto)
    {
        try
        {
            var entity = await _context.ConfirmationVerifications
                .FirstOrDefaultAsync(x => x.ID == ID);

            if (entity == null)
                return NotFound($"Verification with id {ID} not found");

            // Обновляем только нужные поля
            entity.ID_User = dto.ID_User;
            entity.ID_Verification = dto.ID_Verification;
            entity._Date = dto._Date;

            await _context.SaveChangesAsync();

            return Ok(entity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"500 {ex.Message}");
            return StatusCode(500, ex.Message);
        }
    }

}