namespace BusStorm.Sockets
{
    public interface IBusProtocolFactory<T> where T: BusStormMessageBase
    {
        int HeaderSize { get; }
        T MessageFactory();
        void TranslateHeader(byte[] headerBytes, T currentMessage);
        void TranslatePayload(byte[] payloadBytes, T currentMessage);
        byte[] SerializeMessage(T message);
    }
}