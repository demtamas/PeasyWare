using Serilog;
using PeasyWare.WMS.Console.UI;
using PeasyWare.WMS.Console.Services;
using PeasyWare.WMS.Console.Utilities;
using PeasyWare.WMS.Console.Models;

/// <summary>
/// The main entry point for the PeasyWare WMS Console Application.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // --- 1. INITIALIZATION ---
        var dbService = new DatabaseService();
        var config = await dbService.LoadConfigurationAsync();
        var stockInquiryService = new StockInquiryService(dbService);
        var binInquiryService = new BinInquiryService(dbService);
        var binToBinService = new BinToBinService(dbService); 
        var putawayService = new PutawayService(dbService, config);

        // This changes localIpAddress from a nullable 'string?' to a non-nullable 'string'.
        string localIpAddress = NetworkHelper.GetLocalIpAddress() ?? "UNKNOWN_IP";

        // --- 2. LOGGER SETUP ---
        LogManager.Initialize(config.EnableDebugging);

        try
        {
            Log.Information("Application starting up on machine {MachineName}", Environment.MachineName);

            if (!config.EnableLogin)
            {
                Log.Warning("Login is disabled via settings. Shutting down.");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("System is currently locked for maintenance. Please contact an administrator.");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            // --- 3. LOGIN FLOW ---
            int loginAttempts = 0;
            const int maxLoginAttempts = 3;
            while (loginAttempts < maxLoginAttempts && !Session.IsUserLoggedIn())
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PeasyWare WMS - Login");
                Console.ResetColor();
                Console.WriteLine("-------------------------------------");

                Console.Write("Username: ");
                string? username = Console.ReadLine();
                Console.Write("Password: ");
                string? password = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    Console.WriteLine("Username and password cannot be empty.");
                    await Task.Delay(1000);
                    continue;
                }

                User? authenticatedUser = await dbService.AuthenticateUserAsync(username, password);
                if (authenticatedUser != null)
                {
                    Session.Start(authenticatedUser);
                    Log.Information("User {Username} logged in successfully.", authenticatedUser.Username);
                    // This call is now safe because localIpAddress can't be null.
                    await dbService.LogLoginAttemptAsync(username, authenticatedUser.UserId, true, localIpAddress);
                }
                else
                {
                    loginAttempts++;
                    Log.Warning("Failed login attempt for username: {Username}", username);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Invalid credentials. You have {maxLoginAttempts - loginAttempts} attempts remaining.");
                    Console.ResetColor();
                    // This call is also now safe.
                    await dbService.LogLoginAttemptAsync(username, null, false, localIpAddress);
                    await Task.Delay(1500);
                }
            }

            if (!Session.IsUserLoggedIn())
            {
                Log.Error("Maximum login attempts reached. Exiting.");
                Console.WriteLine("Maximum login attempts reached. Exiting application.");
                return;
            }

            // --- 4. MAIN MENU LOOP ---
            bool exitApplication = false;
            while (!exitApplication)
            {
                ConsoleManager.DisplayMainMenu();
                string choice = ConsoleManager.GetUserChoice();

                switch (choice)
                {
                    case "1":
                    case "2":
                    case "7":
                    case "8":
                    case "9":
                        ConsoleManager.ShowComingSoon();
                        break;

                    case "3":
                        await putawayService.RunGuidedPutawayAsync();
                        break;

                    case "4":
                    await binToBinService.RunBinToBinMovementAsync();
                    break;

                    case "5":
                        await stockInquiryService.RunStockQueryAsync();
                        break;

                    case "6":
                        await binInquiryService.RunBinQueryAsync();
                        break;

                    case "0":
                        exitApplication = true;
                        break;

                    default:
                        Console.WriteLine("Invalid choice, please try again.");
                        await Task.Delay(1000);
                        break;
                }
            }

            // --- 5. LOGOUT ---
            Log.Information("User {Username} initiated logout.", Session.CurrentUser?.Username);
            Console.WriteLine($"\nLogging out {Session.CurrentUser?.FullName}. Goodbye!");
            Session.End();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly.");
        }
        finally
        {
            Log.Information("Application shutting down.");
            await Log.CloseAndFlushAsync();
        }
    }
}
