//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace System.Net.WebSockets.WebSocketFrame
{
    /// <summary>
    /// Defines the different states a WebSockets instance can be in.
    /// </summary>
    public enum WebSocketState
    {
        /// <summary>
        /// Reserved for future use.
        /// </summary>
        None = 0,

        /// <summary>
        /// The connection is negotiating the handshake with the remote endpoint.
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// The initial state after the HTTP handshake has been completed.
        /// </summary>
        Open = 2,

        /// <summary>
        /// A close message was sent to the remote endpoint.
        /// </summary>
        CloseSent = 3,

        /// <summary>
        /// A close message was received from the remote endpoint.
        /// </summary>
        CloseReceived = 4,

        /// <summary>
        /// Indicates the WebSocket close handshake completed gracefully.
        /// </summary>
        Closed = 5,

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        Aborted = 6
    }
}
