-- =================================================================================
-- PeasyWare WMS - Test Data Script
-- File: 04_Test_Data.sql
-- Description: This script populates the database with essential master data
-- and sample inventory to enable testing of all application features.
-- =================================================================================

USE WMS_DB;
GO

-- --- AUTH Schema Data ---

-- Insert the default user roles required for the system.
-- 'SA' (Super Admin) has the highest privileges.
INSERT INTO auth.roles (role_name, role_desc) VALUES
('SA', 'Super Administrator with all permissions'),
('Admin', 'Administrator with rights to manage users and settings'),
('Operator', 'Standard warehouse floor operator'),
('Manager', 'Warehouse manager with reporting and override capabilities');
GO

-- Create a 'system' user for automated processes and a default 'seeder' user for initial data.
-- The passwords here are for development purposes only.
EXEC auth.usp_CreateUser
    @username = 'system',
    @password = 'system_password_placeholder', -- This user should not be logged into directly.
    @full_name = 'System Process',
    @email = 'system@peasy.wms',
    @role_name = 'SA',
    @is_active = 1;

EXEC auth.usp_CreateUser
    @username = 'seeder',
    @password = 'seeder',
    @full_name = 'Data Seeder',
    @email = 'seeder@peasy.wms',
    @role_name = 'SA',
    @is_active = 1;
GO

-- Create a default administrator user for application testing.
EXEC auth.usp_CreateUser
    @username = 'admin',
    @password = 'admin',
    @full_name = 'Admin User',
    @email = 'admin@peasy.wms',
    @role_name = 'SA',
    @is_active = 1;
GO

-- Create a personal user for development.
EXEC auth.usp_CreateUser
    @username = 'demjent',
    @password = 'demjent',
    @full_name = 'Tamas Demjen',
    @email = 'tamas.demjen@peasy.wms',
    @role_name = 'SA',
    @is_active = 1;
GO

-- Loop to create a set of standard 'Operator' users for testing multi-user scenarios.
-- NOTE: For a smaller test data set, you could reduce the WHILE loop condition (e.g., WHILE @i <= 3).
DECLARE @i INT = 1;
WHILE @i <= 10
BEGIN
    DECLARE @username NVARCHAR(50);
    DECLARE @password NVARCHAR(100);
    DECLARE @fullName NVARCHAR(100);
    DECLARE @email NVARCHAR(100);
    DECLARE @roleName NVARCHAR(50) = 'Operator';
    DECLARE @isActive BIT = 1;

    -- Construct the user details based on the counter using the safer CONCAT function.
    SET @username = CONCAT('testuser', RIGHT(CONCAT('0', CAST(@i AS NVARCHAR(2))), 2));
    SET @password = @username;
    SET @fullName = @username;
    SET @email = CONCAT(@username, '@peasy.wms');

    PRINT CONCAT('Adding user: ', @username);
    EXEC auth.usp_CreateUser
        @username = @username,
        @password = @password,
        @full_name = @fullName,
        @email = @email,
        @role_name = @roleName,
        @is_active = @isActive;

    SET @i = @i + 1;
END;
GO

-- --- OPERATIONS Schema Data ---

-- Insert the default application settings. These control the runtime behavior of the console app.
INSERT INTO operations.Settings (SettingName, SettingValue, DataType, Description, CreatedBy) VALUES
('ReservationTimeoutMinutes', '15', 'int', 'Duration in minutes a location reservation is held.', 'Seeder'),
('UnlockTimeoutMinutes', '15', 'int', 'Duration in minutes after which an MV pallet is considered stuck and reset.', 'Seeder'),
('AutoCleanupIntervalMinutes', '15', 'int', 'How often the auto maintenance job should ideally run.', 'Seeder'),
('AllowPutawayModification', 'true', 'bool', 'Allows users to manually modify a system-suggested putaway location.', 'Seeder'),
('EnableLogin', 'true', 'bool', 'Disable login during maintenance.', 'Seeder'),
('EnableDebugging', 'true', 'bool', 'Enable or disable verbose logging.', 'Seeder');
GO

-- --- INVENTORY & LOCATIONS Master Data ---

