//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Net.WebSockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace System.Net.WebSockets
{
    /// <summary>
    /// The WebSocket class allows applications to send and receive data after the WebSocket upgrade has completed.
    /// </summary>
    public abstract  class WebSocket : IDisposable
    {
        internal DateTime ClosingTime = DateTime.UtcNow;
        internal WebSocketCloseStatus CloseStatus = WebSocketCloseStatus.Empty;
        internal WebSocketReceiver WebSocketReceiver;
        internal bool Stopped = false;
        internal bool Pinging = false;
        internal DateTime PingTime = DateTime.UtcNow;
        internal NetworkStream ReceiveStream;
        internal bool HasError = false;

        internal WebSocketSender _webSocketSender;
        private readonly object _syncLock = new object();
        internal MessageReceivedEventHandler CallbacksMessageReceivedEventHandler;

        /// <summary>
        /// <see langword="true"/> to indicate it's the server-side of the connection; <see langword="false"/> if it's the client-side.
        /// </summary>
        public bool IsServer { get; private set; }

        /// <summary>
        /// The UTC time of the last received message or Controller message  
        /// </summary>
        internal DateTime LastContactTimeStamp;

        /// <summary>
        /// The timeout which specifies how long to wait for a message before closing
        /// the connection. Default is 60 seconds.
        /// </summary>
        public TimeSpan ServerTimeout  { get; private set; } = new TimeSpan(0, 0, 60);

        /// <summary>
        /// The interval that the client will send keep alive messages to let the
        /// server know to not close the connection. Default is 30 second interval.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; private set; } = new TimeSpan(0, 0, 30);
        
        /// <summary>
        /// Gets the maximum allowed byte length of messages received by the WebSocket .
        /// </summary>
        ///  <value>
        /// The maximum allowed byte length of messages received by the WebSocket. Default is int.MaxValue.
        /// </value>
        public int MaxReceiveFrameSize { get; private set; } = int.MaxValue;

        /// <summary>
        /// Gets or sets the maximum allowed byte length of a partial message send by the WebSocket.
        /// By default if a message that exceeds the size limit it will be broken up in smaller partial messages
        /// Default is 124 bytes
        /// <value>
        /// The maximum allowed byte length of a partial message send by the WebSocket.
        /// </value>
        public int MaxFragmentSize { get; private set; } = 1024;

        /// <summary>
        /// Gets the WebSocket state of the System.Net.WebSockets.ClientWebSocket instance.
        ///</summary>
        /// <value>
        /// The WebSocket state of the System.Net.WebSockets.ClientWebSocket instance.
        /// </value>
        public abstract WebSocketState State { get; set; }

        /// <summary>
        /// Occurs when a message is received. Controller messages are handled internally and 
        /// do not raise an event.
        /// </summary>
        /// <remarks>
        /// The WebSocket will stop to receive any incoming messages including controller messages until 
        /// the provided ReceiveMessageStream is completely read till the end. 
        /// </remarks>
        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        
        /// <summary>
        /// Occurs when the connection is closed. The connection could be closed due to an
        /// error or due to either the server or client intentionally closing the connection
        /// without error.
        /// </summary>
        public event EventHandler ConnectionClosed;

        /// <summary>
        /// Gets the Remote Endpoint where the WebSocket connects to.
        /// </summary>
        /// <value>
        /// The Remote Endpoint where the WebSocket connects to.
        /// </value>
        public IPEndPoint RemoteEndPoint { get; private set; }
        
        /// <summary>
        /// Creates an instance of the System.Net.WebSockets.WebSocket class.
        /// </summary>
        ///<param name="options">Optional WebSocketOptions where extra options can be defined.</param>
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

        /// <summary>
        /// Connects the WebSocket to  the specified
        /// stream, which represents a web socket connection.
        /// </summary>
        /// <param name="stream">The stream for the connection.</param>
        /// <param name="isServer"><see langword="true"/> to indicate it's the server-side of the connection; <see langword="false"/> if it's the client-side.</param>
        /// <param name="remoteEndPoint">The Remote Endpoint where the WebSocket connects to.</param>
        protected void ConnectToStream(NetworkStream stream, bool isServer, IPEndPoint remoteEndPoint)
        {
            ReceiveStream = stream;
            IsServer = isServer;
            RemoteEndPoint = remoteEndPoint;
            LastContactTimeStamp = DateTime.UtcNow;

            //start server sending and receiving async
            WebSocketReceiver = new WebSocketReceiver(stream, remoteEndPoint, this, IsServer, MaxReceiveFrameSize, OnReadError);
            _webSocketSender = new WebSocketSender(stream, IsServer, OnWriteError);

            ReceiveAndControllThread receiveThread = new ReceiveAndControllThread(this);
            new Thread(receiveThread.WorkerThread).Start();

            State = WebSocketState.Open;
        }

        /// <summary>
        /// Sends data over the System.Net.WebSockets.WebSocket connection.
        /// </summary>
        /// <param name="buffer">The buffer containing the message content.</param>
        /// <param name="messageType">Indicates whether the application is sending a binary or text message.</param>
        /// <param name="fragmentSize">Indicates whether the application is sending a binary or text message.
        /// Default -1 will use the maxFragmentSize </param>
        /// <returns><see langword="true"/> if the message was successfully queued for send. <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited.
        /// </remarks>
        public bool Send(byte[] buffer, WebSocketMessageType messageType, int fragmentSize = -1)
        {
            return QueueMessageToSend(new SendMessageFrame()
            {
                Buffer = buffer,
                FragmentSize = fragmentSize < 0 ? MaxFragmentSize : fragmentSize,
                OpCode = messageType == WebSocketMessageType.Text?  OpCode.TextFrame : OpCode.BinaryFrame
            });
        }

        /// <summary>
        /// Sends a text message over the System.Net.WebSockets.WebSocket connection.
        /// </summary>
        /// <param name="message">The text that will be send</param>
        /// <param name="fragmentSize">Indicates whether the application is sending a binary or text message.
        /// Default -1 will use the maxFragmentSize </param>
        /// <returns><see langword="true"/> if the message was successfully queued for send. <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited.
        /// </remarks>
        public bool SendString(string message, int fragmentSize = -1)
        {
            return Send(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, fragmentSize);
        }

        /// <summary>
        /// Sends a binary message over the System.Net.WebSockets.WebSocket connection.
        /// </summary>
        /// <param name="data">The binary data that will be send.</param>
        /// <param name="fragmentSize">Indicates whether the application is sending a binary or text message.
        /// Default -1 will use the maxFragmentSize </param>
        /// <returns><see langword="true"/> if the message was successfully queued for send. <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited.
        /// </remarks>
        public bool SendBytes(byte[] data, int fragmentSize = -1)
        {
            return Send(data, WebSocketMessageType.Binary, fragmentSize);
        }

        /// <summary>
        /// Will start closing the WebSocket connection using the close handshake defined in the WebSocket protocol specification section 7.
        /// </summary>
        /// <param name="closeStatus">Indicates the reason for closing the WebSocket connection.</param>
        /// <param name="statusDescription">Specifies a human readable explanation as to why the connection is closed.</param>
        /// <remarks>
        /// WebSocketCloseStatus.EndpointUnavailable will close the WebSocket synchronous without awaiting response.
        /// </remarks>
        public void Close(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.Empty, string statusDescription = null)
        {
            if (State != WebSocketState.Open)
            {
                //already closing or closed
                return; 
            }

            State = WebSocketState.CloseSent;

            ClosingTime = DateTime.UtcNow;

            RawClose(closeStatus, statusDescription == null ? null : Encoding.UTF8.GetBytes(statusDescription), closeStatus == WebSocketCloseStatus.EndpointUnavailable);
        }
        
        /// <summary>
        /// Aborts the WebSocket connection and cancels any pending IO operations.
        /// </summary>
        public void Abort()
        {
            State = WebSocketState.Aborted;

            HardClose(); 
        }

        // CloseImediately will Send a close message and not await this message. 
        internal void RawClose(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.Empty, byte[] buffer = null, bool CloseImmediately = false)
        {
            //send closing message which needs to be awaited for a period
            if (!(State == WebSocketState.Open || State == WebSocketState.CloseReceived))
            {
                //already closing or closed
                return; 
            }

            CloseStatus = closeStatus;

            byte[] sendBuffer = new byte[0];

            if (!(closeStatus == WebSocketCloseStatus.ClosedAbnormally || closeStatus == WebSocketCloseStatus.Empty)) 
            {
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

            QueueMessageToSend(new SendMessageFrame()
            {
                Buffer = sendBuffer,
                OpCode = OpCode.ConnectionCloseFrame,
            });

            if (CloseImmediately)
            {
                int msWaited = 0;

                while (!_webSocketSender.CloseMessageSent ) 
                {
                    msWaited += 50;
                    Thread.Sleep(50);
                    State = WebSocketState.CloseSent;
                    ClosingTime = DateTime.UtcNow;
                }

                HardClose();
            }
        }

        internal void HardClose()
        {
            State = WebSocketState.Closed;

            StopReceiving();
            _webSocketSender.StopSender();

            Debug.WriteLine($"Connection - {RemoteEndPoint.ToString()} - Closed");
         
            ConnectionClosed?.Invoke(this, new EventArgs());   
        }

        internal bool QueueMessageToSend(SendMessageFrame frame)
        {
            // if connection is closing only a close respond can be send.
            if (State != WebSocketState.Open && frame.OpCode != OpCode.ConnectionCloseFrame)
            {
                Debug.WriteLine($"Connection - {RemoteEndPoint.ToString()} - is closing cannot send messages");
                return false;
            }

            frame.EndPoint = RemoteEndPoint;

            _webSocketSender.QueueMessage(frame);

            return true;
        }

        private void OnReadError(object sender, WebSocketReadEEventArgs e)
        {
            if (!HasError)
            {
                HasError = true;
                Debug.WriteLine($"{e.Frame.EndPoint.ToString()} error - {e.ErrorMessage}");
                RawClose(e.Frame.CloseStatus, Encoding.UTF8.GetBytes(e.ErrorMessage), true);
            }
        }

        private void OnWriteError(object sender, WebSocketWriteErrorEventArgs e)
        {
            if (!HasError)
            {
                HasError = true;
                Debug.WriteLine($"{e.Frame.EndPoint.ToString()} error - {e.ErrorMessage}");

                HardClose();
            }
        }

        //private void RelayMessage(ReceiveMessageFrame messageFrame)
        //{
        //    if (messageFrame.Error)
        //    {
        ///    Debug.WriteLine($"{RemoteEndPoint.ToString()} error - {messageFrame.ErrorMessage}");
        ///    RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
        //    }
        //    else
        //    {
        ///    LastContact = DateTime.UtcNow;
        ///    OnNewMessage(messageFrame);
        //    }
        //}

        //Ping will only commence from this thread because of threading safety. 
        internal void SendPing(string pingContent = "hello")
        {
            Pinging = true;
            PingTime = DateTime.UtcNow;

            QueueMessageToSend(new SendMessageFrame()
            {
                Buffer = Encoding.UTF8.GetBytes(pingContent),
                OpCode = OpCode.PingFrame

            });
        }

        internal void StopReceiving()
        {
            Stopped = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (State != WebSocketState.Closed)
            {
                HardClose();
            }
        }

        /// <summary>
        /// Event raised when a message is received by the WebSocket. 
        /// </summary>
        public event MessageReceivedEventHandler MessageReceived
        {
            add
            {
                lock (_syncLock)
                {
                    MessageReceivedEventHandler callbacksOld = CallbacksMessageReceivedEventHandler;
                    MessageReceivedEventHandler callbacksNew = (MessageReceivedEventHandler)Delegate.Combine(callbacksOld, value);

                    try
                    {
                        CallbacksMessageReceivedEventHandler = callbacksNew;
                    }
                    catch
                    {
                        CallbacksMessageReceivedEventHandler = callbacksOld;

                        throw;
                    }
                }
            }

            remove
            {
                lock (_syncLock)
                {
                    MessageReceivedEventHandler callbacksOld = CallbacksMessageReceivedEventHandler;
                    MessageReceivedEventHandler callbacksNew = (MessageReceivedEventHandler)Delegate.Remove(callbacksOld, value);

                    try
                    {
                        CallbacksMessageReceivedEventHandler = callbacksNew;
                    }
                    catch
                    {
                        CallbacksMessageReceivedEventHandler = callbacksOld;

                        throw;
                    }
                }
            }
        }

    }
}
