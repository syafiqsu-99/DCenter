using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class SourceContext(DbContextOptions<SourceContext> options) : DbContext(options)
{
    public DbSet<WorkOrderDetail> WorkOrderDetails => Set<WorkOrderDetail>();
    public DbSet<WorkOrderNode> WorkOrderNodes => Set<WorkOrderNode>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkOrderDetail>(e =>
        {
            e.HasNoKey();
            e.ToTable("Work_Order_Detail");
            e.Property(x => x.WoNumber).HasColumnName("WO_NUMBER");
            e.Property(x => x.AssemblyItem).HasColumnName("ASSEMBLY_ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.StartQuantity).HasColumnName("START_QUANTITY").HasPrecision(18, 4);
        });

        b.Entity<WorkOrderNode>(e =>
        {
            e.HasNoKey();
            e.ToView(null);
            e.Property(x => x.WorkOrderNumber).HasColumnName("WO_NUMBER");
            e.Property(x => x.AssemblyItem).HasColumnName("ASSEMBLY_ITEM");
            e.Property(x => x.AssemblyDesc).HasColumnName("ASSEMBLY_DESC");
            e.Property(x => x.Qty).HasColumnName("START_QUANTITY").HasPrecision(18, 4);
            e.Property(x => x.Level).HasColumnName("BOM_LEVEL");
            e.Property(x => x.ParentItem).HasColumnName("PARENT_ITEM");
            e.Property(x => x.Item).HasColumnName("ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.Path).HasColumnName("BOM_PATH");
            e.Property(x => x.Mrn).HasColumnName("MRN");
            e.Property(x => x.MrnDesc).HasColumnName("MRN_DESC");
        });
    }
}