-- Defines all valid movement types for logging and auditing purposes.
INSERT INTO inventory.movement_types (type_code, description) VALUES
('RECEIVING', 'Pallet recorded upon arrival'),
('PUTAWAY_INITIAL', 'Initial putaway from Receiving/Bay area'),
('PUTAWAY_MANUAL', 'Manual putaway directly to a location'),
('PUTAWAY_FINALIZED', 'Initial putaway completed to final location'),
('PICKING', 'Pallet used for an order pick'),
('LOADING', 'Pallet loaded onto a shipment'),
('SHIPPING', 'Pallet departure from warehouse'),
('CANCELLED', 'General cancellation of any process/movement'),
('AUTO_RESET', 'System-initiated status reset (e.g., stuck pallets or expired reservations)'), -- Updated description
('BIN_TO_BIN_STARTED', 'Bin-to-bin movement initiated'),
('BIN_TO_BIN_FINALIZED', 'Bin-to-bin movement completed'),
('GENERAL_MOVEMENT_FINALIZED', 'General MV status movement finalized');
GO

-- Defines the units of measure for products.
INSERT INTO inventory.unit_of_measures (uom_code, description) VALUES
('PAL', 'PALLETIZED STOCK'),
('UNIT', 'FULL PALLET IS A UNIT');
GO

-- Defines the types of physical pallets or containers used.
INSERT INTO inventory.packing_materials (name, description) VALUES
('209600', 'Standard 1200x1000 BLUE pallet'),
('209602', 'Standard 1200x1000 RED pallet'),
('250997', 'Standard 1000x800 plastic pallet');
GO

-- Defines the logical sections of the warehouse for putaway strategies.
INSERT INTO locations.location_sections (section_name, section_description, created_by) VALUES
('Floor', 'Bottom level, typically for bulk or heavy items', 'Seeder'),
('Middle', 'Middle racking levels', 'Seeder'),
('Top', 'Top racking levels', 'Seeder');
GO

-- Defines the physical types of locations available.
INSERT INTO locations.location_types (type_name, type_description, AllowConcurrentPutaway, created_by) VALUES
('Staging bay', 'Shipping or receiving bay', 0, 'Seeder'),
('Bay', 'Temp or cross-dock bays', 0, 'Seeder'),
('Racking', 'Standard racking unit for single pallet storage', 0, 'Seeder'),
('Bulk', 'Open floor area for multiple pallet storage', 1, 'Seeder');
GO

