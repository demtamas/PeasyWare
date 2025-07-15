-- =================================================================================
-- PeasyWare WMS - Stored Procedures Script
-- File: 02_Procedures.sql
-- Description: This script creates and configures all stored procedures and
-- scheduled jobs required for the WMS application to function.
-- =================================================================================

USE WMS_DB;
GO

-- =================================================================================
-- AUTH SCHEMA PROCEDURES
-- =================================================================================

-- Renamed from AddNewUser to follow a standard usp_VerbNoun convention.
-- This procedure handles the complete, transactional creation of a new user.
CREATE OR ALTER PROCEDURE auth.usp_CreateUser
    @username NVARCHAR(50),
    @password NVARCHAR(100),
    @full_name NVARCHAR(100),
    @email NVARCHAR(100),
    @role_name NVARCHAR(50),
    @is_active BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @user_id INT;
    DECLARE @salt VARBINARY(16);
    DECLARE @password_hash VARBINARY(64);
    DECLARE @role_id INT;

    -- Ensure the specified role exists before proceeding.
    SELECT @role_id = role_id FROM auth.roles WHERE role_name = @role_name;
    IF @role_id IS NULL
    BEGIN
        THROW 50000, 'Invalid role name provided.', 1;
    END;

    -- Generate a cryptographically random salt and hash the password.
    SET @salt = CRYPT_GEN_RANDOM(16);
    SET @password_hash = HASHBYTES('SHA2_512', @password + CONVERT(NVARCHAR(MAX), @salt, 1));

    -- Use a transaction to ensure that the user and their role assignment are created together.
    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO auth.users (username, password_hash, salt, full_name, email, is_active)
        VALUES (@username, @password_hash, @salt, @full_name, @email, @is_active);

        SET @user_id = SCOPE_IDENTITY();

        INSERT INTO auth.user_roles (user_id, role_id)
        VALUES (@user_id, @role_id);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Renamed from AddRole. Creates a new user role.
CREATE OR ALTER PROCEDURE auth.usp_CreateRole
    @role_name NVARCHAR(100),
    @role_desc NVARCHAR(255)
AS
BEGIN
    -- Prevent duplicate role names.
    IF EXISTS (SELECT 1 FROM auth.roles WHERE role_name = @role_name)
    BEGIN
        RAISERROR('Role already exists.', 16, 1);
        RETURN;
    END;

    INSERT INTO auth.roles (role_name, role_desc, is_active)
    VALUES (@role_name, @role_desc, 1);
END;
GO

-- Renamed from UpdateRole. Updates the details of an existing role.
CREATE OR ALTER PROCEDURE auth.usp_UpdateRole
    @role_id INT,
    @role_name NVARCHAR(100),
    @role_desc NVARCHAR(255)
AS
BEGIN
    UPDATE auth.roles
    SET role_name = @role_name,
        role_desc = @role_desc
    WHERE role_id = @role_id;
END;
GO

-- Renamed from DeactivateRole. Deactivates a role, preventing it from being assigned.
CREATE OR ALTER PROCEDURE auth.usp_DeactivateRole
    @role_id INT
AS
BEGIN
    -- Safety check: prevent deactivation if the role is still assigned to any users.
    IF EXISTS (SELECT 1 FROM auth.user_roles WHERE role_id = @role_id)
    BEGIN
        RAISERROR('Role is assigned to users. Cannot deactivate.', 16, 1);
        RETURN;
    END;

    UPDATE auth.roles
    SET is_active = 0
    WHERE role_id = @role_id;
END;
GO

-- Renamed from UpdateUser. Updates an existing user's details, including their role and optionally their password.
CREATE OR ALTER PROCEDURE auth.usp_UpdateUser
    @user_id INT,
    @username NVARCHAR(50),
    @full_name NVARCHAR(100),
    @email NVARCHAR(100),
    @role_name NVARCHAR(50),
    @is_active BIT,
    @password NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Update the main user details.
    UPDATE auth.users
    SET username = @username,
        full_name = @full_name,
        email = @email,
        is_active = @is_active,
        updated_at = GETUTCDATE()
    WHERE user_id = @user_id;

    -- Update the user's role assignment.
    DECLARE @role_id INT;
    SELECT @role_id = role_id FROM auth.roles WHERE role_name = @role_name;
    UPDATE auth.user_roles
    SET role_id = @role_id
    WHERE user_id = @user_id;

    -- If a new password was provided, re-salt and re-hash it.
    IF @password IS NOT NULL AND @password <> ''
    BEGIN
        DECLARE @salt VARBINARY(16) = CRYPT_GEN_RANDOM(16);
        DECLARE @password_hash VARBINARY(64) = HASHBYTES('SHA2_512', @password + CONVERT(NVARCHAR(MAX), @salt, 1));

        UPDATE auth.users
        SET password_hash = @password_hash, salt = @salt
        WHERE user_id = @user_id;
    END
