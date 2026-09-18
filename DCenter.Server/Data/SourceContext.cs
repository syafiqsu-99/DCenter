using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class SourceContext(DbContextOptions<SourceContext> options) : DbContext(options)
{
    public DbSet<WorkOrderRow> WorkOrderRows => Set<WorkOrderRow>();

    public DbSet<WorkOrderDetail> WorkOrderDetails => Set<WorkOrderDetail>();
    public DbSet<BillOfMaterialOther> BillOfMaterialOthers => Set<BillOfMaterialOther>();
    public DbSet<MRNCategory> MRNCategory => Set<MRNCategory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkOrderRow>().HasNoKey().ToView(null);
        b.Entity<WorkOrderRow>().Property(x => x.Qty).HasPrecision(18, 4);

        b.Entity<WorkOrderDetail>(e =>
        {
            e.HasNoKey();
            e.ToTable("Work_Order_Detail");
            e.Property(x => x.WoNumber).HasColumnName("WO_NUMBER");
            e.Property(x => x.AssemblyItem).HasColumnName("ASSEMBLY_ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.StartQuantity).HasColumnName("START_QUANTITY").HasPrecision(18, 4);
        });

        b.Entity<BillOfMaterialOther>(e =>
        {
            e.HasNoKey();
            e.ToTable("Bill_Of_Material_Others");
            e.Property(x => x.Item).HasColumnName("ITEM");
            e.Property(x => x.Component).HasColumnName("COMPONENT");
            e.Property(x => x.ComponentDesc).HasColumnName("COMPONENT_DESC");
        });

        b.Entity<MRNCategory>(e =>
        {
            e.HasNoKey();
            e.ToTable("Tbl_Item_Category_MRN");
            e.Property(x => x.Item).HasColumnName("ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.MRN).HasColumnName("MRN");
            e.Property(x => x.MRNDesc).HasColumnName("MRN_DESC");
        });
    }
}