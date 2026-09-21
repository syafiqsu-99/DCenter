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
    public DbSet<MrnSpec> MrnSpecs => Set<MrnSpec>();
    public DbSet<BpvcMaterial> BpvcMaterials => Set<BpvcMaterial>();
    public DbSet<ProcessTypeLink> ProcessTypeLinks => Set<ProcessTypeLink>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Welder>(e =>
        {
            e.ToTable("DCenter_Welders");
            e.Property(x => x.WelderName).HasMaxLength(200).IsRequired();
            e.Property(x => x.WelderNo).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.WelderNo);
            e.HasIndex(x => x.WelderName);
        });

        b.Entity<LookupItem>(e =>
        {
            e.ToTable("DCenter_Lookups");
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.Value).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.Category, x.Value }).IsUnique();
        });

        b.Entity<WpsItem>(e =>
        {
            e.ToTable("DCenter_WpsItems");
            e.Property(x => x.WpsNo).HasMaxLength(200).IsRequired();
            e.Property(x => x.BaseMetal).HasMaxLength(200);
            e.Property(x => x.Process).HasMaxLength(100);
            e.Property(x => x.PNo).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.WpsNo);
            e.HasIndex(x => x.PNo);
            e.HasIndex(x => new { x.WpsNo, x.PNo }).IsUnique();
        });

        b.Entity<Report>(e =>
        {
            e.ToTable("DCenter_Reports");
            e.Property(x => x.WorkOrderNumber).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.WorkOrderNumber).IsUnique();
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
            e.ToTable("DCenter_ReportStatusEvents");
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Details).HasMaxLength(1000);
            e.HasIndex(x => x.ReportId);
        });

        b.Entity<Joint>(e =>
        {
            e.ToTable("DCenter_Joints");
            e.HasMany(x => x.Materials)
             .WithOne(x => x.Joint)
             .HasForeignKey(x => x.JointId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<JointMaterial>(e => e.ToTable("DCenter_JointMaterials"));

        b.Entity<MrnSpec>(e =>
        {
            e.ToTable("DCenter_MrnSpecs");
            e.Property(x => x.Mrn).HasMaxLength(100).IsRequired();
            e.Property(x => x.Form).HasMaxLength(200);
            e.Property(x => x.FullSpecification).HasMaxLength(400);
            e.Property(x => x.SpecNo).HasMaxLength(100).IsRequired();
            e.Property(x => x.SpecNoRaw).HasMaxLength(100);
            e.HasIndex(x => x.Mrn);
        });

        b.Entity<BpvcMaterial>(e =>
        {
            e.ToTable("DCenter_BpvcIx");
            e.Property(x => x.SpecNo).HasMaxLength(100).IsRequired();
            e.Property(x => x.SpecNoRaw).HasMaxLength(100);
            e.Property(x => x.Designation).HasMaxLength(200);
            e.Property(x => x.UnsNo).HasMaxLength(100);
            e.Property(x => x.MinTensile).HasMaxLength(100);
            e.Property(x => x.PNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.GroupNo).HasMaxLength(50);
            e.Property(x => x.IsoGroup).HasMaxLength(100);
            e.Property(x => x.BrazingPNo).HasMaxLength(50);
            e.Property(x => x.NominalComposition).HasMaxLength(400);
            e.Property(x => x.TypicalProductForm).HasMaxLength(200);
            e.Property(x => x.NominalThicknessLimits).HasMaxLength(200);
            e.HasIndex(x => new { x.SpecNo, x.PNo });
        });

        b.Entity<ProcessTypeLink>(e =>
        {
            e.ToTable("DCenter_ProcessTypeLinks");
            e.Property(x => x.Process).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.Process, x.Type }).IsUnique();
        });
    }
}