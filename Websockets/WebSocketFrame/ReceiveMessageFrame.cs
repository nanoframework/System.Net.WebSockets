using nanoframework.System.Net.Websockets.WebSocketFrame;

namespace nanoframework.System.Net.Websockets
{
    // Summary:
    //     The Receive Message Frame
    public class ReceiveMessageFrame : MessageFrame
    {
        internal bool IsMasked { get; set; } = false;

        // Summary:
        //     Indicates if the message is fragmented. And what fragment is received. 
        public FragmentationType Fragmentation { get; set; }

        // Summary:
        //     Indicates if the message is fragmented.
        public bool IsFragmented { get => Fragmentation != FragmentationType.NotFragmented; }
        
        // Summary:
        //     The content length of the message in number of bytes.
        public int MessageLength { get; set; }
        
        internal byte[] Masks { get; set; } = new byte[4];

        public byte[] Buffer { get; set; }

        internal WebSocketCloseStatus CloseStatus { get; set; } = WebSocketCloseStatus.Empty;
    }
}
