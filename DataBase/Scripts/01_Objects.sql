-- =================================================================================
-- PeasyWare WMS - Database Object Creation Script
-- File: 01_Objects.sql
-- Description: This script handles the complete setup of the WMS_DB database.
-- It performs a clean drop/create, sets up the necessary user and schemas,
-- and defines all tables, triggers, and primary indexes.
-- =================================================================================

-- Switch to the master database to manage the WMS_DB
USE master;
GO

-- --- Database Drop and Create ---
-- Ensures a clean slate for development and testing by dropping the existing database.
-- The SINGLE_USER mode with ROLLBACK IMMEDIATE forcibly disconnects any active users.
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'WMS_DB')
BEGIN
    ALTER DATABASE WMS_DB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE WMS_DB;
END;
GO

CREATE DATABASE WMS_DB;
GO

-- --- Security Setup: Login and User ---
-- Creates the dedicated SQL login for the application if it doesn't already exist.
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'wms_user')
BEGIN
    CREATE LOGIN wms_user WITH PASSWORD = 'wms_User123?', CHECK_POLICY = OFF;
END;
GO

-- Switch context to the newly created WMS database
USE WMS_DB;
GO

-- Create a database user from the server login and grant it full ownership rights.
-- This simplifies permissions management for this project.
CREATE USER wms_user FOR LOGIN wms_user;
ALTER ROLE db_owner ADD MEMBER wms_user;
GO

-- --- Schema Definitions ---
-- Creates logical containers (schemas) to organize database objects by function.
EXEC('CREATE SCHEMA auth');         -- For user authentication and authorization tables.
EXEC('CREATE SCHEMA customers');    -- For customer-related data.
EXEC('CREATE SCHEMA locations');    -- For all physical and logical warehouse location data.
EXEC('CREATE SCHEMA suppliers');    -- For supplier-related data.
EXEC('CREATE SCHEMA inventory');    -- For all stock-related data, including SKUs and inventory records.
EXEC('CREATE SCHEMA orders');       -- For customer sales orders.
EXEC('CREATE SCHEMA deliveries');   -- For inbound (ASN) and outbound delivery documents.
EXEC('CREATE SCHEMA logs');         -- For audit trails, error logs, and historical data.
EXEC('CREATE SCHEMA operations');   -- For application settings and system-level operational data.
EXEC('CREATE SCHEMA views');        -- For all database views providing simplified data access.
GO

-- --- DDL Auditing ---
-- This section sets up a trigger to automatically log any changes made to the database schema (DDL events).
-- This is a powerful tool for tracking unauthorized or accidental changes.

-- Table to store the DDL change logs.
CREATE TABLE logs.ddl_changes (
    id INT IDENTITY(1,1) PRIMARY KEY,
    event_time DATETIME2 DEFAULT SYSDATETIME(),
    event_type NVARCHAR(100),
    object_name NVARCHAR(255),
    schema_name NVARCHAR(255),
    event_details NVARCHAR(MAX),
    triggered_by NVARCHAR(255) DEFAULT ORIGINAL_LOGIN()
);
GO

-- Database-level trigger that fires on any DDL event (CREATE, ALTER, DROP).
CREATE TRIGGER trg_log_ddl_changes
ON DATABASE
FOR DDL_DATABASE_LEVEL_EVENTS
AS
BEGIN
    SET NOCOUNT ON;
    -- Captures event data using the EVENTDATA() function and inserts it into the log table.
    INSERT INTO logs.ddl_changes (event_type, object_name, schema_name, event_details)
    SELECT
        EVENTDATA().value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(100)'),
        EVENTDATA().value('(/EVENT_INSTANCE/ObjectName)[1]', 'NVARCHAR(255)'),
        EVENTDATA().value('(/EVENT_INSTANCE/SchemaName)[1]', 'NVARCHAR(255)'),
        EVENTDATA().value('(/EVENT_INSTANCE/TSQLCommand)[1]', 'NVARCHAR(MAX)');
END;
GO

-- =================================================================================
-- TABLE CREATION
-- =================================================================================

-- --- AUTH Schema Tables ---

