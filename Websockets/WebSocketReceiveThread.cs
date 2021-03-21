using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    partial class WebSocket
    {
        public DateTime LastReceivedMessage { get; private set; }

        public int ControllerMessagesTimeoutSec { get; private set; }
        public int HeartBeatSec { get; private set; }
        public bool Closing { get; private set; }
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
                        HasError = true;
                        Debug.WriteLine(messageFrame.ErrorMessage);
                        CloseConnection(messageFrame.ErrorMessage);
                        
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
                                
                                if (!Closing)
                                { //conection asked to be closed return awnser
                                    StopConnection(buffer, true);
                                    
                                }
                                else //response to conenction close we can shut down the socket.
                                {
                                    Closing = true;
                                    CloseConnection("Gracefully closing");
                                    
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
                if (_pinging && _pingTime.AddSeconds(ControllerMessagesTimeoutSec) < DateTime.UtcNow)
                {
                    CloseConnection("Ping timeout");
                }
                if (Closing && _closingTime.AddSeconds(ControllerMessagesTimeoutSec) < DateTime.UtcNow)
                {
                    CloseConnection("Grace time for closing has ended");
                }
                if (HeartBeatSec > 0 && !Closing && !_pinging && LastReceivedMessage.AddSeconds(HeartBeatSec) < DateTime.UtcNow)
                {
                    SendPing();
                }

            }


            _receiveStream.Close();
            Console.WriteLine($"ReceiveThread stopped");
        }


        private void RelayMessage(ReceiveMessageFrame messageFrame)
        {
            if (messageFrame.Error)
            {
                Debug.WriteLine($"{RemotEndPoint.ToString()} error - {messageFrame.ErrorMessage}");
                CloseConnection(messageFrame.ErrorMessage);
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
            Closing = true;
            

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
