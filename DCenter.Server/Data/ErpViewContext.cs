using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class ErpViewContext(DbContextOptions<ErpViewContext> options) : DbContext(options)
{
    public DbSet<WorkOrderDetail> WorkOrderDetails => Set<WorkOrderDetail>();
    public DbSet<BomLink> Bom => Set<BomLink>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkOrderDetail>(e =>
        {
            e.HasNoKey();
            e.ToView("V_DCenter_WorkOrder");
            e.Property(x => x.WoNumber).HasColumnName("WO_NUMBER").IsUnicode(false);
            e.Property(x => x.AssemblyItem).HasColumnName("ASSEMBLY_ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.StartQuantity).HasColumnName("START_QUANTITY").HasPrecision(18, 4);
        });

        b.Entity<BomLink>(e =>
        {
            e.HasNoKey();
            e.ToView("V_DCenter_Bom");
            e.Property(x => x.Item).HasColumnName("ITEM").IsUnicode(false);
            e.Property(x => x.Component).HasColumnName("COMPONENT");
            e.Property(x => x.ComponentDesc).HasColumnName("COMPONENT_DESC");
        });
    }
}
