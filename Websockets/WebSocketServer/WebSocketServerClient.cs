//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Net.Sockets;
using System.Net.WebSockets.WebSocketFrame;

namespace System.Net.WebSockets.Server
{
    internal class WebSocketServerClient : WebSocket
    {
        public override WebSocketState State { get; set; } = WebSocketState.Closed;
        internal WebSocketContext WebSocketContext;

        internal WebSocketServerClient(WebSocketServerOptions options) : base(options)
        {
            
        }

        internal void ConnectToStream(NetworkStream stream, Socket socket, WebSocketContext webSocketContext ,MessageReceivedEventHandler messageReceivedHandler)
        {
            WebSocketContext = webSocketContext;
            ConnectToStream(stream, true, socket);
            base.MessageReceived += messageReceivedHandler;
        }
    }
}
