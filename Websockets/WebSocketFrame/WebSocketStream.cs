using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace nanoframework.System.Net.Websockets.WebSocketFrame
{
    public class WebSocketStream : Stream
    {
        private SslStream _sslStream;
        private NetworkStream _networkStream;
        public bool IsSslSteam = false;
        public override bool CanRead => IsSslSteam ? _sslStream.CanRead : _networkStream.CanRead;

        public override bool CanSeek => IsSslSteam ? _sslStream.CanSeek : _networkStream.CanSeek;

        public override bool CanWrite => IsSslSteam ? _sslStream.CanWrite : _networkStream.CanWrite;

        public override long Length => IsSslSteam ? _sslStream.Length : _networkStream.Length;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            if (IsSslSteam) _sslStream.Flush();
            else _networkStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (IsSslSteam) return _sslStream.Read(buffer, offset, count);
            else return _networkStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (IsSslSteam) return _sslStream.Seek(offset, origin);
            else return _networkStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (IsSslSteam) _sslStream.SetLength(value);
            else _networkStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (IsSslSteam)  _sslStream.Write(buffer, offset, count);
            else  _networkStream.Write(buffer, offset, count);
        }

        public bool DataAvailable => IsSslSteam ? _sslStream.DataAvailable : _networkStream.DataAvailable;


        public WebSocketStream(NetworkStream networkStream)
        {
            _networkStream = networkStream;
        }

        public WebSocketStream(SslStream sslStream)
        {
            _sslStream = sslStream;
            IsSslSteam = true;
        }
    }
}
