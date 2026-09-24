using DCenter.Server.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Data;

public static class ConsumableStockModel
{
    public const string TxnSequence = "DCenter_ConsumableTxnSeq";
    public const string BakingSequence = "DCenter_BakingNoSeq";
    public const string HoldingSequence = "DCenter_HoldingNoSeq";
    public const string StockCountSequence = "DCenter_StockCountSeq";

    public static void Apply(ModelBuilder b)
    {
        b.HasSequence<long>(TxnSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(BakingSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(HoldingSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(StockCountSequence).StartsAt(1).IncrementsBy(1);

        b.ApplyConfiguration(new ConsumableItemConfiguration());
        b.ApplyConfiguration(new ConsumableItemLotConfiguration());
        b.ApplyConfiguration(new ConsumableMovementConfiguration());
        b.ApplyConfiguration(new OvenConfiguration());
        b.ApplyConfiguration(new OvenCompartmentConfiguration());
        b.ApplyConfiguration(new BakingRecordConfiguration());
        b.ApplyConfiguration(new HoldingRecordConfiguration());
        b.ApplyConfiguration(new StockCountConfiguration());
        b.ApplyConfiguration(new SupervisorCredentialConfiguration());

        b.Entity<Welder>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_DCenter_Welders_UsageScope",
                $"[UsageScope] IN ({SqlLiteral.List(WelderScope.All)})"));
            e.Property(x => x.UsageScope).HasMaxLength(20).IsRequired().HasDefaultValue(WelderScope.Report);
        });
    }
}

public class ConsumableItemConfiguration : IEntityTypeConfiguration<ConsumableItem>
{
    public void Configure(EntityTypeBuilder<ConsumableItem> e)
    {
        e.ToTable("DCenter_ConsumableItems", t =>
        {
            t.HasCheckConstraint("CK_DCenter_ConsumableItems_Category",
                $"[Category] IN ({SqlLiteral.List(Cat.Categories)})");
            t.HasCheckConstraint("CK_DCenter_ConsumableItems_Limits",
                "[MinStockKg] >= 0 AND [ActivatedMinKg] >= 0 AND ([FinishThresholdKg] IS NULL OR [FinishThresholdKg] >= 0)");
            t.HasCheckConstraint("CK_DCenter_ConsumableItems_OvenType",
                $"[HoldingOvenType] IS NULL OR [HoldingOvenType] IN ({SqlLiteral.List(Cat.OvenTypes)})");
        });
        e.Property(x => x.Category).HasMaxLength(30).IsRequired();
        e.Property(x => x.Specification).HasMaxLength(100).IsRequired();
        e.Property(x => x.Diameter).HasMaxLength(30).IsRequired();
        e.Property(x => x.HoldingOvenType).HasMaxLength(30);
        e.Property(x => x.MinStockKg).HasPrecision(10, 2);
        e.Property(x => x.ActivatedMinKg).HasPrecision(10, 2);
        e.Property(x => x.FinishThresholdKg).HasPrecision(10, 2);
        e.HasIndex(x => new { x.Specification, x.Diameter }).IsUnique();
    }
}

public class ConsumableItemLotConfiguration : IEntityTypeConfiguration<ConsumableItemLot>
{
    public void Configure(EntityTypeBuilder<ConsumableItemLot> e)
    {
        e.ToTable("DCenter_ConsumableItemLots");
        e.Property(x => x.Brand).HasMaxLength(100).IsRequired();
        e.Property(x => x.LotNumber).HasMaxLength(60).IsRequired();
        e.HasOne(x => x.Item).WithMany(i => i.Lots)
            .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ItemId, x.Brand, x.LotNumber }).IsUnique();
    }
}

