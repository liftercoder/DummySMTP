using System;

namespace DummySMTP
{
    class Program
    {
        static void Main(string[] args)
        {
            DummySMTPServerConfig config = new DummySMTPServerConfig
            {
                Port = 25,
                TlsEnabled = true
            };

            DummySMTPServer smtpServer = new DummySMTPServer(config);
            smtpServer.Start();
            Console.WriteLine("Exiting...");
            Console.ReadLine();
        }
    }
}