-- Stores user accounts, credentials, and personal information.
CREATE TABLE auth.users (
    user_id        INT IDENTITY(1,1) PRIMARY KEY,
    username       NVARCHAR(50) UNIQUE NOT NULL,
    password_hash  VARBINARY(64) NOT NULL, -- Hashed password for security.
    salt           VARBINARY(16) NOT NULL, -- Unique salt per user for robust hashing.
    full_name      NVARCHAR(100) NOT NULL,
    email          NVARCHAR(100) UNIQUE NOT NULL,
    is_active      BIT DEFAULT 1,
    created_at     DATETIME DEFAULT GETDATE(),
    updated_at     DATETIME DEFAULT GETDATE()
);
GO

-- Defines the roles available in the system (e.g., Admin, Operator).
CREATE TABLE auth.roles (
    role_id      INT IDENTITY(1,1) PRIMARY KEY,
    role_name    NVARCHAR(50) UNIQUE NOT NULL,
    role_desc    NVARCHAR(255),
    is_active    BIT DEFAULT 1
);
GO

-- Junction table to link users to roles, enabling a many-to-many relationship.
CREATE TABLE auth.user_roles (
    user_id      INT NOT NULL,
    role_id      INT NOT NULL,
    assigned_at  DATETIME DEFAULT GETDATE(),
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES auth.users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES auth.roles(role_id) ON DELETE CASCADE
);
GO

-- Logs every login attempt, successful or not, for security auditing.
CREATE TABLE logs.auth_login_attempts (
    attempt_id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NULL, -- Null for failed attempts where the user ID is unknown.
    username NVARCHAR(100) NOT NULL,
    ip_address NVARCHAR(45) NULL,
    user_agent NVARCHAR(500) NULL,
    login_time DATETIME DEFAULT GETDATE(),
    host_name NVARCHAR(255) NULL,
    success BIT NOT NULL
);
GO

-- --- LOCATIONS Schema Tables ---

-- Defines the different types of storage locations in the warehouse.
CREATE TABLE locations.location_types (
    type_id INT IDENTITY(1,1) PRIMARY KEY,
    type_name NVARCHAR(100) NOT NULL UNIQUE, -- e.g., 'Racking', 'Bulk', 'Staging Bay'
    type_description NVARCHAR(255) NULL,
    -- A critical business rule: determines if multiple putaways can target this type of location simultaneously.
    AllowConcurrentPutaway BIT NOT NULL DEFAULT 0,
    created_at DATETIME DEFAULT GETDATE(),
    created_by NVARCHAR(100) NULL
);
GO

-- Defines logical sections of the warehouse, used for putaway strategies.
CREATE TABLE locations.location_sections (
    section_id INT IDENTITY(1,1) PRIMARY KEY,
    section_name NVARCHAR(100) NOT NULL UNIQUE, -- e.g., 'Floor', 'Top-Level', 'Food Grade'
    section_description NVARCHAR(255) NULL,
    created_at DATETIME DEFAULT GETDATE(),
    created_by NVARCHAR(100) NULL
);
GO

-- The master table for all physical warehouse locations (bins).
CREATE TABLE locations.locations (
    location_id INT IDENTITY(1,1) PRIMARY KEY,
    location_name NVARCHAR(100) NOT NULL UNIQUE,
    type_id INT NOT NULL, -- FK to location_types
    section_id INT NOT NULL, -- FK to location_sections
    aisle NVARCHAR(20) NULL, -- For racking locations, to aid navigation and workload balancing.
    side NVARCHAR(10) NULL, -- For future use (e.g., A/B side of an aisle).
    capacity INT DEFAULT 1 CHECK (capacity >= 1), -- Racking is typically 1; Bulk can be > 1.
    is_active BIT DEFAULT 1, -- Used to block locations for maintenance or safety.
    notes NVARCHAR(255) NULL,
    created_at DATETIME DEFAULT GETDATE(),
    created_by NVARCHAR(100) NULL,
    updated_at DATETIME DEFAULT GETDATE(),
    updated_by NVARCHAR(100) NULL,
    FOREIGN KEY (type_id) REFERENCES locations.location_types(type_id),
    FOREIGN KEY (section_id) REFERENCES locations.location_sections(section_id)
);
GO

