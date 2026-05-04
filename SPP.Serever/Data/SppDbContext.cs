using Microsoft.EntityFrameworkCore;
using SPP.Serever.Models;

public class SppDbContext : DbContext
{
    public SppDbContext(DbContextOptions<SppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Verification> Verifications { get; set; }
    public DbSet<Code> Codes { get; set; }
    public DbSet<ConfirmationVerification> ConfirmationVerifications { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<Day> Days { get; set; }
    public DbSet<WordTimeReady> WordTimeReadies { get; set; }
    public DbSet<Learning> Learning { get; set; }
}

