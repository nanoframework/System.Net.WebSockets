//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace System.Net.WebSockets.WebSocketFrame
{
    /// <summary>
    /// Options to use with a ClientWebSocket object.
    /// </summary>
    public class WebSocketOptions
    {
        /// <summary>
        /// Gets or sets the WebSocket protocol keep-alive interval.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = new TimeSpan(0,0,60);

        /// <summary>
        /// Gets or sets the WebSocket timeout which specifies how long to wait for a message.
        /// </summary>
        /// <value>
        /// The WebSocket timeout which specifies how long to wait for a message.
        /// </value>
        public TimeSpan ServerTimeout { get; set; } = new TimeSpan(0,0,30);

        /// <summary>
        /// Gets or sets the maximum allowed byte length of messages received by the WebSocket .
        /// </summary>
        /// <value>
        /// The maximum allowed byte length of messages received by the WebSocket.
        /// </value>
        public int MaxReceiveFrameSize { get; set; } = int.MaxValue;

        /// <summary>
        /// Gets or sets the maximum allowed byte length of a partial message send by the WebSocket.
        /// By default if a message that exceeds the size limit it will be broken up in smaller partial messages
        /// </summary>
        /// <value>
        /// The maximum allowed byte length of a partial message send by the WebSocket.
        /// </value>
        public int MaxFragmentSize { get; private set; } = 1024;
    }
}