public class ConsumableMovementConfiguration : IEntityTypeConfiguration<ConsumableMovement>
{
    public void Configure(EntityTypeBuilder<ConsumableMovement> e)
    {
        var stages = SqlLiteral.List(Cat.Stages);
        const string receive = Cat.TxnReceive;
        const string voidType = Cat.TxnVoid;
        const string hold = Cat.TxnHold;
        const string move = Cat.TxnMove;
        const string activated = Cat.Activated;

        e.ToTable("DCenter_ConsumableMovements", t =>
        {
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_Type",
                $"[TxnType] IN ({SqlLiteral.List(Cat.TxnTypes)})");
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_Stage",
                $"([FromStage] IS NULL OR [FromStage] IN ({stages})) AND ([ToStage] IS NULL OR [ToStage] IN ({stages})) " +
                "AND ([FromStage] IS NOT NULL OR [ToStage] IS NOT NULL)");
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_Source",
                $"([Source] IS NULL OR [Source] IN ({SqlLiteral.List(Cat.Sources)})) " +
                $"AND ([TxnType] <> N'{receive}' OR [Source] IS NOT NULL)");
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_Qty", "[QuantityKg] > 0");
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_Void",
                $"([TxnType] = N'{voidType}' AND [VoidsMovementId] IS NOT NULL) OR ([TxnType] <> N'{voidType}' AND [VoidsMovementId] IS NULL)");
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_Bin",
                $"([FromCompartmentId] IS NULL OR [FromStage] = N'{activated}') AND ([ToCompartmentId] IS NULL OR [ToStage] = N'{activated}')");
            t.HasCheckConstraint("CK_DCenter_ConsumableMovements_HoldMove",
                $"([TxnType] <> N'{hold}' OR [ToCompartmentId] IS NOT NULL) " +
                $"AND ([TxnType] <> N'{move}' OR ([FromStage] = N'{activated}' AND [ToStage] = N'{activated}' AND [ToCompartmentId] IS NOT NULL))");
        });

        e.Property(x => x.TxnNo).HasMaxLength(20).IsRequired();
        e.Property(x => x.TxnType).HasMaxLength(20).IsRequired();
        e.Property(x => x.FromStage).HasMaxLength(20);
        e.Property(x => x.ToStage).HasMaxLength(20);
        e.Property(x => x.Source).HasMaxLength(20);
        e.Property(x => x.Requestor).HasMaxLength(200);
        e.Property(x => x.Reason).HasMaxLength(40);
        e.Property(x => x.ReferenceNo).HasMaxLength(60);
        e.Property(x => x.Remarks).HasMaxLength(500);
        e.Property(x => x.CreatedBy).HasMaxLength(100);
        e.Property(x => x.QuantityKg).HasPrecision(10, 2);
        e.Property(x => x.CountedQtyKg).HasPrecision(10, 2);

        e.HasOne(x => x.Lot).WithMany(l => l.Movements)
            .HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Welder).WithMany()
            .HasForeignKey(x => x.WelderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ConsumableMovement>().WithMany()
            .HasForeignKey(x => x.VoidsMovementId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.FromCompartment).WithMany()
            .HasForeignKey(x => x.FromCompartmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ToCompartment).WithMany()
            .HasForeignKey(x => x.ToCompartmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.BakingRecord).WithMany()
            .HasForeignKey(x => x.BakingRecordId).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.TxnNo);
        e.HasIndex(x => x.VoidsMovementId).IsUnique().HasFilter("[VoidsMovementId] IS NOT NULL");
        e.HasIndex(x => x.LotId)
            .IncludeProperties(x => new { x.TxnType, x.FromStage, x.ToStage, x.QuantityKg, x.IsVoided });
        e.HasIndex(x => x.TxnDate)
            .IncludeProperties(x => new { x.TxnType, x.QuantityKg, x.IsVoided, x.LotId });
        e.HasIndex(x => new { x.WelderId, x.TxnDate }).HasFilter("[WelderId] IS NOT NULL");
        e.HasIndex(x => x.FromCompartmentId).HasFilter("[FromCompartmentId] IS NOT NULL")
            .IncludeProperties(x => new { x.LotId, x.QuantityKg, x.IsVoided, x.TxnType });
        e.HasIndex(x => x.ToCompartmentId).HasFilter("[ToCompartmentId] IS NOT NULL")
            .IncludeProperties(x => new { x.LotId, x.QuantityKg, x.IsVoided, x.TxnType });
        e.HasIndex(x => x.BakingRecordId).HasFilter("[BakingRecordId] IS NOT NULL");
        e.HasIndex(x => x.CreatedAt);
    }
}

public class OvenConfiguration : IEntityTypeConfiguration<Oven>
{
    public void Configure(EntityTypeBuilder<Oven> e)
    {
        e.ToTable("DCenter_Ovens", t => t.HasCheckConstraint("CK_DCenter_Ovens_Type",
            $"[OvenType] IN ({SqlLiteral.List(Cat.OvenTypes)})"));
        e.Property(x => x.Name).HasMaxLength(50).IsRequired();
        e.Property(x => x.Code).HasMaxLength(10).IsRequired();
        e.Property(x => x.OvenType).HasMaxLength(30).IsRequired();
        e.HasIndex(x => x.Name).IsUnique();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.OvenType).IsUnique();

        e.HasData(FixedOvens.Ovens.Select(o => new Oven { Id = o.Id, Name = o.Name, Code = o.Code, OvenType = o.OvenType }).ToArray());
    }
}

public class OvenCompartmentConfiguration : IEntityTypeConfiguration<OvenCompartment>
{
    public void Configure(EntityTypeBuilder<OvenCompartment> e)
    {
        e.ToTable("DCenter_OvenCompartments", t => t.HasCheckConstraint("CK_DCenter_OvenCompartments_Number",
            $"[Number] BETWEEN 1 AND {FixedOvens.CompartmentsPerOven}"));
        e.Property(x => x.Label).HasMaxLength(20).IsRequired();
        e.HasOne(x => x.Oven).WithMany(o => o.Compartments)
            .HasForeignKey(x => x.OvenId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.OvenId, x.Number }).IsUnique();

