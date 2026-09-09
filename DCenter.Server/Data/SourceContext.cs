using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class SourceContext(DbContextOptions<SourceContext> options) : DbContext(options)
{
    public DbSet<JobRow> JobRows => Set<JobRow>();

    public DbSet<WorkOrderDetail> WorkOrderDetails => Set<WorkOrderDetail>();
    public DbSet<BillOfMaterialOther> BillOfMaterialOthers => Set<BillOfMaterialOther>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<JobRow>().HasNoKey().ToView(null);

        b.Entity<WorkOrderDetail>(e =>
        {
            e.HasNoKey();
            e.ToTable("Work_Order_Detail");
            e.Property(x => x.WoNumber).HasColumnName("WO_NUMBER");
            e.Property(x => x.AssemblyItem).HasColumnName("ASSEMBLY_ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.StartQuantity).HasColumnName("START_QUANTITY");
        });

        b.Entity<BillOfMaterialOther>(e =>
        {
            e.HasNoKey();
            e.ToTable("Bill_Of_Material_Others");
            e.Property(x => x.Item).HasColumnName("ITEM");
            e.Property(x => x.Component).HasColumnName("COMPONENT");
            e.Property(x => x.ComponentDesc).HasColumnName("COMPONENT_DESC");
        });
    }
}