END;
GO

-- Authenticates a user by comparing a hashed password.
-- This procedure is called by the C# application during login.
CREATE OR ALTER PROCEDURE auth.AuthenticateUser
    @username NVARCHAR(50),
    @password NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @userId INT, @storedHash VARBINARY(64), @salt VARBINARY(16), @isActive BIT;

    SELECT @userId = u.user_id, @storedHash = u.password_hash, @salt = u.salt, @isActive = u.is_active
    FROM auth.users u
    WHERE u.username = @username;

    IF @userId IS NULL OR @isActive = 0 BEGIN RETURN; END;

    DECLARE @computedHash VARBINARY(64) = HASHBYTES('SHA2_512', @password + CONVERT(NVARCHAR(MAX), @salt, 1));

    IF @computedHash = @storedHash
    BEGIN
        SELECT u.user_id, u.username, u.full_name, u.email, u.is_active, r.role_name
        FROM auth.users u
        JOIN auth.user_roles ur ON u.user_id = ur.user_id
        JOIN auth.roles r ON ur.role_id = r.role_id
        WHERE u.user_id = @userId
          AND r.role_name = (SELECT TOP 1 role_name FROM auth.user_roles JOIN auth.roles ON user_roles.role_id = roles.role_id WHERE user_id = u.user_id ORDER BY role_name);
    END;
END;
GO

-- =================================================================================
-- INVENTORY & LOCATIONS SCHEMA PROCEDURES
-- =================================================================================
-- NOTE: The logic in these procedures has been extensively tested and should not be changed without careful consideration.

-- Retrieves all key details for a single pallet/stock item by its external ID.
CREATE OR ALTER PROCEDURE inventory.usp_GetStockDetailsByExternalId
    @externalId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.inventory_id AS ItemId, s.id AS InternalId, s.sku_name AS CustomerSkuId, s.full_unit_qty AS FullUnitQty,
        i.external_id AS ExternalId, s.sku_desc AS SkuDescription, i.batch_number AS BatchNumber, i.best_before_date AS BestBeforeDate,
        i.actual_qty AS Quantity, i.status AS StatusCode, t.status_desc AS StatusDescription, l.location_id AS LocationId,
        l.location_name AS CurrentLocation, i.document_ref AS DocumentRef, i.allocated_qty AS AllocatedQuantity,
        s.preferred_storage_type AS PreferredStorageType, s.preferred_section_id AS PreferredSectionId
    FROM inventory.inventory i
    LEFT JOIN inventory.sku s ON i.sku_name = s.id
    LEFT JOIN locations.locations l ON i.location_id = l.location_id
    LEFT JOIN inventory.stock_status t ON i.status = t.status_name
    WHERE TRIM(i.external_id) = TRIM(@externalId);
END;
GO

-- Retrieves a list of all stock items within a specific location.
CREATE OR ALTER PROCEDURE inventory.usp_GetStockByLocation
    @locationName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.id AS InternalId, s.sku_name AS CustomerSkuId, i.external_id AS ExternalId, s.sku_desc AS SkuDescription,
        i.batch_number AS BatchNumber, i.best_before_date AS BestBeforeDate, i.actual_qty AS Quantity, i.status AS StatusCode,
        t.status_desc AS StatusDescription, l.location_name AS CurrentLocation, i.document_ref AS DocumentRef, i.allocated_qty AS AllocatedQuantity
    FROM inventory.inventory i
    LEFT JOIN inventory.sku s ON i.sku_name = s.id
    LEFT JOIN locations.locations l ON i.location_id = l.location_id
    LEFT JOIN inventory.stock_status t ON i.status = t.status_name
    WHERE l.location_name = @locationName;
END;
GO

