using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RaceIQ.Infrastructure;

public class RaceIQDbContext : IdentityDbContext<ApplicationUser>
{
    public RaceIQDbContext(DbContextOptions<RaceIQDbContext> options)
        : base(options)
    {
    }
}
