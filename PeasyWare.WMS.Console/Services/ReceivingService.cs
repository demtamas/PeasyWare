using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PeasyWare.WMS.App.Data;
using PeasyWare.WMS.Console.Models;
using Serilog;
using static System.Console;

namespace PeasyWare.WMS.Console.Services
{
    /// <summary>
    /// Manages the workflow for receiving inbound deliveries, including amending details and handling partial receipts.
    /// </summary>
    public class ReceivingService
    {
        private readonly DatabaseService _dbService;
        private readonly SessionConfig _config;
        private readonly ILogger _log = Log.ForContext<ReceivingService>();

        public ReceivingService(DatabaseService dbService, SessionConfig config)
        {
            _dbService = dbService;
            _config = config;
        }

        /// <summary>
        /// Runs the main entry point for the receiving feature.
        /// </summary>
        public async Task RunReceivingAsync()
        {
            _log.Information("Receiving process started by user {User}", Session.CurrentUser?.Username);

            var selectedDelivery = await SelectReceivableDelivery();
            if (selectedDelivery is null) return;

            var selectedBay = await GetAndValidateReceivingBayAsync();
            if (selectedBay is null) return;

            await ProcessDelivery(selectedDelivery, selectedBay);
        }

        private async Task<ReceivableDelivery?> SelectReceivableDelivery()
        {
            while (true)
            {
                Clear();
                WriteLine("--- Receiving: Select Delivery ---");
                var deliveries = await _dbService.GetReceivableDeliveriesAsync();

                if (deliveries.Count == 0)
                {
                    WriteLine("\nNo deliveries are currently active for receiving.");
                    WriteLine("Press any key to return to the main menu...");
                    ReadKey();
                    return null;
                }

                WriteLine("\nSelect a delivery to receive:");
                for (int i = 0; i < deliveries.Count; i++)
                {
                    var d = deliveries[i];
                    if (d.StatusDescription != "Arrived" && d.StatusDescription != "Expected")
                    {
                        ForegroundColor = ConsoleColor.Yellow;
                        WriteLine($"  {i + 1}. {d.DocumentRef} (Expected: {d.ExpectedArrivalDate:yyyy-MM-dd}) - {d.StatusDescription}");
                        ResetColor();
                    }
                    else
                    {
                         WriteLine($"  {i + 1}. {d.DocumentRef} (Expected: {d.ExpectedArrivalDate:yyyy-MM-dd})");
                    }
                }

                WriteLine("\n----------------------------------------------------");
                WriteLine("Enter # to select (or 'q' to return to menu):");
                Write("> ");
                string? input = ReadLine()?.Trim();

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase)) return null;