-- Retrieves comprehensive details about a single location, including capacity and reservation info.
CREATE OR ALTER PROCEDURE locations.usp_GetLocationDetailsByNameWithCapacityAndReservation
    @locationName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        l.location_id AS LocationId, l.location_name AS LocationName, l.type_id AS TypeId, lt.AllowConcurrentPutaway,
        l.is_active AS IsActive, l.notes AS Notes, l.capacity AS CapacityTotal,
        ISNULL(COUNT(DISTINCT i.inventory_id), 0) AS CapacityUsed,
        (SELECT COUNT(*) FROM locations.location_reservations lr WHERE lr.location_id = l.location_id AND lr.expires_at > SYSUTCDATETIME()) AS ReservedCount,
        CASE WHEN EXISTS (SELECT 1 FROM locations.location_reservations lr WHERE lr.location_id = l.location_id AND lr.expires_at > SYSUTCDATETIME()) THEN 1 ELSE 0 END AS IsReservedForPutaway
    FROM locations.locations l
    INNER JOIN locations.location_types lt ON l.type_id = lt.type_id
    LEFT JOIN inventory.inventory i ON l.location_id = i.location_id
    WHERE l.location_name = @locationName
    GROUP BY l.location_id, l.location_name, l.type_id, lt.AllowConcurrentPutaway, l.is_active, l.notes, l.capacity;
END;
GO

-- The main suggestion engine for putaway. Finds the best available location for a given product.
CREATE OR ALTER PROCEDURE locations.usp_SuggestAvailableLocations_WithAisleLogic
    @TypeId INT,
    @SectionId INT = NULL,
    @TopN INT = 10,
    @CurrentLocationId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @PreferredAisle NVARCHAR(20);
    -- 1. Find the least busy aisle by counting active reservations.
    SELECT TOP 1 @PreferredAisle = a.aisle
    FROM (SELECT DISTINCT aisle FROM locations.locations WHERE aisle IS NOT NULL AND type_id = @TypeId) a
    LEFT JOIN locations.location_reservations lr ON a.aisle = lr.aisle AND lr.expires_at > SYSUTCDATETIME()
    GROUP BY a.aisle
    ORDER BY COUNT(lr.reservation_id) ASC, a.aisle ASC;
    -- 2. Find a location that meets all criteria.
    SELECT TOP (@TopN)
        l.location_id, l.location_name, l.capacity,
        (l.capacity - (COUNT(i.inventory_id) + COUNT(DISTINCT res.reservation_id))) AS available_capacity,
        l.aisle, l.section_id, ls.section_name AS SectionName, lt.type_name AS TypeName
    FROM locations.locations l
    JOIN locations.location_types lt ON l.type_id = lt.type_id
    JOIN locations.location_sections ls ON l.section_id = ls.section_id
    LEFT JOIN inventory.inventory i ON l.location_id = i.location_id
    LEFT JOIN locations.location_reservations res ON l.location_id = res.location_id AND res.expires_at > SYSUTCDATETIME()
    WHERE l.is_active = 1 AND l.type_id = @TypeId AND (@SectionId IS NULL OR l.section_id = @SectionId)
    GROUP BY l.location_id, l.location_name, l.capacity, l.aisle, l.section_id, ls.section_name, lt.type_name
    HAVING (COUNT(i.inventory_id) + COUNT(DISTINCT res.reservation_id)) < l.capacity
    ORDER BY CASE WHEN l.aisle = @PreferredAisle THEN 0 ELSE 1 END, l.location_name;
END;
GO

-- Retrieves the details of an active, in-progress putaway task for a specific pallet.
CREATE OR ALTER PROCEDURE locations.usp_GetActiveTaskDetailsForPallet
    @ExternalId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 res.reservation_id, res.location_id, loc.location_name, sec.section_name, loc.aisle, lt.type_name
    FROM locations.location_reservations res
    JOIN locations.locations loc ON res.location_id = loc.location_id
    JOIN locations.location_sections sec ON loc.section_id = sec.section_id
    JOIN locations.location_types lt ON loc.type_id = lt.type_id
    WHERE res.reserved_by_pallet_id = @ExternalId AND res.expires_at > SYSUTCDATETIME()
    ORDER BY res.reserved_at DESC;
END;
GO

