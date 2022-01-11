//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets.WebSocketFrame;

namespace System.Net.WebSockets
{
    internal class WebSocketReceiver
    {
        private bool _receivingFragmentedMessage = false;

        private readonly IPEndPoint _remoteEndPoint;
        private readonly NetworkStream _inputStream;
        private readonly bool _isServer;
        private readonly int _maxReceiveFrameSize;
        private readonly WebSocket _webSocket;
        private readonly WebSocketReadErrorHandler _websocketReadErrorCallBack;

        internal delegate void WebSocketReadErrorHandler(object sender, WebSocketReadEEventArgs e);

        internal WebSocketReceiver(NetworkStream inputStream, IPEndPoint remoteEndpoint, WebSocket webSocket, bool isServer, int maxReceiveFrameSize, WebSocketReadErrorHandler websocketReadErrorCallBack)
        {
            _inputStream = inputStream;
            _remoteEndPoint = remoteEndpoint;
            _isServer = isServer;
            _maxReceiveFrameSize = maxReceiveFrameSize;
            _webSocket = webSocket;
            _websocketReadErrorCallBack = websocketReadErrorCallBack;
        }

        internal ReceiveMessageFrame StartReceivingMessage()
        {
            try
            {
                return DecodeHeader(ReadFixedSizeBuffer(2));
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10060)
                {
                    Debug.WriteLine("timeout");
                    return null;
                }
                else
                {
                    var errorReturnFrame = new ReceiveMessageFrame() { EndPoint = _remoteEndPoint };
                    ReadError(ex, errorReturnFrame);
                    return errorReturnFrame;
                }
            }
        }
        
        private ReceiveMessageFrame DecodeHeader(byte[] header) //TODO: put everything a ReceiveMessageFrame creator... Factory style?
        {
            ReceiveMessageFrame messageFrame = new ReceiveMessageFrame() { EndPoint = _remoteEndPoint };

            bool fin = (header[0] & 0b10000000) != 0;
            int tempOpCode = header[0] & 0b00001111;

            if (tempOpCode < 3 || (tempOpCode > 7 && tempOpCode < 11)) //TODO: think about unkown upcode?
            {
                messageFrame.OpCode = (OpCode)(tempOpCode);
            }
            else
            {
                return SetMessageError(messageFrame, "Unsuported OpCode", WebSocketCloseStatus.InvalidMessageType);
            }

            //single frame message
            if (fin && messageFrame.OpCode != OpCode.ContinuationFrame) 
            {
                messageFrame.Fragmentation = FragmentationType.NotFragmented;

                //controller messages can be sent between fragmented messages
                if (_receivingFragmentedMessage && !messageFrame.IsControllFrame) 
                {
                    return SetMessageError(messageFrame, "Already receiving another fragmented message", WebSocketCloseStatus.PolicyViolation);
                }
            }
            //fin 0 and opcode !=0 -> begin fragmented message
            else if (messageFrame.OpCode != OpCode.ContinuationFrame && !fin) 
            {
                messageFrame.Fragmentation = FragmentationType.FirstFragment;

                if (_receivingFragmentedMessage)
                {
                    return SetMessageError(messageFrame, "Already receiving another fragmented message", WebSocketCloseStatus.PolicyViolation);
                }

                _receivingFragmentedMessage = true;
            }
            // fin 0 and opcode 0 -> in between frame of fragmented message
            else if (messageFrame.OpCode == OpCode.ContinuationFrame && !fin) 
            {
                messageFrame.Fragmentation = FragmentationType.Fragment;

                if (!_receivingFragmentedMessage)
                {
                    return SetMessageError(messageFrame, "Unexpected ContinuationFrame fragmented frame", WebSocketCloseStatus.PolicyViolation);
                }
            }
            //fin 1 and opcode 0 -> last of fragmented message
            else if (messageFrame.OpCode == OpCode.ContinuationFrame && fin) 
            {
                messageFrame.Fragmentation = FragmentationType.FinalFragment;

                if (!_receivingFragmentedMessage)
                {
                    return SetMessageError(messageFrame, "Unexpected ContinuationFrame FinalContinuationFrame", WebSocketCloseStatus.PolicyViolation);
                }

                _receivingFragmentedMessage = false;
            }
            else
            {
                throw new Exception("Wrong type of message, should never be hit");

            }

            // must be true, "All messages from the client to the server have this bit set"
            messageFrame.IsMasked = (header[1] & 0b10000000) != 0;

            // & 0111 1111
            messageFrame.MessageLength = header[1] % 128; 

            if (messageFrame.MessageLength == 126)
            {
                byte[] smallLargeMessageLength = ReadFixedSizeBuffer(2);

                smallLargeMessageLength = WebSocketHelpers.ReverseBytes(smallLargeMessageLength);
                messageFrame.MessageLength = BitConverter.ToUInt16(smallLargeMessageLength, 0);
            }
            else if (messageFrame.MessageLength == 127)
            {
                byte[] largeLargeMessageLength = ReadFixedSizeBuffer(8);

                largeLargeMessageLength = WebSocketHelpers.ReverseBytes(largeLargeMessageLength);

                ulong longmsglength = BitConverter.ToUInt64(largeLargeMessageLength, 0);

                if (longmsglength > (ulong)_maxReceiveFrameSize)
                {
                    return SetMessageError(messageFrame, $"max message size is {_maxReceiveFrameSize}", WebSocketCloseStatus.MessageTooBig);
                }

                messageFrame.MessageLength = (int)longmsglength;

            }

            if (messageFrame.MessageLength > 125 && messageFrame.IsControllFrame)
            {
                return SetMessageError(messageFrame, $"Control frames can only have a length of max 125 bytes", WebSocketCloseStatus.MessageTooBig);
            }

            if (messageFrame.MessageLength > _maxReceiveFrameSize)
            {
                return SetMessageError(messageFrame, $"max message size is {_maxReceiveFrameSize}", WebSocketCloseStatus.MessageTooBig);
            }

            if (messageFrame.IsMasked)
            {
                messageFrame.Masks = ReadFixedSizeBuffer(4);
            }
            // not a control frame
            else if ((int)messageFrame.OpCode < 5 && _isServer)
            {
                return SetMessageError(messageFrame, "Data messages  from clients have to be masked", WebSocketCloseStatus.ProtocolError);
            }

            return messageFrame;
        }

        internal byte[] ReadBuffer(int messageLength, byte[] masks = null)
        {
            return ReadFixedSizeBuffer(messageLength, masks);
        }

        private ReceiveMessageFrame SetMessageError(ReceiveMessageFrame frame, string errorMsg, WebSocketCloseStatus closeCode)
        {
            frame.ErrorMessage = errorMsg;
            return frame;
        }

        private void ReadError(Exception ex, ReceiveMessageFrame frame)
        {
            frame.ErrorMessage = ex.Message;
            _websocketReadErrorCallBack?.Invoke(this, new WebSocketReadEEventArgs() { Frame = frame });
        }

        byte[] ReadFixedSizeBuffer(int size, byte[] masks = null)
        {
            byte[] buffer = new byte[size];
            int offset = 0;

            while (size > 0)
            {
                int bytes = _inputStream.Read(buffer, offset, size);
                offset += bytes;
                size -= bytes;
            }

            if(masks != null)
            {
                for(int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (byte)(buffer[i] ^ masks[i % masks.Length]);
                }
            }

            return buffer;
        }
    }

    internal class WebSocketReadEEventArgs : EventArgs
    {
        public ReceiveMessageFrame Frame { get; set; }

        public string ErrorMessage => Frame.ErrorMessage;
    }
}
