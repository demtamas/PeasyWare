-- =================================================================================
-- PeasyWare WMS - Database Views Script (Raw Timestamps)
-- File: 03_Views.sql
-- Description: This version of the script displays all timestamps as they are stored
-- in the database (in UTC) without any conversion to local time.
-- =================================================================================

USE WMS_DB;
GO

-- =================================================================================
-- OPERATIONAL VIEWS
-- =================================================================================

-- Provides a comprehensive, human-readable overview of all warehouse locations.
CREATE OR ALTER VIEW views.BrowseLocations AS
SELECT
    l.location_id,
    l.location_name AS Location,
    lt.type_name AS Type,
    ls.section_name AS Section,
    l.capacity AS Capacity,
    l.is_active AS Active,
    l.notes AS Notes,
    COUNT(i.inventory_id) AS 'Stock Count',
    (l.capacity - COUNT(i.inventory_id)) AS 'Available capacity',
    -- Displays the raw UTC timestamp of the last update.
    MAX(i.updated_at) AS 'Last Movement',
    l.updated_by AS 'Last Updated By',
    CASE WHEN COUNT(DISTINCT lr.location_id) > 0 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS Reserved
FROM
    locations.locations l
JOIN
    locations.location_types lt ON l.type_id = lt.type_id
JOIN
    locations.location_sections ls ON l.section_id = ls.section_id
LEFT JOIN
    inventory.inventory i ON l.location_id = i.location_id
LEFT JOIN
    locations.location_reservations lr ON l.location_id = lr.location_id AND lr.expires_at > SYSUTCDATETIME()
GROUP BY
    l.location_id, l.location_name, lt.type_name, ls.section_name,
    l.capacity, l.is_active, l.notes, l.updated_by;
GO

-- Provides a detailed summary of every inventory item (pallet) in the warehouse.
CREATE OR ALTER VIEW views.Inventory_summary AS
SELECT
    inv.external_id AS pallet_number,
    sku.sku_name AS material,
    sku.sku_desc AS description,
    inv.batch_number AS batch,
    inv.best_before_date AS best_before,
    inv.actual_qty AS quantity,
    inv.allocated_qty AS allocated,
    inv.document_ref AS reference,
    inv.status AS status,
    loc.location_name AS location,
    inv.weight AS weight,
    -- Displays the raw UTC timestamps.
    inv.movement_started_at AS movement_started,
    inv.created_at AS received_at,
    inv.created_by AS received_by,
    inv.updated_at AS last_moved,
    u.username AS last_moved_by
FROM inventory.inventory AS inv
JOIN inventory.sku AS sku ON inv.sku_name = sku.id
JOIN locations.locations AS loc ON inv.location_id = loc.location_id
LEFT JOIN auth.users AS u on inv.updated_by = u.user_id;
GO

-- Displays all currently active, non-expired location reservations for putaway tasks.
CREATE OR ALTER VIEW views.Reserved_Locations AS
SELECT 
    r.reservation_id,
    l.location_name,
    r.reserved_by_pallet_id,
    u.username,
    r.reservation_type,
    -- Displays the raw UTC timestamps.
    r.reserved_at,
    r.expires_at,
    l.aisle
FROM 
    locations.location_reservations r 
JOIN 
    locations.locations l on r.location_id = l.location_id 
JOIN 
    auth.users u on r.reserved_by = u.user_id
WHERE
    r.expires_at > SYSUTCDATETIME(); -- Ensure only active reservations are shown.
GO

-- =================================================================================
-- SYSTEM ADMINISTRATION VIEWS
-- =================================================================================

-- A view to easily see the run history of the SQL Server Agent job that performs auto-maintenance.
CREATE OR ALTER VIEW views.CleanUpJob_Viewer AS
SELECT
    j.name AS job_name,
    h.run_date,
    h.run_time,
    h.run_duration,
    h.step_name,
    h.message,
    CASE h.run_status
        WHEN 0 THEN 'Failed'
        WHEN 1 THEN 'Succeeded'
        WHEN 2 THEN 'Retry'
        WHEN 3 THEN 'Canceled'
        ELSE 'Unknown'
    END AS run_status
FROM msdb.dbo.sysjobhistory h
JOIN msdb.dbo.sysjobs j ON h.job_id = j.job_id
WHERE j.name = 'WMS Auto Maintenance';
GO
