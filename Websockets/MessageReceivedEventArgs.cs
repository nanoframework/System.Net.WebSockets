//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace nanoframework.System.Net.Websockets
{
    /// <summary>
    /// Provides data for the MessageReceived event.
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The message frame received by the WebSocket. 
        /// </summary>
        public ReceiveMessageFrame Frame { get; set; }
    }
}
