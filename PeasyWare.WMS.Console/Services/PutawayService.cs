using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PeasyWare.WMS.Console.Models;
using Serilog;
using static System.Console;

namespace PeasyWare.WMS.Console.Services
{
    /// <summary>
    /// Manages the end-to-end Guided Putaway workflow.
    /// This service handles pallet validation, location suggestion, task initiation, modification, cancellation, and finalization.
    /// </summary>
    public class PutawayService
    {
        private readonly DatabaseService _dbService;
        private readonly SessionConfig _config;
        private readonly ILogger _log = Log.ForContext<PutawayService>();

        /// <summary>
        /// Initializes a new instance of the PutawayService class.
        /// </summary>
        /// <param name="dbService">A reference to the database service for data access.</param>
        /// <param name="config">A reference to the application's session configuration settings.</param>
        public PutawayService(DatabaseService dbService, SessionConfig config)
        {
            _dbService = dbService;
            _config = config;
        }

        /// <summary>
        /// Runs the main loop for the Guided Putaway feature, prompting the user to scan pallets.
        /// </summary>
        public async Task RunGuidedPutawayAsync()
        {
            _log.Information("Guided Putaway process started by user {User}", Session.CurrentUser?.Username);

            while (true) // This is the main loop for scanning a new pallet.
            {
                Clear();
                WriteLine("--- Guided Putaway ---");
                WriteLine("\nScan Pallet ID to put away (or press 'q' to return to menu):");
                Write("> ");
                string? inputId = ReadLine()?.Trim();

                if (string.Equals(inputId, "q", StringComparison.OrdinalIgnoreCase)) break;
                if (string.IsNullOrWhiteSpace(inputId)) continue;

                // Step 1: Validate the scanned pallet to ensure it can be moved.
                var validationResult = await ValidatePalletForPutaway(inputId);
                if (!validationResult.IsValid)
                {
                    _log.Warning("Pallet {PalletId} failed validation: {Reason}", inputId, validationResult.Message);
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine($"Validation Failed: {validationResult.Message}");
                    ResetColor();
                    WriteLine("Press any key to scan the next pallet...");
                    ReadKey();
                    continue;
                }

                var pallet = validationResult.StockItem!;
                _log.Information("Pallet {PalletId} validated successfully.", pallet.ExternalId);

                SuggestedLocation? suggestion;

                // Step 2: Differentiate between resuming an existing task and creating a new one.
                if (pallet.StatusCode == "MV")
                {
                    // --- PATH 1: RESUME EXISTING TASK ---
                    _log.Information("Pallet {PalletId} is already in transit. Resuming existing task.", pallet.ExternalId);
                    var activeTask = await _dbService.GetActiveTaskForPalletAsync(pallet.ExternalId);
                    if (activeTask is null)
                    {
                        _log.Error("Data inconsistency: Pallet {PalletId} is 'MV' but has no active reservation.", pallet.ExternalId);
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine("Error: Pallet is in transit but its task could not be found.");
                        ResetColor();
                        ReadKey();
                        continue;
                    }
                    suggestion = activeTask.Suggestion;
                }
                else
                {
                    // --- PATH 2: CREATE NEW TASK ---
                    _log.Debug("Requesting new putaway suggestion for pallet {PalletId}", pallet.ExternalId);
                    suggestion = await _dbService.GetPutawaySuggestionAsync(pallet.PreferredStorageType, pallet.PreferredSectionId, pallet.LocationId);

                    if (suggestion is null)
                    {
                        _log.Warning("Stored procedure returned no suitable location for pallet {PalletId}", pallet.ExternalId);
                        WriteLine("System could not find a suitable location for this product.");
                        ReadKey();
                        continue;
                    }

                    if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); break; }

                    // Initiate the move in the database, creating a reservation and setting status to 'MV'.
                    bool success = await _dbService.InitiatePutawayAsync(pallet.ExternalId, suggestion.LocationId, Session.CurrentUser.UserId);
                    if (!success)
                    {
                        _log.Error("Failed to initiate putaway transaction for pallet {PalletId}", pallet.ExternalId);
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine("\nError: Could not initiate putaway. Please try again.");
                        ResetColor();
                        ReadKey();
                        continue;
                    }
                }

