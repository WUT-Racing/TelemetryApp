using Microsoft.EntityFrameworkCore;

public class TelemetryContext : DbContext
{
    public DbSet<TestSession> TestSessions {get;set;}
    public DbSet<TelemetryPoint> TelemetryPoints {get;set;}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Filename={Constants.DatabaseFilename}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryPoint>().HasOne(p=>p.session)
        .WithMany(s => s.TelemetryPoints)
        .HasForeignKey(p=>p.sessionId);
        base.OnModelCreating(modelBuilder);
    }
}