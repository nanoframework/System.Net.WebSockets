using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    internal class ReceiveAndControllThread
    {
        private readonly WebSocketClient _webSocketClient;

        public ReceiveAndControllThread(WebSocketClient webSocketClient)
        {
            _webSocketClient = webSocketClient;
        }

        public void WorkerThread() //this thread is always running and thus the best place for controlling ping and other messages
        {
            while (!_webSocketClient.Stopped)
            {
                var messageFrame = _webSocketClient.WebSocketReceiver.StartReceivingMessage();

                if (messageFrame == null)
                {
                    //Here we could let the thread sleep to safe resources   
                }
                else
                {
                    //handle error
                    if (messageFrame.Error)
                    {
                        _webSocketClient.HasError = true;

                        Debug.WriteLine($"{_webSocketClient.RemoteEndPoint} closed with error: {messageFrame.ErrorMessage}");

                        _webSocketClient.RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                    }
                    else if (messageFrame.IsControllFrame)
                    {
                        byte[] buffer = _webSocketClient.WebSocketReceiver.ReadBuffer(messageFrame.MessageLength);

                        _webSocketClient.LastContactTimeStamp = DateTime.UtcNow;

                        switch (messageFrame.OpCode)
                        {
                            case OpCode.PingFrame:
                                // need to send a Pong
                                var pong = new SendMessageFrame() { Buffer = buffer, OpCode = OpCode.PongFrame};
                                messageFrame.OpCode = OpCode.PongFrame;
                                messageFrame.IsMasked = false;
                                _webSocketClient.QueueMessageToSend(pong);
                                break;

                            case OpCode.PongFrame: 
                                // received a Pong
                                // checking if content Pong matches Ping is not implemented due to thread safety and memory consumption considerations
                                _webSocketClient.Pinging = false; 
                                break;

                            case OpCode.ConnectionCloseFrame:
                                _webSocketClient.CloseStatus = WebSocketCloseStatus.Empty;

                                if (buffer.Length > 1)
                                {
                                    byte[] closeByteCode = new byte[] { buffer[1], buffer[0] };
                                    UInt16 statusCode = BitConverter.ToUInt16(closeByteCode, 0);
                                    if (statusCode > 999 && statusCode < 1012) 
                                    {
                                        _webSocketClient.CloseStatus = (WebSocketCloseStatus)statusCode;
                                    }
                                }

                                //connection asked to be closed return answer
                                if (_webSocketClient.State != WebSocketFrame.WebSocketState.CloseSent)
                                {
                                    _webSocketClient.State = WebSocketFrame.WebSocketState.CloseReceived;

                                    _webSocketClient.RawClose(WebSocketCloseStatus.NormalClosure, buffer, true);
                                }
                                //response to connection close we can shut down the socket.
                                else
                                {
                                    _webSocketClient.HardClose();   
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (messageFrame.Error)
                        {
                            Debug.WriteLine($"Error message from '{_webSocketClient.RemoteEndPoint}' error - {messageFrame.ErrorMessage}");

                            _webSocketClient.RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                        }
                        else
                        {
                            messageFrame.Buffer = _webSocketClient.WebSocketReceiver.ReadBuffer(messageFrame.MessageLength);

                            _webSocketClient.LastContactTimeStamp = DateTime.UtcNow;

                            OnNewMessage(messageFrame);
                        }
                    }
                }

                //Controlling ping and ControllerMessagesTimeout
                if (_webSocketClient.Pinging && _webSocketClient.PingTime.Add(_webSocketClient.ServerTimeout ) < DateTime.UtcNow)
                {
                    _webSocketClient.RawClose(WebSocketCloseStatus.PolicyViolation, Encoding.UTF8.GetBytes("Ping timeout"), true);

                    Debug.WriteLine($"{_webSocketClient.RemoteEndPoint} ping timed out");
                }

                if (_webSocketClient.State == WebSocketFrame.WebSocketState.CloseSent && _webSocketClient.ClosingTime.Add(_webSocketClient.ServerTimeout ) < DateTime.UtcNow)
                {
                    _webSocketClient.HardClose();
                }

                if (_webSocketClient.KeepAliveInterval != Timeout.InfiniteTimeSpan && _webSocketClient.State != WebSocketFrame.WebSocketState.CloseSent && !_webSocketClient.Pinging && _webSocketClient.LastContactTimeStamp.Add(_webSocketClient.KeepAliveInterval) < DateTime.UtcNow)
                {
                    _webSocketClient.SendPing();
                }
            }

            _webSocketClient.ReceiveStream.Close();
        }

        private void OnNewMessage(ReceiveMessageFrame message)
        {
            _webSocketClient.CallbacksMessageReceivedEventHandler?.Invoke(this, new MessageReceivedEventArgs() { Frame = message });
        }
    }
}
