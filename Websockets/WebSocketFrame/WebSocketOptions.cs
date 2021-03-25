using System;

using System.Text;

namespace nanoframework.System.Net.Websockets.WebSocketFrame
{
    //
    // Summary:
    //     Options to use with a System.Net.WebSockets.ClientWebSocket object.
    public class WebSocketOptions
    {

        //
        // Summary:
        //     Gets or sets the WebSocket protocol keep-alive interval.
        //
        // Returns:
        //     The WebSocket protocol keep-alive interval.
        public TimeSpan KeepAliveInterval { get; set; } = new TimeSpan(0,0,60);

        //
        // Summary:
        //     Gets or sets the WebSocket timeout which specifies how long to wait for a message.
        //
        //  Returns:
        //     The WebSocket timeout which specifies how long to wait for a message.
        public TimeSpan ServerTimeout { get; set; } = new TimeSpan(0,0,30);

        //
        // Summary:
        //     Gets or sets the maximum allowed byte length of messages received by the WebSocket .
        //
        //  Returns:
        //     The maximum allowed byte length of messages received by the WebSocket.
        public int MaxReceiveFrameSize { get; set; } = int.MaxValue;

        //
        // Summary:
        //     Gets or sets the maximum allowed byte length of a partial message send by the WebSocket. By default if a message that exceeds the size limit it will be broken up in smaller partial messages
        //
        //  Returns:
        //     The maximum allowed byte length of a partial message send by the WebSocket.
        public int MaxFragmentSize { get; private set; } = 1024;

    }
}
