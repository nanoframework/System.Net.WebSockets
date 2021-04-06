//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Net.WebSockets.WebSocketFrame;
using System.Net;
using System.Net.Sockets;

namespace System.Net.WebSockets.Server
{
    internal class WebSocketServerClient : WebSocket
    {
        public override WebSocketState State { get; set; } = WebSocketState.Closed;

        internal WebSocketServerClient(WebSocketServerOptions options) : base(options)
        {
            
        }

        internal void ConnectToStream(NetworkStream stream, IPEndPoint remoteEndPoint, MessageReceivedEventHandler messageReceivedHandler)
        {
            ConnectToStream(stream, true, remoteEndPoint);
            base.MessageReceived += messageReceivedHandler;
        }
    }
}
