using nanoframework.System.Net.Websockets.WebSocketFrame;
using System.Net;

namespace nanoframework.System.Net.Websockets
{
    // Summary:
    //     The base WebSocket Message frame.
    public class MessageFrame
    {

        internal OpCode OpCode { get; set; }
        internal bool IsControllFrame { get => (int)OpCode > 7; }
        internal bool Error { get => ErrorMessage != null; }
        internal string ErrorMessage { get; set; } = null;

        // Summary:
        //     The Remote Endpoint from which the message is received.
        public IPEndPoint EndPoint { get; set; }

        //
        // Summary:
        //     Indicates whether the current message is a UTF-8 message or a binary message.
        //
        // Returns:
        //     Returns System.Net.WebSockets.WebSocketMessageType.
        public WebSocketMessageType MessageType => OpCode == OpCode.TextFrame ? WebSocketMessageType.Text : WebSocketMessageType.Binary;

    }
}
