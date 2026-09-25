using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Data;

public class SourceContext(DbContextOptions<SourceContext> options) : DbContext(options)
{
    public DbSet<WorkOrderDetail> WorkOrderDetails => Set<WorkOrderDetail>();
    public DbSet<BomTreeRow> BomTree => Set<BomTreeRow>();
    public DbSet<ItemMrn> ItemMrns => Set<ItemMrn>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkOrderDetail>(e =>
        {
            e.HasNoKey();
            e.ToTable("Work_Order_Detail");
            e.Property(x => x.WoNumber).HasColumnName("WO_NUMBER").IsUnicode(false);
            e.Property(x => x.AssemblyItem).HasColumnName("ASSEMBLY_ITEM");
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.StartQuantity).HasColumnName("START_QUANTITY").HasPrecision(18, 4);
        });

        b.Entity<BomTreeRow>(e =>
        {
            e.HasNoKey();
            e.ToView("vw_DCenter_BomTree");
            e.Property(x => x.RootItem).HasColumnName("ROOT_ITEM").IsUnicode(false);
            e.Property(x => x.Level).HasColumnName("BOM_LEVEL");
            e.Property(x => x.ParentItem).HasColumnName("PARENT_ITEM");
            e.Property(x => x.Component).HasColumnName("COMPONENT");
            e.Property(x => x.ComponentDesc).HasColumnName("COMPONENT_DESC");
            e.Property(x => x.Path).HasColumnName("BOM_PATH");
        });

        b.Entity<ItemMrn>(e =>
        {
            e.HasNoKey();
            e.ToTable("Tbl_Item_Category_MRN");
            e.Property(x => x.Item).HasColumnName("ITEM").IsUnicode(false);
            e.Property(x => x.ItemDesc).HasColumnName("ITEM_DESC");
            e.Property(x => x.Mrn).HasColumnName("MRN");
            e.Property(x => x.MrnDesc).HasColumnName("MRN_DESC");
            e.Property(x => x.CategorySetName).HasColumnName("CATEGORY_SET_NAME");
        });
    }
}
