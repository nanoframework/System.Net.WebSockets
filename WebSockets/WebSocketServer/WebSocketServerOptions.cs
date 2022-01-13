//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Net.WebSockets.WebSocketFrame;

namespace System.Net.WebSockets.Server
{
    /// <summary>
    /// Options to use with a WebSocketServer object.
    /// </summary>
    public class WebSocketServerOptions : WebSocketOptions
    {
        /// <summary>
        /// The local Port to listen on.
        /// </summary>
        public int Port { get; set; } = 80;

        /// <summary>
        /// The server name that is presented to the client during the handshake
        /// </summary>
        public string ServerName { get; set; } = "NFWebSocketServer";

        /// <summary>
        /// The maximum number of clients that can connect to the server.
        /// </summary>
        public int MaxClients { get; set; } = 10;

        /// <summary>
        /// The remote Prefix clients need to connect to.
        /// </summary>
        public string Prefix { get; set; } = "/";

        /// <summary>
        /// Determines if the websocket runs as a independent websocket server with it's own HttpListner.
        /// If set to true, the Websocket server will run a dedicated HttpListner on the defined port.
        /// If set to false, one has to run their own HTTPListner and use WebsocketServer.AddWebSocket(HttpListenerContext context) to manually add Websocket clients.
        /// </summary>
        public bool IsStandAlone { get; set; } = true;
    }
}
