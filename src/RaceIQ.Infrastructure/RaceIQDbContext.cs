using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class RaceIQDbContext : IdentityDbContext<ApplicationUser>
{
    public RaceIQDbContext(DbContextOptions<RaceIQDbContext> options)
        : base(options)
    {
    }

    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AnalysisReport> AnalysisReports => Set<AnalysisReport>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
}
