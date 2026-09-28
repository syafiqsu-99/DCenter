/*
    DCenter work order views. Run this against the DCenter database (the DefaultConnection
    database), not OracleBetsyDB. Nothing is created or changed in OracleBetsyDB. Safe to re-run.

    Requirements
    - The DCenter database must be on the same SQL Server instance as OracleBetsyDB. On another
      machine (e.g. a localdb dev box), replace every "OracleBetsyDB.dbo." with a linked-server
      name such as "[MYNILASPMFGDB01].OracleBetsyDB.dbo.".
    - The login used by DefaultConnection needs SELECT on OracleBetsyDB.dbo.Work_Order_Detail,
      OracleBetsyDB.dbo.Tbl_Item_Category_MRN and OracleBetsyDB.dbo.Bill_Of_Material_Others.

    If an earlier DCenter script created objects inside OracleBetsyDB, remove them there:
        -- USE [OracleBetsyDB];
        -- DROP VIEW IF EXISTS dbo.vw_DCenter_BomTree;
        -- DROP FUNCTION IF EXISTS dbo.fn_DCenter_WorkOrderBom;

    Re-run this script whenever it changes. The API reads work order parts through
    dbo.fn_DCenter_WorkOrderParts, which needs dbo.fn_DCenter_BomTree and dbo.vw_DCenter_Bom.
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_WorkOrder
AS
SELECT w.WO_NUMBER, w.ASSEMBLY_ITEM, w.ITEM_DESC, w.START_QUANTITY
FROM OracleBetsyDB.dbo.Work_Order_Detail w;
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_ItemMrn
AS
SELECT m.ITEM, m.ITEM_DESC, m.MRN, m.MRN_DESC, m.CATEGORY_SET_NAME
FROM OracleBetsyDB.dbo.Tbl_Item_Category_MRN m;
GO

DROP VIEW IF EXISTS dbo.vw_DCenter_BomTree;
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_Bom
AS
SELECT DISTINCT b.ITEM, b.COMPONENT, b.COMPONENT_DESC
FROM OracleBetsyDB.dbo.Bill_Of_Material_Others b
WHERE b.ITEM IS NOT NULL AND b.COMPONENT IS NOT NULL;
GO

/*
    BOM below one assembly item. The recursion starts at @RootItem only, so SQL Server
    walks that assembly's branch instead of expanding the whole BOM table.
*/
CREATE OR ALTER FUNCTION dbo.fn_DCenter_BomTree (@RootItem varchar(4000))
RETURNS TABLE
AS
RETURN
WITH tree AS (
    SELECT
        b.ITEM AS ROOT_ITEM,
        1 AS BOM_LEVEL,
        b.ITEM AS PARENT_ITEM,
        b.COMPONENT,
        b.COMPONENT_DESC,
        CAST('/' + b.ITEM + '/' + b.COMPONENT + '/' AS varchar(4000)) AS BOM_PATH
    FROM dbo.vw_DCenter_Bom b
    WHERE b.ITEM = @RootItem

    UNION ALL

    SELECT
        t.ROOT_ITEM,
        t.BOM_LEVEL + 1,
        c.ITEM,
        c.COMPONENT,
        c.COMPONENT_DESC,
        CAST(t.BOM_PATH + c.COMPONENT + '/' AS varchar(4000))
    FROM tree t
    JOIN dbo.vw_DCenter_Bom c ON c.ITEM = t.COMPONENT
    WHERE t.BOM_LEVEL < 20
      AND CHARINDEX('/' + c.COMPONENT + '/', t.BOM_PATH) = 0
)
SELECT ROOT_ITEM, BOM_LEVEL, PARENT_ITEM, COMPONENT, COMPONENT_DESC, BOM_PATH
FROM tree;
GO

/*
    Everything the Report tab shows for one work order in a single call: the assembly
    (level 0), every BOM component below it, and each item's preferred MRN.
    Columns are named after the API's WorkOrderNode properties.
*/
CREATE OR ALTER FUNCTION dbo.fn_DCenter_WorkOrderParts (@WoNumber varchar(4000))
RETURNS TABLE
AS
RETURN
WITH wo AS (
    SELECT
        w.WO_NUMBER,
        ISNULL(MAX(w.ASSEMBLY_ITEM), '') AS ASSEMBLY_ITEM,
        MAX(w.ITEM_DESC) AS ASSEMBLY_DESC,
        CAST(MAX(w.START_QUANTITY) AS decimal(18, 4)) AS QTY
    FROM dbo.vw_DCenter_WorkOrder w
    WHERE w.WO_NUMBER = @WoNumber
    GROUP BY w.WO_NUMBER
),
nodes AS (
    SELECT
        CAST(0 AS int) AS BOM_LEVEL,
        CAST(NULL AS varchar(4000)) AS PARENT_ITEM,
        CAST(wo.ASSEMBLY_ITEM AS varchar(4000)) AS ITEM,
        CAST(wo.ASSEMBLY_DESC AS nvarchar(4000)) AS ITEM_DESC,
        CAST('/' + wo.ASSEMBLY_ITEM + '/' AS varchar(4000)) AS BOM_PATH
    FROM wo

    UNION ALL

    SELECT
        t.BOM_LEVEL,
        CAST(t.PARENT_ITEM AS varchar(4000)),
        CAST(t.COMPONENT AS varchar(4000)),
        CAST(t.COMPONENT_DESC AS nvarchar(4000)),
        t.BOM_PATH
    FROM wo
    CROSS APPLY dbo.fn_DCenter_BomTree(wo.ASSEMBLY_ITEM) t
)
SELECT
    wo.WO_NUMBER AS WorkOrderNumber,
    wo.ASSEMBLY_ITEM AS AssemblyItem,
    wo.ASSEMBLY_DESC AS AssemblyDesc,
    wo.QTY AS Qty,
    n.BOM_LEVEL AS [Level],
    n.PARENT_ITEM AS ParentItem,
    n.ITEM AS Item,
    COALESCE(NULLIF(LTRIM(RTRIM(n.ITEM_DESC)), N''), m.ITEM_DESC) AS ItemDesc,
    n.BOM_PATH AS [Path],
    m.MRN AS Mrn,
    m.MRN_DESC AS MrnDesc
FROM wo
CROSS JOIN nodes n
OUTER APPLY (
    SELECT TOP (1) x.ITEM_DESC, x.MRN, x.MRN_DESC
    FROM dbo.vw_DCenter_ItemMrn x
    WHERE x.ITEM = n.ITEM
    ORDER BY CASE WHEN x.MRN IS NULL THEN 1 ELSE 0 END, x.CATEGORY_SET_NAME
) m;
GO
