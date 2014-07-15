using System;
using BusStorm.Sockets;

namespace BusStorm.SimpleMessage
{
  public class BusMessage : BusStormMessageBase
  {
    public static readonly Guid BROADCAST = Guid.Empty;

        public BusMessage()
        {
            IsManualPayload = false;
        }

        public Guid From { get; set; }

        public Guid To { get; set; }

        public Guid SequenceId { get; set; }

        public  BusCommands Command { get; set; }

        public bool IsManualPayload { get; set; }

        public dynamic Payload { get; set; }
    }
}