using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using PeasyWare.WMS.Console.Models;
using PeasyWare.WMS.Console.Models.DTOs;
using System.Text.Json;

namespace PeasyWare.WMS.App.Data
{
    /// <summary>
    /// Handles all direct communication with the WMS database.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString = string.Empty;

        // ✅ API uses this constructor
        public DatabaseService(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Database connection string cannot be null or empty.");

            //_connectionString = connectionString;
            _connectionString = "Server=192.168.0.61,1433;Database=WMS_DB;User Id=sa;Password=wslAdmin?;Encrypt=False;TrustServerCertificate=True;";
        }

        // ✅ Console app uses this one
        public DatabaseService()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfiguration configuration = builder.Build();
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Database connection string 'DefaultConnection' not found in appsettings.json.");
            }
        }

        /// <summary>
        /// Loads critical application settings from the operations.Settings table in the database.
        /// </summary>
        /// <returns>A SessionConfig object populated with settings from the database.</returns>
        public async Task<SessionConfig> LoadConfigurationAsync()
        {
            var config = new SessionConfig();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("SELECT SettingName, SettingValue FROM operations.Settings", connection);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var settingName = reader["SettingName"]?.ToString();
                            var settingValue = reader["SettingValue"]?.ToString();
                            switch (settingName)
                            {
                                case "EnableLogin":
                                    bool.TryParse(settingValue, out bool enableLogin);
                                    config.EnableLogin = enableLogin;
                                    break;
                                case "EnableDebugging":
                                    bool.TryParse(settingValue, out bool enableDebugging);
                                    config.EnableDebugging = enableDebugging;
                                    break;
                                // Corrected to match the setting name in the database test data.
                                case "AllowPutawayModification":
                                    // Using a different local variable name for clarity.
                                    bool.TryParse(settingValue, out bool allowModification);
                                    config.AllowPutawayModification = allowModification;
                                    break;
                                // In the LoadConfigurationAsync method, add this new case to the switch block:
                                case "ShowExpectedOnReceive":
                                    bool.TryParse(settingValue, out bool showExpected);
                                    config.ShowExpectedOnReceive = showExpected;
                                    break;
                                case "ReservationTimeoutMinutes":
                                    int.TryParse(settingValue, out int reservationTimeout);
                                    config.ReservationTimeoutMinutes = reservationTimeout;
                                    break;
                                case "UnlockTimeoutMinutes":
                                    int.TryParse(settingValue, out int unlockTimeout);
                                    config.UnlockTimeoutMinutes = unlockTimeout;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"FATAL: Could not connect to database or load configuration. {ex.Message}");
                Environment.Exit(1);
            }
            return config;
        }

        /// <summary>
        /// Authenticates a user against the database using the auth.AuthenticateUser stored procedure.
        /// </summary>
        /// <param name="username">The user's username.</param>
        /// <param name="password">The user's password.</param>
        /// <returns>A User object if authentication is successful; otherwise, null.</returns>
        public async Task<User?> AuthenticateUserAsync(string username, string password)
        {
            User? user = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("auth.AuthenticateUser", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add(new SqlParameter("@username", username));
                command.Parameters.Add(new SqlParameter("@password", password));
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        user = new User
                        {
                            UserId = Convert.ToInt32(reader["user_id"]),
                            Username = reader["username"]?.ToString() ?? string.Empty,
                            FullName = reader["full_name"]?.ToString() ?? string.Empty,
                            Email = reader["email"]?.ToString() ?? string.Empty,
                            RoleName = reader["role_name"]?.ToString() ?? string.Empty
                        };
                    }
                }
            }
            return user;
        }

        /// <summary>
        /// Logs a user login attempt to the logs.auth_login_attempts table.
        /// </summary>
        /// <param name="username">The username that was attempted.</param>
        /// <param name="userId">The user's ID if the login was successful.</param>
        /// <param name="success">A boolean indicating if the login was successful.</param>
        /// <param name="ipAddress">The IP address of the client machine.</param>
        public async Task LogLoginAttemptAsync(string username, int? userId, bool success, string ipAddress)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand(
                        @"INSERT INTO logs.auth_login_attempts (username, user_id, ip_address, user_agent, login_time, host_name, success) VALUES (@username, @user_id, @ip_address, @user_agent, @login_time, @host_name, @success)",
                        connection);

                    command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar) { Value = username });
                    command.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = userId });
                    command.Parameters.Add(new SqlParameter("@ip_address", SqlDbType.VarChar) { Value = ipAddress });
                    command.Parameters.Add(new SqlParameter("@user_agent", SqlDbType.NVarChar) { Value = "PeasyWare.WMS.Console" });
                    command.Parameters.Add(new SqlParameter("@login_time", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
                    command.Parameters.Add(new SqlParameter("@host_name", SqlDbType.NVarChar) { Value = Environment.MachineName });
                    command.Parameters.Add(new SqlParameter("@success", SqlDbType.Bit) { Value = success });

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                System.Console.WriteLine($"\nWarning: Could not write to login attempts log. {ex.Message}");
                System.Console.ResetColor();
            }
        }

        /// <summary>
        /// Retrieves detailed information for a single stock item by its external ID (e.g., pallet ID).
        /// </summary>
        /// <param name="externalId">The external identifier of the stock item to query.</param>
        /// <returns>A StockItemDetails object if found; otherwise, null.</returns>
        public async Task<StockItemDetails?> GetStockDetailsByExternalIdAsync(string externalId)
        {
            StockItemDetails? item = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("inventory.usp_GetStockDetailsByExternalId", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add(new SqlParameter("@externalId", externalId));
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        item = new StockItemDetails
                        {
                            ItemId = Convert.ToInt32(reader["ItemId"]),
                            InternalId = Convert.ToInt32(reader["InternalId"]),
                            CustomerSkuId = reader["CustomerSkuId"]?.ToString() ?? string.Empty,
                            FullUnitQty = Convert.ToInt32(reader["FullUnitQty"]),
                            ExternalId = reader["ExternalId"]?.ToString() ?? string.Empty,
                            SkuDescription = reader["SkuDescription"]?.ToString() ?? string.Empty,
                            BatchNumber = reader["BatchNumber"]?.ToString() ?? string.Empty,
                            BestBeforeDate = reader["BestBeforeDate"] as DateTime?,
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            StatusCode = reader["StatusCode"]?.ToString() ?? string.Empty,
                            StatusDescription = reader["StatusDescription"]?.ToString() ?? string.Empty,
                            LocationId = Convert.ToInt32(reader["LocationId"]),
                            CurrentLocation = reader["CurrentLocation"]?.ToString() ?? string.Empty,
                            DocumentRef = reader["DocumentRef"]?.ToString() ?? string.Empty,
                            AllocatedQuantity = Convert.ToInt32(reader["AllocatedQuantity"]),
                            PreferredStorageType = Convert.ToInt32(reader["PreferredStorageType"]),
                            PreferredSectionId = reader["PreferredSectionId"] == DBNull.Value ? null : Convert.ToInt32(reader["PreferredSectionId"])
                        };
                    }
                }
            }
            return item;
        }

        /// <summary>
        /// Retrieves a list of all stock items currently in a specific location.
        /// </summary>
        /// <param name="locationName">The name of the location to query.</param>
        /// <returns>A list of StockItemDetails objects.</returns>
        public async Task<List<StockItemDetails>> GetStockByLocationAsync(string locationName)
        {
            var items = new List<StockItemDetails>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("inventory.usp_GetStockByLocation", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add(new SqlParameter("@locationName", locationName));
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = new StockItemDetails
                        {
                            InternalId = Convert.ToInt32(reader["InternalId"]),
                            CustomerSkuId = reader["CustomerSkuId"]?.ToString() ?? string.Empty,
                            ExternalId = reader["ExternalId"]?.ToString() ?? string.Empty,
                            SkuDescription = reader["SkuDescription"]?.ToString() ?? string.Empty,
                            BatchNumber = reader["BatchNumber"]?.ToString() ?? string.Empty,
                            BestBeforeDate = reader["BestBeforeDate"] as DateTime?,
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            StatusCode = reader["StatusCode"]?.ToString() ?? string.Empty,
                            StatusDescription = reader["StatusDescription"]?.ToString() ?? string.Empty,
                            CurrentLocation = reader["CurrentLocation"]?.ToString() ?? string.Empty,
                            DocumentRef = reader["DocumentRef"]?.ToString() ?? string.Empty
                        };
                        items.Add(item);
                    }
                }
            }
            return items;
        }

        /// <summary>
        /// Retrieves detailed properties of a specific location, including its capacity and reservation status.
        /// </summary>
        /// <param name="locationName">The name of the location to query.</param>
        /// <returns>A LocationDetails object if found; otherwise, null.</returns>
        public async Task<LocationDetails?> GetLocationDetailsAsync(string locationName)
        {
            LocationDetails? details = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("locations.usp_GetLocationDetailsByNameWithCapacityAndReservation", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add(new SqlParameter("@locationName", locationName));
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        details = new LocationDetails
                        {
                            LocationId = Convert.ToInt32(reader["LocationId"]),
                            LocationName = reader["LocationName"]?.ToString() ?? string.Empty,
                            TypeId = Convert.ToInt32(reader["TypeId"]),
                            AllowConcurrentPutaway = Convert.ToBoolean(reader["AllowConcurrentPutaway"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            Notes = reader["Notes"]?.ToString() ?? string.Empty,
                            CapacityTotal = Convert.ToInt32(reader["CapacityTotal"]),
                            CapacityUsed = Convert.ToInt32(reader["CapacityUsed"]),
                            ReservedCount = Convert.ToInt32(reader["ReservedCount"]),
                            IsReservedForPutaway = Convert.ToBoolean(reader["IsReservedForPutaway"])
                        };
                    }
                }
            }
            return details;
        }

        /// <summary>
        /// Gets the best system-suggested location for a putaway task.
        /// </summary>
        /// <param name="typeId">The preferred storage type ID for the product.</param>
        /// <param name="sectionId">The preferred section ID for the product.</param>
        /// <param name="currentLocationId">The current location ID of the pallet being moved.</param>
        /// <returns>A SuggestedLocation object if a suitable location is found; otherwise, null.</returns>
        public async Task<SuggestedLocation?> GetPutawaySuggestionAsync(int typeId, int? sectionId, int currentLocationId)
        {
            SuggestedLocation? suggestion = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("locations.usp_SuggestAvailableLocations_WithAisleLogic", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.AddWithValue("@TypeId", typeId);
                command.Parameters.AddWithValue("@SectionId", sectionId);
                command.Parameters.AddWithValue("@TopN", 1);
                command.Parameters.AddWithValue("@CurrentLocationId", currentLocationId);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        suggestion = new SuggestedLocation
                        {
                            LocationId = Convert.ToInt32(reader["location_id"]),
                            LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                            Aisle = reader["aisle"]?.ToString() ?? string.Empty,
                            SectionName = reader["SectionName"]?.ToString() ?? string.Empty,
                            TypeName = reader["TypeName"]?.ToString() ?? string.Empty
                        };
                    }
                }
            }
            return suggestion;
        }

        /// <summary>
        /// Initiates a putaway task in the database within a transaction.
        /// </summary>
        /// <param name="externalId">The external ID of the pallet.</param>
        /// <param name="targetLocationId">The ID of the destination location.</param>
        /// <param name="userId">The ID of the user performing the action.</param>
        /// <returns>True if the transaction was successful; otherwise, false.</returns>
        public async Task<bool> InitiatePutawayAsync(string externalId, int targetLocationId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("inventory.usp_InitiatePutaway", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@TargetLocationId", targetLocationId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Cancels an in-progress putaway task within a transaction.
        /// </summary>
        /// <param name="externalId">The external ID of the pallet.</param>
        /// <param name="userId">The ID of the user performing the action.</param>
        /// <returns>True if the transaction was successful; otherwise, false.</returns>
        public async Task<bool> CancelPutawayAsync(string externalId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("inventory.usp_CancelPutaway", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Finalizes a putaway task, moving the pallet to its destination within a transaction.
        /// </summary>
        /// <param name="externalId">The external ID of the pallet.</param>
        /// <param name="scannedLocationName">The name of the location scanned by the user for confirmation.</param>
        /// <param name="userId">The ID of the user performing the action.</param>
        /// <returns>True if the transaction was successful; otherwise, false.</returns>
        public async Task<bool> FinalizePutawayAsync(string externalId, string scannedLocationName, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("inventory.usp_FinalizePutaway", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@ScannedLocationName", scannedLocationName);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Modifies an in-progress putaway task, changing its destination location within a transaction.
        /// </summary>
        /// <param name="externalId">The external ID of the pallet.</param>
        /// <param name="newTargetLocationId">The ID of the new destination location.</param>
        /// <param name="userId">The ID of the user performing the action.</param>
        /// <returns>True if the transaction was successful; otherwise, false.</returns>
        public async Task<bool> ModifyPutawayAsync(string externalId, int newTargetLocationId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("inventory.usp_ModifyPutaway", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@NewTargetLocationId", newTargetLocationId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves the details of an active putaway task for a specific pallet.
        /// </summary>
        /// <param name="externalId">The external ID of the pallet.</param>
        /// <returns>An ActiveTaskDetails object if a task is found; otherwise, null.</returns>
        public async Task<ActiveTaskDetails?> GetActiveTaskForPalletAsync(string externalId)
        {
            ActiveTaskDetails? task = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("locations.usp_GetActiveTaskDetailsForPallet", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.AddWithValue("@ExternalId", externalId);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        task = new ActiveTaskDetails
                        {
                            ReservationId = Convert.ToInt32(reader["reservation_id"]),
                            Suggestion = new SuggestedLocation
                            {
                                LocationId = Convert.ToInt32(reader["location_id"]),
                                LocationName = reader["location_name"]?.ToString() ?? string.Empty,
                                SectionName = reader["section_name"]?.ToString() ?? string.Empty,
                                Aisle = reader["aisle"]?.ToString() ?? string.Empty,
                                TypeName = reader["type_name"]?.ToString() ?? string.Empty
                            }
                        };
                    }
                }
            }
            return task;
        }

        public async Task<LocationReservation?> GetReservationForPalletAsync(string externalId)
        {
            LocationReservation? reservation = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand(
                    @"SELECT TOP 1 reservation_id, location_id, reserved_by_pallet_id, reserved_by, reservation_type, reserved_at, expires_at 
                    FROM locations.location_reservations 
                    WHERE reserved_by_pallet_id = @palletId AND expires_at > SYSUTCDATETIME()",
                    connection);
                command.Parameters.Add(new SqlParameter("@palletId", externalId));
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        reservation = new LocationReservation
                        {
                            ReservationId = Convert.ToInt32(reader["reservation_id"]),
                            LocationId = Convert.ToInt32(reader["location_id"]),
                            ReservedByPalletId = reader["reserved_by_pallet_id"]?.ToString() ?? string.Empty,
                            ReservedByUserId = Convert.ToInt32(reader["reserved_by"]),
                            ReservationType = reader["reservation_type"]?.ToString() ?? string.Empty,
                            ReservedAt = Convert.ToDateTime(reader["reserved_at"]),
                            ExpiresAt = Convert.ToDateTime(reader["expires_at"])
                        };
                    }
                }
            }
            return reservation;
        }

        // --- NEW METHODS FOR BIN TO BIN V2 ---

        public async Task<bool> InitiateBinToBinMoveAsync(string externalId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("inventory.usp_InitiateBinToBinMove", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        public async Task<bool> AssignAndReserveBinToBinMoveAsync(string externalId, string targetLocationName, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("inventory.usp_AssignAndReserveBinToBinMove", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@TargetLocationName", targetLocationName);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Retrieves a list of all inbound deliveries that are ready to be activated.
        /// </summary>
        /// <returns>A list of InboundDeliverySummary objects.</returns>
        public async Task<List<InboundDeliverySummary>> GetActivatableInboundsAsync()
        {
            var deliveries = new List<InboundDeliverySummary>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("deliveries.usp_GetActivatableInbounds", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        deliveries.Add(new InboundDeliverySummary
                        {
                            InboundId = Convert.ToInt32(reader["inbound_id"]),
                            DocumentRef = reader["document_ref"]?.ToString() ?? string.Empty,
                            ExpectedArrivalDate = Convert.ToDateTime(reader["expected_arrival_date"]),
                            StatusCode = reader["status"]?.ToString() ?? string.Empty,
                            StatusDescription = reader["status_description"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }
            return deliveries;
        }

        /// <summary>
        /// Activates a specific inbound delivery, making it ready for receiving.
        /// </summary>
        /// <param name="documentRef">The document reference of the delivery to activate.</param>
        /// <param name="userId">The ID of the user performing the action.</param>
        /// <returns>True if the activation was successful; otherwise, false.</returns>
        public async Task<bool> ActivateInboundDeliveryAsync(string documentRef, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("deliveries.usp_ActivateInboundDelivery", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@DocumentRef", documentRef);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves a list of all inbound deliveries that are activated and ready for receiving.
        /// </summary>
        public async Task<List<ReceivableDelivery>> GetReceivableDeliveriesAsync()
        {
            var deliveries = new List<ReceivableDelivery>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("deliveries.usp_GetReceivableDeliveries", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        deliveries.Add(new ReceivableDelivery
                        {
                            InboundId = Convert.ToInt32(reader["inbound_id"]),
                            DocumentRef = reader["document_ref"]?.ToString() ?? string.Empty,
                            ExpectedArrivalDate = Convert.ToDateTime(reader["expected_arrival_date"]),
                            StatusDescription = reader["status_description"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }
            return deliveries;
        }

        /// Retrieves all line items for a specific inbound delivery.
        /// </summary>
        /// <param name="inboundId">The ID of the inbound delivery.</param>
        public async Task<List<InboundRow>> GetInboundRowsAsync(int inboundId)
        {
            var rows = new List<InboundRow>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var command = new SqlCommand("deliveries.usp_GetInboundRows", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.AddWithValue("@InboundId", inboundId);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rows.Add(new InboundRow
                        {
                            SkuId = Convert.ToInt32(reader["SkuId"]),
                            SkuName = reader["SkuName"]?.ToString() ?? string.Empty,
                            SkuDescription = reader["SkuDescription"]?.ToString() ?? string.Empty,
                            ExpectedQty = Convert.ToInt32(reader["ExpectedQty"]),
                            ReceivedQty = Convert.ToInt32(reader["ReceivedQty"]),
                            ExternalId = reader["ExternalId"]?.ToString() ?? string.Empty,
                            BatchNumber = reader["BatchNumber"]?.ToString() ?? string.Empty,
                            BestBeforeDate = reader["BestBeforeDate"] as DateTime?
                        });
                    }
                }
            }
            return rows;
        }

        /// <summary>
        /// Executes the receiving transaction for a single pallet with operator-confirmed details.
        /// </summary>
        public async Task<bool> ReceivePalletAsync(string documentRef, string externalId, int skuId, int actualQuantity, string actualBatchNumber, DateTime? actualBestBeforeDate, string receivingBayName, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("deliveries.usp_ReceivePallet", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    command.Parameters.AddWithValue("@DocumentRef", documentRef);
                    command.Parameters.AddWithValue("@ExternalId", externalId);
                    command.Parameters.AddWithValue("@SkuId", skuId);
                    command.Parameters.AddWithValue("@ActualQuantity", actualQuantity);
                    command.Parameters.AddWithValue("@ActualBatchNumber", actualBatchNumber);
                    command.Parameters.AddWithValue("@ActualBestBeforeDate", actualBestBeforeDate);
                    command.Parameters.AddWithValue("@ReceivingBayName", receivingBayName);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// Finalizes a receiving task, updating the inbound delivery header status
        /// to 'Complete' or 'Partially Complete' based on the received quantities.
        /// </summary>
        /// <param name="inboundId">The ID of the inbound delivery to finalize.</param>
        /// <param name="userId">The ID of the user performing the action.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        public async Task<bool> FinalizeReceivingAsync(int inboundId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var command = new SqlCommand("deliveries.usp_FinalizeReceiving", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    command.Parameters.AddWithValue("@InboundId", inboundId);
                    command.Parameters.AddWithValue("@UserId", userId);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true; // Success
                }
            }
            catch (Exception)
            {
                // The SP will handle transaction rollback. Return false to indicate failure.
                return false;
            }
        }
        public async Task<(bool Success, string Message)> ImportInboundAsync(InboundDeliveryDto inbound)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Insert Header
                using var headerCmd = new SqlCommand("deliveries.usp_CreateInboundDelivery", connection, transaction);
                headerCmd.CommandType = CommandType.StoredProcedure;
                headerCmd.Parameters.AddWithValue("@DeliveryNumber", inbound.DeliveryNumber);
                headerCmd.Parameters.AddWithValue("@SupplierCode", inbound.SupplierCode);
                headerCmd.Parameters.AddWithValue("@ETA", inbound.EstimatedArrival);

                var deliveryId = Convert.ToInt32(await headerCmd.ExecuteScalarAsync());

                // Insert Lines
                foreach (var line in inbound.Lines)
                {
                    using var lineCmd = new SqlCommand("deliveries.usp_AddInboundLine", connection, transaction);
                    lineCmd.CommandType = CommandType.StoredProcedure;
                    lineCmd.Parameters.AddWithValue("@DeliveryId", deliveryId);
                    lineCmd.Parameters.AddWithValue("@SKU", line.SKU ?? (object)DBNull.Value);
                    lineCmd.Parameters.AddWithValue("@Quantity", line.Quantity);
                    lineCmd.Parameters.AddWithValue("@Status", line.Status ?? (object)DBNull.Value);
                    lineCmd.Parameters.AddWithValue("@ExternalId", line.ExternalId ?? (object)DBNull.Value);
                    lineCmd.Parameters.AddWithValue("@BatchNumber", line.BatchNumber ?? (object)DBNull.Value);
                    lineCmd.Parameters.AddWithValue("@BestBeforeDate", line.BestBeforeDate ?? (object)DBNull.Value);
                    lineCmd.Parameters.AddWithValue("@CreatedBy", "1"); // Always system user (id = 1)

                    await lineCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return (true, "Inbound delivery imported successfully.");
            }
            catch (Exception ex)
            {
                await LogInboundRetryAsync(connection, transaction, inbound, "FAILED", ex.Message);
                transaction.Rollback();
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task LogInboundImportAsync(SqlConnection conn, SqlTransaction? trx, string? docRef, string status, string? errorMessage, string? jsonPayload)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO logs.inbound_import_log (document_ref, status, error_message, payload)
                VALUES (@ref, @status, @error, @payload)", conn, trx);

            cmd.Parameters.AddWithValue("@ref", docRef ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@error", errorMessage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@payload", jsonPayload ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        private async Task AddToRetryQueueAsync(SqlConnection conn, string docRef, string jsonPayload, string error)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO logs.inbound_import_retry_queue 
                (document_ref, payload, last_error, retry_attempts, status)
                VALUES (@ref, @payload, @error, 0, 'PENDING')", conn);

            cmd.Parameters.AddWithValue("@ref", docRef);
            cmd.Parameters.AddWithValue("@payload", jsonPayload);
            cmd.Parameters.AddWithValue("@error", error);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task LogInboundRetryAsync(SqlConnection conn, SqlTransaction? trx, InboundDeliveryDto inbound, string status, string error)
        {
            var payload = JsonSerializer.Serialize(inbound);
            var docRef = inbound.DeliveryNumber ?? "UNKNOWN";

            await LogInboundImportAsync(conn, trx, docRef, status, error, payload);
            await AddToRetryQueueAsync(conn, docRef, payload, error);
        }
    }
}
