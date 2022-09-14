using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DummySMTP
{
    public class DummySMTPServer
    {
        public DummySMTPServer(DummySMTPServerConfig config)
        {
            _port = config.Port;
            _tlsCertThumbprint = config.TlsCertThumbprint;
        }

        const byte CR = 0x0D, LF = 0x0A;
        delegate string[] ResponseFunc(string[] parts);
        readonly int _port;
        byte[] _buffer = new byte[100000];
        string _tlsCertThumbprint;
        TcpListener _server;
        List<string> _messages = new List<string>();
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

        bool IsQuitMessage(string message) => message == "QUIT";
        bool IsStartTLSMessage(string message) => message == "STARTTLS";
        bool IsBeginDataContentMessage(string message) => message == "DATA";
        bool IsEndDataContentMessage(string message) => message == ".";

        public void Start()
        {
            _server = new TcpListener(IPAddress.Any, _port);
            _server.Start();
            Log($"server listening on port {_port}");
            AcceptClients();
        }

        private void AcceptClients()
        {
            Log("waiting for connections...");
            TcpClient client = _server.AcceptTcpClient();
            Log($"accepted connection request from: {client.Client.LocalEndPoint}");

            if (client.Connected)
            {
                Log($"connected to client: {client.Client.LocalEndPoint}");
                client.ReceiveTimeout = 6000;
                using (Stream stream = client.GetStream())
                using (SslStream secureStream = new SslStream(stream))
                {
                    Write(stream, ReadyResponse);

                    try
                    {
                        Read(stream, secureStream);
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException || ex is ObjectDisposedException)
                        {
                            Log("lost connection with client");
                        }
                        else
                        {
                            Log($"{ex.Message} {ex.InnerException?.Message}");
                        }
                    }
                }
            }

            AcceptClients();
        }

        private string FrameMessage(string message) => new string(message.Append('\r').Append('\n').ToArray());

        private bool EndOfMessage(int index) => index > 0 && _buffer[index - 1] == CR && _buffer[index] == LF;

        private void Log(string message)
        {
            if(!message.Any() || message == null)
            {
                return;
            }

            message = $"{DateTime.Now:G}: {new string(message.Skip(1).Prepend(char.ToUpper(message[0])).ToArray())}";
            Console.WriteLine(message);
            WriteLog(message);
        }

        private byte[] ToBytes(string message) => Encoding.UTF8.GetBytes(message);

        private string FromBytes(byte[] bytes) => Encoding.UTF8.GetString(bytes);

        private string ReadMessage(Stream stream)
        {
            int offset = 0;

            while (true)
            {
                stream.Read(_buffer, offset, 1);

                if (EndOfMessage(offset))
                {
                    string message = Sanitize(FromBytes(_buffer.Take(offset).ToArray()));
                    _messages.Add(message);
                    Log($"received: {message}");
                    _buffer = new byte[10000];
                    return message;
                }

                ++offset;
            }
        }

        private void Write(Stream stream, string[] messages)
        {
            foreach (string message in messages)
            {
                try
                {
                    Write(stream, message);
                }
                catch
                {
                    Log("couldn't write to the stream, connection gone");
                    return;
                }
            }
        }

        private void Write(Stream stream, string message)
        {
            byte[] buffer = ToBytes(FrameMessage(message));
            Log($"sending: {message}");
            stream.Write(buffer, 0, buffer.Length);
        }

        private void Read(Stream stream, SslStream secureStream = null)
        {
            bool dataMode = false;
            List<string> emailLines = new List<string>();

            while (true)
            {
                string message = ReadMessage(stream);

                if (IsQuitMessage(message))
                {
                    Log("disconnecting from client");
                    return;
                }


                if (dataMode)
                {
                    if (IsEndDataContentMessage(message))
                    {
                        dataMode = false;
                        SaveEmail(emailLines);
                        emailLines = new List<string>();
                    }
                    else
                    {
                        emailLines.Add(message);
                    }
                }

                if (!dataMode)
                {
                    string[] payload = GetResponsePayload(message);
                    Write(stream, payload);

                    if (IsBeginDataContentMessage(message))
                    {
                        dataMode = true;
                    }
                }

                if (secureStream != null && IsStartTLSMessage(message))
                {
                    Log("initiating TLS handshake");
                    TlsHandshake(_tlsCertThumbprint, secureStream);
                    Log("secure channel initialized");
                    Read(secureStream);
                    return;
                }
            }
        }

        private void TlsHandshake(string certThumbprint, SslStream stream)
        {
            using (X509Store certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                Log("opening certificate store");
                certStore.Open(OpenFlags.ReadOnly);

                using (X509Certificate2 cert = certStore.Certificates.Find(X509FindType.FindByThumbprint, certThumbprint, true)[0])
                {
                    if (cert == null)
                    {
                        throw new NullReferenceException($"couldn't find certificate with thumbprint: {certThumbprint}");
                    }

                    Log($"found certificate with thumbprint: {cert.Thumbprint}");
                    Log("authenticating with certificate");

                    stream.AuthenticateAsServer(cert, false, SslProtocols.Tls12, false);
                    Log("authenticated");
                }
            }
        }

        private string Sanitize(string message) => new string(message.Where(x => !new char[] { '\r', '\n' }.Contains(x)).ToArray());

        private string[] GetResponsePayload(string message)
        {
            string[] parts = message.Split(' ');

            string cmd = parts[0];

            ResponseFunc response;

            if (!_responseConfig.TryGetValue(cmd, out response))
            {
                response = _responseConfig["DEFAULT"];
            }

            return response(parts);
        }

        private void WriteLog(string content)
        {
            File.AppendAllText("log.txt", $"{DateTime.Now:G}: {content}\r\n");
        }

        private void SaveEmail(List<string> emailLines)
        {
            Directory.CreateDirectory("dummy-smtp-inbox");
            File.AppendAllLines($"dummy-smtp-inbox\\{DateTime.Now:ddMMyyyyHHmmssffffff}.eml", emailLines);
        }

        private const string
            ReadyResponse = "220 Ready"
            , HelloResponse = "250-server.test.com Hello {0}"
            , TLSResponse = "250-STARTTLS"
            , OKResponse = "250 OK"
            , AcceptedResponse = "250 Accepted"
            , DataResponse = "354 Start mail input; end with <CR><LF>.<CR><LF>"
            , StartTLSResponse = "220 2.0.0 SMTP server ready"
        ;
    }
}
