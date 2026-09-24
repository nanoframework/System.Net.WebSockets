//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Net.WebSockets.WebSocketFrame;
using System.Text;
using System.Threading;

namespace System.Net.WebSockets
{
    internal class ReceiveAndControllThread
    {
        private readonly WebSocket _webSocket;
        private int _checkingTimeouts = 0;

        public ReceiveAndControllThread(WebSocket webSocket)
        {
            _webSocket = webSocket;
        }

        public void WorkerThread() //this thread is always running and thus the best place for controlling ping and other messages
        {
            var timeoutCheckerTimer = new Timer(CheckTimeouts, null, 5000, 5000);


            while (!_webSocket.Stopped)
            {
                try
                {
                    ProcessIncomingMessage();
                }
                catch (Exception ex)
                {
                    // failure reading from the stream (connection closed, reset, disposed, etc.)
                    if (!_webSocket.Stopped)
                    {
                        _webSocket.HasError = true;

                        Debug.WriteLine($"{_webSocket.RemoteEndPoint} closed with error: {ex.Message}");

                        // don't leak internal exception details to the peer
                        _webSocket.RawClose(WebSocketCloseStatus.EndpointUnavailable, Encoding.UTF8.GetBytes("Connection error"), true);

                        // RawClose returns without closing if a close was already sent (or the socket is closing),
                        // and the timeout checker is disposed below, so make sure the connection is closed
                        _webSocket.HardClose();
                    }

                    // stream can't be used anymore
                    break;
                }
            }

            timeoutCheckerTimer.Change(Timeout.Infinite, Timeout.Infinite);
            timeoutCheckerTimer.Dispose();
            _webSocket.ReceiveStream.Close();
        }

        private void ProcessIncomingMessage()
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
                    byte[] buffer = _webSocket.WebSocketReceiver.ReadBuffer(messageFrame.MessageLength, messageFrame.Masks);

                    _webSocket.TouchLastContact();

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
                            _webSocket.OnPongReceived();
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
                            if (_webSocket.TryMarkCloseReceived())
                            {
                                _webSocket.RawClose(WebSocketCloseStatus.NormalClosure, buffer, true);
                            }

                            // either this is the response to our close, or RawClose lost a race with another close,
                            // so we can shut down the socket (no-op if already closed)
                            _webSocket.HardClose();
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
                        messageFrame.Buffer = _webSocket.WebSocketReceiver.ReadBuffer(messageFrame.MessageLength, messageFrame.Masks);

                        _webSocket.TouchLastContact();

                        OnNewMessage(messageFrame);
                    }
                }
            }
        }


        // Runs on the timer thread, concurrently with the receive thread.
        // Shared state is accessed through WebSocket helpers that only hold a lock for field access,
        // and nothing here blocks, so a receive thread holding a lock can't stall this check.
        private void CheckTimeouts(object state)
        {
            // skip this tick if the previous one is still running
            if (Interlocked.CompareExchange(ref _checkingTimeouts, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (_webSocket.Stopped)
                {
                    return;
                }

                //Controlling ping and ControllerMessagesTimeout
                switch (_webSocket.EvaluateTimeouts(DateTime.UtcNow))
                {
                    case WebSocket.TimeoutAction.PingTimeout:
                        Debug.WriteLine($"{_webSocket.RemoteEndPoint} ping timed out");

                        // don't wait for the close message to be sent, a following check will close the connection
                        _webSocket.BeginClose(WebSocketCloseStatus.PolicyViolation, Encoding.UTF8.GetBytes("Ping timeout"));
                        break;

                    case WebSocket.TimeoutAction.HardClose:
                        _webSocket.HardClose();
                        break;

                    case WebSocket.TimeoutAction.SendPing:
                        _webSocket.SendPing();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{_webSocket.RemoteEndPoint} error checking timeouts: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _checkingTimeouts, 0);
            }
        }

        private void OnNewMessage(ReceiveMessageFrame message)
        {
            _webSocket.CallbacksMessageReceivedEventHandler?.Invoke(_webSocket, new MessageReceivedEventArgs() { Frame = message });
        }
    }
}
