namespace BusStorm.Sockets
{
    public interface IProtocolFactory<T> where T: BusStormMessageBase
    {
        int HeaderSize { get; }
        T MessageFactory();
        void TranslateHeader(byte[] headerBytes, T currentMessage);
        void TranslatePayload(byte[] payloadBytes, T currentMessage);
        byte[] SerializeMessage(T message);
        bool ReceivedSequenceSelector(T sendedMessage,T message);
        bool ReceiveMore(T sendedMessage,T message);
    }
}