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
    public DbSet<RaceResult> RaceResults => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Activity>().Ignore(a => a.IsRace);

        builder.Entity<RaceResult>()
            .HasIndex(r => new { r.UserId, r.Provider, r.ProviderResultId })
            .IsUnique();

        // A race result can arrive before its matching Strava activity, or never get
        // one at all - keep the row (and the match state it carries) rather than
        // losing it if the activity is later deleted.
        builder.Entity<RaceResult>()
            .HasOne<Activity>()
            .WithMany()
            .HasForeignKey(r => r.ActivityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<RaceResult>()
            .HasIndex(r => r.ActivityId);

        // An analysis report only exists because of its activity - remove it with it.
        builder.Entity<AnalysisReport>()
            .HasOne<Activity>()
            .WithMany()
            .HasForeignKey(r => r.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Activity>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Activity>()
            .HasIndex(a => new { a.UserId, a.StravaActivityId })
            .IsUnique();

        builder.Entity<ConnectedAccount>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ConnectedAccount>()
            .HasIndex(a => new { a.UserId, a.Provider })
            .IsUnique();
    }
}
