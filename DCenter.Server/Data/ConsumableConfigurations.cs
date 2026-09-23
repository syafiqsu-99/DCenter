using DCenter.Server.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DCenter.Server.Data;

internal static class SqlLiteral
{
    public static string List(IEnumerable<string> values)
        => string.Join(", ", values.Select(v => $"N'{v.Replace("'", "''")}'"));
}

public class ConsumableConfiguration : IEntityTypeConfiguration<Consumable>
{
    public void Configure(EntityTypeBuilder<Consumable> e)
    {
        e.ToTable("DCenter_Consumables", t =>
        {
            t.HasCheckConstraint("CK_DCenter_Consumables_Type",
                $"[ConsumableType] IN ({SqlLiteral.List(ConsumableCatalog.Types)})");
            t.HasCheckConstraint("CK_DCenter_Consumables_MinStock", "[MinStockKg] >= 0");
        });
        e.Property(x => x.ConsumableType).HasMaxLength(30).IsRequired();
        e.Property(x => x.Manufacturer).HasMaxLength(100).IsRequired();
        e.Property(x => x.Specification).HasMaxLength(100).IsRequired();
        e.Property(x => x.Diameter).HasMaxLength(30).IsRequired();
        e.Property(x => x.MinStockKg).HasPrecision(10, 2);
        e.HasIndex(x => new { x.ConsumableType, x.Manufacturer, x.Specification, x.Diameter }).IsUnique();
    }
}

public class ConsumableLotConfiguration : IEntityTypeConfiguration<ConsumableLot>
{
    public void Configure(EntityTypeBuilder<ConsumableLot> e)
    {
        e.ToTable("DCenter_ConsumableLots");
        e.Property(x => x.LotNumber).HasMaxLength(60).IsRequired();
        e.HasOne(x => x.Consumable).WithMany(c => c.Lots)
            .HasForeignKey(x => x.ConsumableId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ConsumableId, x.LotNumber }).IsUnique();
    }
}

public class ConsumableTransactionConfiguration : IEntityTypeConfiguration<ConsumableTransaction>
{
    public void Configure(EntityTypeBuilder<ConsumableTransaction> e)
    {
        const string receive = ConsumableCatalog.TxnReceive;
        const string issue = ConsumableCatalog.TxnIssue;
        const string voidType = ConsumableCatalog.TxnVoid;

        e.ToTable("DCenter_ConsumableTransactions", t =>
        {
            t.HasCheckConstraint("CK_DCenter_ConsumableTxn_Type",
                $"[TxnType] IN ({SqlLiteral.List(ConsumableCatalog.TxnTypes)})");
            t.HasCheckConstraint("CK_DCenter_ConsumableTxn_Location",
                $"[Location] IN ({SqlLiteral.List(ConsumableCatalog.Locations)})");
            t.HasCheckConstraint("CK_DCenter_ConsumableTxn_Sign",
                $"[QuantityKg] <> 0 AND ([TxnType] <> N'{receive}' OR [QuantityKg] > 0) AND ([TxnType] <> N'{issue}' OR [QuantityKg] < 0)");
            t.HasCheckConstraint("CK_DCenter_ConsumableTxn_Void",
                $"([TxnType] = N'{voidType}' AND [VoidsTxnId] IS NOT NULL) OR ([TxnType] <> N'{voidType}' AND [VoidsTxnId] IS NULL)");
        });

        e.Property(x => x.TxnType).HasMaxLength(10).IsRequired();
        e.Property(x => x.Location).HasMaxLength(20).IsRequired();
        e.Property(x => x.QuantityKg).HasPrecision(10, 2);
        e.Property(x => x.Requestor).HasMaxLength(200);
        e.Property(x => x.ReferenceNo).HasMaxLength(60);
        e.Property(x => x.Remarks).HasMaxLength(500);

        e.HasOne(x => x.Lot).WithMany(l => l.Transactions)
            .HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ConsumableTransaction>().WithMany()
            .HasForeignKey(x => x.VoidsTxnId).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.VoidsTxnId).IsUnique().HasFilter("[VoidsTxnId] IS NOT NULL");
        e.HasIndex(x => new { x.LotId, x.Location })
            .IncludeProperties(x => new { x.TxnType, x.QuantityKg, x.IsVoided });
        e.HasIndex(x => x.TxnDate)
            .IncludeProperties(x => new { x.TxnType, x.QuantityKg, x.IsVoided, x.LotId, x.Location });
        e.HasIndex(x => x.CreatedAt);
    }
}
