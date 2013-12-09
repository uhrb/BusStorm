BusStorm
========

Bus storm protocol libraries


BusStorm is libraries to build your own client/server based on binary protocol.

All that you need - implement IBusProtocolFactory and pass instance of
implementation class to server/client ctor`s. 
this libraries are developed for using fixed size header protocol,
but you can try using it with valuable size header - just implement some
logic in factory in HeaderSize getter.



