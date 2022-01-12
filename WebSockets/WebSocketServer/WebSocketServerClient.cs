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

        internal WebSocketServerClient(WebSocketServerOptions options) : base(options)
        {
            
        }

        internal void ConnectToStream(WebSocketContext webSocketContext ,MessageReceivedEventHandler messageReceivedHandler)
        {
            ConnectToStream(webSocketContext.NetworkStream, true, webSocketContext.Socket);
            base.MessageReceived += messageReceivedHandler;
        }
    }
}