-- Initiates a putaway task within a single transaction.
CREATE OR ALTER PROCEDURE inventory.usp_InitiatePutaway
    @ExternalId NVARCHAR(100),
    @TargetLocationId INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @CurrentStatus CHAR(2), @CurrentLocationId INT, @SkuId INT, @ActualQty INT, @Aisle NVARCHAR(20);
        SELECT @CurrentStatus = i.status, @CurrentLocationId = i.location_id, @SkuId = i.sku_name, @ActualQty = i.actual_qty
        FROM inventory.inventory i WHERE i.external_id = @ExternalId;
        SELECT @Aisle = aisle FROM locations.locations WHERE location_id = @TargetLocationId;
        INSERT INTO locations.location_reservations (location_id, reserved_by_pallet_id, reserved_by, reservation_type, reserved_at, expires_at, aisle)
        VALUES (@TargetLocationId, @ExternalId, @UserId, 'PUTAWAY', SYSUTCDATETIME(), DATEADD(minute, 15, SYSUTCDATETIME()), @Aisle);
        UPDATE inventory.inventory SET original_status = @CurrentStatus, status = 'MV', movement_started_at = SYSUTCDATETIME(), updated_at = SYSUTCDATETIME(), updated_by = @UserId
        WHERE external_id = @ExternalId;
        INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
        SELECT inventory_id, @SkuId, @CurrentLocationId, @TargetLocationId, @CurrentStatus, 'MV', @ActualQty, @UserId, 'PUTAWAY_INITIAL', 'Putaway movement initiated by user.'
        FROM inventory.inventory WHERE external_id = @ExternalId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- Cancels an in-progress putaway task within a single transaction.
CREATE OR ALTER PROCEDURE inventory.usp_CancelPutaway
    @ExternalId NVARCHAR(100),
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @OriginalStatus CHAR(2), @InventoryId INT, @SkuId INT, @CurrentLocationId INT, @ActualQty INT;
        SELECT @OriginalStatus = i.original_status, @InventoryId = i.inventory_id, @SkuId = i.sku_name, @CurrentLocationId = i.location_id, @ActualQty = i.actual_qty
        FROM inventory.inventory i WHERE i.external_id = @ExternalId AND i.status = 'MV';
        IF @@ROWCOUNT = 0 BEGIN THROW 50001, 'Cannot cancel: Pallet is not in a movable status.', 1; END
        UPDATE inventory.inventory SET status = @OriginalStatus, original_status = NULL, movement_started_at = NULL, updated_at = SYSUTCDATETIME(), updated_by = @UserId
        WHERE inventory_id = @InventoryId;
        DELETE FROM locations.location_reservations WHERE reserved_by_pallet_id = @ExternalId;
        INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
        VALUES (@InventoryId, @SkuId, @CurrentLocationId, @CurrentLocationId, 'MV', @OriginalStatus, @ActualQty, @UserId, 'CANCELLED', 'Putaway task cancelled by user.');
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- Finalizes a putaway task within a single transaction.
CREATE OR ALTER PROCEDURE inventory.usp_FinalizePutaway
    @ExternalId NVARCHAR(100),
    @ScannedLocationName NVARCHAR(100),
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @InventoryId INT, @ScannedLocationId INT, @OriginalStatus CHAR(2), @CurrentLocationId INT, @SkuId INT, @ActualQty INT;
        DECLARE @LocationIsActive BIT, @LocationCapacity INT, @AllowConcurrentPutaway BIT, @CurrentStockCount INT;
        SELECT @InventoryId = i.inventory_id, @OriginalStatus = i.original_status, @CurrentLocationId = i.location_id, @SkuId = i.sku_name, @ActualQty = i.actual_qty
        FROM inventory.inventory i WHERE i.external_id = @ExternalId AND i.status = 'MV';
        SELECT @ScannedLocationId = l.location_id, @LocationIsActive = l.is_active, @LocationCapacity = l.capacity, @AllowConcurrentPutaway = lt.AllowConcurrentPutaway
        FROM locations.locations l JOIN locations.location_types lt ON l.type_id = lt.type_id WHERE l.location_name = @ScannedLocationName;
        IF @InventoryId IS NULL OR @ScannedLocationId IS NULL BEGIN THROW 50002, 'Pallet or location not found, or pallet is not in transit.', 1; END
        IF @LocationIsActive = 0 BEGIN THROW 50005, 'Finalization Failed: Destination location is now blocked.', 1; END
        SELECT @CurrentStockCount = COUNT(*) FROM inventory.inventory WHERE location_id = @ScannedLocationId;
        IF @AllowConcurrentPutaway = 0 AND @CurrentStockCount > 0 BEGIN THROW 50004, 'Finalization Failed: Racking location is now occupied.', 1; END
        ELSE IF @AllowConcurrentPutaway = 1 AND @CurrentStockCount >= @LocationCapacity BEGIN THROW 50006, 'Finalization Failed: Bulk location is now at full capacity.', 1; END
        UPDATE inventory.inventory SET location_id = @ScannedLocationId, status = @OriginalStatus, original_status = NULL, movement_started_at = NULL, document_ref = NULL, updated_at = SYSUTCDATETIME(), updated_by = @UserId
        WHERE inventory_id = @InventoryId;
        DELETE FROM locations.location_reservations WHERE reserved_by_pallet_id = @ExternalId AND location_id = @ScannedLocationId;
        INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
        VALUES (@InventoryId, @SkuId, @CurrentLocationId, @ScannedLocationId, 'MV', @OriginalStatus, @ActualQty, @UserId, 'PUTAWAY_FINALIZED', 'Putaway move finalized by user.');
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- Modifies an in-progress putaway task within a single transaction.
CREATE OR ALTER PROCEDURE inventory.usp_ModifyPutaway
    @ExternalId NVARCHAR(100),
    @NewTargetLocationId INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @OldReservationId INT;
        SELECT @OldReservationId = reservation_id FROM locations.location_reservations WHERE reserved_by_pallet_id = @ExternalId AND expires_at > SYSUTCDATETIME();
        IF @OldReservationId IS NULL BEGIN THROW 50003, 'No active reservation found to modify for this pallet.', 1; END
        DELETE FROM locations.location_reservations WHERE reservation_id = @OldReservationId;
        DECLARE @Aisle NVARCHAR(20), @InventoryId INT, @SkuId INT;
        SELECT @Aisle = aisle FROM locations.locations WHERE location_id = @NewTargetLocationId;
        SELECT @InventoryId = inventory_id, @SkuId = sku_name FROM inventory.inventory WHERE external_id = @ExternalId;
        INSERT INTO locations.location_reservations (location_id, reserved_by_pallet_id, reserved_by, reservation_type, reserved_at, expires_at, aisle)
        VALUES (@NewTargetLocationId, @ExternalId, @UserId, 'PUTAWAY', SYSUTCDATETIME(), DATEADD(minute, 15, SYSUTCDATETIME()), @Aisle);
        INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, moved_qty, moved_by, movement_type, note)
        VALUES (@InventoryId, @SkuId, NULL, @NewTargetLocationId, 0, @UserId, 'PUTAWAY_MANUAL', 'Suggestion modified to new location.');
        UPDATE inventory.inventory SET updated_at = SYSUTCDATETIME(), updated_by = @UserId WHERE inventory_id = @InventoryId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- =================================================================================
