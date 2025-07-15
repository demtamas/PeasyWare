using PeasyWare.WMS.Console.Models;

/// <summary>
/// Provides a static, application-wide service to manage the current user's session state.
/// This class holds the information of the authenticated user and makes it globally accessible.
/// </summary>
public static class Session
{
    /// <summary>
    /// Gets the User object for the currently authenticated user.
    /// This property is null if no user is logged in.
    /// The setter is private to ensure that the session can only be started or ended
    /// through the dedicated Start() and End() methods.
    /// </summary>
    public static User? CurrentUser { get; private set; }

    /// <summary>
    /// Starts a new session for the specified user.
    /// </summary>
    /// <param name="user">The user object representing the authenticated user.</param>
    public static void Start(User user)
    {
        CurrentUser = user;
    }

    /// <summary>
    /// Ends the current user session by clearing the user information.
    /// </summary>
    public static void End()
    {
        CurrentUser = null;
    }

    /// <summary>
    /// Checks whether a user is currently logged into the application.
    /// </summary>
    /// <returns>True if a user session is active; otherwise, false.</returns>
    public static bool IsUserLoggedIn()
    {
        return CurrentUser != null;
    }
}