//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoframework.System.Net.Websockets
{
    internal enum OpCode
    {
        ContinuationFrame = 0,
        TextFrame = 1,
        BinaryFrame = 2,
        ConnectionCloseFrame = 8,
        PingFrame = 9,
        PongFrame = 10
    }
}
