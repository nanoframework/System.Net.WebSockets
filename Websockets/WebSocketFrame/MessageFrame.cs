using nanoframework.System.Net.Websockets.WebSocketFrame;
using System.Net;

namespace nanoframework.System.Net.Websockets
{
    public class MessageFrame
    {
        internal OpCode OpCode { get; set; }
        internal bool IsControllFrame { get => (int)OpCode > 7; }
        internal bool Error { get => ErrorMessage != null; }
        internal string ErrorMessage { get; set; } = null;
        public IPEndPoint EndPoint { get; set; }
        public WebSocketMessageType MessageType => OpCode == OpCode.TextFrame ? WebSocketMessageType.Text : WebSocketMessageType.Binary;

    }
}
