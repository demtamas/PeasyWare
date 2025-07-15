using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace PeasyWare.WMS.Console.Utilities
{
    /// <summary>
    /// Provides static helper methods for retrieving network-related information about the local machine.
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Attempts to retrieve the local IPv4 address of the machine running the application.
        /// </summary>
        /// <returns>The local IPv4 address as a string if found; otherwise, null.</returns>
        public static string? GetLocalIpAddress()
        {
            try
            {
                // Get the host entry for the local machine using its DNS host name.
                var host = Dns.GetHostEntry(Dns.GetHostName());

                // Iterate through the list of IP addresses associated with the host.
                // We use LINQ's FirstOrDefault to find the first address that belongs to the
                // 'InterNetwork' address family, which corresponds to IPv4.
                var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

                // Return the IP address as a string. If no IPv4 address was found, this will return null.
                return ipAddress?.ToString();
            }
            catch
            {
                // If any network-related exception occurs (e.g., host not found),
                // we catch it and return null to prevent the application from crashing.
                return null;
            }
        }
    }
}
