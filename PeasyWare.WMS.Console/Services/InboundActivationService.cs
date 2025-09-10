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
    /// Manages the workflow for activating inbound deliveries.
    /// </summary>
    public class InboundActivationService
    {
        private readonly DatabaseService _dbService;
        private readonly ILogger _log = Log.ForContext<InboundActivationService>();

        public InboundActivationService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task RunActivationAsync()
        {
            _log.Information("Inbound Activation process started by user {User}", Session.CurrentUser?.Username);

            while (true)
            {
                Clear();
                WriteLine("--- Activate Inbound Delivery ---");

                // Get the list of deliveries that can be activated.
                var deliveries = await _dbService.GetActivatableInboundsAsync();

                if (deliveries.Count == 0)
                {
                    WriteLine("\nNo inbound deliveries are currently awaiting activation.");
                }
                else
                {
                    // Display the list to the user.
                    WriteLine("\nDeliveries Awaiting Activation:");
                    for (int i = 0; i < deliveries.Count; i++)
                    {
                        var d = deliveries[i];
                        WriteLine($"  {i + 1}. {d.DocumentRef} (Expected: {d.ExpectedArrivalDate:yyyy-MM-dd}, Status: {d.StatusDescription})");
                    }
                }

                WriteLine("\n----------------------------------------------------");
                WriteLine("Enter Delivery Ref or # to activate (or 'q' to return to menu):");
                Write("> ");
                string? input = ReadLine()?.Trim();

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase)) break;
                if (string.IsNullOrWhiteSpace(input)) continue;

                string? docToActivate = null;

                // Check if the user entered a list number (e.g., "1", "2").
                if (int.TryParse(input, out int choice) && choice > 0 && choice <= deliveries.Count)
                {
                    docToActivate = deliveries[choice - 1].DocumentRef;
                }
                else
                {
                    // Assume the user typed the full document reference.
                    docToActivate = input;
                }

                // Activate the selected delivery.
                if (Session.CurrentUser is null) { _log.Error("Critical error: Session lost."); break; }

                _log.Information("User {User} attempting to activate delivery {DocumentRef}", Session.CurrentUser.Username, docToActivate);
                bool success = await _dbService.ActivateInboundDeliveryAsync(docToActivate, Session.CurrentUser.UserId);

                if (success)
                {
                    ForegroundColor = ConsoleColor.Green;
                    WriteLine($"\nSuccess! Delivery '{docToActivate}' has been activated for receiving.");
                    _log.Information("Delivery {DocumentRef} activated successfully.", docToActivate);
                }
                else
                {
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine($"\nError: Could not activate delivery '{docToActivate}'. It may not exist or is already active.");
                    _log.Warning("Failed to activate delivery {DocumentRef}.", docToActivate);
                }
                ResetColor();
                WriteLine("Press any key to continue...");
                ReadKey();
            }
        }
    }
}
