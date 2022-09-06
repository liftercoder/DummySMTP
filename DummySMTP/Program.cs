using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DummySMTP
{
    class SmtpServerConfig
    {
        public int Port { get; set; }
        public bool TlsEnabled { get; set; }
    }

    class SmtpServer
    {
        readonly int _port;
        byte[] _buffer;
        TcpListener _server;
        List<string> _messages = new List<string>();

        public SmtpServer(SmtpServerConfig config)
        {
            _port = config.Port;
        }

        public void Start()
        {
            _server = new TcpListener(IPAddress.Any, _port);
            _server.Start();
            Console.WriteLine($"Server listening on {_port}");
            AcceptClients();
        }

        private string FrameMessage(string message)
        {
            return new string(message.Append('\r').Append('\n').ToArray());
        }

        private void AcceptClients()
        {
            TcpClient client = _server.AcceptTcpClient();
            Console.WriteLine($"Accepted connection request from: {client.Client.LocalEndPoint}");

            if (client.Connected)
            {
                Console.WriteLine("Connected to client");
                using (NetworkStream stream = client.GetStream())
                {
                    string ack = FrameMessage("220 test.com ESMTP Exim");
                    Console.WriteLine($"Sending to client: {ack}");
                    stream.Write(Encoding.ASCII.GetBytes(ack), 0, ack.Length);
                    _buffer = new byte[1000];
                    Receive(stream);
                }
            }
            else
            {
                AcceptClients();
            }
        }

        const byte CR = 0x0D, LF = 0x0A;

        private void Receive(NetworkStream stream, int offset = 0)
        {
            stream.Read(_buffer, offset, 1);

            if(offset > 0 && _buffer[offset-1] == CR && _buffer[offset] == LF)
            {
                string message = Encoding.ASCII.GetString(_buffer.Take(offset).ToArray());
                _messages.Add(message);
                Console.WriteLine(message);
                byte[] response = Encoding.ASCII.GetBytes(FrameMessage(GetResponse(message)));
                if(stream == null) // Need to check socket connected, pass that in instead of stream?
                {
                    Console.WriteLine("Connection ended");
                    return;
                }
                stream.Write(response, 0, response.Length);
                Receive(stream);
            }

            Receive(stream, ++offset);
        }

        private string Sanitize(string message)
        {
            return new string(message.Where(x => !new char[] { '\r', '\n' }.Contains(x)).ToArray());
        }

        private string GetResponse(string message)
        {
            IEnumerable<string> parts = Sanitize(message).Split(' ');
            string cmd = parts.First();
            string response = "";

            switch (cmd)
            {
                case "HELO":
                case "EHLO":
                    response = string.Format(HelloResponse, parts.ElementAt(1));
                    break;
                case "MAIL":
                    response = OKResponse;
                    break;
                case "RCPT":
                    response = AcceptedResponse;
                    break;
                case "DATA":
                    response = DataResponse;
                    break;
                default:
                    response = OKResponse;
                    break;
            }

            return response;
        }

        private string HelloResponse => "250 Hello {0}";
        private string OKResponse => "250 OK";
        private string AcceptedResponse => "250 Accepted";
        private string DataResponse => "354 Enter message, ending with \".\" on a line by itself";
        private string QuitResponse => "quit";
    }

    class Program
    {
        static void Main(string[] args)
        {
            SmtpServerConfig config = new SmtpServerConfig
            {
                Port = 25,
                TlsEnabled = true
            };

            SmtpServer smtpServer = new SmtpServer(config);
            smtpServer.Start();
            Console.WriteLine("Exiting...");
            Console.ReadLine();
        }
    }
}
