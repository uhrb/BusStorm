using System;

namespace BusStorm.Sockets.ClientConsole
{
    public class Program
    {
        static void Main()
        {
            var clientId = Guid.NewGuid();
            var hostString = "localhost";
            var portString = "10137";
            Console.Write("Enter host ({0}):",hostString);
            if (string.IsNullOrEmpty(hostString = Console.ReadLine()))
            {
                hostString = "localhost";
            }
            Console.Write("Enter port ({0}):",portString);
            if (string.IsNullOrEmpty(portString = Console.ReadLine()))
            {
                portString = "10137";
            }
            Console.Write("Ready to connect to {0}:{1}. Press ENTER to connect...",hostString,portString);
            Console.ReadLine();
            var client = new BusClientSocket(hostString, Convert.ToInt32(portString));
            client.Connect();
            string userInput;
            Console.Write(">");
            while ("exit" != (userInput = Console.ReadLine()))
            {
                switch (userInput)
                {

                    case "r":
                        client.SendAsync(new BusMessage
                            {
                                Command = BusCommands.ReplyGovernor,
                                From = clientId,
                                To = BusMessage.BROADCAST,
                                SequenceId = Guid.NewGuid(),
                            });
                        break;
                    case "rr":
                        client.SendAsync(new BusMessage
                        {
                            Command = BusCommands.ReplyGovernor,
                            From = clientId,
                            To = BusMessage.BROADCAST,
                            SequenceId = Guid.NewGuid(),
                            Payload = new
                                {
                                    message = "Hello!"
                                }
                        });
                        break;
                }
                Console.Write("\n>");
            }
            client.Disconnect();
            Console.Write("Connection complete. Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
