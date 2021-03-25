using System;
using System.Text;

namespace nanoframework.System.Net.Websockets
{
    //
    // Summary:
    //     Indicates the message Fragmentation Type.
    public enum FragmentationType
    {
        //
        // Summary:
        //     The message is not fragmented.
        NotFragmented,

        //
        // Summary:
        //     Message Frame contains the first fragment of a fragmented message.
        FirstFragment,

        //
        // Summary:
        //     Message Frame contains a next fragment of the message.
        Fragment,

        //
        // Summary:
        //     Message frame contains the last fragment of the message.
        FinalFragment
    }
}
