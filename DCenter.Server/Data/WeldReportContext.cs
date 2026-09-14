using DCenter.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class WeldReportContext(DbContextOptions<WeldReportContext> options) : DbContext(options)
{
    public DbSet<Welder> Welders => Set<Welder>();
    public DbSet<LookupItem> Lookups => Set<LookupItem>();
    public DbSet<WpsItem> WpsItems => Set<WpsItem>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Joint> Joints => Set<Joint>();
    public DbSet<JointMaterial> JointMaterials => Set<JointMaterial>();
    public DbSet<ReportStatusEvent> ReportStatusEvents => Set<ReportStatusEvent>();

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

        b.Entity<WpsItem>(e =>
        {
            e.ToTable("wps_items");
            e.Property(x => x.WpsNo).HasMaxLength(200).IsRequired();
            e.Property(x => x.Rev).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(400);
            e.HasIndex(x => x.WpsNo).IsUnique();
        });

        b.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.Property(x => x.JobNumber).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.JobNumber).IsUnique();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Joints)
             .WithOne(x => x.Report)
             .HasForeignKey(x => x.ReportId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.StatusEvents)
             .WithOne(x => x.Report)
             .HasForeignKey(x => x.ReportId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ReportStatusEvent>(e =>
        {
            e.ToTable("report_status_events");
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.ReportId);
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