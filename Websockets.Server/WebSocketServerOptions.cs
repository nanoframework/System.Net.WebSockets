using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Text;

namespace nanoframework.System.Net.Websockets.Server
{
    public class WebSocketServerOptions : WebSocketOptions
    {
        public int Port { get; internal set; } = 80;
        public string ServerName { get; internal set; } = "NFWebSocketServer";
        public int MaxClients { get; internal set; } = 10;
        public string Prefix { get; internal set; } = "/";
    }
}