-- Creates the physical warehouse locations.
INSERT INTO locations.locations (location_name, type_id, section_id, capacity, is_active, created_by) VALUES
('BAY01', 1, 1, 999, 1, 'wms_user'), ('BAY02', 1, 1, 999, 1, 'wms_user'),
('BAY03', 1, 1, 999, 1, 'wms_user'), ('BAY04', 1, 1, 999, 1, 'wms_user'),
('A1-01-01', 3, 1, 1, 1, 'wms_user'), ('A1-01-02', 3, 2, 1, 0, 'wms_user'),
('A1-01-03', 3, 2, 1, 1, 'wms_user'), ('A1-01-04', 3, 3, 1, 1, 'wms_user'),
('A1-02-01', 3, 1, 1, 1, 'wms_user'), ('A1-02-02', 3, 2, 1, 1, 'wms_user'),
('A1-02-03', 3, 2, 1, 1, 'wms_user'), ('A1-02-04', 3, 3, 1, 1, 'wms_user'),
('A1-03-01', 3, 1, 1, 1, 'wms_user'), ('A1-03-02', 3, 2, 1, 1, 'wms_user'),
('A1-03-03', 3, 2, 1, 1, 'wms_user'), ('A1-03-04', 3, 3, 1, 1, 'wms_user'),
('A1-04-01', 3, 1, 1, 1, 'wms_user'), ('A1-04-02', 3, 2, 1, 0, 'wms_user'),
('A1-04-03', 3, 2, 1, 1, 'wms_user'), ('A1-04-04', 3, 3, 1, 1, 'wms_user'),
('B1-01-01', 3, 1, 1, 1, 'wms_user'), ('B1-01-02', 3, 2, 1, 1, 'wms_user'),
('B1-01-03', 3, 3, 1, 1, 'wms_user'), ('B1-02-01', 3, 1, 1, 1, 'wms_user'),
('B1-02-02', 3, 2, 1, 1, 'wms_user'), ('B1-02-03', 3, 3, 1, 1, 'wms_user'),
('B1-03-01', 3, 1, 1, 1, 'wms_user'), ('B1-03-02', 3, 2, 1, 1, 'wms_user'),
('B1-03-03', 3, 3, 1, 1, 'wms_user'), ('B1-04-01', 3, 1, 1, 1, 'wms_user'),
('B1-04-02', 3, 2, 1, 1, 'wms_user'), ('B1-04-03', 3, 3, 1, 1, 'wms_user'),
('C1-01-03', 3, 3, 1, 1, 'wms_user'), ('C1-02-01', 3, 1, 1, 1, 'wms_user'),
('C1-02-02', 3, 2, 1, 1, 'wms_user'), ('C1-02-03', 3, 3, 1, 1, 'wms_user'),
('C1-03-01', 3, 1, 1, 1, 'wms_user'), ('C1-03-02', 3, 2, 1, 1, 'wms_user'),
('C1-03-03', 3, 3, 1, 1, 'wms_user'), ('D1-01-03', 3, 3, 1, 1, 'wms_user'),
('D1-02-01', 3, 1, 1, 1, 'wms_user'), ('D1-02-02', 3, 2, 1, 1, 'wms_user'),
('D1-02-03', 3, 3, 1, 1, 'wms_user'), ('B1-BULK', 4, 1, 9999, 1, 'wms_user'),
('B2-BULK', 4, 1, 9999, 1, 'wms_user'), ('X1-BULK', 4, 1, 9999, 1, 'wms_user');
GO

-- Add notes to any inactive locations for clarity.
UPDATE locations.locations SET notes = 'Location blocked for maintenance' WHERE is_active = 0;
GO

-- Assign aisles to all racking locations for the suggestion logic.
UPDATE locations.locations SET aisle = '1' WHERE location_name LIKE 'A%' AND type_id = 3;
UPDATE locations.locations SET aisle = '2' WHERE location_name LIKE 'B%' AND type_id = 3;
UPDATE locations.locations SET aisle = '2' WHERE location_name LIKE 'C%' AND type_id = 3; -- C locations also in Aisle 2, for density
UPDATE locations.locations SET aisle = '3' WHERE location_name LIKE 'D%' AND type_id = 3;
GO

-- Creates the master data for the products (SKUs).
INSERT INTO inventory.sku (sku_name, ean, sku_desc, uom_code, weight_per_unit, packing_material_id, full_unit_qty, preferred_storage_type, preferred_section_id, is_active, created_by)
VALUES
('251129', '05010102322516', 'FIRST TEST PRODUCT', 'PAL', 6, 1, 180, 3, 3, 1, 'admin'),
('251269', '04060800310286', 'SECOND TEST PRODUCT', 'PAL', 7, 1, 160, 3, 3, 1, 'admin'),
('206937', '05010102194816', 'ANOTHER TEST PRODUCT', 'PAL', 10, 1, 75, 3, 3, 1, 'admin');
GO

-- Defines the valid statuses for inventory items.
INSERT INTO inventory.stock_status (status_name, status_desc) 
VALUES
('AV', 'AVAILABLE'), ('BL', 'BLOCKED'), ('MV', 'IN TRANSIT'), ('AL', 'ALLOCATED'),
('EX', 'IN EXECUTION'), ('OU', 'OUT OF WAREHOUSE'), ('QQ', 'QUARANTEENED');
GO

