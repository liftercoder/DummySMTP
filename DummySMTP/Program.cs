using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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
        delegate string[] ResponseFunc(string[] parts);
        readonly int _port;
        byte[] _buffer;
        TcpListener _server;
        List<string> _messages = new List<string>();
        SslStream _secureStream;
        Dictionary<string, ResponseFunc> _responseConfig = new Dictionary<string, ResponseFunc>
        {
            { "HELO", (string[] parts) => new string[] { string.Format(HelloResponse, parts.ElementAt(1)), TLSResponse, OKResponse } },
            { "EHLO", (string[] parts) => new string[] { string.Format(HelloResponse, parts.ElementAt(1)), TLSResponse, OKResponse } },
            { "MAIL", (string[] parts) => new string[] { OKResponse } },
            { "RCPT", (string[] parts) => new string[] { AcceptedResponse } },
            { "DATA", (string[] parts) => new string[] { DataResponse } },
            { "STARTTLS", (string[] parts) => new string[] { StartTLSResponse } },
            { "DEFAULT", (string[] parts) => new string[] { OKResponse } }
        };

        public SmtpServer(SmtpServerConfig config)
        {
            _port = config.Port;
        }

        public void Start()
        {
            _server = new TcpListener(IPAddress.Any, _port);
            _server.Start();
            Log($"Server listening on {_port}");
            AcceptClients();
        }

        private void AcceptClients()
        {
            TcpClient client = _server.AcceptTcpClient();
            Log($"Accepted connection request from: {client.Client.LocalEndPoint}");

            if (client.Connected)
            {
                Log("Connected to client");
                using (Stream stream = client.GetStream())
                using(_secureStream = new SslStream(stream))
                {
                    string ack = FrameMessage("220 Ready");
                    Log($"Sending to client: {ack}");
                    stream.Write(Encoding.ASCII.GetBytes(ack), 0, ack.Length);
                    _buffer = new byte[10000];
                    Receive(stream);
                }
            }
            else
            {
                AcceptClients();
            }
        }

        const byte CR = 0x0D, LF = 0x0A;

        private string FrameMessage(string message) => new string(message.Append('\r').Append('\n').ToArray());

        private bool EndOfMessage(int index) => index > 0 && _buffer[index - 1] == CR && _buffer[index] == LF;

        private void Log(string message) => Console.WriteLine(message);

        private byte[] ToBytes(string message) => Encoding.UTF8.GetBytes(message);

        private string FromBytes(byte[] bytes) => Encoding.UTF8.GetString(bytes);

        private void Receive(Stream stream)
        {
            int offset = 0;

            while (true)
            {
                stream.Read(_buffer, offset, 1);

                if (EndOfMessage(offset))
                {
                    string message = Sanitize(FromBytes(_buffer.Take(offset).ToArray()));
                    _messages.Add(message);
                    Log($"Received: {message}");

                    string[] payload = GetResponsePayload(message);

                    foreach (string response in payload)
                    {
                        byte[] buffer = ToBytes(FrameMessage(response));
                        Log($"Sending: {response}");
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    // TODO: tidy up
                    if (message == "STARTTLS")
                    {
                        X509Store certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                        certStore.Open(OpenFlags.ReadOnly);
                        X509Certificate2 cert = certStore.Certificates.Find(X509FindType.FindBySerialNumber, "1567752d2866598749220a1c712f944e", true)[0];
                        try
                        {
                            _secureStream.AuthenticateAsServer(cert, false, false);
                            if(!_secureStream.IsMutuallyAuthenticated)
                            {
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            return;
                        }
                    }

                    offset = 0;
                    _buffer = new byte[10000];
                    continue;
                }

                ++offset;
            }
        }

        private string Sanitize(string message) => new string(message.Where(x => !new char[] { '\r', '\n' }.Contains(x)).ToArray());

        private string[] GetResponsePayload(string message)
        {
            string[] parts = message.Split(' ');

            string cmd = parts[0];

            ResponseFunc response;

            if(!_responseConfig.TryGetValue(cmd, out response))
            {
                response = _responseConfig["DEFAULT"];
            }

            return response(parts);
        }

        private const string HelloResponse = "250-server.test.com Hello {0}";
        private const string TLSResponse = "250-STARTTLS";
        private const string OKResponse = "250 OK";
        private const string AcceptedResponse = "250 Accepted";
        private const string DataResponse = "354 Enter message, ending with \".\" on a line by itself";
        private const string QuitResponse = "quit";
        private const string StartTLSResponse = "220 2.0.0 SMTP server ready";
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
