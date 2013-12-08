using System;
using System.Collections.Generic;
using System.Threading;

namespace BusStorm.Sockets.ServerLoadTest
{
    public class Program
    {
        private static string _hostString;
        private static string _portString;
        private const string EncKey = "networkpassword";

        public static void Main()
        {
            _hostString = "localhost";
            _portString = "10137";
            var connectionString = "10";
            Console.Write("Enter host ({0}):", _hostString);
            if (string.IsNullOrEmpty(_hostString = Console.ReadLine()))
            {
                _hostString = "localhost";
            }
            Console.Write("Enter port ({0}):", _portString);
            if (string.IsNullOrEmpty(_portString = Console.ReadLine()))
            {
                _portString = "10137";
            }
            Console.Write("Enter number of connections ({0}):",connectionString);
            if (string.IsNullOrEmpty(connectionString = Console.ReadLine()))
            {
                connectionString = "10";
            }
            Console.Write("Ready to connect to {0}:{1}. Press ENTER to connect...", _hostString, _portString);
            var list = new List<Thread>();
            for (var i = 0; i < Convert.ToInt32(connectionString)-2; i++)
            {
                list.Add(new Thread(ClientWorker));
            }
            list.Add(new Thread(SendAndReceiveWorker));
            list.Add(new Thread(SendAndReceiveWorker));
            foreach (var t in list)
            {
                t.Start(new Random().Next(100,2000));
            }
            Console.Write("Connection complete. Press Enter to disconnect.");
            Console.ReadLine();
            foreach (var thread in list)
            {
                thread.Abort();
                thread.Join();
            }
            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();
        }

        private static void SendAndReceiveWorker(object state)
        {
            var sleep = (int) state;
            var clientId = Guid.NewGuid();
            var client = new BusClientSocket(_hostString, Convert.ToInt32(_portString),EncKey);
            client.Connect();
            try
            {
                while (true)
                {
                    var msg = client.SendAndReceive(new BusMessage
                    {
                        Command = BusCommands.ReplyGovernor,
                        From = clientId,
                        To = BusMessage.BROADCAST,
                        SequenceId = Guid.NewGuid(),
                        Payload = new
                        {
                            message = "Hello from client!"
                        }
                    });
                    Console.WriteLine("Reply was {0}",msg.Command);
                    Thread.Sleep(sleep);
                }
            }
            catch (ThreadAbortException)
            {

            }
            finally
            {
                client.Disconnect();
            }
        }

        private static void ClientWorker(object state)
        {
            var sleep = (int)state;
            var clientId = Guid.NewGuid();
            var client = new BusClientSocket(_hostString, Convert.ToInt32(_portString),EncKey);
            client.Connect();
            try
            {
                while (true)
                {
                    client.SendAsync(new BusMessage
                        {
                            Command = BusCommands.ReplyGovernor,
                            From = clientId,
                            To = BusMessage.BROADCAST,
                            SequenceId = Guid.NewGuid(),
                            Payload = new
                                {
                                    message = "Hello from client!"
                                }
                        });
                    Thread.Sleep(sleep);
                }
            }
            catch (ThreadAbortException)
            {
                
            }
            finally
            {
                client.Disconnect();
            }
        }
    }
}
