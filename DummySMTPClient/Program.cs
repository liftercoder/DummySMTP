using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace DummySMTPClient
{
    class Program
    {
        static void Main(string[] args)
        {
            const string hostname = "localhost";
            const int port = 25;

            Console.WriteLine("Starting up...");
            Console.WriteLine($"Sending emails to {hostname}:{port}");

            using (SmtpClient client = new SmtpClient(hostname, port))
            {
                client.EnableSsl = true;
                for (int i = 0; i < 10; i++)
                {
                    Console.WriteLine($"Sending email #{i}");
                    try
                    {
                        MailMessage message = new MailMessage
                        {
                            IsBodyHtml = true,
                            BodyTransferEncoding = System.Net.Mime.TransferEncoding.Base64
                        };
                        message.To.Add("to@to.com");
                        message.CC.Add("cc@cc.com");
                        message.Bcc.Add("bcc@bcc.com");
                        message.Subject = "This is a test";
                        message.From = new MailAddress("from@from.com", "display name");
                        message.Body = "<h1>Hello</h1><p>This is a test</p>";
                        client.Send(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send email #{i}");
                        Console.WriteLine($"Error message: {ex.Message} {ex.InnerException?.Message}");
                    }
                }
                Console.WriteLine("Finished");
            }

            Console.WriteLine("Disposing connection");
            Console.WriteLine("End");
            Console.ReadKey();
        }
    }
}
