using nanoframework.System.Net.Websockets.WebSocketFrame;
using System.Net;

namespace nanoframework.System.Net.Websockets
{
    public class ReceiveMessageFrame : MessageFrame
    {

        internal bool IsMasked { get; set; } = false;
        public FragmentationType Fragmentation { get; set; }
        public bool IsFragmented { get => Fragmentation != FragmentationType.NotFragmented; }
        
        public int MessageLength { get; set; }
        internal byte[] Masks { get; set; } = new byte[4];
        public ReceiveMessageStream MessageStream { get; set; }

        internal WebSocketCloseStatus CloseStatus { get; set; } = WebSocketCloseStatus.Empty;
    }
}
