using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using BusStorm.Logging;

namespace BusStorm.Sockets
{
    public class BusClientConnection :IChannelWriter<BusMessage>
    {
        private readonly long _connectionNumber;
        private readonly Subject<BusMessage> _receivedMessages;
        private readonly Subject<byte[]> _sendedBytes;
        private readonly List<byte> _currentReceiveBuffer;
        private BusMessage _currentMessage;
        private readonly object _receiveLocker = new object();
        private long _messagesReceived;
        private readonly string _encryptionKey;


        protected BusClientConnection(string encryptionKey)
        {
            _encryptionKey = encryptionKey;
            Tracer.Log("Client connection created from protected ctor");
            _currentReceiveBuffer = new List<byte>();
            _receivedMessages = new Subject<BusMessage>();
            _sendedBytes = new Subject<byte[]>();
            Connected = false;
        }

        public bool Connected { get; protected set; }

        internal BusClientConnection(Socket socket, long connectionNumber, string encryptionKey):this(encryptionKey)
        {
            Tracer.Log("Client connection created from internal ctor");
            _connectionNumber = connectionNumber;
            Socket = socket;
            Connected = true;
            StartReceiving();
        }

        protected Socket Socket { get; set; }

        public long ConnectionNumber { get { return _connectionNumber; } }

        public IObservable<BusMessage> ReceivedMessages { get { return _receivedMessages.ObserveOn(NewThreadScheduler.Default); }}

        public IObservable<byte[]> SendedBytes { get { return _sendedBytes.ObserveOn(NewThreadScheduler.Default); } }

        protected void StartReceiving()
        {
            Tracer.Log("Client connection StartReceiving fired");
            Task.Run(() =>
                {
                    Tracer.Log("Client connection StartReceiving task execution in progress");
                    var buffer = new byte[1024];
                    var args = new SocketAsyncEventArgs();
                    args.SetBuffer(buffer, 0, buffer.Length);
                    args.Completed += AsyncSocketOperationCompleted;
                    if (!Socket.ReceiveAsync(args))
                    {
                        AsyncSocketOperationCompleted(Socket, args);
                    }
                    Tracer.Log("Client connection StartReceiving task execution complete");
                }).ConfigureAwait(false);
        }

        void AsyncSocketOperationCompleted(object sender, SocketAsyncEventArgs e)
        {
            Tracer.Log("Client connection AsyncSocketOperationCompleted {0} {1}",e.LastOperation,e.SocketError);
            switch (e.LastOperation)
            {
                    case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                    case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
            }
        }

        public Task SendAsync(byte[] data)
        {
            if (!Connected)
            {
                throw new InvalidOperationException();
            }
            Tracer.Log("Client connection SendAsync fired {0}",data.Length);
            return Task.Run(() =>
                {
                    Tracer.Log("Client connection SendAsync task in progress");
                    var args = new SocketAsyncEventArgs();
                    args.Completed += AsyncSocketOperationCompleted;
                    args.SetBuffer(data, 0, data.Length);
                    if (!Socket.SendAsync(args))
                    {
                        AsyncSocketOperationCompleted(Socket, args);
                    }
                    Tracer.Log("Client connection SendAsync task complete");
                });
        }

        public Task SendAsync(BusMessage message)
        {
            return SendAsync(message.ToByteArray(_encryptionKey));
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            Tracer.Log("Client connection ProcessReceive fired {0} {1}",e.BytesTransferred,e.SocketError);
            if (e.SocketError != SocketError.Success)
            {
                var ee = new SocketException();
                ee.Data.Add("SocketError", e.SocketError);
                Tracer.Log("Client connection socket error fired");
                _receivedMessages.OnError(ee);
                // and complete 
                CompleteConnection();
                return;
            }
            
            if (e.BytesTransferred == 0)
            {
                CompleteConnection();
                return;
            }

            var bytes = new byte[e.BytesTransferred];
            Array.Copy(e.Buffer,bytes,e.BytesTransferred);
            Tracer.Log("Client connection received {0} bytes",bytes.Length);
            lock (_receiveLocker)
            {
                _currentReceiveBuffer.AddRange(bytes);
                if (_currentMessage == null)
                {
                    // new message receive starting
                    StartHeaderReceiving();
                }
                // if message is not null- that means that receiving payload in progress
                CheckMessageComplete();
            }
            StartReceiving();
        }

        private void CompleteConnection()
        {
            _currentMessage = null;
            _currentReceiveBuffer.Clear();
            Connected = false;
            _receivedMessages.OnCompleted();
        }

        public BusMessage SendAndReceive(BusMessage message)
        {
            var seqid = message.SequenceId;
            var eve = new ManualResetEvent(false);
            BusMessage received = null;
            var sub = ReceivedMessages.SkipWhile(l => l.SequenceId != seqid).Take(1).Subscribe(next =>
                {
                    received = next;
                    eve.Set();
                });
            SendAsync(message).Wait();
            eve.WaitOne();
            sub.Dispose();
            return received;
        }

        public Task<BusMessage> SendAndReceiveAsync(BusMessage message)
        {
            return Task.Run(() => SendAndReceive(message));
        }

        private void StartHeaderReceiving()
        {
            Tracer.Log("Client connection start new header");
            if (_currentReceiveBuffer.Count < BusMessage.HeaderSize)
            {
                return;
            }
            // header in buffer, lets translate
            var headerBytes = _currentReceiveBuffer.GetRange(0, BusMessage.HeaderSize).ToArray();
            Tracer.Log("Client connection header received {0}",headerBytes.Length);
            var msg = BusMessage.TransalteHeader(headerBytes);
            _currentMessage = msg;
            // remove header bytes from buffer
            _currentReceiveBuffer.RemoveRange(0, BusMessage.HeaderSize);
        }

        private void CheckMessageComplete()
        {
            // message not started - exiting
            if (_currentMessage == null)
            {
                return;
            }
            Tracer.Log("Client connection check payload receiving bytes");
            if (_currentReceiveBuffer.Count < _currentMessage.OnWirePayloadLength)
            {
                // payload receive is not complete
                return;
            }
            // received payload
            var payloadBytes = _currentReceiveBuffer.GetRange(0, _currentMessage.OnWirePayloadLength).ToArray();
            Tracer.Log("Client connection payload received {0}",payloadBytes.Length);
            // translate payload
            _currentMessage.TranslatePayload(payloadBytes,_encryptionKey);
            var msg = _currentMessage;
            _currentMessage = null;
            _currentReceiveBuffer.RemoveRange(0, msg.OnWirePayloadLength);
            _receivedMessages.OnNext(msg);
            var i = Interlocked.Increment(ref _messagesReceived);
            Tracer.Log("Client connection message complete {0}",i);
            if (_currentReceiveBuffer.Count > 0)
            {
                // the rest of buffer is new message
                StartHeaderReceiving();
            }
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            Tracer.Log("Client connection process send fired {0} {1}",e.BytesTransferred,e.SocketError);
            if (e.SocketError != SocketError.Success)
            {
                var ee = new SocketException();
                ee.Data.Add("SocketError", e.SocketError);
                _sendedBytes.OnError(ee);
            }
            else
            {
                var buff = new byte[e.BytesTransferred];
                Array.Copy(e.Buffer,buff,e.BytesTransferred);
                Tracer.Log("Client connection sended {0} bytes",buff.Length);
                _sendedBytes.OnNext(buff);
            }
        }
    }
}