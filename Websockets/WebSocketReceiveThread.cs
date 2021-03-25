using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    partial class WebSocket
    {
        public DateTime LastReceivedMessage { get; private set; }


        internal DateTime _closingTime = DateTime.UtcNow;
        public int MessageReadTimeoutMs { get; private set; }

        public bool IsServer { get; private set; }

        private MessageReceivedEventHandler _messageReceivedEventHandler;

        
        private WebSocketReceiver _webSocketReceiver;
        private WebSocketSender _webSocketSender;

        //private Thread _receiveThread;
        private bool _pinging = false;
        private DateTime _pingTime = DateTime.UtcNow;
        private AutoResetEvent are = new AutoResetEvent(false);
        private bool _stopped = false;





        private byte[] messageHeader = new byte[2];
        private WebSocketCloseStatus _closeStatus = WebSocketCloseStatus.Empty;

        private void ReceiveAndControllThread() //this thread is always running and thus the best place for controlling ping and other messages
        {
            while (!_stopped)
            {

                var messageFrame = _webSocketReceiver.StartReceivingMessage();

                if (messageFrame == null)
                {
                    //Here we could let the thread sleep to safe recources.   
                }
                else
                {
                    if (messageFrame.Error) //handle error
                    {
                        _hasError = true;
                        Debug.WriteLine($"{RemoteEndPoint} closed with error: {messageFrame.ErrorMessage}");
                        RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                        
                        
                    }
                    else if (messageFrame.IsControllFrame)
                    {
                        byte[] buffer = new byte[messageFrame.MessageLength];
                        messageFrame.MessageStream.Read(buffer, 0, messageFrame.MessageLength);

                        LastReceivedMessage = DateTime.UtcNow;
                        switch (messageFrame.OpCode)
                        {
                            case OpCode.PingFrame: //send Pong
                                var pong = new SendMessageFrame() { Buffer = buffer, OpCode = OpCode.PongFrame};
                                messageFrame.OpCode = OpCode.PongFrame;
                                messageFrame.IsMasked = false;
                                QueueSendMessage(pong);
                                break;
                            case OpCode.PongFrame: //pongframe
                                _pinging = false; //checking if content pong matches ping is not implemented due to threadsafety and memoryconsumption considerations
                                break;
                            case OpCode.ConnectionCloseFrame:
                                _closeStatus = WebSocketCloseStatus.Empty;
                                if (buffer.Length > 1)
                                {
                                    byte[] closeByteCode = new byte[] { buffer[1], buffer[0] };
                                    UInt16 statusCode = BitConverter.ToUInt16(closeByteCode, 0);
                                    if (statusCode > 999 && statusCode < 1012) 
                                    {
                                        _closeStatus = (WebSocketCloseStatus)statusCode;
                                    }
                                }

                                if (State != WebSocketFrame.WebSocketState.CloseSent)
                                { //conection asked to be closed return awnser
                                    State = WebSocketFrame.WebSocketState.CloseReceived;
                                    RawClose(WebSocketCloseStatus.NormalClosure, buffer = null, true);
                                    
                                }
                                else //response to conenction close we can shut down the socket.
                                {
                                    
                                    HardClose();
                                    
                                }
                                break;
                        }
                    }
                    else
                    {
                        RelayMessage(messageFrame);
                    }

                }

                //Controlling ping and ControllerMessagesTimeout
                if (_pinging && _pingTime.Add(ServerTimeout ) < DateTime.UtcNow)
                {
                    RawClose(WebSocketCloseStatus.PolicyViolation, Encoding.UTF8.GetBytes("Ping timeout"), true);
                    Debug.WriteLine($"{RemoteEndPoint} ping timed out");
                }
                if (State == WebSocketFrame.WebSocketState.CloseSent && _closingTime.Add(ServerTimeout ) < DateTime.UtcNow)
                {
                    HardClose();
                }
                if (KeepAliveInterval != Timeout.InfiniteTimeSpan && State != WebSocketFrame.WebSocketState.CloseSent && !_pinging && LastReceivedMessage.Add(KeepAliveInterval) < DateTime.UtcNow)
                {
                    SendPing();
                }

            }

            _receiveStream.Close();
        }


        private void RelayMessage(ReceiveMessageFrame messageFrame)
        {
            if (messageFrame.Error)
            {
                Debug.WriteLine($"{RemoteEndPoint.ToString()} error - {messageFrame.ErrorMessage}");
                RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
            }
            else
            {
                LastReceivedMessage = DateTime.UtcNow;
                OnNewMessage(messageFrame);

                are.WaitOne();

            }
        }

        //Ping will ony commence from this thread because of threading safety. 
        private void SendPing(string pingContent = "hello")
        {
            _pinging = true;
            _pingTime = DateTime.UtcNow;
            QueueSendMessage(new SendMessageFrame()
            {
                Buffer = Encoding.UTF8.GetBytes(pingContent),
                OpCode = OpCode.PingFrame

            });
        }

        private void StopReceiving()
        {
            _stopped = true;
        }

        private void OnNewMessage(ReceiveMessageFrame message)
        {
            _messageReceivedEventHandler?.Invoke(this, new MessageReceivedEventArgs() { Frame = message });
        }

        private void OnMessageRead(object sender, EventArgs e)
        {
            //message is read so continue the receive thread.
            are.Set();
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public ReceiveMessageFrame Frame { get; set; }
    }
}
