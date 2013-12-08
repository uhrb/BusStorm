using System;
using System.Collections.Generic;
using System.Threading;
using BusStorm.SimpleMessage;

namespace BusStorm.Sockets.ClientLoadTest
{
    public class Program
    {

        private static readonly List<BusClientConnection<BusMessage>> Connections = new List<BusClientConnection<BusMessage>>();
        private static readonly Guid ServerId = Guid.NewGuid();
        private const string EncKey = "networkpassword";

        static void Main()
        {
            var thread1 = new Thread(PingWorker);
            var thread2 = new Thread(PingWorker);
            var thread3 = new Thread(PingWorker);
            thread1.Start(1000);
            thread2.Start(200);
            thread3.Start(1200);
            var portString = "10137";
            Console.Write("Enter port ({0}):", portString);
            if (string.IsNullOrEmpty(portString = Console.ReadLine()))
            {
                portString = "10137";
            }
            Console.Write("Ready to start server on 0.0.0.0:{0}. Press ENTER to start", portString);
            Console.ReadLine();
            var server = new BusServerSocket<BusMessage>(new SimpleBusMessageProtocolFactory(EncKey));
            server.Connections.Subscribe(next =>
                {
                    Console.WriteLine("New connection accepted {0}", next.ConnectionNumber);
                    lock (Connections)
                    {
                        Connections.Add(next);
                    }
                }, 
                error => Console.WriteLine("Server fired accept connection error {0}",error), () =>
                    {
                        lock (Connections)
                        {
                            Connections.Clear();
                        }
                        Console.WriteLine("Server connections completed");
                    });
            server.Start(Convert.ToInt32(portString));
            Console.WriteLine("Server started. Press ENTER to stop");
            Console.ReadLine();
            thread1.Abort();
            thread2.Abort();
            thread3.Abort();
            thread1.Join();
            thread2.Join();
            thread3.Join();
            server.Stop();
            Console.WriteLine("Server stopped. Press Enter to exit");
            Console.ReadLine();
        }

        private static void PingWorker(object state)
        {
            var sleep = (int) state;
            try
            {
                while (true)
                {
                    BusClientConnection<BusMessage>[] cons;
                    lock (Connections)
                    {
                        cons = Connections.ToArray();
                    }
                    foreach (var con in cons)
                    {
                        con.SendAsync(new BusMessage
                            {
                                Command = BusCommands.Ping,
                                From = ServerId,
                                To = BusMessage.BROADCAST,
                                SequenceId = Guid.NewGuid(),
                                Payload = new
                                    {
                                        message = "Ping from server"
                                    }
                            });
                    }
                    Thread.Sleep(sleep);
                }
            }
            catch (ThreadAbortException)
            {
            }
        }
    }
}
