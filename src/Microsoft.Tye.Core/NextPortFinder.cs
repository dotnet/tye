using System.Net;
using System.Net.Sockets;

namespace Microsoft.Tye
{
    public sealed class NextPortFinder
    {
        public static NextPortFinder Instance { get; } = new NextPortFinder();

        private NextPortFinder()
        {
        }

        public int GetNextPort()
        {
            // Let the OS assign the next available port. Unless we cycle through all ports
            // on a test run, the OS will always increment the port number when making these calls.
            // This prevents races in parallel test runs where a test is already bound to
            // a given port, and a new test is able to bind to the same port due to port
            // reuse being enabled by default by the OS.
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }
    }
}
