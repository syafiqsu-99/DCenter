using System.Globalization;

namespace DCenter.Server.Entities;

public static class StockCatalog
{
    public const string BarePowderFiller = "Bare & Powder Filler";
    public const string ElectrodeFiller = "Electrode Filler";
    public static readonly string[] Categories = [BarePowderFiller, ElectrodeFiller];

    public const string WeldShop = "Weld Shop";
    public const string ToolCrib = "Tool Crib";
    public static readonly string[] Sources = [WeldShop, ToolCrib];

    public const string Normal = "Normal";
    public const string Baking = "Baking";
    public const string Activated = "Activated";
    public static readonly string[] Stages = [Normal, Baking, Activated];
    public static readonly string[] ActiveStages = [Normal, Activated];

    public const string TxnReceive = "Receive";
    public const string TxnTransfer = "Transfer";
    public const string TxnSendToBake = "SendToBake";
    public const string TxnHold = "Hold";
    public const string TxnMove = "Move";
    public const string TxnIssue = "Issue";
    public const string TxnReturn = "Return";
    public const string TxnFinish = "Finish";
    public const string TxnAdjust = "Adjust";
    public const string TxnDispose = "Dispose";
    public const string TxnVoid = "Void";

    public static readonly string[] TxnTypes =
    [
        TxnReceive, TxnTransfer, TxnSendToBake, TxnHold, TxnMove, TxnIssue,
        TxnReturn, TxnFinish, TxnAdjust, TxnDispose, TxnVoid,
    ];

    public const string ReasonUsedUp = "Used up";
    public const string ReasonCountVariance = "Count variance";
    public const string ReasonWeighing = "Weighing inaccuracy";
    public const string ReasonFoundStock = "Found stock";
    public const string ReasonOther = "Other";
    public static readonly string[] AdjustReasons =
        [ReasonUsedUp, ReasonCountVariance, ReasonWeighing, ReasonFoundStock, ReasonOther];

    public const string OvenAlloySteel = "Alloy Steel";
    public const string OvenMildSteel = "Mild Steel";
    public const string OvenNiAlloy = "Ni Alloy";
    public const string OvenStainlessSteel = "Stainless Steel";
    public static readonly string[] OvenTypes = [OvenAlloySteel, OvenMildSteel, OvenNiAlloy, OvenStainlessSteel];

    public const string StatusQueued = "Queued";
    public const string StatusBaking = "Baking";
    public const string StatusBaked = "Baked";
    public const string StatusRebakeQueued = "RebakeQueued";
    public const string StatusRebaking = "Rebaking";
    public const string StatusRebaked = "Rebaked";
    public const string StatusClosed = "Closed";
    public const string StatusCancelled = "Cancelled";
    public static readonly string[] BakingStatuses =
    [
        StatusQueued, StatusBaking, StatusBaked, StatusRebakeQueued, StatusRebaking, StatusRebaked, StatusClosed, StatusCancelled,
    ];
    public static readonly string[] OpenBakingStatuses =
        [StatusQueued, StatusBaking, StatusBaked, StatusRebakeQueued, StatusRebaking, StatusRebaked];

    public const string UnassignedBin = "Unassigned";
    public const string MigratedReference = "MIGRATED";
    public const string PersonInChargeLookup = "ConsumablePIC";

    public static string DiaSpec(string diameter, string specification) => $"{diameter} {specification}".Trim();

    public static string FormatDiameter(decimal diameter) => diameter.ToString("0.00", CultureInfo.InvariantCulture);

}
