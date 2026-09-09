using DCenter.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class WeldReportContext(DbContextOptions<WeldReportContext> options) : DbContext(options)
{
    public DbSet<Welder> Welders => Set<Welder>();
    public DbSet<LookupItem> Lookups => Set<LookupItem>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Joint> Joints => Set<Joint>();
    public DbSet<JointMaterial> JointMaterials => Set<JointMaterial>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Welder>(e =>
        {
            e.ToTable("welders");
            e.Property(x => x.WelderName).HasMaxLength(200).IsRequired();
            e.Property(x => x.WelderNo).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.WelderNo);
            e.HasIndex(x => x.WelderName);
        });

        b.Entity<LookupItem>(e =>
        {
            e.ToTable("lookups");
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.Value).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.Category, x.Value }).IsUnique();
        });

        b.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.Property(x => x.JobNumber).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.JobNumber).IsUnique();
            e.HasMany(x => x.Joints)
             .WithOne(x => x.Report)
             .HasForeignKey(x => x.ReportId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Joint>(e =>
        {
            e.ToTable("joints");
            e.HasMany(x => x.Materials)
             .WithOne(x => x.Joint)
             .HasForeignKey(x => x.JointId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<JointMaterial>(e => e.ToTable("joint_materials"));
    }
}
