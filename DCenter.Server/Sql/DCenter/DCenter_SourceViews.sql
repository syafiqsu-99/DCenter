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

CREATE OR ALTER VIEW dbo.vw_DCenter_BomTree
AS
WITH bom AS (
    SELECT DISTINCT b.ITEM, b.COMPONENT, b.COMPONENT_DESC
    FROM OracleBetsyDB.dbo.Bill_Of_Material_Others b
    WHERE b.ITEM IS NOT NULL AND b.COMPONENT IS NOT NULL
),
tree AS (
    SELECT
        b.ITEM AS ROOT_ITEM,
        1 AS BOM_LEVEL,
        b.ITEM AS PARENT_ITEM,
        b.COMPONENT,
        b.COMPONENT_DESC,
        CAST('/' + b.ITEM + '/' + b.COMPONENT + '/' AS varchar(4000)) AS BOM_PATH
    FROM bom b

    UNION ALL

    SELECT
        t.ROOT_ITEM,
        t.BOM_LEVEL + 1,
        c.ITEM,
        c.COMPONENT,
        c.COMPONENT_DESC,
        CAST(t.BOM_PATH + c.COMPONENT + '/' AS varchar(4000))
    FROM tree t
    JOIN bom c ON c.ITEM = t.COMPONENT
    WHERE t.BOM_LEVEL < 20
      AND CHARINDEX('/' + c.COMPONENT + '/', t.BOM_PATH) = 0
)
SELECT ROOT_ITEM, BOM_LEVEL, PARENT_ITEM, COMPONENT, COMPONENT_DESC, BOM_PATH
FROM tree;
GO
