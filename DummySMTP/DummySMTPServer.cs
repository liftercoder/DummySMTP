using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DummySMTP
{
    public class DummySMTPServer
    {
        const byte CR = 0x0D, LF = 0x0A;
        delegate string[] ResponseFunc(string[] parts);
        readonly int _port;
        byte[] _buffer = new byte[100000];
        const string _certificateSerialNo = "1567752d2866598749220a1c712f944e";
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

        bool IsQuitMessage(string message) => message == "QUIT";
        bool IsStartTLSMessage(string message) => message == "STARTTLS";

        public DummySMTPServer(DummySMTPServerConfig config)
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
            Log("Listenting for clients...");
            TcpClient client = _server.AcceptTcpClient();
            Log($"accepted connection request from: {client.Client.LocalEndPoint}");

            if (client.Connected)
            {
                Log($"connected to client: {client.Client.LocalEndPoint}");
                using (Stream stream = client.GetStream())
                using (_secureStream = new SslStream(stream))
                {
                    string ack = FrameMessage(ReadyResponse);
                    Log($"sending: {ack}");
                    stream.Write(ToBytes(ack), 0, ack.Length);
                    Receive(stream);
                }
            }

            AcceptClients();
        }

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

                    if (IsQuitMessage(message))
                    {
                        Log("Disconnecting from client");
                        _secureStream.Close();
                        _secureStream.Dispose();
                        return;
                    }

                    if (IsStartTLSMessage(message))
                    {
                        TlsHandshake(_certificateSerialNo, _secureStream);
                        ReceiveSecure();
                        return;
                    }

                    offset = 0;
                    _buffer = new byte[10000];
                    continue;
                }

                ++offset;
            }
        }

        private void ReceiveSecure()
        {
            int offset = 0;

            while (true)
            {
                _secureStream.Read(_buffer, offset, 1);

                if (EndOfMessage(offset))
                {
                    string message = Sanitize(FromBytes(_buffer.Take(offset).ToArray()));
                    _messages.Add(message);
                    Log($"Received: {message}");

                    if(IsQuitMessage(message))
                    {
                        Log("Disconnecting from client");
                        _secureStream.Close();
                        _secureStream.Dispose();
                        return;
                    }

                    string[] payload = GetResponsePayload(message);

                    foreach (string line in payload)
                    {
                        byte[] responseBuffer = ToBytes(FrameMessage(line));
                        Log($"Sending: {line}");
                        _secureStream.Write(responseBuffer, 0, responseBuffer.Length);
                    }

                    offset = 0;
                    _buffer = new byte[10000];
                    continue;
                }

                ++offset;
            }
        }

        private void TlsHandshake(string certSerialNo, SslStream stream)
        {
            X509Store certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            certStore.Open(OpenFlags.ReadOnly);
            X509Certificate2 cert = certStore.Certificates.Find(X509FindType.FindBySerialNumber, certSerialNo, true)[0];

            stream.AuthenticateAsServer(cert, false, false);
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

        private const string
            ReadyResponse = "220 Ready"
            , HelloResponse = "250-server.test.com Hello {0}"
            , TLSResponse = "250-STARTTLS"
            , OKResponse = "250 OK"
            , AcceptedResponse = "250 Accepted"
            , DataResponse = "354 Enter message, ending with \".\" on a line by itself"
            , StartTLSResponse = "220 2.0.0 SMTP server ready"
        ;
    }
}
