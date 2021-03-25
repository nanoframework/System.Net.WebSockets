using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    public  partial  class WebSocket : IDisposable
    {
        
        //
        // Summary:
        //     The timeout which specifies how long to wait for a message before closing
        //     the connection. Default is 60 seconds.
        public TimeSpan ServerTimeout  { get; private set; } = new TimeSpan(0, 0, 60);

        //
        // Summary:
        //     The interval that the client will send keep alive messages to let the
        //     server know to not close the connection. Default is 30 second interval.

        public TimeSpan KeepAliveInterval { get; private set; } = new TimeSpan(0, 0, 30);
        
        //
        // Summary:
        //     Gets the maximum allowed byte length of messages received by the WebSocket .
        //
        //  Returns:
        //     The maximum allowed byte length of messages received by the WebSocket. Default is int.MaxValue.
        public int MaxReceiveFrameSize { get; private set; } = int.MaxValue;

        //
        // Summary:
        //     Gets or sets the maximum allowed byte length of a partial message send by the WebSocket.
        //     By default if a message that exceeds the size limit it will be broken up in smaller partial messages
        //     Default is 124 bytes
        //
        //  Returns:
        //     The maximum allowed byte length of a partial message send by the WebSocket.
        public int MaxFragmentSize { get; private set; } = 1024;



        //
        // Summary:
        //     Gets the WebSocket state of the System.Net.WebSockets.ClientWebSocket instance.
        //
        // Returns:
        //     The WebSocket state of the System.Net.WebSockets.ClientWebSocket instance.
        public WebSocketState State { get; protected set; } = WebSocketState.Closed;


        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event EventHandler ConnectionClosed;
        
        public IPEndPoint RemoteEndPoint { get; private set; }
        //public bool Closed { get; private set; } = false;

        private Stream _receiveStream;
        private Thread _receiveThread;
        private bool _hasError = false;


        //internal HttpListenerContext httpContext;

        //Mesage size can not be larger than int.MaxValue because that is the largest byte[] supported
        //no action should ben undertaken on errors so no event is raised. 
        //Will garbage collector dispose of connection??
        //Data server to client is not masked
        //Pong is not responded on. 
        //also create a websocket using networkStream
        //create a websocket that listens on ws:// using sockets and streams. 
        //There is a receiving stream that receives only the header of the next message and hands off the message stream in a callback. the thread will stop and start when complete message stream is read
        //Reason for not reading the whole message and handing it off in an event is twofold. 1. bigger messages could make the device run out of memory 2. multiple messages and message fragments could get out of order. 



        protected WebSocket( WebSocketOptions options = null)
        {
            if (options != null)
            {
                ServerTimeout = options.ServerTimeout;
                KeepAliveInterval = options.KeepAliveInterval;
                MaxReceiveFrameSize = options.MaxReceiveFrameSize;
                MaxFragmentSize = options.MaxFragmentSize;
            }


        }


        //
        // Summary:
        //     Connects the Websocket to  the specified
        //     stream, which represents a web socket connection.
        //
        // Parameters:
        //   stream:
        //     The stream for the connection.
        //
        //   isServer:
        //     true to indicate it's the server-side of the connection; false if it's the client-side.
        //
        //   subProtocol:
        //     The agreed upon sub-protocol that was used when creating the connection.
        //
        //   keepAliveInterval:
        //     The keep-alive interval to use, or System.Threading.Timeout.InfiniteTimeSpan
        //     to disable keep-alives.
        //


        protected void ConnectToStream(Stream stream, bool isServer, IPEndPoint remoteEndPoint, MessageReceivedEventHandler messageReceivedHandler)
        {
            _receiveStream = stream;
            IsServer = isServer;
            RemoteEndPoint = remoteEndPoint;
            LastReceivedMessage = DateTime.UtcNow;

            //start server sending and receiving async
            _messageReceivedEventHandler = messageReceivedHandler;
            _webSocketReceiver = new WebSocketReceiver(stream, remoteEndPoint, this, IsServer, MaxReceiveFrameSize, OnMessageRead, OnReadError);
            _webSocketSender = new WebSocketSender(stream, IsServer, OnWriteError);
            _receiveThread = new Thread(ReceiveAndControllThread);
            _receiveThread.Start();
            State = WebSocketState.Open;

        }

      
        // Parameters:
        //   buffer:
        //
        //   messageType:
        //
        //  fragmentSize:
        //      Override the maxFragmentSize used
        //      Default -1 will use the maxFragmentSize 
        public bool Send(byte[] buffer, WebSocketMessageType messageType, int fragmentSize = -1)
        {
            return QueueSendMessage(new SendMessageFrame()
            {
                Buffer = buffer,
                FragmentSize = fragmentSize < 0 ? MaxFragmentSize : fragmentSize,
                OpCode = messageType == WebSocketMessageType.Text?  OpCode.TextFrame : OpCode.BinaryFrame
            });
        }
        public bool SendString(string message, int fragmentSize = -1)
        {
            return Send(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, fragmentSize);
        }

        public bool SendBytes(byte[] data, int fragmentSize = -1)
        {
            return Send(data, WebSocketMessageType.Binary, fragmentSize);
        }

        public void Close(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.Empty, string statusDescription = null)
        {
            if (State != WebSocketState.Open) return; //already closing or closed
            State = WebSocketState.CloseSent;
            _closingTime = DateTime.UtcNow;
            RawClose(closeStatus, statusDescription == null ? null : Encoding.UTF8.GetBytes(statusDescription), closeStatus == WebSocketCloseStatus.EndpointUnavailable);
        }

        public void Abort()
        {
            State = WebSocketState.Aborted;
            HardClose(); 
        }

        //CloseImediately will Send a close message and not await this message. 
        private void RawClose(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.Empty, byte[] buffer = null, bool CloseImmediately = false)
        {
            //send closing message which needs to be awaited for a period
            if (!(State == WebSocketState.Open || State == WebSocketState.CloseReceived)) return; //already closing or closed
            _closeStatus = closeStatus;

            byte[] sendBuffer = new byte[0];
            if (!(closeStatus == WebSocketCloseStatus.ClosedAbnormally || closeStatus == WebSocketCloseStatus.Empty)) {
                if (buffer == null)
                {
                    sendBuffer = new byte[2];
                }
                else if (buffer.Length > 125 - 2)
                {
                    sendBuffer = new byte[125];
                    Array.Copy(buffer, 0, sendBuffer, 2, 125 - 2);
                }
                else
                {
                    sendBuffer = new byte[2 + buffer.Length];
                    Array.Copy(buffer, 0, sendBuffer, 2, buffer.Length);
                }

                var closeStatusBytes = BitConverter.GetBytes((UInt16)closeStatus);
                sendBuffer[0] = closeStatusBytes[1];
                sendBuffer[1] = closeStatusBytes[0];
            }

            QueueSendMessage(new SendMessageFrame()
            {
                Buffer = sendBuffer,
                OpCode = OpCode.ConnectionCloseFrame,
            });

            if (CloseImmediately)
            {
                int msWaited = 0;
                while (!_webSocketSender.CloseMessageSend ) 
                {
                    msWaited += 50;
                    Thread.Sleep(50);
                    State = WebSocketState.CloseSent;
                    _closingTime = DateTime.UtcNow;
                }
                HardClose();
            }
            
            
        }

        private void HardClose()
        {
            State = WebSocketState.Closed;
            StopReceiving();
            are.Set();
            _webSocketSender.StopSender();
            Debug.WriteLine($"Connection - {RemoteEndPoint.ToString()} - Closed");
         
            
            ConnectionClosed?.Invoke(this, new EventArgs());

            
        }

        private bool QueueSendMessage(SendMessageFrame frame)
        {
            if (State != WebSocketState.Open && frame.OpCode != OpCode.ConnectionCloseFrame) //if connection is closing only a close respond can be send.
            {
                Debug.WriteLine($"Connection - {RemoteEndPoint.ToString()} - is closing cannot send messages");
                return false;
            }
            frame.EndPoint = RemoteEndPoint;
            _webSocketSender.QueueMessage(frame);
            return true;

        }

        private void OnReadError(object sender, WebSocketReadErrorArgs e)
        {
            if (!_hasError)
            {
                _hasError = true;
                Debug.WriteLine($"{e.frame.EndPoint.ToString()} error - {e.ErrorMessage}");
                RawClose(e.frame.CloseStatus, Encoding.UTF8.GetBytes(e.ErrorMessage), true);
            }
        }

        private void OnWriteError(object sender, WebSocketWriteErrorArgs e)
        {
            if (!_hasError)
            {
                _hasError = true;
                Debug.WriteLine($"{e.frame.EndPoint.ToString()} error - {e.ErrorMessage}");

                HardClose();
            }
        }

        public void Dispose()
        {
            if (State != WebSocketState.Closed)
            {
                HardClose();
            }
        }
    }
}
