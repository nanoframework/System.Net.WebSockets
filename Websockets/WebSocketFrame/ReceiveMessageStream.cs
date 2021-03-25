using System;
using System.Diagnostics;
using System.IO;
using static nanoframework.System.Net.Websockets.WebSocketReceiver;

namespace nanoframework.System.Net.Websockets
{
    public class ReceiveMessageStream : Stream
    {
        private ReceiveMessageFrame _messageFrame;
        private int _length;
        private Stream _messageInputStream;
        private WebSocket _webSocket;
        private EventHandler _messageReadCallback;
        private WebSocketReadErrorHandler _websocketReadErrorCallBack;

        internal ReceiveMessageStream(ReceiveMessageFrame messageFrame, Stream receiveStream, int length, WebSocket webSocket, EventHandler messageReadCallback, WebSocketReadErrorHandler websocketReadErrorCallBack)
        {
            _messageFrame = messageFrame;
            _messageInputStream = receiveStream;
            _length = length;
            _webSocket = webSocket;
            _messageReadCallback = messageReadCallback;
            _websocketReadErrorCallBack = websocketReadErrorCallBack;

        }
        public override bool CanRead => _length > 0;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        // Summary:
        //     Reads a sequence of bytes from the current ReceiveMessageStream
        //     and advances the position within the stream by the number of bytes read.
        //
        // Parameters:
        //   buffer:
        //     An array of bytes. When this method returns, the buffer contains the specified
        //     byte array with the values between offset and (offset + count - 1) replaced by
        //     the bytes read from the current source.
        //
        //   offset:
        //     The zero-based byte offset in buffer at which to begin storing the data read
        //     from the current stream.
        //
        //   count:
        //     The maximum number of bytes to be read from the current stream.
        //
        // Returns:
        //     The total number of bytes read into the buffer. This can be less than the number
        //     of bytes requested if that many bytes are not currently available, or zero (0)
        //     if the end of the stream has been reached.
        //
        // Remarks:
        //     The WebSocket will stop to receive any incoming messages including controller messages until 
        //     the stream is completely read till the end. 
        public override int Read(byte[] buffer, int offset, int count)
        {

            if (count > _length) count = _length;
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                if (_webSocket.State == WebSocketFrame.WebSocketState.Closed || _webSocket.State == WebSocketFrame.WebSocketState.Aborted) break; //TODO: what if socket is closing? not reading means final message is blocked. socket is closed so no more reading....

                try
                {
                    int bytesAvailable = (int)_messageInputStream.Length;
                    int bytesRead = 0;
                    if (bytesAvailable > 0) {
                        bytesRead = _messageInputStream.Read(buffer, totalBytesRead, (count - totalBytesRead > bytesAvailable ? bytesAvailable : count - totalBytesRead) );  //only get available bytes so we don't timeout. 
                    
                    }

                    if (bytesRead == 0) //nothing to read from stream? means something is wrong
                    {
                        _messageFrame.ErrorMessage = "Message read timeout";
                        _websocketReadErrorCallBack?.Invoke(this, new WebSocketReadErrorArgs() { frame = _messageFrame });
                    }

                    if (_messageFrame.IsMasked) //decode message using mask
                    {
                        for (int i = totalBytesRead; i < totalBytesRead + bytesRead; ++i)
                        {
                            buffer[i] = (byte)(buffer[i] ^ _messageFrame.Masks[i % 4]);
                        }
                    }

                    totalBytesRead += bytesRead;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{_messageFrame.EndPoint} error reading message: {ex.Message}"); //error
                    _messageFrame.ErrorMessage = "read error";
                    _messageFrame.CloseStatus = WebSocketFrame.WebSocketCloseStatus.PolicyViolation;
                    _websocketReadErrorCallBack?.Invoke(this, new WebSocketReadErrorArgs() { frame = _messageFrame });

                }
            }


            _length = _length - totalBytesRead;
            if(_length == 0)
            {
                _messageReadCallback?.Invoke(this, new EventArgs());
            }


            return totalBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}

