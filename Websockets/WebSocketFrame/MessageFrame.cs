using System.Net;

namespace nanoframework.System.Net.Websockets
{
    public class MessageFrame
    {
        public OpCode OpCode { get; set; }
        public bool IsControllFrame { get => (int)OpCode > 7; }
        public bool Error { get => ErrorMessage != null; }
        public string ErrorMessage { get; set; } = null;
        public IPEndPoint EndPoint { get; set; }


    }
}