-- Creates the initial inventory records (pallets) in the warehouse, starting in the receiving bays.
-- NOTE: For a smaller test data set, you could reduce the number of pallets inserted here.
INSERT INTO inventory.inventory (external_id, sku_name, batch_number, best_before_date, actual_qty, expected_qty, document_ref, status, location_id, weight_per_unit, created_by)
VALUES
('250101027278429125', 2, '001395842A', '2025-03-31', 160, 160, 'TEST_INB01', 'AV', 1, 8, 'system'),
('350101028006975225', 1, '001409254A', '2025-06-30', 101, 101, 'TEST_INB01', 'AV', 1, 8, 'system'),
('350101028007388437', 3, '001413958A', '2025-12-31', 60, 60, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148160663', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148160656', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148160649', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148160657', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148160650', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148162656', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148560649', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148111657', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('150101027148112650', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'AV', 1, 8, 'system'),
('250101027278429126', 2, '001395842A', '2025-03-31', 160, 160, 'TEST_INB01', 'AV', 2, 8, 'system'),
('350101028006975226', 1, '001409254A', '2025-06-30', 101, 101, 'TEST_INB01', 'AV', 2, 8, 'system'),
('350101028007388438', 3, '001413958A', '2025-12-31', 60, 60, 'TEST_INB01', 'BL', 2, 8, 'system'),
('150101027148160664', 3, '001413958A', '2025-12-31', 75, 75, 'TEST_INB01', 'BL', 2, 8, 'system');
GO

-- --- Sample Inbound Delivery ---

-- 1. Create Inbound Delivery #1 (1 Pallet)
INSERT INTO deliveries.inbound_header (document_ref, supplier_id, expected_arrival_date, status, is_activated_for_receiving, total_expected_pallets, total_expected_quantity, created_by)
VALUES ('ASN-TEST-01', 1, GETDATE(), 'EXP', 0, 1, 180, 'seeder');
GO

DECLARE @InboundId1 INT;
SELECT @InboundId1 = inbound_id FROM deliveries.inbound_header WHERE document_ref = 'ASN-TEST-01';
IF @InboundId1 IS NOT NULL
BEGIN
    INSERT INTO deliveries.inbound_rows (inbound_id, sku_id, expected_qty, external_id, batch_number, best_before_date, created_by)
    VALUES (@InboundId1, 1, 180, '350101028006975227', 'BATCH-001', '2026-06-01', 'seeder');
END;
GO

-- 2. Create Inbound Delivery #2 (2 Pallets)
INSERT INTO deliveries.inbound_header (document_ref, supplier_id, expected_arrival_date, status, is_activated_for_receiving, total_expected_pallets, total_expected_quantity, created_by)
VALUES ('ASN-TEST-02', 1, GETDATE(), 'EXP', 0, 2, 235, 'seeder');
GO

DECLARE @InboundId2 INT;
SELECT @InboundId2 = inbound_id FROM deliveries.inbound_header WHERE document_ref = 'ASN-TEST-02';
IF @InboundId2 IS NOT NULL
BEGIN
    INSERT INTO deliveries.inbound_rows (inbound_id, sku_id, expected_qty, external_id, batch_number, best_before_date, created_by)
    VALUES 
    (@InboundId2, 2, 160, '250101027278429127', 'BATCH-002', '2026-08-15', 'seeder'),
    (@InboundId2, 3, 75,  '150101027148160665', 'BATCH-003', '2027-01-01', 'seeder');
END;
GO

-- 3. Create Inbound Delivery #3 (3 Pallets)
INSERT INTO deliveries.inbound_header (document_ref, supplier_id, expected_arrival_date, status, is_activated_for_receiving, total_expected_pallets, total_expected_quantity, created_by)
VALUES ('ASN-TEST-03', 1, GETDATE(), 'EXP', 0, 3, 415, 'seeder');
GO

DECLARE @InboundId3 INT;
SELECT @InboundId3 = inbound_id FROM deliveries.inbound_header WHERE document_ref = 'ASN-TEST-03';
IF @InboundId3 IS NOT NULL
BEGIN
    INSERT INTO deliveries.inbound_rows (inbound_id, sku_id, expected_qty, external_id, batch_number, best_before_date, created_by)
    VALUES 
    (@InboundId3, 1, 180, '350101028006975228', 'BATCH-004', '2026-06-01', 'seeder'),
    (@InboundId3, 2, 160, '250101027278429128', 'BATCH-005', '2026-08-15', 'seeder'),
    (@InboundId3, 3, 75,  '150101027148160666', 'BATCH-006', '2027-01-01', 'seeder');
END;
GO

PRINT '✅ Sample inbound delivery created successfully.';
GO

