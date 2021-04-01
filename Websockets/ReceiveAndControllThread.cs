using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    internal class ReceiveAndControllThread
    {
        private readonly WebSocket _webSocket;

        public ReceiveAndControllThread(WebSocket webSocket)
        {
            _webSocket = webSocket;
        }

        public void WorkerThread() //this thread is always running and thus the best place for controlling ping and other messages
        {
            var timeoutCheckerTimer = new Timer(CheckTimeouts, Thread.CurrentThread, 5000, 5000); 
            

            while (!_webSocket.Stopped)
            {
                var messageFrame = _webSocket.WebSocketReceiver.StartReceivingMessage();

                if (messageFrame == null)
                {
                    //Here we could let the thread sleep to safe resources   
                }
                else
                {
                    //handle error
                    if (messageFrame.Error)
                    {
                        _webSocket.HasError = true;

                        Debug.WriteLine($"{_webSocket.RemoteEndPoint} closed with error: {messageFrame.ErrorMessage}");

                        _webSocket.RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                    }
                    else if (messageFrame.IsControllFrame)
                    {
                        byte[] buffer = _webSocket.WebSocketReceiver.ReadBuffer(messageFrame.MessageLength);

                        _webSocket.LastContactTimeStamp = DateTime.UtcNow;

                        switch (messageFrame.OpCode)
                        {
                            case OpCode.PingFrame:
                                // need to send a Pong
                                var pong = new SendMessageFrame() { Buffer = buffer, OpCode = OpCode.PongFrame};
                                messageFrame.OpCode = OpCode.PongFrame;
                                messageFrame.IsMasked = false;
                                _webSocket.QueueMessageToSend(pong);
                                break;

                            case OpCode.PongFrame: 
                                // received a Pong
                                // checking if content Pong matches Ping is not implemented due to thread safety and memory consumption considerations
                                _webSocket.Pinging = false; 
                                break;

                            case OpCode.ConnectionCloseFrame:
                                _webSocket.CloseStatus = WebSocketCloseStatus.Empty;

                                if (buffer.Length > 1)
                                {
                                    byte[] closeByteCode = new byte[] { buffer[1], buffer[0] };
                                    UInt16 statusCode = BitConverter.ToUInt16(closeByteCode, 0);
                                    if (statusCode > 999 && statusCode < 1012) 
                                    {
                                        _webSocket.CloseStatus = (WebSocketCloseStatus)statusCode;
                                    }
                                }

                                //connection asked to be closed return answer
                                if (_webSocket.State != WebSocketFrame.WebSocketState.CloseSent)
                                {
                                    _webSocket.State = WebSocketFrame.WebSocketState.CloseReceived;

                                    _webSocket.RawClose(WebSocketCloseStatus.NormalClosure, buffer, true);
                                }
                                //response to connection close we can shut down the socket.
                                else
                                {
                                    _webSocket.HardClose();   
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (messageFrame.Error)
                        {
                            Debug.WriteLine($"Error message from '{_webSocket.RemoteEndPoint}' error - {messageFrame.ErrorMessage}");

                            _webSocket.RawClose(messageFrame.CloseStatus, Encoding.UTF8.GetBytes(messageFrame.ErrorMessage), true);
                        }
                        else
                        {
                            messageFrame.Buffer = _webSocket.WebSocketReceiver.ReadBuffer(messageFrame.MessageLength);

                            _webSocket.LastContactTimeStamp = DateTime.UtcNow;

                            OnNewMessage(messageFrame);
                        }
                    }
                }

                
            }

            _webSocket.ReceiveStream.Close();
            timeoutCheckerTimer.Change(Timeout.Infinite, Timeout.Infinite);
            timeoutCheckerTimer.Dispose();
        }


        private void CheckTimeouts(object thread)
        {
            var receiveThread = (Thread)thread;
            receiveThread.Suspend();
            //Controlling ping and ControllerMessagesTimeout
            if (_webSocket.Pinging && _webSocket.PingTime.Add(_webSocket.ServerTimeout) < DateTime.UtcNow)
            {
                _webSocket.RawClose(WebSocketCloseStatus.PolicyViolation, Encoding.UTF8.GetBytes("Ping timeout"), true);

                Debug.WriteLine($"{_webSocket.RemoteEndPoint} ping timed out");
            }

            if (_webSocket.State == WebSocketFrame.WebSocketState.CloseSent && _webSocket.ClosingTime.Add(_webSocket.ServerTimeout) < DateTime.UtcNow)
            {
                _webSocket.HardClose();
            }

            if (_webSocket.KeepAliveInterval != Timeout.InfiniteTimeSpan && _webSocket.State != WebSocketFrame.WebSocketState.CloseSent && !_webSocket.Pinging && _webSocket.LastContactTimeStamp.Add(_webSocket.KeepAliveInterval) < DateTime.UtcNow)
            {
                _webSocket.SendPing();
            }

            receiveThread.Resume();
            
        }

        private void OnNewMessage(ReceiveMessageFrame message)
        {
            _webSocket.CallbacksMessageReceivedEventHandler?.Invoke(this, new MessageReceivedEventArgs() { Frame = message });
        }

    }
}