                // Step 3: Both paths lead to the confirmation screen where the operator takes action.
                await HandleConfirmation(pallet, suggestion);
            }
        }

        /// <summary>
        /// Manages the user interaction for confirming, canceling, or modifying a putaway task.
        /// </summary>
        /// <param name="pallet">The stock item being put away.</param>
        /// <param name="suggestion">The system-suggested destination location.</param>
        private async Task HandleConfirmation(StockItemDetails pallet, SuggestedLocation suggestion)
        {
            while (true)
            {
                Clear();
                WriteLine("--- Putaway Task ---");
                ForegroundColor = ConsoleColor.Yellow;
                WriteLine($"\nPallet:   {pallet.ExternalId}");
                WriteLine($"Material: {pallet.SkuDescription}");
                string palletType = (pallet.Quantity == pallet.FullUnitQty) ? "Full Pallet" : "Partial Pallet";
                WriteLine($"Quantity: {pallet.Quantity} / {pallet.FullUnitQty} ({palletType})");
                ResetColor();

                ForegroundColor = ConsoleColor.Cyan;
                WriteLine($"\nPROCEED TO LOCATION: {suggestion.LocationName} ({suggestion.SectionName})");
                ResetColor();

                WriteLine("\n----------------------------------------------------");
                WriteLine($"Scan '{suggestion.LocationName}' to CONFIRM arrival.");
                if (_config.AllowPutawayModification)
                {
                    WriteLine("Press 'C' to CANCEL task or 'M' to MODIFY.");
                }
                else
                {
                    WriteLine("Press 'C' to CANCEL task.");
                }
                WriteLine("----------------------------------------------------");
                Write("> ");
                string? confirmation = ReadLine()?.Trim();

                // Path 1: User confirms the move by scanning the location.
                if (string.Equals(confirmation, suggestion.LocationName, StringComparison.OrdinalIgnoreCase))
                {
                    if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return; }
                    bool finalized = await _dbService.FinalizePutawayAsync(pallet.ExternalId, suggestion.LocationName, Session.CurrentUser.UserId);
                    if (finalized)
                    {
                        ForegroundColor = ConsoleColor.Green;
                        WriteLine("\nSuccess! Putaway complete.");
                    }
                    else
                    {
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine("\nFinalization Failed: The destination location may now be occupied or blocked.");
                        WriteLine("The task has been cancelled. Please re-scan the pallet to get a new task.");
                        await _dbService.CancelPutawayAsync(pallet.ExternalId, Session.CurrentUser.UserId);
                    }
                    ResetColor();
                    await Task.Delay(2000);
                    return;
                }
                // Path 2: User cancels the task.
                else if (string.Equals(confirmation, "c", StringComparison.OrdinalIgnoreCase))
                {
                    if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return; }
                    bool cancelled = await _dbService.CancelPutawayAsync(pallet.ExternalId, Session.CurrentUser.UserId);
                    if (cancelled)
                    {
                        ForegroundColor = ConsoleColor.Green;
                        WriteLine("\nSuccess! Putaway task has been cancelled.");
                    }
                    else
                    {
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine("\nError: Could not cancel putaway task.");
                    }
                    ResetColor();
                    await Task.Delay(2000);
                    return;
                }
                // Path 3: User modifies the destination.
                else if (_config.AllowPutawayModification && string.Equals(confirmation, "m", StringComparison.OrdinalIgnoreCase))
                {
                    WriteLine("\nScan new destination location:");
                    Write("> ");
                    string? newLocationName = ReadLine()?.Trim();

                    if (string.IsNullOrWhiteSpace(newLocationName)) continue;

                    var manualValidation = await ValidateManualLocationAsync(newLocationName, pallet);
                    if (!manualValidation.IsValid)
                    {
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine($"\nValidation Failed: {manualValidation.Message}");
                        ResetColor();
                        await Task.Delay(2500);
                        continue;
                    }

                    if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return; }

                    bool modified = await _dbService.ModifyPutawayAsync(pallet.ExternalId, manualValidation.StockItem!.LocationId, Session.CurrentUser.UserId);
                    if (modified)
                    {
                        var newSuggestionDetails = await _dbService.GetActiveTaskForPalletAsync(pallet.ExternalId);
                        suggestion = newSuggestionDetails!.Suggestion;
                        _log.Information("Modification successful. New task for pallet {PalletId} to location {NewLoc}", pallet.ExternalId, newLocationName);
                        continue; // Loop again to show the NEW task to the user.
                    }
                    else
                    {
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine("\nError: Could not modify task. Please re-scan pallet from the start.");
                        ResetColor();
                        await Task.Delay(2500);
                        return;
                    }
                }
                // Path 4: User input is invalid.
                else
                {
                    ForegroundColor = ConsoleColor.Red;
                    if (!string.IsNullOrWhiteSpace(confirmation))
                    {
                        WriteLine($"\nInvalid Scan. Expected '{suggestion.LocationName}', but received '{confirmation}'.");
                    }
                    else
                    {
                        WriteLine("\nInvalid command. Please scan the location or press 'C' or 'M'.");
                    }
                    WriteLine("Please try again.");
                    ResetColor();
                    await Task.Delay(3000);
                }
            }
        }

        /// <summary>
        /// Validates a manually entered location to ensure it is a safe and valid destination for a putaway.
        /// </summary>
        /// <param name="locationName">The name of the location to validate.</param>
        /// <param name="pallet">The pallet being put away (not currently used, but available for future rules).</param>
        /// <returns>A ValidationResult indicating if the location is valid.</returns>
        private async Task<ValidationResult> ValidateManualLocationAsync(string locationName, StockItemDetails pallet)
        {
            var locDetails = await _dbService.GetLocationDetailsAsync(locationName);
            if (locDetails is null) return ValidationResult.Fail("Location does not exist.");
            if (!locDetails.IsActive) return ValidationResult.Fail($"Location '{locationName}' is not active.");
            if (locDetails.AllowConcurrentPutaway)
            {
                if ((locDetails.CapacityUsed + locDetails.ReservedCount) >= locDetails.CapacityTotal)
                    return ValidationResult.Fail($"Location '{locationName}' is at full capacity.");
            }
            else
            {
                if (locDetails.IsReservedForPutaway) return ValidationResult.Fail($"Location '{locationName}' is already reserved for another task.");
                if (locDetails.CapacityUsed > 0) return ValidationResult.Fail($"Location '{locationName}' is not empty.");
            }
            return ValidationResult.Success(new StockItemDetails { LocationId = locDetails.LocationId });
        }

        /// <summary>
        /// Performs initial validation on a scanned pallet to ensure it is eligible for a putaway task.
        /// </summary>
        /// <param name="externalId">The external ID of the pallet to validate.</param>
        /// <returns>A ValidationResult indicating if the pallet is valid.</returns>
        private async Task<ValidationResult> ValidatePalletForPutaway(string externalId)
        {
            var pallet = await _dbService.GetStockDetailsByExternalIdAsync(externalId);
            if (pallet is null) return ValidationResult.Fail($"Pallet with ID '{externalId}' not found.");
            var invalidStatuses = new List<string> { "AL", "EX", "OU" };
            if (invalidStatuses.Contains(pallet.StatusCode)) return ValidationResult.Fail($"Pallet cannot be moved. Status is '{pallet.StatusDescription}'.");
            if (pallet.StatusCode == "MV")
            {
                var reservation = await _dbService.GetReservationForPalletAsync(externalId);
                if (reservation is null) return ValidationResult.Fail("Pallet is In Transit but has no active destination reservation.");
                if (reservation.ReservedByUserId != Session.CurrentUser?.UserId) return ValidationResult.Fail($"Pallet move is reserved by another user (ID: {reservation.ReservedByUserId}).");
            }
            return ValidationResult.Success(pallet);
        }
    }
}
