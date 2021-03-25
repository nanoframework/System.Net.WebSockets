using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Text;

namespace nanoframework.System.Net.Websockets.Server
{
    //
    // Summary:
    //     Options to use with a System.Net.WebSockets.WebSocketServer object.
    public class WebSocketServerOptions : WebSocketOptions
    {
        //
        // Summary:
        //     The local Port to listen on.
        public int Port { get; internal set; } = 80;

        //
        // Summary:
        //     The server name that is presented to the client during the handshake
        public string ServerName { get; internal set; } = "NFWebSocketServer";

        //
        // Summary:
        //     The maximum number of clients that can connect to the server.
        public int MaxClients { get; internal set; } = 10;
        //
        // Summary:
        //     The remote Prefix clients need to connect to.
        public string Prefix { get; internal set; } = "/";
    }
}
