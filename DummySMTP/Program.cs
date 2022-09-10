﻿using System;
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
        readonly int _port;
        byte[] _buffer;
        TcpListener _server;
        List<string> _messages = new List<string>();
        SslStream _secureStream;

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
                using (Stream stream = client.GetStream())
                using(_secureStream = new SslStream(stream))
                {
                    string ack = FrameMessage("220 Ready");
                    Console.WriteLine($"Sending to client: {ack}");
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

        private void Receive(Stream stream)
        {
            int offset = 0;

            while (true)
            {
                stream.Read(_buffer, offset, 1);

                if (offset > 0 && _buffer[offset - 1] == CR && _buffer[offset] == LF)
                {
                    string message = Encoding.UTF8.GetString(_buffer.Take(offset).ToArray());
                    _messages.Add(message);
                    Console.WriteLine(message);

                    string[] payload = GetResponsePayload(message);

                    List<byte[]> data = new List<byte[]>();

                    foreach (string response in payload)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes(FrameMessage(response));

                        Console.WriteLine($"Sending: {response}");
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    if (message == "STARTTLS\r")
                    {
                        X509Store certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                        certStore.Open(OpenFlags.ReadOnly);
                        X509Certificate2 cert = certStore.Certificates.Find(X509FindType.FindBySerialNumber, "1567752d2866598749220a1c712f944e", true)[0];
                        try
                        {
                            _secureStream.AuthenticateAsServer(cert, false, false);
                            if(!_secureStream.IsAuthenticated)
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

        private string Sanitize(string message)
        {
            return new string(message.Where(x => !new char[] { '\r', '\n' }.Contains(x)).ToArray());
        }

        private string[] GetResponsePayload(string message)
        {
            IEnumerable<string> parts = Sanitize(message).Split(' ');
            string cmd = parts.First();
            string[] response;

            switch (cmd)
            {
                case "HELO":
                case "EHLO":
                    response = new string[] { string.Format(HelloResponse, parts.ElementAt(1)), TLSResponse, OKResponse };
                    break;
                case "MAIL":
                    response = new string[] { OKResponse };
                    break;
                case "RCPT":
                    response = new string[] { AcceptedResponse };
                    break;
                case "DATA":
                    response = new string[] { DataResponse };
                    break;
                case "STARTTLS":
                    response = new string[] { StartTLSResponse };
                    break;
                default:
                    response = new string[] { OKResponse };
                    break;
            }

            return response;
        }

        //private string HelloResponse => "250 OK";
        private string HelloResponse => "250-server.test.com Hello {0}";
        private string TLSResponse => "250-STARTTLS";
        private string OKResponse => "250 OK";
        private string AcceptedResponse => "250 Accepted";
        private string DataResponse => "354 Enter message, ending with \".\" on a line by itself";
        private string QuitResponse => "quit";
        private string StartTLSResponse => "220 2.0.0 SMTP server ready";
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