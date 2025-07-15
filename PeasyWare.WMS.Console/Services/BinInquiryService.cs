using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PeasyWare.WMS.Console.Models;
using static System.Console;

namespace PeasyWare.WMS.Console.Services
{
    /// <summary>
    /// Provides functionality for the Bin Query feature, allowing users to view the contents of a warehouse location.
    /// </summary>
    public class BinInquiryService
    {
        private readonly DatabaseService _dbService;

        /// <summary>
        /// Initializes a new instance of the BinInquiryService class.
        /// </summary>
        /// <param name="dbService">A reference to the database service for data access.</param>
        public BinInquiryService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// Runs the main loop for the Bin Query feature. It displays the result of the previous query
        /// before prompting for the next, creating a continuous workflow.
        /// </summary>
        public async Task RunBinQueryAsync()
        {
            // These variables will hold the results from the previous iteration of the loop.
            List<StockItemDetails>? lastItemsResult = null;
            LocationDetails? lastLocationResult = null;
            string lastQueriedLocation = string.Empty;

            while (true)
            {
                Clear();
                ForegroundColor = ConsoleColor.Magenta;
                WriteLine("--- Bin Query ---");
                ResetColor();

                // --- 1. Display the result of the PREVIOUS query ---
                if (!string.IsNullOrEmpty(lastQueriedLocation))
                {
                    if (lastItemsResult != null && lastItemsResult.Count > 0)
                    {
                        if (lastItemsResult.Count == 1)
                        {
                            DisplaySingleItemDetails(lastItemsResult[0]);
                        }
                        else
                        {
                            // For multiple items, show the aggregated view first.
                            DisplayAggregatedView(lastItemsResult, lastQueriedLocation);
                            
                            // *** THE FIX IS HERE: Re-introducing the prompt for the detailed view. ***
                            Write("\nPress 'D' for details, or any other key for the next query... ");
                            var key = ReadKey(true); // 'true' hides the key press from the console
                            if (key.Key == ConsoleKey.D)
                            {
                                HandleDetailedNavigationView(lastItemsResult);
                                // After returning from the detailed view, we want a fresh prompt.
                                lastQueriedLocation = string.Empty; 
                                continue;
                            }
                        }
                    }
                    else
                    {
                        DisplayEmptyLocationDetails(lastLocationResult, lastQueriedLocation);
                    }
                    WriteLine("\n--------------------------------------");
                }

                // --- 2. Prompt for the NEXT query ---
                WriteLine("\nEnter Location Name to query (or press 'q' to return to menu):");
                Write("> ");
                string? inputLocation = ReadLine()?.Trim();

                if (string.Equals(inputLocation, "q", StringComparison.OrdinalIgnoreCase)) break;
                if (string.IsNullOrWhiteSpace(inputLocation))
                {
                    lastItemsResult = null;
                    lastLocationResult = null;
                    lastQueriedLocation = string.Empty;
                    continue;
                }
                
                // --- 3. Fetch data for the current query to be displayed in the NEXT loop iteration ---
                lastQueriedLocation = inputLocation;
                lastItemsResult = await _dbService.GetStockByLocationAsync(inputLocation);
                if (lastItemsResult.Count == 0)
                {
                    lastLocationResult = await _dbService.GetLocationDetailsAsync(inputLocation);
                }
            }
        }

        /// <summary>
        /// Displays the status details for a location that is empty or does not exist.
        /// </summary>
        private void DisplayEmptyLocationDetails(LocationDetails? locDetails, string queriedLocation)
        {
            if (locDetails is null)
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine($"\nLocation '{queriedLocation}' does not exist.");
            }
            else
            {
                ForegroundColor = ConsoleColor.Yellow;
                WriteLine($"\n--- Location '{locDetails.LocationName}' is Empty ---");
                Write("  Status:    ");
                if (locDetails.IsActive)
                {
                    ForegroundColor = ConsoleColor.Green;
                    WriteLine("Available");
                }
                else
                {
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine("Blocked");
                }
                ResetColor();
                if (!locDetails.IsActive && !string.IsNullOrWhiteSpace(locDetails.Notes))
                {
                    ForegroundColor = ConsoleColor.Yellow;
                    WriteLine($"  Notes:     {locDetails.Notes}");
                }
                WriteLine($"  Capacity:  {locDetails.CapacityUsed} / {locDetails.CapacityTotal}");
                WriteLine($"  Reserved:  {(locDetails.IsReservedForPutaway ? "Yes" : "No")}");
            }
            ResetColor();
        }

        /// <summary>
        /// Displays the full details for a single stock item.
        /// </summary>
        private void DisplaySingleItemDetails(StockItemDetails item)
        {
            ForegroundColor = ConsoleColor.Cyan;
            WriteLine("\n--- Location Contents ---");
            WriteLine($"  Pallet ID:    {item.ExternalId}");
            WriteLine($"  Material:     {item.CustomerSkuId} ({item.SkuDescription})");
            WriteLine($"  Status:       {item.StatusCode} ({item.StatusDescription})");
            WriteLine($"  Quantity:     {item.Quantity}");
            WriteLine($"  Batch:        {item.BatchNumber}");
            WriteLine($"  Best Before:  {item.BestBeforeDate:yyyy-MM-dd}");
            ResetColor();
        }

        /// <summary>
        /// Displays an aggregated summary view for a location with multiple stock items.
        /// </summary>
        private void DisplayAggregatedView(List<StockItemDetails> items, string locationName)
        {
            ForegroundColor = ConsoleColor.Cyan;
            WriteLine($"\n--- Location Contents: {items.Count} Items ---");
            WriteLine($"  Location:      {locationName}");
            WriteLine($"  Total Pallets: {items.Count}");
            WriteLine($"  Total Quantity:{items.Sum(i => i.Quantity)}");
            ResetColor();
        }

        /// <summary>
        /// Manages the user interface for navigating through a list of stock items one by one.
        /// </summary>
        private void HandleDetailedNavigationView(List<StockItemDetails> items)
        {
            int currentIndex = 0;
            while (true)
            {
                Clear();
                var currentItem = items[currentIndex];
                ForegroundColor = ConsoleColor.Yellow;
                WriteLine($"--- Detailed View: Item {currentIndex + 1} of {items.Count} ---");
                ResetColor();
                // Reuse the single item display method, but add the location back for context.
                ForegroundColor = ConsoleColor.Cyan;
                WriteLine("\n--- Item Details ---");
                WriteLine($"  Location:     {currentItem.CurrentLocation}");
                WriteLine($"  Pallet ID:    {currentItem.ExternalId}");
                WriteLine($"  Material:     {currentItem.CustomerSkuId} ({currentItem.SkuDescription})");
                WriteLine($"  Status:       {currentItem.StatusCode} ({currentItem.StatusDescription})");
                WriteLine($"  Quantity:     {currentItem.Quantity}");
                WriteLine($"  Batch:        {currentItem.BatchNumber}");
                WriteLine($"  Best Before:  {currentItem.BestBeforeDate:yyyy-MM-dd}");
                WriteLine("--------------------\n");
                ResetColor();

                WriteLine("(N)ext  |  (P)revious  |  (B)ack");
                Write("> ");
                var navKey = ReadKey(true).Key;

                if (navKey == ConsoleKey.N)
                {
                    currentIndex = Math.Min(items.Count - 1, currentIndex + 1);
                }
                else if (navKey == ConsoleKey.P)
                {
                    currentIndex = Math.Max(0, currentIndex - 1);
                }
                else if (navKey == ConsoleKey.B)
                {
                    break; // Exit the detailed view loop.
                }
            }
        }
    }
}