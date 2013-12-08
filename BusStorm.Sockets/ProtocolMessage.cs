using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusStorm.Sockets
{
    public class ProtocolMessage
    {
        private static readonly int GuidSize;

        public static int HeaderSize { get; private set; }

        static ProtocolMessage()
        {
            GuidSize = Guid.NewGuid().ToByteArray().Length;
            HeaderSize = GuidSize*3 + sizeof (int) + sizeof (bool) + sizeof (int);
        }

        public Guid From { get; set; }

        public Guid To { get; set; }

        public Guid SequenceId { get; set; }

        public  StandartCommands Command { get; set; }

        public bool IsManualPayload { get; set; }

        public int OnWirePayloadLength { get; set; }

        public dynamic Payload { get; set; }

        internal byte[] ToByteArray()
        {
            byte[] smBytes;
            if (Payload == null)
            {
                smBytes = new byte[] { };
            }
            else if (IsManualPayload)
            {
                smBytes = (byte[])Payload;
            }
            else
            {
                using (var ms = new MemoryStream())
                {
                    using (TextWriter tw = new StreamWriter(ms))
                    {
                        JsonSerializer.Create().Serialize(tw, Payload);
                        tw.Flush();
                        smBytes = ms.ToArray();
                    }
                }
            }
            OnWirePayloadLength = smBytes.Length;
            return
                    To.ToByteArray()
                    .Concat(From.ToByteArray())
                    .Concat(SequenceId.ToByteArray())
                    .Concat(BitConverter.GetBytes((int)Command))
                    .Concat(BitConverter.GetBytes(IsManualPayload))
                    .Concat(BitConverter.GetBytes(smBytes.Length))
                    .Concat(smBytes)
                    .ToArray();
        }

        internal static ProtocolMessage TransalteHeader(byte[] header)
        {
            var toBytes = new byte[GuidSize];
            var fromBytes = new byte[GuidSize];
            var sequenceBytes = new byte[GuidSize];
            var commandBytes = new byte[sizeof (int)];
            var isManualBytes = new byte[sizeof (bool)];
            var payloadLengthBytes = new byte[sizeof (int)];
            Array.Copy(header,toBytes,toBytes.Length);
            Array.Copy(header,GuidSize,fromBytes,0,fromBytes.Length);
            Array.Copy(header,GuidSize*2,sequenceBytes,0,sequenceBytes.Length);
            Array.Copy(header,GuidSize*3,commandBytes,0,commandBytes.Length);
            Array.Copy(header,GuidSize*3+sizeof(int),isManualBytes,0,isManualBytes.Length);
            Array.Copy(header,GuidSize*3+sizeof(int)+sizeof(bool),payloadLengthBytes,0,payloadLengthBytes.Length);
            return new ProtocolMessage
                {
                    Command = BitConverter.ToInt32(commandBytes, 0).AsCommand(),
                    From = new Guid(fromBytes),
                    To = new Guid(toBytes),
                    SequenceId = new Guid(sequenceBytes),
                    IsManualPayload = BitConverter.ToBoolean(isManualBytes, 0),
                    OnWirePayloadLength = BitConverter.ToInt32(payloadLengthBytes, 0)
                };
        }

        public void TranslatePayload(byte[] payloadBytes)
        {
            if (OnWirePayloadLength == 0)
            {
                return;
            }

            if (IsManualPayload)
            {
                Payload = payloadBytes;
            }

            Payload = JObject.Parse(Encoding.UTF8.GetString(payloadBytes));
        }
    }
}