using System;
using System.Text;

namespace nanoframework.System.Net.Websockets
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
