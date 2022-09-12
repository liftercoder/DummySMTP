using System;
using System.Linq;

namespace DummySMTP
{
    class Program
    {
        static void Main(string[] args)
        {
            string certThumbprint = null;

            try
            {
                int index = args.ToList().IndexOf("-certThumbprint");
                certThumbprint = args[index + 1];
            }
            catch
            {
                Console.WriteLine("server expects the argument: -certThumbprint <thumbprint>");
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
                Console.WriteLine($"error message: {ex.Message}");
                Console.WriteLine($"inner error message: {ex.InnerException?.Message}");
            }

            Console.WriteLine("exiting...");
            Console.ReadKey();
        }
    }
}
