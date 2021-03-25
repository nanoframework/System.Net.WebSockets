using nanoframework.System.Net.Websockets.WebSocketFrame;
using System.Net;

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

        // Summary:
        //     The MessageStream that contains the message.
        //
        // Remarks:
        //     The WebSocket will stop to receive any incoming messages including controller messages until 
        //     the stream is completely read till the end. 
        public ReceiveMessageStream MessageStream { get; set; }

        internal WebSocketCloseStatus CloseStatus { get; set; } = WebSocketCloseStatus.Empty;
    }
}
