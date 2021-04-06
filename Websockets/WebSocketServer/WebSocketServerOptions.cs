using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Text;

namespace nanoframework.System.Net.Websockets.Server
{
    /// <summary>
    /// Options to use with a WebSocketServer object.
    /// </summary>
    public class WebSocketServerOptions : WebSocketOptions
    {
        /// <summary>
        /// The local Port to listen on.
        /// </summary>
        public int Port { get; internal set; } = 80;

        /// <summary>
        /// The server name that is presented to the client during the handshake
        /// </summary>
        public string ServerName { get; internal set; } = "NFWebSocketServer";

        /// <summary>
        /// The maximum number of clients that can connect to the server.
        /// </summary>
        public int MaxClients { get; internal set; } = 10;

        /// <summary>
        /// The remote Prefix clients need to connect to.
        /// </summary>
        public string Prefix { get; internal set; } = "/";
    }
}
