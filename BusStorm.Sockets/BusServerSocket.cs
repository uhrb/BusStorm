using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using BusStorm.Logging;

namespace BusStorm.Sockets
{
    public class BusServerSocket
    {
        private Socket _sock;

        private readonly Subject<BusClientConnection> _connectionsSubject;

        private long _connectionCounter;

        public BusServerSocket()
        {
            Tracer.Log("Server socket created with public ctor");
            _connectionsSubject = new Subject<BusClientConnection>();
            _connectionCounter = 0;
            Connected = false;
        }

        public bool Connected { get; private set; }

        public void Start(int port)
        {
            Tracer.Log("Server socket started with {0}",port);
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _sock.Bind(new IPEndPoint(IPAddress.Any,port));
            _sock.Listen(50);
            Connected = true;
            StartAccept();
        }

        public IObservable<BusClientConnection> Connections { get { return _connectionsSubject.ObserveOn(NewThreadScheduler.Default); } } 

        private void StartAccept()
        {
            Tracer.Log("Server socket start accept fired");
            Task.Run(() =>
                {
                    Tracer.Log("Server socket start accept task in progress");
                    var args = new SocketAsyncEventArgs();
                    args.Completed += AccepAsyncCompleted;
                    AcceptAsync(args);
                    Tracer.Log("Server socket start accept task complete");
                }).ConfigureAwait(false);
        }

        private void AcceptAsync(SocketAsyncEventArgs args)
        {
            if (!Connected)
            {
                return;
            }
            Tracer.Log("Server socket accept async fired");
            if (!_sock.AcceptAsync(args))
            {
                AccepAsyncCompleted(_sock,args);
            }
        }

        void AccepAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            Tracer.Log("Server socket AcceptAsyncComplete fired {0}",e.SocketError);
            if (e.SocketError == SocketError.Success)
            {
                Interlocked.Increment(ref _connectionCounter);
                var client = new BusClientConnection(e.AcceptSocket,_connectionCounter);
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
            Tracer.Log("Server socket AcceptAsyncComplete done");
        }
        
        public void Stop()
        {
            Tracer.Log("Server socket stop fired");
            Connected = false;
            _connectionsSubject.OnCompleted();
            _sock.Close();
            _sock = null;
        }
    }
}
