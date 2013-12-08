using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace BusStorm.Sockets
{
    public class ClientConnection
    {
        private readonly long _connectionNumber;
        private readonly Subject<ProtocolMessage> _receivedMessages;
        private readonly Subject<byte[]> _sendedBytes;
        private readonly List<byte> _currentReceiveBuffer;
        private ProtocolMessage _currentMessage;
        private readonly object _receiveLocker = new object();

        protected ClientConnection()
        {
            _currentReceiveBuffer = new List<byte>();
            _receivedMessages = new Subject<ProtocolMessage>();
            _sendedBytes = new Subject<byte[]>();
        }

        internal ClientConnection(Socket socket, long connectionNumber):this()
        {
            _connectionNumber = connectionNumber;
            Socket = socket;
            StartReceiving();
        }

        protected Socket Socket { get; set; }

        public long ConnectionNumber { get { return _connectionNumber; } }

        public IObservable<ProtocolMessage> ReceivedMessages { get { return _receivedMessages.ObserveOn(NewThreadScheduler.Default); }}

        public IObservable<byte[]> SendedBytes { get { return _sendedBytes.ObserveOn(NewThreadScheduler.Default); } }

        protected void StartReceiving()
        {
            Task.Run(() =>
                {
                    var buffer = new byte[1024];
                    var args = new SocketAsyncEventArgs();
                    args.SetBuffer(buffer, 0, buffer.Length);
                    args.Completed += AsyncSocketOperationCompleted;
                    if (!Socket.ReceiveAsync(args))
                    {
                        AsyncSocketOperationCompleted(Socket, args);
                    }
                }).ConfigureAwait(false);
        }

        void AsyncSocketOperationCompleted(object sender, SocketAsyncEventArgs e)
        {
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
            return Task.Run(() =>
                {
                    var args = new SocketAsyncEventArgs();
                    args.Completed += AsyncSocketOperationCompleted;
                    args.SetBuffer(data, 0, data.Length);
                    if (!Socket.SendAsync(args))
                    {
                        AsyncSocketOperationCompleted(Socket, args);
                    }
                });
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                var ee = new SocketException();
                ee.Data.Add("SocketError", e.SocketError);
                _receivedMessages.OnError(ee);
            }
            else
            {
                var bytes = new byte[e.BytesTransferred];
                Array.Copy(e.Buffer,bytes,e.BytesTransferred);
                Console.WriteLine("Received {0} bytes",bytes.Length);
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
            }
            StartReceiving();
        }

        private void StartHeaderReceiving()
        {
            if (_currentReceiveBuffer.Count < ProtocolMessage.HeaderSize)
            {
                return;
            }
            // header in buffer, lets translate
            var headerBytes = _currentReceiveBuffer.GetRange(0, ProtocolMessage.HeaderSize).ToArray();
            var msg = ProtocolMessage.TransalteHeader(headerBytes);
            _currentMessage = msg;
            // remove header bytes from buffer
            _currentReceiveBuffer.RemoveRange(0, ProtocolMessage.HeaderSize);
        }

        private void CheckMessageComplete()
        {
            // message not started - exiting
            if (_currentMessage == null)
            {
                return;
            }

            if (_currentReceiveBuffer.Count < _currentMessage.OnWirePayloadLength)
            {
                // payload receive is not complete
                return;
            }
            // received payload
            var payloadBytes = _currentReceiveBuffer.GetRange(0, _currentMessage.OnWirePayloadLength).ToArray();
            // translate payload
            _currentMessage.TranslatePayload(payloadBytes);
            var msg = _currentMessage;
            _currentMessage = null;
            _currentReceiveBuffer.RemoveRange(0, msg.OnWirePayloadLength);
            _receivedMessages.OnNext(msg);
            if (_currentReceiveBuffer.Count > 0)
            {
                // the rest of buffer is new message
                StartHeaderReceiving();
            }
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
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
                Console.WriteLine("Sended {0} bytes",buff.Length);
                _sendedBytes.OnNext(buff);
            }
        }
    }
}