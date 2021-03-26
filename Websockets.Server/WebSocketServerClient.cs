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
        internal WebSocketServerClient(WebSocketServerOptions options) : base(options)
        {
            
        }


        internal void ConnectToStream(WebSocketStream stream, IPEndPoint remoteEndPoint, MessageReceivedEventHandler messageReceivedHandler)
        {
            base.ConnectToStream(stream, true, remoteEndPoint, messageReceivedHandler);
        }

    }
}