-- Stores temporary reservations on locations for in-progress tasks like putaway.
-- This prevents two operators from being sent to the same empty bin.
CREATE TABLE locations.location_reservations (
    reservation_id INT IDENTITY(1,1) PRIMARY KEY, 
    location_id INT NOT NULL FOREIGN KEY REFERENCES locations.locations(location_id),
    reserved_by_pallet_id NVARCHAR(255) NULL, -- The pallet that this reservation is for.
    reserved_by INT NOT NULL, -- The user who initiated the task.
    reservation_type NVARCHAR(50) NOT NULL DEFAULT 'PUTAWAY',
    reserved_at DATETIME2 NOT NULL, -- Using DATETIME2 for higher precision.
    expires_at DATETIME2 NOT NULL, -- Reservations are temporary and will be cleared by a maintenance job if they expire.
    aisle NVARCHAR(20) NULL -- Denormalized for performance in the aisle-balancing logic.
);
GO

-- Audit table to log all changes to the locations master table.
CREATE TABLE logs.locations_backup (
    backup_id INT IDENTITY(1,1) PRIMARY KEY,
    action_type NVARCHAR(10) NOT NULL,
    action_time DATETIME DEFAULT GETDATE(),
    action_by NVARCHAR(100) NULL,
    location_id INT,
    location_name NVARCHAR(100),
    type_id INT,
    section_id INT,
    aisle NVARCHAR(20),
    side NVARCHAR(10),
    capacity INT,
    is_active BIT,
    notes NVARCHAR(255),
    created_at DATETIME,
    created_by NVARCHAR(100),
    updated_at DATETIME,
    updated_by NVARCHAR(100)
);
GO

-- Trigger that captures INSERT, UPDATE, and DELETE operations on the locations table.
CREATE OR ALTER TRIGGER locations.trg_locations_audit
ON locations.locations
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- This trigger uses a series of IF statements to log the state of the data
    -- for each type of DML action.

    -- Captures INSERTs by logging the content of the 'inserted' pseudo-table.
    IF EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
    BEGIN
        INSERT INTO logs.locations_backup (action_type, action_by, location_id, location_name, type_id, section_id, aisle, side, capacity, is_active, notes, created_at, created_by, updated_at, updated_by)
        SELECT 'INSERT', ORIGINAL_LOGIN(), i.location_id, i.location_name, i.type_id, i.section_id, i.aisle, i.side, i.capacity, i.is_active, i.notes, i.created_at, i.created_by, i.updated_at, i.updated_by
        FROM inserted i;
    END

    -- Captures DELETEs by logging the content of the 'deleted' pseudo-table.
    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
    BEGIN
        INSERT INTO logs.locations_backup (action_type, action_by, location_id, location_name, type_id, section_id, aisle, side, capacity, is_active, notes, created_at, created_by, updated_at, updated_by)
        SELECT 'DELETE', ORIGINAL_LOGIN(), d.location_id, d.location_name, d.type_id, d.section_id, d.aisle, d.side, d.capacity, d.is_active, d.notes, d.created_at, d.created_by, d.updated_at, d.updated_by
        FROM deleted d;
    END

    -- Captures UPDATEs by logging the state of the row *before* the update (from the 'deleted' pseudo-table).
    IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
    BEGIN
        INSERT INTO logs.locations_backup (action_type, action_by, location_id, location_name, type_id, section_id, aisle, side, capacity, is_active, notes, created_at, created_by, updated_at, updated_by)
        SELECT 'UPDATE', ORIGINAL_LOGIN(), d.location_id, d.location_name, d.type_id, d.section_id, d.aisle, d.side, d.capacity, d.is_active, d.notes, d.created_at, d.created_by, d.updated_at, d.updated_by
        FROM deleted d;
    END
END;
GO

-- --- INVENTORY Schema Tables ---

-- Defines valid units of measure (e.g., 'EA' for Each, 'CS' for Case).
CREATE TABLE inventory.unit_of_measures (
    uom_code CHAR(4) PRIMARY KEY,
    description NVARCHAR(100) NOT NULL
);
GO

-- Defines types of packing materials (e.g., 'Standard Pallet', 'Cardboard Box').
CREATE TABLE inventory.packing_materials (
    material_id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(255)
);
GO

