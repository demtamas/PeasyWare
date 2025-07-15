using static System.Console;

namespace PeasyWare.WMS.Console.UI
{
    /// <summary>
    /// Provides a centralized set of static methods for managing all console user interface interactions.
    /// This class handles displaying menus, reading user input, and showing standard messages.
    /// </summary>
    public static class ConsoleManager
    {
        /// <summary>
        /// Clears the console and displays the main operational menu to the user,
        /// including the currently logged-in user's details.
        /// </summary>
        public static void DisplayMainMenu()
        {
            Clear();
            ForegroundColor = ConsoleColor.Green;
            WriteLine("======================================");
            WriteLine("        PeasyWare WMS - Main Menu");
            WriteLine("======================================");
            ResetColor();
            WriteLine($"Operator: {Session.CurrentUser?.FullName} ({Session.CurrentUser?.RoleName})");
            WriteLine("--------------------------------------");
            WriteLine(" 1. Activating Inbound   (Coming Soon)");
            WriteLine(" 2. Receiving            (Coming Soon)");
            WriteLine(" 3. Guided Putaway");
            WriteLine(" 4. Bin to Bin Movement");
            WriteLine(" 5. Stock Query");
            WriteLine(" 6. Bin Query");
            WriteLine(" 7. Cycle Count          (Coming Soon)");
            WriteLine(" 8. Picking              (Coming Soon)");
            WriteLine(" 9. Shipping             (Coming Soon)");
            WriteLine("--------------------------------------");
            WriteLine(" 0. Exit");
            WriteLine("======================================");
        }

        /// <summary>
        /// Prompts the user to enter their choice and reads the input from the console.
        /// </summary>
        /// <returns>The user's trimmed input as a string.</returns>
        public static string GetUserChoice()
        {
            Write("\nPlease enter your choice: ");
            return ReadLine()?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Displays a standard "Coming Soon" message for features that are not yet implemented.
        /// It waits for the user to press any key before returning.
        /// </summary>
        public static void ShowComingSoon()
        {
            ForegroundColor = ConsoleColor.DarkYellow;
            WriteLine("\nThis feature is not yet implemented.");
            ResetColor();
            WriteLine("Press any key to return to the menu...");
            ReadKey();
        }
    }
}
