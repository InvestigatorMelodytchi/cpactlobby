using System;
using System.Collections.Generic;
using System.Text;

namespace Counterpact_Lobby
{
    class Program
    {
        // Main method.
        static void Main(string[] args)
        {
            // Launching server.
            HTTPServer serverInstance = new HTTPServer();
            serverInstance.ServerLaunch();
            Console.WriteLine($"[{DateTime.Now}] Counterpact lobby server launched.\n");
        }

        // End class.
    }
}