-- Master data table for all products (SKUs) handled by the warehouse.
CREATE TABLE inventory.sku (
    id INT IDENTITY(1,1) PRIMARY KEY, -- Internal, immutable primary key.
    sku_name NVARCHAR(20) NOT NULL UNIQUE, -- Customer-facing product code.
    ean NVARCHAR(20) NULL UNIQUE, -- Barcode for easy scanning.
    sku_desc NVARCHAR(100) NOT NULL,
    uom_code CHAR(4) NOT NULL FOREIGN KEY REFERENCES inventory.unit_of_measures(uom_code),
    weight_per_unit DECIMAL(10, 3) NOT NULL,
    packing_material_id INT NOT NULL FOREIGN KEY REFERENCES inventory.packing_materials(material_id),
    full_unit_qty INT NOT NULL, -- Standard quantity for a full pallet/case of this SKU.
    preferred_storage_type INT DEFAULT 2 FOREIGN KEY REFERENCES locations.location_types(type_id),
    preferred_section_id INT NULL FOREIGN KEY REFERENCES locations.location_sections(section_id),
    is_active BIT NOT NULL DEFAULT 1,
    created_by NVARCHAR(50),
    created_at DATETIME DEFAULT GETDATE(),
    updated_by NVARCHAR(50),
    updated_at DATETIME
);
GO

-- Defines the possible statuses for a stock item.
CREATE TABLE inventory.stock_status (
    status_name CHAR(2) PRIMARY KEY,
    status_desc NVARCHAR(100)
);
GO

-- The main transactional table representing every physical pallet/unit of stock in the warehouse.
CREATE TABLE inventory.inventory (
    inventory_id INT IDENTITY(1,1) PRIMARY KEY,
    external_id NVARCHAR(100) UNIQUE, -- The unique license plate (e.g., SSCC) for this pallet.
    sku_name INT NOT NULL FOREIGN KEY REFERENCES inventory.sku(id),
    batch_number NVARCHAR(100),
    best_before_date DATE,
    actual_qty INT NOT NULL DEFAULT 0, -- The physical quantity currently on the pallet.
    expected_qty INT NOT NULL DEFAULT 0, -- The quantity expected upon receipt.
    allocated_qty INT NOT NULL DEFAULT 0, -- The quantity reserved for an outbound order.
    document_ref NVARCHAR(100), -- e.g., ASN number on arrival, cleared after first putaway.
    status CHAR(2) FOREIGN KEY REFERENCES inventory.stock_status(status_name),
    original_status CHAR(2) NULL, -- Stores the status a pallet was in before a move started.
    location_id INT NULL FOREIGN KEY REFERENCES locations.locations(location_id),
    weight_per_unit DECIMAL(10,3) NOT NULL,
    weight AS (actual_qty * weight_per_unit) PERSISTED, -- A computed column for total weight.
    movement_started_at DATETIME2 NULL, -- Timestamp for when a move task begins.
    created_at DATETIME NOT NULL DEFAULT GETDATE(),
    created_by NVARCHAR(100) NOT NULL,
    updated_at DATETIME NULL,
    updated_by NVARCHAR(100) NULL
);
GO

-- Defines the different types of inventory movements for logging purposes.
CREATE TABLE inventory.movement_types (
    type_code VARCHAR(30) PRIMARY KEY,
    description NVARCHAR(255) NOT NULL
);
GO

-- A complete log of every stock movement that occurs in the warehouse.
-- Corrected the misplaced FOREIGN KEY constraint.
CREATE TABLE inventory.inventory_movements (
    movement_id INT IDENTITY(1,1) PRIMARY KEY,
    inventory_id INT NOT NULL FOREIGN KEY REFERENCES inventory.inventory(inventory_id),
    sku_id INT NOT NULL FOREIGN KEY REFERENCES inventory.sku(id),
    from_location_id INT NULL FOREIGN KEY REFERENCES locations.locations(location_id),
    to_location_id INT NULL FOREIGN KEY REFERENCES locations.locations(location_id),
    from_status CHAR(2),
    to_status CHAR(2),
    from_qty INT,
    to_qty INT,
    moved_qty INT NOT NULL,
    moved_by INT FOREIGN KEY REFERENCES auth.users(user_id),
    moved_at DATETIME NOT NULL DEFAULT GETDATE(),
    note NVARCHAR(255),
    movement_type VARCHAR(30) DEFAULT 'PUTAWAY' FOREIGN KEY REFERENCES inventory.movement_types(type_code),
    moved_date AS CAST(moved_at AS DATE) PERSISTED
);
GO

-- Helper function to allow indexing on nullable location ID columns.
-- SQL Server cannot create a unique index on a key that includes nullable columns directly.
-- This function converts a NULL ID to 0, allowing it to be indexed.
CREATE FUNCTION inventory.GetLocationIdForIndex(@location_id INT)
RETURNS INT
WITH SCHEMABINDING
AS
BEGIN
    RETURN ISNULL(@location_id, 0);
