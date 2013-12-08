using System;
using System.Reactive.Disposables;

namespace BusStorm.Sockets.ServerConsole
{
    public class Program
    {
        public static void Main()
        {
            var portString = "10137";
            Console.Write("Enter port ({0}):",portString);
            if (string.IsNullOrEmpty(portString = Console.ReadLine()))
            {
                portString = "10137";
            }
            Console.Write("Ready to start server on 0.0.0.0:{0}. Press ENTER to start",portString);
            Console.ReadLine();
            var server = new BusServerSocket();
            var disp = new CompositeDisposable();
            var sub2= server.Connections.Subscribe(next =>
                {
                    var sub = next.ReceivedMessages.Subscribe(msg =>
                        {
                            if (msg.Command == BusCommands.ReplyGovernor)
                            {
                                next.SendAsync(new BusMessage
                                    {
                                        Command = BusCommands.Success,
                                        SequenceId = msg.SequenceId
                                    });
                            }
                            Console.WriteLine("Replyed to client");
                        }, error => Console.WriteLine("ON CLIENT ERROR {0}", error.Message),
                                                    () => Console.WriteLine("CLIENT COMPLETE"));
                    disp.Add(sub);
                }, error => Console.WriteLine("SERVER Connection error {0}", error.Message),() => Console.WriteLine("SERVER CONNECTION COMPLETE"));
            disp.Add(sub2);
            server.Start(Convert.ToInt32(portString));
            Console.WriteLine("Server started. Press ENTER to stop");
            Console.ReadLine();
            server.Stop();
            Console.WriteLine("Server stopped. Press Enter to exit");
            Console.ReadLine();
            disp.Dispose();
        }
    }
}
