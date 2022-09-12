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

            try
            {
                int index = args.ToList().IndexOf("-certThumbprint");
                certThumbprint = args[index + 1];
            }
            catch
            {
                Log("Error: server expects the argument: -certThumbprint <thumbprint>", errorColor);
                Console.ReadKey();
                return;
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
                Log("Exception occurred", errorColor);
                Log($"Message: {ex.Message}", errorColor);
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
