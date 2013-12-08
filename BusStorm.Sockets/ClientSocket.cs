using System.Net.Sockets;

namespace BusStorm.Sockets
{
    public class ClientSocket : ClientConnection
    {
        private readonly string _host;
        private readonly int _port;

        public ClientSocket(string host,int port)
        {
            _host = host;
            _port = port;
            Socket = new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);
        }

        public void Connect()
        {
            Socket.Connect(_host,_port);
            StartReceiving();
        }

        public void Disconnect(bool reuse = false)
        {
            Socket.Disconnect(reuse);
        }
    }
}