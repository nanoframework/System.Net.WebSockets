namespace nanoframework.System.Net.Websockets
{
    internal class SendMessageFrame : MessageFrame
    {
        public byte[] Buffer { get; set; }
        public int FragmentSize { get; set; } = 0;
        public bool IsFragmented { get => FragmentSize > 127; }

    }
}
