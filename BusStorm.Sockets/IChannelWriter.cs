using System.Threading.Tasks;

namespace BusStorm.Sockets
{
    public interface IChannelWriter<in T>
    {
        Task SendAsync(T message);
    }
}
