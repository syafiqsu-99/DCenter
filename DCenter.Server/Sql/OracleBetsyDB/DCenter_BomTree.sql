/*
    DCenter multi-level bill of material objects for OracleBetsyDB.
    Creates (or updates) one view and one inline table-valued function.
    Existing tables and their data are not modified. Safe to re-run.
*/
USE [OracleBetsyDB]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW dbo.vw_DCenter_BomTree
AS
WITH bom AS (
    SELECT DISTINCT b.ITEM, b.COMPONENT, b.COMPONENT_DESC
    FROM dbo.Bill_Of_Material_Others b
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

CREATE OR ALTER FUNCTION dbo.fn_DCenter_WorkOrderBom (@WoNumber varchar(240))
RETURNS TABLE
AS
RETURN
WITH wo AS (
    SELECT DISTINCT w.WO_NUMBER, w.ASSEMBLY_ITEM, w.ITEM_DESC, w.START_QUANTITY
    FROM dbo.Work_Order_Detail w
    WHERE w.WO_NUMBER = @WoNumber
),
bom AS (
    SELECT DISTINCT b.ITEM, b.COMPONENT, b.COMPONENT_DESC
    FROM dbo.Bill_Of_Material_Others b
    WHERE b.ITEM IS NOT NULL AND b.COMPONENT IS NOT NULL
),
tree AS (
    SELECT
        wo.WO_NUMBER,
        0 AS BOM_LEVEL,
        CAST(NULL AS varchar(40)) AS PARENT_ITEM,
        wo.ASSEMBLY_ITEM AS ITEM,
        wo.ITEM_DESC AS ITEM_DESC,
        CAST('/' + ISNULL(wo.ASSEMBLY_ITEM, '') + '/' AS varchar(4000)) AS BOM_PATH
    FROM wo

    UNION ALL

    SELECT
        t.WO_NUMBER,
        t.BOM_LEVEL + 1,
        c.ITEM,
        c.COMPONENT,
        c.COMPONENT_DESC,
        CAST(t.BOM_PATH + c.COMPONENT + '/' AS varchar(4000))
    FROM tree t
    JOIN bom c ON c.ITEM = t.ITEM
    WHERE t.BOM_LEVEL < 20
      AND CHARINDEX('/' + c.COMPONENT + '/', t.BOM_PATH) = 0
)
SELECT
    t.WO_NUMBER,
    wo.ASSEMBLY_ITEM,
    wo.ITEM_DESC AS ASSEMBLY_DESC,
    wo.START_QUANTITY,
    t.BOM_LEVEL,
    t.PARENT_ITEM,
    t.ITEM,
    COALESCE(NULLIF(t.ITEM_DESC, ''), mrn.ITEM_DESC) AS ITEM_DESC,
    t.BOM_PATH,
    mrn.MRN,
    mrn.MRN_DESC
FROM tree t
JOIN wo ON wo.WO_NUMBER = t.WO_NUMBER
OUTER APPLY (
    SELECT TOP (1) m.ITEM_DESC, m.MRN, m.MRN_DESC
    FROM dbo.Tbl_Item_Category_MRN m
    WHERE m.ITEM = t.ITEM
    ORDER BY CASE WHEN m.MRN IS NULL THEN 1 ELSE 0 END, m.CATEGORY_SET_NAME
) mrn;
GO