END;
GO    

-- Add computed columns to the movements table that use the helper function.
ALTER TABLE inventory.inventory_movements
ADD from_location_idx AS inventory.GetLocationIdForIndex(from_location_id) PERSISTED;
GO

ALTER TABLE inventory.inventory_movements
ADD to_location_idx AS inventory.GetLocationIdForIndex(to_location_id) PERSISTED;
GO

-- A unique index to prevent duplicate movement log entries.
-- It ensures that the same pallet cannot be logged as moving from and to the same place,
-- with the same quantity and type, at the exact same millisecond.
CREATE UNIQUE INDEX UQ_InventoryMovement ON inventory.inventory_movements (
    inventory_id,
    from_location_idx,
    to_location_idx,
    moved_qty,
    moved_at,
    movement_type
);
GO

-- Audit table to log specific field changes on the inventory table.
CREATE TABLE logs.inventory_changes (
    inventory_id INT NOT NULL,
    changed_at DATETIME2 NOT NULL,
    changed_by NVARCHAR(100) NOT NULL,
    change_type NVARCHAR(50) NOT NULL, -- e.g., 'UPDATE', 'INSERT', 'DELETE'
    field_name NVARCHAR(100) NOT NULL,
    old_value NVARCHAR(MAX) NULL,
    new_value NVARCHAR(MAX) NULL
);
GO

-- Trigger to capture changes to key fields on the inventory table for detailed auditing.
CREATE OR ALTER TRIGGER inventory.tr_inventory_audit
ON inventory.inventory
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @now DATETIME2 = SYSDATETIME();
    DECLARE @user NVARCHAR(100) = SYSTEM_USER;

    -- This trigger uses a series of UNION ALL statements to check if specific columns have changed
    -- and logs only the columns that were actually updated in a given transaction.
    INSERT INTO logs.inventory_changes (inventory_id, changed_at, changed_by, change_type, field_name, old_value, new_value)
    SELECT i.inventory_id, @now, @user, 'UPDATE', 'status', CAST(d.status AS NVARCHAR), CAST(i.status AS NVARCHAR)
    FROM inserted i JOIN deleted d ON i.inventory_id = d.inventory_id WHERE ISNULL(i.status, '') <> ISNULL(d.status, '')
    UNION ALL
    SELECT i.inventory_id, @now, @user, 'UPDATE', 'location_id', CAST(d.location_id AS NVARCHAR), CAST(i.location_id AS NVARCHAR)
    FROM inserted i JOIN deleted d ON i.inventory_id = d.inventory_id WHERE ISNULL(i.location_id, -1) <> ISNULL(d.location_id, -1)
    UNION ALL
    SELECT i.inventory_id, @now, @user, 'UPDATE', 'actual_qty', CAST(d.actual_qty AS NVARCHAR), CAST(i.actual_qty AS NVARCHAR)
    FROM inserted i JOIN deleted d ON i.inventory_id = d.inventory_id WHERE ISNULL(i.actual_qty, -1) <> ISNULL(d.actual_qty, -1)
    UNION ALL
    SELECT i.inventory_id, @now, @user, 'UPDATE', 'allocated_qty', CAST(d.allocated_qty AS NVARCHAR), CAST(i.allocated_qty AS NVARCHAR)
    FROM inserted i JOIN deleted d ON i.inventory_id = d.inventory_id WHERE ISNULL(i.allocated_qty, -1) <> ISNULL(d.allocated_qty, -1)
    UNION ALL
    SELECT i.inventory_id, @now, @user, 'UPDATE', 'movement_started_at', CAST(d.movement_started_at AS NVARCHAR), CAST(i.movement_started_at AS NVARCHAR)
    FROM inserted i JOIN deleted d ON i.inventory_id = d.inventory_id WHERE ISNULL(CONVERT(NVARCHAR, i.movement_started_at), '') <> ISNULL(CONVERT(NVARCHAR, d.movement_started_at), '');
END;
GO

-- Clustered index for efficient querying of the inventory change logs.
CREATE CLUSTERED INDEX IX_InventoryChanges_InventoryId_ChangedAt
ON logs.inventory_changes (inventory_id, changed_at DESC);
GO

-- --- OPERATIONS Schema Tables ---

