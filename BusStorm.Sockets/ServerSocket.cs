using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace BusStorm.Sockets
{
    public class ServerSocket
    {
        private readonly Socket _sock;

        private readonly Subject<ClientConnection> _connectionsSubject;

        private long _connectionCounter;

        public ServerSocket()
        {
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _connectionsSubject = new Subject<ClientConnection>();
            _connectionCounter = 0;
        }

        public void Start(int port)
        {
            _sock.Bind(new IPEndPoint(IPAddress.Any,port));
            _sock.Listen(50);
            StartAccept();
        }

        private void StartAccept()
        {
            Task.Run(() =>
                {
                    var args = new SocketAsyncEventArgs();
                    args.Completed += AccepAsyncCompleted;
                    AcceptAsync(args);
                }).ConfigureAwait(false);
        }

        private void AcceptAsync(SocketAsyncEventArgs args)
        {
            if (!_sock.AcceptAsync(args))
            {
                AccepAsyncCompleted(_sock,args);
            }
        }

        void AccepAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                Interlocked.Increment(ref _connectionCounter);
                var client = new ClientConnection(e.AcceptSocket,_connectionCounter);
                _connectionsSubject.OnNext(client);
            }
            else
            {
                var ee = new SocketException();
                ee.Data.Add("SocketError",e.SocketError);
                _connectionsSubject.OnError(ee);
            }
            e.AcceptSocket = null;
            StartAccept();
        }
        
        public void Close()
        {
            _sock.Close();
        }
    }
}
