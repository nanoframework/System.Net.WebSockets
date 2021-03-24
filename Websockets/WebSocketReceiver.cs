using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    internal class WebSocketReceiver
    {

        private Stream _inputStream;
        private bool _isServer;
        private int _maxReceiveFrameSize;
        private WebSocket _webSocket;
        private EventHandler _messageReadCallBack;
        private WebSocketReadErrorHandler _websocketReadErrorCallBack;
        internal delegate void WebSocketReadErrorHandler(object sender, WebSocketReadErrorArgs e);


        IPEndPoint _remoteEndPoint;


        private bool ReceivingFragmentedMessage = false;


        internal WebSocketReceiver(Stream inputStream, IPEndPoint remoteEndpoint, WebSocket webSocket, bool isServer, int maxReceiveFrameSize, EventHandler messageReadCallBack, WebSocketReadErrorHandler websocketReadErrorCallBack)
        {
            _inputStream = inputStream;
            _remoteEndPoint = remoteEndpoint;
            _isServer = isServer;
            _maxReceiveFrameSize = maxReceiveFrameSize;
            _webSocket = webSocket;
            _messageReadCallBack = messageReadCallBack;
            _websocketReadErrorCallBack = websocketReadErrorCallBack;

            

            
        }

        internal ReceiveMessageFrame StartReceivingMessage()
        {
            byte[] messageHeader = new byte[2];
            int numBytesReceived = 0;
            try
            {
                
                if (_inputStream.Length > 1) { 
                    numBytesReceived = _inputStream.Read(messageHeader, 0, 2);
                }

                if (numBytesReceived == 0)
                {
                    //TODO: Here we could let the thread sleep to safe recources.   
                    return null;
                }
                else
                {
                    if (numBytesReceived == 1) //still wating on second header byte
                    {
                        numBytesReceived = _inputStream.Read(messageHeader, 1, 1);
                        if (numBytesReceived == 0)
                        {
                            return SetMessageError(new ReceiveMessageFrame() { EndPoint = _remoteEndPoint }, "Incomplete Header Received", WebSocketCloseStatus.ProtocolError);
                        }
                    }
                }
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
                    //
                }
            }
            return DecodeHeader(messageHeader);
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

            if (fin && messageFrame.OpCode != OpCode.ContinuationFrame) //single frame message
            {
                messageFrame.Fragmentation = FragmentationType.NotFragmented;
                if (ReceivingFragmentedMessage && !messageFrame.IsControllFrame) //controller messages can be send during fragented messages
                {
                    return SetMessageError(messageFrame, "Already receiving another fragmetend message", WebSocketCloseStatus.PolicyViolation);
                }
            }
            else if (messageFrame.OpCode != OpCode.ContinuationFrame && !fin) //fin 0 and opcode !=0 -> begin fragmented message
            {
                messageFrame.Fragmentation = FragmentationType.FirstFragment;
                if (ReceivingFragmentedMessage)
                {
                    return SetMessageError(messageFrame, "Already receiving another fragmetend message", WebSocketCloseStatus.PolicyViolation);
                }
                ReceivingFragmentedMessage = true;
            }
            else if (messageFrame.OpCode == OpCode.ContinuationFrame && !fin) //fin 0 and opcode 0 -> inbetween frame of fragmetend message
            {
                messageFrame.Fragmentation = FragmentationType.Fragment;
                if (!ReceivingFragmentedMessage)
                {
                    return SetMessageError(messageFrame, "Unexpected ContinuationFrame fragmetend frame", WebSocketCloseStatus.PolicyViolation);
                }
            }
            else if (messageFrame.OpCode == OpCode.ContinuationFrame && fin) //fin 1 and opcode 0 -> last of fragmented message.
            {
                messageFrame.Fragmentation = FragmentationType.FinalFragment;
                if (!ReceivingFragmentedMessage)
                {
                    return SetMessageError(messageFrame, "Unexpected ContinuationFrame FinalContinuationFrame", WebSocketCloseStatus.PolicyViolation);
                }
                ReceivingFragmentedMessage = false;
            }
            else
            {
                throw new Exception("Wrong type of message, should never be hit");

            }


            messageFrame.IsMasked = (header[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
            messageFrame.MessageLength = header[1] % 128; // & 0111 1111


            if (messageFrame.MessageLength == 126)
            {
                byte[] smallLargeMessageLength = new byte[2];
                if (_inputStream.Read(smallLargeMessageLength, 0, 2) != 2)
                {
                    return SetMessageError(messageFrame, "Timeout receiving message lenght", WebSocketCloseStatus.ProtocolError);
                };
                smallLargeMessageLength = WebSocketHelpers.ReverseBytes(smallLargeMessageLength);
                messageFrame.MessageLength = BitConverter.ToUInt16(smallLargeMessageLength, 0);
            }
            else if (messageFrame.MessageLength == 127)
            {

                byte[] largeLargeMessageLength = new byte[8];
                if (_inputStream.Read(largeLargeMessageLength, 0, 8) != 8)
                {
                    return SetMessageError(messageFrame, "Timeout receiving message lenght", WebSocketCloseStatus.ProtocolError);
                };
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
                return SetMessageError(messageFrame, $"Controll frames can only have a length of max 125 bytes", WebSocketCloseStatus.MessageTooBig);
            }

            if (messageFrame.MessageLength > _maxReceiveFrameSize)
            {
                return SetMessageError(messageFrame, $"max message size is {_maxReceiveFrameSize}", WebSocketCloseStatus.MessageTooBig);
            }

            messageFrame.Masks = new byte[4]; ;
            if (messageFrame.IsMasked)
            {
                if (_inputStream.Read(messageFrame.Masks, 0, 4) != 4)
                {
                    return SetMessageError(messageFrame, "message mask receive timeout", WebSocketCloseStatus.ProtocolError);
                };
            }
            else if ((int)messageFrame.OpCode < 5 && _isServer) // not a controll frame
            {
                return SetMessageError(messageFrame, "Data messages  from clients have to be masked", WebSocketCloseStatus.ProtocolError);
            }

            messageFrame.MessageStream = new ReceiveMessageStream(messageFrame, _inputStream, messageFrame.MessageLength, _webSocket, _messageReadCallBack, _websocketReadErrorCallBack);

            return messageFrame;
        }




        private ReceiveMessageFrame SetMessageError(ReceiveMessageFrame frame, string errorMsg, WebSocketCloseStatus closeCode)
        {
            frame.ErrorMessage = errorMsg;
            return frame;
        }

        private void ReadError(Exception ex, ReceiveMessageFrame frame)
        {
            frame.ErrorMessage = ex.Message;
            _websocketReadErrorCallBack?.Invoke(this, new WebSocketReadErrorArgs() { frame = frame });
        }


    }

    internal class WebSocketReadErrorArgs : EventArgs
    {
        public ReceiveMessageFrame frame { get; set; }
        public string ErrorMessage => frame.ErrorMessage;
    }
}