-- A centralized table to store application-wide settings and operational parameters.
-- This allows for changing system behavior without redeploying the application.
CREATE TABLE operations.Settings (
    SettingName NVARCHAR(100) PRIMARY KEY,
    SettingValue NVARCHAR(MAX) NOT NULL,
    DataType NVARCHAR(50) NOT NULL, -- e.g., 'bool', 'int', 'string' for parsing in the app.
    Description NVARCHAR(255) NULL,
    IsSensitive BIT NOT NULL DEFAULT 0, -- For future use, to flag settings that should be encrypted or hidden.
    CreatedBy NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UpdatedBy NVARCHAR(100) NULL,
    UpdatedAt DATETIME2 NULL
);
GO

-- Audit table to log all changes to the settings table.
CREATE TABLE logs.Settings_backup (
    AuditId INT IDENTITY(1,1) PRIMARY KEY,
    ActionType NVARCHAR(10) NOT NULL,
    ActionTime DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ActionBy NVARCHAR(100) NULL,
    SettingName NVARCHAR(100),
    OldSettingValue NVARCHAR(MAX) NULL,
    NewSettingValue NVARCHAR(MAX) NULL,
    OldDataType NVARCHAR(50) NULL,
    NewDataType NVARCHAR(50) NULL
);
GO

-- Trigger that captures all changes to the settings table.
CREATE OR ALTER TRIGGER operations.trg_Settings_backup
ON operations.Settings
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActionBy NVARCHAR(100) = ORIGINAL_LOGIN();

    -- Logic to handle INSERT, UPDATE, and DELETE actions.
    IF EXISTS(SELECT * FROM inserted) AND NOT EXISTS(SELECT * FROM deleted) -- INSERT
    BEGIN
        INSERT INTO logs.Settings_backup (ActionType, ActionBy, SettingName, NewSettingValue, NewDataType)
        SELECT 'INSERT', @ActionBy, i.SettingName, i.SettingValue, i.DataType
        FROM inserted i;
    END
    ELSE IF EXISTS(SELECT * FROM deleted) AND NOT EXISTS(SELECT * FROM inserted) -- DELETE
    BEGIN
        INSERT INTO logs.Settings_backup (ActionType, ActionBy, SettingName, OldSettingValue, OldDataType)
        SELECT 'DELETE', @ActionBy, d.SettingName, d.SettingValue, d.DataType
        FROM deleted d;
    END
    ELSE IF EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted) -- UPDATE
    BEGIN
        -- Only log if a value actually changed.
        IF EXISTS (SELECT * FROM inserted i JOIN deleted d ON i.SettingName = d.SettingName
                   WHERE i.SettingValue <> d.SettingValue OR i.DataType <> d.DataType
                      OR i.Description <> d.Description OR i.IsSensitive <> d.IsSensitive
                      OR i.IsSensitive IS NULL AND d.IsSensitive IS NOT NULL
                      OR i.IsSensitive IS NOT NULL AND d.IsSensitive IS NULL
                   )
        BEGIN
            INSERT INTO logs.Settings_backup (ActionType, ActionBy, SettingName, OldSettingValue, NewSettingValue, OldDataType, NewDataType)
            SELECT 'UPDATE', @ActionBy, i.SettingName, d.SettingValue, i.SettingValue, d.DataType, i.DataType
            FROM inserted i JOIN deleted d ON i.SettingName = d.SettingName;
        END
    END
END;
GO

-- =================================================================================
-- PERFORMANCE INDEXES
-- =================================================================================
-- Additional non-clustered indexes to improve query performance for common lookups.

CREATE INDEX IX_Inventory_ExternalId ON inventory.inventory(external_id);
GO
CREATE INDEX IX_Locations_Available ON locations.locations(is_active)
INCLUDE (location_id, type_id, section_id);
GO
CREATE NONCLUSTERED INDEX IX_Locations_Type_Section_Capacity
ON locations.locations(type_id, section_id, capacity)
INCLUDE (location_id, location_name, is_active);
GO
CREATE NONCLUSTERED INDEX IX_Locations_Name ON locations.locations(location_name)
INCLUDE (location_id, is_active, capacity);
GO
CREATE NONCLUSTERED INDEX IX_Locations_Type_Section ON locations.locations(type_id, section_id)
INCLUDE (location_id, location_name, is_active, capacity);
GO
CREATE NONCLUSTERED INDEX IX_LocationReservations_Expiry ON locations.location_reservations(expires_at, location_id);
GO
