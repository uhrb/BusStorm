using BusStorm.Sockets;

namespace BusStorm.SimpleMessage
{
    public static class BusExtensions
    {
        public static BusCommands AsCommand(this int value)
        {
            try
            {
                return (BusCommands)value;
            }
            catch
            {
                return BusCommands.UnknownCommand;
            }    
        }

    
        //public static 
    }

}