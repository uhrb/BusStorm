using System;
using System.IO;
using System.Linq;
using System.Text;
using BusStorm.Logging;
using BusStorm.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusStorm.SimpleMessage
{
    public class SimpleBusMessageBusProtocolFactory : IBusProtocolFactory<BusMessage>
    {

        private static readonly int GuidSize;
        
        private static readonly int StaticHeaderSize;
        private readonly string _encryptionKey;

        static SimpleBusMessageBusProtocolFactory()
        {
            GuidSize = Guid.NewGuid().ToByteArray().Length;
            StaticHeaderSize= GuidSize*3 + sizeof (int) + sizeof (bool) + sizeof (int);
        }

        public SimpleBusMessageBusProtocolFactory(string encryptionKey)
        {
            _encryptionKey = encryptionKey;
        }

        public int HeaderSize { get { return StaticHeaderSize; } }

        public BusMessage MessageFactory()
        {
            return new BusMessage();
        }

        public void TranslateHeader(byte[] header, BusMessage currentMessage)
        {
            var toBytes = new byte[GuidSize];
            var fromBytes = new byte[GuidSize];
            var sequenceBytes = new byte[GuidSize];
            var commandBytes = new byte[sizeof (int)];
            var isManualBytes = new byte[sizeof (bool)];
            var payloadLengthBytes = new byte[sizeof (int)];
            Array.Copy(header, toBytes, toBytes.Length);
            Array.Copy(header, GuidSize, fromBytes, 0, fromBytes.Length);
            Array.Copy(header, GuidSize*2, sequenceBytes, 0, sequenceBytes.Length);
            Array.Copy(header, GuidSize*3, commandBytes, 0, commandBytes.Length);
            Array.Copy(header, GuidSize*3 + sizeof (int), isManualBytes, 0, isManualBytes.Length);
            Array.Copy(header, GuidSize*3 + sizeof (int) + sizeof (bool), payloadLengthBytes, 0,
                       payloadLengthBytes.Length);
            currentMessage.Command = BitConverter.ToInt32(commandBytes, 0).AsCommand();
            currentMessage.From = new Guid(fromBytes);
            currentMessage.To = new Guid(toBytes);
            currentMessage.SequenceId = new Guid(sequenceBytes);
            currentMessage.IsManualPayload = BitConverter.ToBoolean(isManualBytes, 0);
            currentMessage.OnWirePayloadSize = BitConverter.ToInt32(payloadLengthBytes, 0);
        }

        public void TranslatePayload(byte[] payloadBytes, BusMessage currentMessage)
        {
            if (currentMessage.OnWirePayloadSize == 0)
            {
                return;
            }

            var bytes = new BusCryptor().DecryptData(payloadBytes, _encryptionKey);

            if (currentMessage.IsManualPayload)
            {
                currentMessage.Payload = payloadBytes;
            }

            if (bytes.Length == 0)
            {
                return;
            }

            currentMessage.Payload = JObject.Parse(Encoding.UTF8.GetString(bytes));
        }

        public byte[] SerializeMessage(BusMessage message)
        {
            Tracer.Log("Protocol message to byte array conversion fired");
            byte[] smBytes;
            if (message.Payload == null)
            {
                smBytes = new byte[] { };
            }
            else if (message.IsManualPayload)
            {
                smBytes = (byte[])message.Payload;
            }
            else
            {
                using (var ms = new MemoryStream())
                {
                    using (TextWriter tw = new StreamWriter(ms))
                    {
                        JsonSerializer.Create().Serialize(tw, message.Payload);
                        tw.Flush();
                        smBytes = ms.ToArray();
                    }
                }
            }

            smBytes = new BusCryptor().EncryptData(smBytes, _encryptionKey);

            message.OnWirePayloadSize = smBytes.Length;
            var bb = message.To.ToByteArray()
              .Concat(message.From.ToByteArray())
              .Concat(message.SequenceId.ToByteArray())
              .Concat(BitConverter.GetBytes((int)message.Command))
              .Concat(BitConverter.GetBytes(message.IsManualPayload))
              .Concat(BitConverter.GetBytes(smBytes.Length))
              .Concat(smBytes)
              .ToArray();
            Tracer.Log("Protocol message converted to {0} bytes", bb.Length);
            return bb;
        }
    }
}