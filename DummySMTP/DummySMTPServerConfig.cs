namespace DummySMTP
{
    public class DummySMTPServerConfig
    {
        public int Port { get; set; }
        public bool TlsEnabled { get; set; }
        public string TlsCertThumbprint { get; set; }
    }
}
