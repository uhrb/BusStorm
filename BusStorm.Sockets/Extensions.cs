namespace BusStorm.Sockets
{
    public static class Extensions
    {
        public static StandartCommands AsCommand(this int value)
        {
            try
            {
                return (StandartCommands)value;
            }
            catch
            {
                return StandartCommands.UnknownCommand;
            }    
        }
    }
}