using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    partial class WebSocket
    {
        internal DateTime _closingTime = DateTime.UtcNow;
        private MessageReceivedEventHandler _messageReceivedEventHandler;       
        private WebSocketReceiver _webSocketReceiver;
        private WebSocketSender _webSocketSender;
        private bool _pinging = false;
        private DateTime _pingTime = DateTime.UtcNow;

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
                    //Here we could let the thread sleep to safe resources   
                }
                else
                {
                    //handle error
                    if (messageFrame.Error)
                    {
                        _hasError = true;

                        Debug.WriteLine($"{RemoteEndPoint} closed with error: {messageFrame.ErrorMessage}");

                        RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                    }
                    else if (messageFrame.IsControllFrame)
                    {
                        byte[] buffer = _webSocketReceiver.ReadBuffer(messageFrame.MessageLength);

                        LastContact = DateTime.UtcNow;

                        switch (messageFrame.OpCode)
                        {
                            case OpCode.PingFrame: //send Pong
                                var pong = new SendMessageFrame() { Buffer = buffer, OpCode = OpCode.PongFrame};
                                messageFrame.OpCode = OpCode.PongFrame;
                                messageFrame.IsMasked = false;
                                QueueMessageToSend(pong);
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

                                //conection asked to be closed return awnser
                                if (State != WebSocketFrame.WebSocketState.CloseSent)
                                { 
                                    State = WebSocketFrame.WebSocketState.CloseReceived;

                                    RawClose(WebSocketCloseStatus.NormalClosure, buffer = null, true);
                                }
                                //response to conenction close we can shut down the socket.
                                else
                                {
                                    HardClose();   
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (messageFrame.Error)
                        {
                            Debug.WriteLine($"{RemoteEndPoint.ToString()} error - {messageFrame.ErrorMessage}");

                            RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                        }
                        else
                        {
                            messageFrame.Buffer = _webSocketReceiver.ReadBuffer(messageFrame.MessageLength);

                            Debug.WriteLine($"received message: {messageFrame.MessageType.ToString()}");

                            LastContact = DateTime.UtcNow;

                            OnNewMessage(messageFrame);
                        }
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

                if (KeepAliveInterval != Timeout.InfiniteTimeSpan && State != WebSocketFrame.WebSocketState.CloseSent && !_pinging && LastContact.Add(KeepAliveInterval) < DateTime.UtcNow)
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
                LastContact = DateTime.UtcNow;
                OnNewMessage(messageFrame);
            }
        }

        //Ping will only commence from this thread because of threading safety. 
        private void SendPing(string pingContent = "hello")
        {
            _pinging = true;
            _pingTime = DateTime.UtcNow;

            QueueMessageToSend(new SendMessageFrame()
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
    }
}
