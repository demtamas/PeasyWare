using System;
using System.Threading.Tasks;
using PeasyWare.WMS.App.Data;
using PeasyWare.WMS.Console.Models;
using static System.Console;

namespace PeasyWare.WMS.Console.Services
{
    /// <summary>
    /// Provides functionality for the Stock Query feature.
    /// This service allows users to retrieve and display detailed information about a specific stock item.
    /// </summary>
    public class StockInquiryService
    {
        private readonly DatabaseService _dbService;

        /// <summary>
        /// Initializes a new instance of the StockInquiryService class.
        /// </summary>
        /// <param name="dbService">A reference to the database service for data access.</param>
        public StockInquiryService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// Runs the main loop for the Stock Query feature. It displays the result of the previous query
        /// before prompting for the next, creating a continuous workflow.
        /// </summary>
        public async Task RunStockQueryAsync()
        {
            // This variable will hold the result from the previous iteration of the loop.
            StockItemDetails? lastStockItem = null;
            string lastQueriedId = string.Empty;

            while (true)
            {
                Clear();
                ForegroundColor = ConsoleColor.Cyan;
                WriteLine("--- Stock Query ---");
                ResetColor();

                // --- 1. Display the result of the PREVIOUS query ---
                if (lastStockItem != null)
                {
                    DisplayStockItemDetails(lastStockItem);
                }
                else if (!string.IsNullOrEmpty(lastQueriedId))
                {
                    // If there was a previous query but no result, show the "not found" message.
                    ForegroundColor = ConsoleColor.Yellow;
                    WriteLine($"\nNo stock item found with ID: '{lastQueriedId}'");
                    ResetColor();
                }

                if (!string.IsNullOrEmpty(lastQueriedId))
                {
                    WriteLine("\n--------------------------------------");
                }

                // --- 2. Prompt for the NEXT query ---
                WriteLine("\nEnter Pallet ID to query (or press 'q' to return to menu):");
                Write("> ");

                string? inputId = ReadLine()?.Trim();

                if (string.Equals(inputId, "q", StringComparison.OrdinalIgnoreCase))
                {
                    break; // Exit the loop to go back to the main menu.
                }

                if (string.IsNullOrWhiteSpace(inputId))
                {
                    // If the user enters nothing, clear the previous results and restart the loop.
                    lastStockItem = null;
                    lastQueriedId = string.Empty;
                    continue;
                }

                // --- 3. Fetch data for the current query to be displayed in the NEXT loop iteration ---
                lastQueriedId = inputId;
                lastStockItem = await _dbService.GetStockDetailsByExternalIdAsync(inputId);
            }
        }

        /// <summary>
        /// Displays the details for a single stock item.
        /// </summary>
        /// <param name="stockItem">The stock item to display.</param>
        private void DisplayStockItemDetails(StockItemDetails stockItem)
        {
            ForegroundColor = ConsoleColor.Cyan;
            WriteLine("\n--- Stock Details Found ---");
            WriteLine($"  Pallet ID:    {stockItem.ExternalId}");
            WriteLine($"  Material:     {stockItem.CustomerSkuId} ({stockItem.SkuDescription})");
            WriteLine($"  Location:     {stockItem.CurrentLocation}");
            WriteLine($"  Status:       {stockItem.StatusCode} ({stockItem.StatusDescription})");
            WriteLine($"  Quantity:     {stockItem.Quantity} / {stockItem.FullUnitQty}");
            WriteLine($"  Batch:        {stockItem.BatchNumber}");
            WriteLine($"  Best Before:  {stockItem.BestBeforeDate:yyyy-MM-dd}");
            WriteLine($"  Reference:    {stockItem.DocumentRef}");
            ResetColor();
        }
    }
}
