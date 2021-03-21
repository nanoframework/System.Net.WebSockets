using System;
using System.Text;

namespace nanoframework.System.Net.Websockets
{

    public enum FragmentationType
    {
        NotFragmented,
        FirstFragment,
        Fragment,
        FinalFragment
    }
}
