//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace System.Net.WebSockets
{
    internal class SendMessageFrame : MessageFrame
    {
        public byte[] Buffer { get; set; }

        public int FragmentSize { get; set; } = 0;

        public bool IsFragmented { get => FragmentSize > 127; }
    }
}
