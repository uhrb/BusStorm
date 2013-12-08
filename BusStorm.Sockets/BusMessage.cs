using System;
using System.IO;
using System.Linq;
using System.Text;
using BusStorm.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusStorm.Sockets
{
    public class BusMessage
    {
        private static readonly int GuidSize;

        public static readonly Guid BROADCAST = Guid.Empty;

        internal static int HeaderSize { get; private set; }

        static BusMessage()
        {
            GuidSize = Guid.NewGuid().ToByteArray().Length;
            HeaderSize = GuidSize*3 + sizeof (int) + sizeof (bool) + sizeof (int);
        }

        public BusMessage()
        {
            IsManualPayload = false;
        }

        public Guid From { get; set; }

        public Guid To { get; set; }

        public Guid SequenceId { get; set; }

        public  BusCommands Command { get; set; }

        public bool IsManualPayload { get; set; }

        public int OnWirePayloadLength { get; set; }

        public dynamic Payload { get; set; }

        internal byte[] ToByteArray(string encryptionKey)
        {
            Tracer.Log("Protocol message to byte array conversion fired");
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

            smBytes = new BusCryptor().EncryptData(smBytes, encryptionKey);

            OnWirePayloadLength = smBytes.Length;
                  var bb =   To.ToByteArray()
                    .Concat(From.ToByteArray())
                    .Concat(SequenceId.ToByteArray())
                    .Concat(BitConverter.GetBytes((int)Command))
                    .Concat(BitConverter.GetBytes(IsManualPayload))
                    .Concat(BitConverter.GetBytes(smBytes.Length))
                    .Concat(smBytes)
                    .ToArray();
            Tracer.Log("Protocol message converted to {0} bytes",bb.Length);
            return bb;
        }

        internal static BusMessage TransalteHeader(byte[] header)
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
            return new BusMessage
                {
                    Command = BitConverter.ToInt32(commandBytes, 0).AsCommand(),
                    From = new Guid(fromBytes),
                    To = new Guid(toBytes),
                    SequenceId = new Guid(sequenceBytes),
                    IsManualPayload = BitConverter.ToBoolean(isManualBytes, 0),
                    OnWirePayloadLength = BitConverter.ToInt32(payloadLengthBytes, 0)
                };
        }

        public void TranslatePayload(byte[] payloadBytes,string encryptionKey)
        {
            if (OnWirePayloadLength == 0)
            {
                return;
            }

            var bytes = new BusCryptor().DecryptData(payloadBytes, encryptionKey);

            if (IsManualPayload)
            {
                Payload = payloadBytes;
            }

            if (bytes.Length == 0)
            {
                return;
            }

            Payload = JObject.Parse(Encoding.UTF8.GetString(bytes));
        }
    }
}