                if (int.TryParse(input, out int choice) && choice > 0 && choice <= deliveries.Count)
                {
                    return deliveries[choice - 1];
                }
                else
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine("Invalid selection. Please try again."); ResetColor();
                    await Task.Delay(1500);
                }
            }
        }
        
        private async Task<string?> GetAndValidateReceivingBayAsync()
        {
            while (true)
            {
                Clear();
                WriteLine("--- Receiving: Scan Bay ---");
                WriteLine("\nScan the receiving bay location (or press 'q' to return to menu):");
                Write("> ");
                string? bayName = ReadLine()?.Trim();

                if (string.Equals(bayName, "q", StringComparison.OrdinalIgnoreCase)) return null;
                if (string.IsNullOrWhiteSpace(bayName)) continue;

                var bayDetails = await _dbService.GetLocationDetailsAsync(bayName);

                if (bayDetails is null) { ForegroundColor = ConsoleColor.Red; WriteLine($"Error: Location '{bayName}' does not exist."); ResetColor(); await Task.Delay(2000); continue; }
                if (!bayDetails.IsActive) { ForegroundColor = ConsoleColor.Red; WriteLine($"Error: Location '{bayName}' is blocked."); ResetColor(); await Task.Delay(2000); continue; }
                if (bayDetails.TypeId != 1 && bayDetails.TypeId != 2) { ForegroundColor = ConsoleColor.Red; WriteLine($"Error: Location '{bayName}' is not a valid receiving bay."); ResetColor(); await Task.Delay(2000); continue; }

                if (bayDetails.CapacityUsed > 0)
                {
                    ForegroundColor = ConsoleColor.DarkYellow;
                    WriteLine($"\nWarning: Location '{bayName}' is not empty ({bayDetails.CapacityUsed} pallet(s) present).");
                    ResetColor();
                    Write("Do you want to proceed with receiving into this bay? (Y/N): ");
                    var confirmKey = ReadKey(true);
                    if (confirmKey.Key != ConsoleKey.Y)
                    {
                        WriteLine("\nSelection cancelled. Please choose another bay.");
                        await Task.Delay(2000);
                        continue;
                    }
                }
                return bayName;
            }
        }

        private async Task ProcessDelivery(ReceivableDelivery delivery, string receivingBay)
        {
            while(true)
            {
                var inboundRows = await _dbService.GetInboundRowsAsync(delivery.InboundId);
                
                var totalExpected = inboundRows.Sum(r => r.ExpectedQty);
                var totalReceived = inboundRows.Sum(r => r.ReceivedQty);

                if (totalReceived >= totalExpected)
                {
                    ForegroundColor = ConsoleColor.Green;
                    WriteLine($"\nDelivery '{delivery.DocumentRef}' is fully received.");
                    ResetColor();
                    WriteLine("Press any key to return to the main menu...");
                    ReadKey();
                    break;
                }

                DisplayReceivingScreen(delivery, receivingBay, inboundRows);

                WriteLine("\nScan Pallet ID to receive (or 'b' to go back/finish):");
                Write("> ");
                string? palletId = ReadLine()?.Trim();

                if (string.Equals(palletId, "b", StringComparison.OrdinalIgnoreCase)) 
                {
                    // --- THE FIX IS HERE: Give the user control over finishing the task. ---
                    if (totalReceived > 0)
                    {
                        ForegroundColor = ConsoleColor.Yellow;
                        Write("\nAre you finished receiving this delivery? (Y/N): ");
                        var confirmKey = ReadKey(true);
                        WriteLine(); // Move to the next line after key press

                        if (confirmKey.Key == ConsoleKey.Y)
                        {
                            // User confirms they are done. Finalize the delivery status.
                            if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); break; }
                            await _dbService.FinalizeReceivingAsync(delivery.InboundId, Session.CurrentUser.UserId);
                            _log.Information("User finished receiving for delivery {DocumentRef}. Final status set.", delivery.DocumentRef);

                            if (totalReceived < totalExpected)
                            {
                                WriteLine("\nDelivery marked as 'Partially Complete'. Please notify your manager of any discrepancies.");
                            }
                            else
                            {
                                WriteLine("\nDelivery marked as 'Complete'.");
                            }
                            WriteLine("Press any key to return to the main menu...");
                            ReadKey();
                        }
                        else
                        {
                            // User wants to resume later. Do nothing and just exit.
                            WriteLine("\nResuming later. Returning to the main menu.");
                            await Task.Delay(2000);
                        }
                    }
                    break; // Exit the receiving loop in all 'b' cases.
                }
                if (string.IsNullOrWhiteSpace(palletId)) continue;

                var expectedRow = inboundRows.FirstOrDefault(r => r.ExternalId.Equals(palletId, StringComparison.OrdinalIgnoreCase));

                if (expectedRow is null)
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine($"Error: Pallet '{palletId}' is not expected on this delivery."); ResetColor();
                    await Task.Delay(2000); continue;
                }

                if (expectedRow.ReceivedQty >= expectedRow.ExpectedQty)
                {
                    ForegroundColor = ConsoleColor.Yellow; WriteLine($"Warning: Pallet '{palletId}' has already been fully received."); ResetColor();
                    await Task.Delay(2000); continue;
                }

                var confirmedDetails = await VerifyAndAmendDetails(expectedRow);

                if (confirmedDetails is null)
                {
                    WriteLine("\nReceiving for this pallet cancelled."); await Task.Delay(1500); continue;
                }

                if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); break; }
                
                _log.Information("User {User} receiving pallet {PalletId} against delivery {DocumentRef}", Session.CurrentUser.Username, palletId, delivery.DocumentRef);
                
                bool success = await _dbService.ReceivePalletAsync(
                    delivery.DocumentRef, palletId, confirmedDetails.SkuId, confirmedDetails.ExpectedQty,
                    confirmedDetails.BatchNumber, confirmedDetails.BestBeforeDate, receivingBay, Session.CurrentUser.UserId
                );

                if (success)
                {
                    ForegroundColor = ConsoleColor.Green; WriteLine($"\nSuccess! Pallet '{palletId}' received into {receivingBay}.");
                    _log.Information("Pallet {PalletId} received successfully.", palletId);
                }
                else
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine("\nError: Failed to receive pallet. A database error occurred.");
                    _log.Error("ReceivePallet transaction failed for pallet {PalletId}", palletId);
                }
                ResetColor();
                await Task.Delay(2500);
            }
        }
        
        private async Task<InboundRow?> VerifyAndAmendDetails(InboundRow expected)
        {
            // Create a mutable copy of the expected details to allow for changes.
            var confirmed = new InboundRow
            {
                SkuId = expected.SkuId, SkuDescription = expected.SkuDescription, ExpectedQty = expected.ExpectedQty,
                BatchNumber = expected.BatchNumber, BestBeforeDate = expected.BestBeforeDate, ExternalId = expected.ExternalId
            };

            while (true)
            {
                Clear();
                WriteLine("--- Verify & Amend Details ---");
                ForegroundColor = ConsoleColor.Yellow;
                WriteLine($"\nSKU:        {confirmed.SkuDescription}");
                WriteLine($"Batch:      {confirmed.BatchNumber}");
                WriteLine($"BBE:        {confirmed.BestBeforeDate:yyyy-MM-dd}");
                WriteLine($"Quantity:   {confirmed.ExpectedQty}");
                ResetColor();
                WriteLine("\n----------------------------------------------------");
                WriteLine($"Scan pallet '{confirmed.ExternalId}' again to CONFIRM, or choose an option:");
                WriteLine("(Q)uantity | (B)atch | B(E)st Before | (C)ancel");
                Write("> ");

                string? input = ReadLine()?.Trim();

                if (string.Equals(input, confirmed.ExternalId, StringComparison.OrdinalIgnoreCase))
                {
                    return confirmed;
                }
                else if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                {
                    // --- THE FIX IS HERE: Add validation for the quantity input ---
                    Write("Enter new Quantity: ");
                    if (int.TryParse(ReadLine(), out int newQty) && newQty > 0)
                    {
                        // Only update if the parsed number is a positive integer.
                        confirmed.ExpectedQty = newQty;
                    }
                    else
                    {
                        // Show an error message if the input is invalid.
                        ForegroundColor = ConsoleColor.Red;
                        WriteLine("Invalid input. Quantity must be a whole number greater than 0.");
                        ResetColor();
                        await Task.Delay(2000);
                    }
                }
                else if (string.Equals(input, "b", StringComparison.OrdinalIgnoreCase))
                {
                    Write("Enter new Batch Number: ");
                    confirmed.BatchNumber = ReadLine()?.Trim() ?? confirmed.BatchNumber;
                }
                else if (string.Equals(input, "e", StringComparison.OrdinalIgnoreCase))
                {
                    Write("Enter new BBE (YYYY-MM-DD): ");
                    if (DateTime.TryParse(ReadLine(), out DateTime newBbe)) confirmed.BestBeforeDate = newBbe;
                }
                else if (string.Equals(input, "c", StringComparison.OrdinalIgnoreCase))
                {
                    return null; // User cancelled.
                }
                else
                {
                    ForegroundColor = ConsoleColor.Red; WriteLine("Invalid input."); ResetColor();
                    await Task.Delay(1500);
                }
            }
        }

        private void DisplayReceivingScreen(ReceivableDelivery delivery, string receivingBay, List<InboundRow> rows)
        {
            Clear();
            WriteLine($"--- Receiving Delivery: {delivery.DocumentRef} into {receivingBay} ---");
            var totalExpected = rows.Sum(r => r.ExpectedQty);
            var totalReceived = rows.Sum(r => r.ReceivedQty);
            WriteLine($"\nStatus: {totalReceived} / {totalExpected} units received.");
            WriteLine("----------------------------------------------------");
            if (_config.ShowExpectedOnReceive)
            {
                WriteLine("Expected Items:");
                foreach (var row in rows)
                {
                    var statusColor = row.ReceivedQty >= row.ExpectedQty ? ConsoleColor.Green : ConsoleColor.Yellow;
                    ForegroundColor = statusColor;
                    WriteLine($"  - {row.SkuDescription,-25} | Pallet: {row.ExternalId,-20} | Qty: {row.ReceivedQty}/{row.ExpectedQty}");
                }
                ResetColor();
                WriteLine("----------------------------------------------------");
            }
        }
    }
}
