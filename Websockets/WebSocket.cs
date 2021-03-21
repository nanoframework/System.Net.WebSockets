using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    public partial class WebSocket : IDisposable
    {

        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        public event EventHandler ConnectionClosed;
        public int MaxReceiveFrameSize { get; private set; }
        public IPEndPoint RemotEndPoint { get; private set; }
        public string url { get; private set; }

        private Stream _receiveStream;
        private Thread _receiveThread;
        private bool HasError = false;
        public bool Closed { get; private set; } = false;
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


        public WebSocket(Stream stream, IPEndPoint remoteEndPoint, MessageReceivedEventHandler messageReceivedCallBack, bool isServer = true, string prefix = "", int controllerMessagesTimeoutSec = 10, int maxReceiveFrameSize = int.MaxValue, int messageReadTimeoutMs = 100, int heartBeatSec = 0)
        {
            MessageReadTimeoutMs = messageReadTimeoutMs;
            RemotEndPoint = remoteEndPoint;
            LastReceivedMessage = DateTime.UtcNow;
            ControllerMessagesTimeoutSec = controllerMessagesTimeoutSec;
            HeartBeatSec = heartBeatSec;
            MaxReceiveFrameSize = maxReceiveFrameSize;
            url = prefix;
            IsServer = isServer;
            _receiveStream = stream;

            //start server sending and receiving async
            _messageReceivedEventHandler = messageReceivedCallBack;
            _webSocketReceiver = new WebSocketReceiver(stream, remoteEndPoint, this, IsServer, maxReceiveFrameSize, messageReadTimeoutMs, OnMessageRead, OnReadError);
            _webSocketSender = new WebSocketSender(stream, IsServer, OnWriteError);
            _receiveThread = new Thread(ReceiveAndControllThread);
            _receiveThread.Start();

        }



        public bool SendString(string message, int fragmentSize = 0)
        {
            return QueueSendMessage(new SendMessageFrame()
            {
                Buffer = Encoding.UTF8.GetBytes(message),
                FragmentSize = fragmentSize,
                OpCode = OpCode.TextFrame,


            });
        }

        public bool SendBytes(byte[] data, int fragmentSize = 0)
        {
            return QueueSendMessage(new SendMessageFrame()
            {
                Buffer = data,
                FragmentSize = fragmentSize,
                OpCode = OpCode.BinaryFrame,

            });
        }

        public bool SendStream(Stream stream, int fragmentSize = 0)
        {
            throw new NotImplementedException();
        }

        public void StopConnection(byte[] buffer = null, bool CloseImmediately = false)
        {
            //send closing message which needs to be awaited for a period
            if (Closing) return;

            Closing = true;
            _closingTime = DateTime.UtcNow;
            QueueSendMessage(new SendMessageFrame()
            {

                Buffer = buffer == null ? new byte[0] : buffer,
                OpCode = OpCode.ConnectionCloseFrame,


            });

            if (CloseImmediately)
            {
                int msWaited = 0;
                while (!_webSocketSender.CloseMessageSend ) 
                {
                    msWaited += 50;
                    Thread.Sleep(50);
                }
                CloseConnection();
            }
            
            
        }

        private void CloseConnection(string message = null)
        {

            StopReceiving();
            are.Set();
            _webSocketSender.StopSender();
            Debug.WriteLine($"Connection - {RemotEndPoint.ToString()} - Closed");
            ConnectionClosed?.Invoke(this, new EventArgs());
            Closed = true;

            //TODO: Let WebSocketServer deal with connection
        }



        private bool QueueSendMessage(SendMessageFrame frame)
        {
            if (Closing && frame.OpCode != OpCode.ConnectionCloseFrame) //if connection is closing only a close respond can be send.
            {
                Debug.WriteLine($"Connection - {RemotEndPoint.ToString()} - is closing cannot send messages");
                return false;
            }
            frame.EndPoint = RemotEndPoint;
            _webSocketSender.QueueMessage(frame);
            return true;

        }

        private void OnReadError(object sender, WebSocketReadErrorArgs e)
        {
            if (!HasError)
            {
                HasError = true;
                Debug.WriteLine($"{e.frame.EndPoint.ToString()} error - {e.ErrorMessage}");

                CloseConnection(e.ErrorMessage);
            }
        }

        private void OnWriteError(object sender, WebSocketWriteErrorArgs e)
        {
            if (!HasError)
            {
                HasError = true;
                Debug.WriteLine($"{e.frame.EndPoint.ToString()} error - {e.ErrorMessage}");

                CloseConnection(e.ErrorMessage);
            }
        }

        public void Dispose()
        {
            if (!Closed)
            {
                CloseConnection();
            }
        }
    }
}
