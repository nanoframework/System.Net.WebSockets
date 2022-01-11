//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Net.WebSockets.WebSocketFrame;

namespace System.Net.WebSockets
{
    /// <summary>
    /// The Receive Message Frame
    /// </summary>
    public class ReceiveMessageFrame : MessageFrame
    {
        internal bool IsMasked { get; set; } = false;
        internal byte[] Masks { get; set; } = new byte[4];
        internal WebSocketCloseStatus CloseStatus { get; set; } = WebSocketCloseStatus.Empty;

        /// <summary>
        /// Indicates if the message is fragmented. And what fragment is received. 
        /// </summary>
        public FragmentationType Fragmentation { get; set; }

        /// <summary>
        /// Indicates if the message is fragmented.
        /// </summary>
        public bool IsFragmented { get => Fragmentation != FragmentationType.NotFragmented; }

        /// <summary>
        /// The content length of the message in number of bytes.
        /// </summary>
        public int MessageLength { get; set; }

        /// <summary>
        /// Buffer holding the message content.
        /// </summary>
        public byte[] Buffer { get; set; }
    }
}
