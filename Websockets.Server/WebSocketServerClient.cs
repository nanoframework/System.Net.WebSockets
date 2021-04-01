using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace nanoframework.System.Net.Websockets.Server
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