        e.HasData(FixedOvens.Ovens
            .SelectMany(o => Enumerable.Range(1, FixedOvens.CompartmentsPerOven).Select(n => new OvenCompartment
            {
                Id = FixedOvens.CompartmentId(o.Id, n),
                OvenId = o.Id,
                Number = n,
                Label = $"C{n}",
            }))
            .ToArray());
    }
}

public class BakingRecordConfiguration : IEntityTypeConfiguration<BakingRecord>
{
    public void Configure(EntityTypeBuilder<BakingRecord> e)
    {
        e.ToTable("DCenter_BakingRecords", t =>
        {
            t.HasCheckConstraint("CK_DCenter_BakingRecords_Status",
                $"[Status] IN ({SqlLiteral.List(Cat.BakingStatuses)})");
            t.HasCheckConstraint("CK_DCenter_BakingRecords_Qty", "[QuantityKg] > 0");
            t.HasCheckConstraint("CK_DCenter_BakingRecords_Times",
                "([BakeStop] IS NULL OR ([BakeStart] IS NOT NULL AND [BakeStop] > [BakeStart])) " +
                "AND ([RebakeStart] IS NULL OR ([BakeStop] IS NOT NULL AND [RebakeStart] > [BakeStop])) " +
                "AND ([RebakeStop] IS NULL OR ([RebakeStart] IS NOT NULL AND [RebakeStop] > [RebakeStart]))");
        });
        e.Property(x => x.BakingNo).HasMaxLength(20).IsRequired();
        e.Property(x => x.PersonInCharge).HasMaxLength(100).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.Remarks).HasMaxLength(500);
        e.Property(x => x.CreatedBy).HasMaxLength(100);
        e.Property(x => x.QuantityKg).HasPrecision(10, 2);
        e.HasOne(x => x.Lot).WithMany()
            .HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.BakingNo).IsUnique();
        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.BakingDate);
    }
}

public class HoldingRecordConfiguration : IEntityTypeConfiguration<HoldingRecord>
{
    public void Configure(EntityTypeBuilder<HoldingRecord> e)
    {
        e.ToTable("DCenter_HoldingRecords", t =>
        {
            t.HasCheckConstraint("CK_DCenter_HoldingRecords_Target",
                "([CompartmentId] IS NOT NULL AND [IsFinishedAfterBaking] = 0) " +
                "OR ([CompartmentId] IS NULL AND [IsFinishedAfterBaking] = 1 AND [WelderId] IS NOT NULL)");
            t.HasCheckConstraint("CK_DCenter_HoldingRecords_Qty", "[QuantityKg] > 0");
        });
        e.Property(x => x.HoldingNo).HasMaxLength(20).IsRequired();
        e.Property(x => x.WelderName).HasMaxLength(200);
        e.Property(x => x.TxnNo).HasMaxLength(20).IsRequired();
        e.Property(x => x.Remarks).HasMaxLength(500);
        e.Property(x => x.CreatedBy).HasMaxLength(100);
        e.Property(x => x.QuantityKg).HasPrecision(10, 2);
        e.HasOne(x => x.BakingRecord).WithMany()
            .HasForeignKey(x => x.BakingRecordId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Welder).WithMany()
            .HasForeignKey(x => x.WelderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Compartment).WithMany()
            .HasForeignKey(x => x.CompartmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.HoldingNo).IsUnique();
        e.HasIndex(x => x.TxnNo);
        e.HasIndex(x => x.HoldingDate);
    }
}

public class StockCountConfiguration : IEntityTypeConfiguration<StockCount>
{
    public void Configure(EntityTypeBuilder<StockCount> e)
    {
        e.ToTable("DCenter_StockCounts", t =>
        {
            t.HasCheckConstraint("CK_DCenter_StockCounts_Scope", $"[Scope] IN ({SqlLiteral.List(Cat.ActiveStages)})");
            t.HasCheckConstraint("CK_DCenter_StockCounts_Kg", "[GainKg] >= 0 AND [LossKg] >= 0");
        });
        e.Property(x => x.ReferenceNo).HasMaxLength(20).IsRequired();
        e.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        e.Property(x => x.Category).HasMaxLength(30);
        e.Property(x => x.TxnNo).HasMaxLength(20);
        e.Property(x => x.Remarks).HasMaxLength(500);
        e.Property(x => x.CreatedBy).HasMaxLength(100);
        e.Property(x => x.GainKg).HasPrecision(10, 2);
        e.Property(x => x.LossKg).HasPrecision(10, 2);
        e.HasIndex(x => x.ReferenceNo).IsUnique();
        e.HasIndex(x => new { x.Scope, x.CountDate });
    }
}

public class SupervisorCredentialConfiguration : IEntityTypeConfiguration<SupervisorCredential>
{
    public void Configure(EntityTypeBuilder<SupervisorCredential> e)
    {
        e.ToTable("DCenter_SupervisorCredentials", t => t.HasCheckConstraint("CK_DCenter_SupervisorCredentials_Single",
            $"[Id] = {SupervisorCredential.SingletonId}"));
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        e.Property(x => x.UpdatedBy).HasMaxLength(100);
    }
}