-- SQL SERVER AGENT JOB SETUP
-- =================================================================================

-- =================================================================================
-- PeasyWare WMS - System Maintenance Procedure
-- Description: This procedure is designed to be run on a schedule by the SQL Server Agent.
-- It performs two key cleanup tasks:
-- 1. Resets "stuck" pallets that have been in transit for too long.
-- 2. Deletes expired location reservations and logs the cleanup action.
-- =================================================================================
CREATE OR ALTER PROCEDURE dbo.WMS_AutoMaintenance
AS
BEGIN
    SET NOCOUNT ON;

    -- Declare variables to hold settings from the database
    DECLARE @PutawayTimeoutMinutes INT;
    DECLARE @ReservationExpiryMinutes INT;
    DECLARE @AuditUser INT;

    -- Get the 'system' user ID for logging purposes.
    SELECT @AuditUser = user_id FROM auth.users WHERE username = 'system';

    PRINT '--- WMS Auto Maintenance Job started (using UTC)...';

    -- Fetch settings from the database with default fallbacks.
    BEGIN TRY
        SELECT @PutawayTimeoutMinutes = TRY_CAST(SettingValue AS INT) FROM operations.Settings WHERE SettingName = 'UnlockTimeoutMinutes';
        SELECT @ReservationExpiryMinutes = TRY_CAST(SettingValue AS INT) FROM operations.Settings WHERE SettingName = 'ReservationTimeoutMinutes';
        IF @PutawayTimeoutMinutes IS NULL OR @PutawayTimeoutMinutes <= 0 BEGIN SET @PutawayTimeoutMinutes = 15; PRINT 'Warning: Using default UnlockTimeoutMinutes: 15.'; END
        IF @ReservationExpiryMinutes IS NULL OR @ReservationExpiryMinutes <= 0 BEGIN SET @ReservationExpiryMinutes = 15; PRINT 'Warning: Using default ReservationTimeoutMinutes: 15.'; END
    END TRY
    BEGIN CATCH
        PRINT 'ERROR fetching settings: ' + ERROR_MESSAGE();
        SET @PutawayTimeoutMinutes = 15; SET @ReservationExpiryMinutes = 15;
    END CATCH;

    -- --- 1. Clean up STUCK PALLETS ---
    PRINT 'Looking for stuck pallets older than ' + CAST(@PutawayTimeoutMinutes AS NVARCHAR) + ' minutes.';
    
    CREATE TABLE #StuckPallets (inventory_id INT PRIMARY KEY, external_id NVARCHAR(100), reserved_by INT NULL);

    INSERT INTO #StuckPallets (inventory_id, external_id, reserved_by)
    SELECT i.inventory_id, i.external_id, i.updated_by
    FROM inventory.inventory i
    WHERE i.status = 'MV'
      AND i.movement_started_at IS NOT NULL
      AND i.movement_started_at <= DATEADD(minute, -@PutawayTimeoutMinutes, SYSUTCDATETIME());

    UPDATE i
    SET
        status = COALESCE(i.original_status, 'BL'), -- Revert to original status, or 'BL' if null
        movement_started_at = NULL,
        updated_at = SYSUTCDATETIME(),
        updated_by = @AuditUser
    FROM inventory.inventory i
    JOIN #StuckPallets sp ON i.inventory_id = sp.inventory_id;
    
    INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
    SELECT i.inventory_id, i.sku_name, i.location_id, i.location_id, 'MV', i.status, i.actual_qty, @AuditUser, 'AUTO_RESET', 
           'Stuck pallet reset by auto maintenance job. User who initiated putaway (approx): ' + ISNULL(CAST(sp.reserved_by AS NVARCHAR), 'N/A')
    FROM #StuckPallets sp JOIN inventory.inventory i ON sp.inventory_id = i.inventory_id;

    DELETE lr
    FROM locations.location_reservations lr
    JOIN #StuckPallets sp ON lr.reserved_by_pallet_id = sp.external_id;

    DECLARE @ResetCount INT = (SELECT COUNT(*) FROM #StuckPallets);
    PRINT '✅ Reset ' + CAST(@ResetCount AS NVARCHAR) + ' stuck tasks.';
    DROP TABLE #StuckPallets;

    -- --- 2. Clean up EXPIRED RESERVATIONS ---
    PRINT 'Looking for expired reservations.';

    CREATE TABLE #ExpiredReservations (reservation_id INT PRIMARY KEY, pallet_id NVARCHAR(255), user_id INT);
    INSERT INTO #ExpiredReservations (reservation_id, pallet_id, user_id)
    SELECT reservation_id, reserved_by_pallet_id, reserved_by
    FROM locations.location_reservations
    WHERE expires_at <= SYSUTCDATETIME();

    INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
    SELECT
        i.inventory_id,
        i.sku_name,
        i.location_id,
        i.location_id,
        i.status,
        i.status,
        0,
        @AuditUser,
        'AUTO_RESET', -- *** THE FIX IS HERE: Reusing the existing movement type. ***
        'Reservation expired and was auto-removed. Original user (approx): ' + ISNULL(CAST(er.user_id AS NVARCHAR), 'N/A')
    FROM #ExpiredReservations er
    JOIN inventory.inventory i ON er.pallet_id = i.external_id;

    DELETE FROM locations.location_reservations
    WHERE reservation_id IN (SELECT reservation_id FROM #ExpiredReservations);

    DECLARE @DeletedReservationCount INT = @@ROWCOUNT;
    PRINT '🧹 Deleted and logged: ' + CAST(@DeletedReservationCount AS NVARCHAR) + ' expired reservations';
    DROP TABLE #ExpiredReservations;
    
    PRINT '--- Job completed.';
END
GO

-- The following section sets up the SQL Server Agent job to run the maintenance procedure automatically.
-- It ensures that any old job or schedule with the same name is cleaned up first.

-- 1. Delete old job if it exists.
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'WMS Auto Maintenance')
BEGIN
    EXEC msdb.dbo.sp_delete_job @job_name = N'WMS Auto Maintenance', @delete_unused_schedule = 1;
    PRINT '🧹 Deleted existing job: WMS Auto Maintenance';
END
GO

-- 2. Delete old schedule if it exists.
IF EXISTS (SELECT 1 FROM msdb.dbo.sysschedules WHERE name = N'WMS Maintenance 15min')
BEGIN
    DECLARE @existing_schedule_id INT;
    SELECT @existing_schedule_id = schedule_id FROM msdb.dbo.sysschedules WHERE name = N'WMS Maintenance 15min';
    EXEC msdb.dbo.sp_delete_schedule @schedule_id = @existing_schedule_id;
    PRINT '🧹 Deleted existing schedule: WMS Maintenance 15min';
END
GO

-- This block creates the job, schedule, and attaches them in a single batch to avoid scope issues.
BEGIN
    -- 3. Create the new job.
    EXEC msdb.dbo.sp_add_job
        @job_name = N'WMS Auto Maintenance',
        @enabled = 1,
        @description = N'Handles auto-unlock of stuck pallets and cleanup of expired location reservations every 15 minutes.',
        @owner_login_name = N'sa';

    -- 4. Add a step to the job that executes our maintenance procedure.
    EXEC msdb.dbo.sp_add_jobstep
        @job_name = N'WMS Auto Maintenance',
        @step_name = N'Run Maintenance Procedure',
        @subsystem = N'TSQL',
        @command = N'EXEC dbo.WMS_AutoMaintenance;',
        @database_name = N'WMS_DB';

    -- 5. Create the schedule to run the job every 15 minutes.
    DECLARE @CurrentDate INT = CONVERT(INT, CONVERT(VARCHAR(8), GETDATE(), 112));
    DECLARE @ScheduleID INT;
    EXEC msdb.dbo.sp_add_schedule
        @schedule_name = N'WMS Maintenance 15min',
        @enabled = 1,
        @freq_type = 4, -- Daily
        @freq_interval = 1, -- Every day
        @freq_subday_type = 4, -- Minutes
        @freq_subday_interval = 15, -- Every 15 minutes
        @active_start_date = @CurrentDate,
        @active_start_time = 0,
        @active_end_time = 235959,
        @schedule_id = @ScheduleID OUTPUT;

    -- 6. Attach the schedule to the job.
    EXEC msdb.dbo.sp_attach_schedule
        @job_name = N'WMS Auto Maintenance',
        @schedule_id = @ScheduleID;

    -- 7. Assign the job to the current server.
    EXEC msdb.dbo.sp_add_jobserver
        @job_name = N'WMS Auto Maintenance',
        @server_name = N'(local)';

    PRINT '✅ WMS Auto Maintenance job created and scheduled every 15 minutes!';
END
GO

-- =================================================================================
-- Bin to Bin Movement Procedures (V2 - Robust Workflow)
-- =================================================================================
-- This procedure is the FINAL step. It validates the destination and completes the move.
CREATE OR ALTER PROCEDURE inventory.usp_InitiateBinToBinMove
    @ExternalId NVARCHAR(100),
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentStatus CHAR(2), @InventoryId INT, @SkuId INT, @CurrentLocationId INT, @ActualQty INT;

        SELECT 
            @CurrentStatus = i.status, 
            @InventoryId = i.inventory_id, 
            @SkuId = i.sku_name, 
            @CurrentLocationId = i.location_id, 
            @ActualQty = i.actual_qty
        FROM inventory.inventory i WHERE i.external_id = @ExternalId;

        -- Pre-validation of the pallet's status.
        IF @CurrentStatus IN ('MV', 'AL', 'EX', 'OU')
        BEGIN
            THROW 50011, 'Validation Failed: Pallet status prevents movement.', 1;
        END

        -- All checks passed, update the status to lock the pallet.
        UPDATE inventory.inventory
        SET
            original_status = @CurrentStatus,
            status = 'MV',
            movement_started_at = SYSUTCDATETIME(),
            updated_at = SYSUTCDATETIME(),
            updated_by = @UserId
        WHERE inventory_id = @InventoryId;

        -- Log the start of the movement.
        INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
        VALUES (@InventoryId, @SkuId, @CurrentLocationId, NULL, @CurrentStatus, 'MV', @ActualQty, @UserId, 'BIN_TO_BIN_STARTED', 'Bin to bin movement initiated by user.');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- This procedure is the FINAL step. It validates the destination and completes the move.
CREATE OR ALTER PROCEDURE inventory.usp_FinalizeBinToBinMove
    @ExternalId NVARCHAR(100),
    @TargetLocationName NVARCHAR(100),
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get current state of the pallet and target location
        DECLARE @InventoryId INT, @CurrentLocationId INT, @SkuId INT, @ActualQty INT, @OriginalStatus CHAR(2);
        DECLARE @TargetLocationId INT, @TargetIsActive BIT, @TargetCapacity INT, @TargetAllowConcurrent BIT;
        DECLARE @TargetCurrentStockCount INT;

        SELECT @InventoryId = i.inventory_id, @CurrentLocationId = i.location_id, @SkuId = i.sku_name, @ActualQty = i.actual_qty, @OriginalStatus = i.original_status
        FROM inventory.inventory i WHERE i.external_id = @ExternalId AND status = 'MV';

        SELECT @TargetLocationId = l.location_id, @TargetIsActive = l.is_active, @TargetCapacity = l.capacity, @TargetAllowConcurrent = lt.AllowConcurrentPutaway
        FROM locations.locations l JOIN locations.location_types lt ON l.type_id = lt.type_id
        WHERE l.location_name = @TargetLocationName;

        IF @InventoryId IS NULL OR @TargetLocationId IS NULL
            THROW 50007, 'Validation Failed: Pallet or target location not found.', 1;

        -- *** THE FIX IS HERE: Add validation to prevent moving to the same location. ***
        IF @CurrentLocationId = @TargetLocationId
            THROW 50012, 'Validation Failed: Source and target location cannot be the same.', 1;

        IF @TargetIsActive = 0
            THROW 50008, 'Validation Failed: Target location is blocked.', 1;

        SELECT @TargetCurrentStockCount = COUNT(*) FROM inventory.inventory WHERE location_id = @TargetLocationId;

        IF @TargetAllowConcurrent = 0 AND @TargetCurrentStockCount > 0
            THROW 50009, 'Validation Failed: Target racking location is already occupied.', 1;
        
        IF @TargetAllowConcurrent = 1 AND @TargetCurrentStockCount >= @TargetCapacity
            THROW 50010, 'Validation Failed: Target bulk location is at full capacity.', 1;

        -- All checks passed, execute the move.
        UPDATE inventory.inventory
        SET
            location_id = @TargetLocationId,
            status = @OriginalStatus,
            original_status = NULL,
            movement_started_at = NULL,
            document_ref = NULL,
            updated_at = SYSUTCDATETIME(),
            updated_by = @UserId
        WHERE inventory_id = @InventoryId;

        INSERT INTO inventory.inventory_movements (inventory_id, sku_id, from_location_id, to_location_id, from_status, to_status, moved_qty, moved_by, movement_type, note)
        VALUES (@InventoryId, @SkuId, @CurrentLocationId, @TargetLocationId, 'MV', @OriginalStatus, @ActualQty, @UserId, 'BIN_TO_BIN_FINALIZED', 'Bin to bin move completed by user.');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- This procedure validates and reserves the target location for a Bin to Bin move.
CREATE OR ALTER PROCEDURE inventory.usp_AssignAndReserveBinToBinMove
    @ExternalId NVARCHAR(100),
    @TargetLocationName NVARCHAR(100),
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @TargetLocationId INT, @TargetIsActive BIT, @TargetCapacity INT, @TargetAllowConcurrent BIT;
        DECLARE @TargetCurrentStockCount INT, @TargetReservedCount INT;

        SELECT @TargetLocationId = l.location_id, @TargetIsActive = l.is_active, @TargetCapacity = l.capacity, @TargetAllowConcurrent = lt.AllowConcurrentPutaway
        FROM locations.locations l JOIN locations.location_types lt ON l.type_id = lt.type_id
        WHERE l.location_name = @TargetLocationName;

        IF @TargetLocationId IS NULL
            THROW 50007, 'Validation Failed: Target location not found.', 1;

        IF @TargetIsActive = 0
            THROW 50008, 'Validation Failed: Target location is blocked.', 1;

        -- Check effective capacity of the target location (physical + reserved)
        SELECT @TargetCurrentStockCount = COUNT(*) FROM inventory.inventory WHERE location_id = @TargetLocationId;
        SELECT @TargetReservedCount = COUNT(*) FROM locations.location_reservations WHERE location_id = @TargetLocationId AND expires_at > SYSUTCDATETIME();

        IF @TargetAllowConcurrent = 0 AND (@TargetCurrentStockCount > 0 OR @TargetReservedCount > 0)
            THROW 50009, 'Validation Failed: Target racking location is already occupied or reserved.', 1;
        
        IF @TargetAllowConcurrent = 1 AND (@TargetCurrentStockCount + @TargetReservedCount) >= @TargetCapacity
            THROW 50010, 'Validation Failed: Target bulk location is at full capacity.', 1;

        -- All validations passed, create the reservation.
        INSERT INTO locations.location_reservations (location_id, reserved_by_pallet_id, reserved_by, reservation_type, reserved_at, expires_at)
        VALUES (@TargetLocationId, @ExternalId, @UserId, 'BIN_TO_BIN', SYSUTCDATETIME(), DATEADD(minute, 15, SYSUTCDATETIME()));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
