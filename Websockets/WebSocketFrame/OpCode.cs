namespace nanoframework.System.Net.Websockets
{
    public enum OpCode
    {
        ContinuationFrame = 0,
        TextFrame = 1,
        BinaryFrame = 2,
        ConnectionCloseFrame = 8,
        PingFrame = 9,
        PongFrame = 10
    }
}
