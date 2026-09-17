using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure;

public class RaceIQDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IDataProtector _credentialProtector;

    public RaceIQDbContext(DbContextOptions<RaceIQDbContext> options, IDataProtectionProvider dataProtection)
        : base(options)
    {
        _credentialProtector = dataProtection.CreateProtector("RaceIQ.ConnectedAccount.Credentials");
    }

    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AnalysisReport> AnalysisReports => Set<AnalysisReport>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<RaceResult> RaceResults => Set<RaceResult>();
    public DbSet<RecoveryDay> RecoveryDays => Set<RecoveryDay>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Activity>().Ignore(a => a.IsRace);
        builder.Entity<RecoveryDay>().Ignore(d => d.Band);

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

        builder.Entity<RecoveryDay>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One recovery reading per user per day - the upsert key.
        builder.Entity<RecoveryDay>()
            .HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();

        // Encrypt the provider secrets at rest: the Strava and WHOOP access/refresh tokens
        // and the ZwiftRacing API key live in these two columns (ZwiftPower stores no
        // credential any more, only the public rider id). ExternalAccountId is not a secret
        // and stays in clear text. The column type is unchanged (text), so this needs no
        // migration.
        //
        // EF Core builds this model once per context type and caches it, so anything the
        // converter lambdas capture lives for the whole process. Capture the protector
        // itself (a stateless object), not `this` - otherwise the first DbContext instance
        // would be kept alive forever by the model cache.
        var protector = _credentialProtector;

        var requiredConverter = new ValueConverter<string, string>(
            plaintext => protector.Protect(plaintext),
            stored => Unprotect(protector, stored));

        var optionalConverter = new ValueConverter<string?, string?>(
            plaintext => plaintext == null ? null : protector.Protect(plaintext),
            stored => stored == null ? null : Unprotect(protector, stored));

        builder.Entity<ConnectedAccount>()
            .Property(a => a.AccessToken)
            .HasConversion(requiredConverter);

        builder.Entity<ConnectedAccount>()
            .Property(a => a.RefreshToken)
            .HasConversion(optionalConverter);
    }

    // Tolerates rows written before encryption was enabled (or with a since-rotated key):
    // an undecryptable value is returned as-is, so a stale credential surfaces as a normal
    // auth failure that flips the account to NeedsReconnect rather than crashing every read.
    private static string Unprotect(IDataProtector protector, string stored)
    {
        try
        {
            return protector.Unprotect(stored);
        }
        catch (CryptographicException)
        {
            return stored;
        }
    }
}
