using System;
using System.IO;
using System.Net.Sockets;

namespace nanoframework.System.Net.Websockets.WebSocketFrame
{
    public class WebSocketStream : Stream
    {
        private NetworkStream _networkStream;

        public bool IsSslSteam = false;

        public override bool CanRead => _networkStream.CanRead;

        public override bool CanSeek =>  _networkStream.CanSeek;

        public override bool CanWrite =>  _networkStream.CanWrite;

        public override long Length =>_networkStream.Length;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            _networkStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _networkStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _networkStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _networkStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _networkStream.Write(buffer, offset, count);
        }

        public bool DataAvailable => _networkStream.DataAvailable;

        public WebSocketStream(NetworkStream networkStream)
        {
            _networkStream = networkStream;
        }
    }
}
