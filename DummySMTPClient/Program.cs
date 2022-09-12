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
            Console.WriteLine("Starting up...");

            using (SmtpClient client = new SmtpClient("localhost", 25))
            {
                client.EnableSsl = true;
                client.Send("test@from.com", "test@to.com", "asubject", "abody");
            }
        }
    }
}
