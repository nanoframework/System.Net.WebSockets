//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace System.Net.WebSockets
{
    /// <summary>
    /// Indicates the message Fragmentation Type.
    /// </summary>
    public enum FragmentationType
    {
        /// <summary>
        /// The message is not fragmented.
        /// </summary>
        NotFragmented,

        /// <summary>
        /// Message Frame contains the first fragment of a fragmented message.
        /// </summary>
        FirstFragment,

        /// <summary>
        /// Message Frame contains a next fragment of the message.
        /// </summary>
        Fragment,

        /// <summary>
        /// Message frame contains the last fragment of the message. 
        /// </summary>
        FinalFragment
    }
}
