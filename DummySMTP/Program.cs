using System;
using System.Linq;

namespace DummySMTP
{
    class Program
    {
        static void Main(string[] args)
        {
            string certThumbprint = null;

            Console.ForegroundColor = ConsoleColor.White;
            ConsoleColor errorColor = ConsoleColor.Red;

            if(args.Length == 2 && args[0] == "-certThumbprint")
            {
                certThumbprint = args[1];
                Log("Starting in tls mode");
            }
            else
            {
                Log("Starting in non-secure mode");
            }

            DummySMTPServerConfig config = new DummySMTPServerConfig
            {
                Port = 25,
                TlsEnabled = true,
                TlsCertThumbprint = certThumbprint
            };

            DummySMTPServer smtpServer = new DummySMTPServer(config);

            try
            {
                smtpServer.Start();
            }
            catch(Exception ex)
            {
                Log("xception occurred", errorColor);
                Log($"essage: {ex.Message}", errorColor);
                Log($"Inner error message: {ex.InnerException?.Message}", errorColor);
                Log($"Stack trace: {ex?.StackTrace}", errorColor);
            }

            Console.WriteLine("Exiting...");
            Console.ReadKey();
        }

        static void Log(string message, ConsoleColor col = ConsoleColor.White)
        {
            Console.ForegroundColor = col;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
