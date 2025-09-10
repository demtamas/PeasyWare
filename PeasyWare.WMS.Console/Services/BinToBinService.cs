using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PeasyWare.WMS.App.Data;
using PeasyWare.WMS.Console.Models;
using Serilog;
using static System.Console;

namespace PeasyWare.WMS.Console.Services
{
    /// <summary>
    /// Manages the Bin to Bin Movement workflow.
    /// </summary>
    public class BinToBinService
    {
        private readonly DatabaseService _dbService;
        private readonly ILogger _log = Log.ForContext<BinToBinService>();

        public BinToBinService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task RunBinToBinMovementAsync()
        {
            _log.Information("Bin to Bin Movement process started by user {User}", Session.CurrentUser?.Username);
            
            while (true)
            {
                // --- 1. Get and Lock Pallet ---
                var pallet = await GetAndLockPalletAsync();
                if (pallet is null)
                {
                    // User chose to quit.
                    break;
                }
                
                // --- 2. Get Target Location and Finalize ---
                await GetTargetAndFinalizeAsync(pallet);
            }
        }

        private async Task<StockItemDetails?> GetAndLockPalletAsync()
        {
            while(true)
            {
                Clear();
                WriteLine("--- Bin to Bin Movement ---");
                WriteLine("\nScan Pallet ID to move (or press 'q' to return to menu):");
                Write("> ");
                string? inputId = ReadLine()?.Trim();

                if (string.Equals(inputId, "q", StringComparison.OrdinalIgnoreCase)) return null;
                if (string.IsNullOrWhiteSpace(inputId)) continue;

                // First, validate the pallet without locking it.
                var palletDetails = await _dbService.GetStockDetailsByExternalIdAsync(inputId);
                if (palletDetails is null)
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine($"Error: Pallet with ID '{inputId}' not found."); ResetColor();
                    ReadKey(); continue;
                }
                var invalidStatuses = new List<string> { "MV", "AL", "EX", "OU" };
                if (invalidStatuses.Contains(palletDetails.StatusCode))
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine($"Error: Pallet cannot be moved. Status is '{palletDetails.StatusDescription}'."); ResetColor();
                    ReadKey(); continue;
                }

                // If validation passes, now try to lock it.
                if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return null; }
                bool initiated = await _dbService.InitiateBinToBinMoveAsync(palletDetails.ExternalId, Session.CurrentUser.UserId);
                if (!initiated)
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine("\nError: Could not initiate move. The pallet status may have changed."); ResetColor();
                    await Task.Delay(2000); continue;
                }
                
                _log.Information("Pallet {PalletId} status set to 'MV' for Bin to Bin move.", palletDetails.ExternalId);
                return palletDetails;
            }
        }

        private async Task GetTargetAndFinalizeAsync(StockItemDetails pallet)
        {
            while(true)
            {
                Clear();
                WriteLine("--- Bin to Bin Movement: Target ---");
                DisplayPalletSummary(pallet);
                
                WriteLine($"\nScan TARGET location for pallet '{pallet.ExternalId}':");
                WriteLine("(Or press 'c' to cancel this move)");
                Write("> ");
                string? targetLocationName = ReadLine()?.Trim();

                if (string.Equals(targetLocationName, "c", StringComparison.OrdinalIgnoreCase))
                {
                    if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return; }
                    // Use the generic CancelPutaway procedure, as it correctly reverts the 'MV' status.
                    await _dbService.CancelPutawayAsync(pallet.ExternalId, Session.CurrentUser.UserId);
                    WriteLine("\nMove cancelled.");
                    await Task.Delay(1500);
                    return;
                }

                if (string.IsNullOrWhiteSpace(targetLocationName)) continue;

                // --- Initiate the reservation ---
                if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return; }
                bool reserved = await _dbService.AssignAndReserveBinToBinMoveAsync(pallet.ExternalId, targetLocationName, Session.CurrentUser.UserId);

                if (!reserved)
                {
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine("\nError: Target location is invalid, blocked, or full.");
                    ResetColor();
                    WriteLine("Press any key to try a different location...");
                    ReadKey();
                    continue; // Loop back to ask for a new target location
                }

                // --- Final Confirmation ---
                await HandleConfirmation(pallet, targetLocationName);
                return; // Exit this sub-workflow and go back to scanning the next pallet.
            }
        }

        private async Task HandleConfirmation(StockItemDetails pallet, string targetLocationName)
        {
             // This confirmation step is now much simpler.
             Clear();
             WriteLine("--- Bin to Bin Confirmation ---");
             DisplayPalletSummary(pallet);
             ForegroundColor = ConsoleColor.Cyan;
             WriteLine($"\nMove To:   {targetLocationName}");
             ResetColor();
             
             WriteLine("\n----------------------------------------------------");
             WriteLine($"Scan '{targetLocationName}' to CONFIRM move.");
             WriteLine("Press 'C' to CANCEL task.");
             WriteLine("----------------------------------------------------");
             Write("> ");
             string? confirmation = ReadLine()?.Trim();

             if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); return; }

             if (string.Equals(confirmation, targetLocationName, StringComparison.OrdinalIgnoreCase))
             {
                 bool finalized = await _dbService.FinalizePutawayAsync(pallet.ExternalId, targetLocationName, Session.CurrentUser.UserId);
                 if (finalized) { ForegroundColor = ConsoleColor.Green; WriteLine("\nSuccess! Move complete."); }
                 else { ForegroundColor = ConsoleColor.Red; WriteLine("\nError: Could not finalize move. Please try again."); }
             }
             else
             {
                 // Any incorrect scan or 'C' will cancel the move.
                 await _dbService.CancelPutawayAsync(pallet.ExternalId, Session.CurrentUser.UserId);
                 WriteLine("\nMove cancelled.");
             }
             ResetColor();
             await Task.Delay(2000);
        }

        private void DisplayPalletSummary(StockItemDetails pallet)
        {
            ForegroundColor = ConsoleColor.Yellow;
            WriteLine($"\nMoving Pallet: {pallet.ExternalId}");
            WriteLine($"  - Material:    {pallet.SkuDescription}");
            WriteLine($"  - Quantity:    {pallet.Quantity}");
            WriteLine($"  - From:        {pallet.CurrentLocation}");
            ResetColor();
        }
    }
}