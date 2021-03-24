using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    public class WebSocketSender
    {
        Stack ControllerMessages = new Stack();
        Queue MessageFrames = new Queue();
        //public int MaxBufferSize = 0; //TODO: Cap the number of bytes that can live within the buffer. Else it can clug up and run out of memory. 
        private object _controllerMessageLock = new object();
        private object _messageFrameLock = new object();
        private bool _isServer;
        private Stream _outputStream;
        private Thread _sendThread;
        private Random randomizer = new Random();
        private AutoResetEvent are = new AutoResetEvent(false);
        private WebSocketWriteErrorHandler _webSocketWriteErrorCallback;
        internal delegate void WebSocketWriteErrorHandler(object sender, WebSocketWriteErrorArgs e);
        internal bool CloseMessageSend { get; private set; } = false;

        internal WebSocketSender(Stream outputStream, bool isServer, WebSocketWriteErrorHandler webSocketWriteErrorCallback)
        {
            _outputStream = outputStream;
            _isServer = isServer;
            _sendThread = new Thread(SendMessageWorkerThread);
            _sendThread.Start();
            _webSocketWriteErrorCallback = webSocketWriteErrorCallback;
            
        }
        
        internal void QueueMessage(SendMessageFrame sendMessage)
        {
            if (sendMessage.IsControllFrame)
            {
                lock (_controllerMessageLock)
                {
                    ControllerMessages.Push(sendMessage);
                }
            }
            else
            {
                lock (_messageFrameLock)
                {
                    MessageFrames.Enqueue(sendMessage);
                }
            }

            are.Set();

        }

        internal void StopSender()
        {
             
            CloseMessageSend = true;
            are.Set();
        }


        private void SendMessageWorkerThread()
        {
            bool controllerFrameSend = false;
            bool messageFrameSend = false;
            while (!CloseMessageSend) 
            {
                controllerFrameSend = CheckControllerMessages();
                if (!CloseMessageSend)
                {
                    messageFrameSend = CheckMessagesFrames();
                }

                if(!controllerFrameSend && !messageFrameSend)
                {
                    are.WaitOne(); 
                }
            }
            _outputStream.Close();
            Debug.WriteLine($"SendThread stopped");
        }

        private bool CheckMessagesFrames()
        {

            SendMessageFrame messageFrame = null;
            lock (_controllerMessageLock)
            {
                if (MessageFrames.Count > 0) //always do controller messages first in last-in first-out order
                {
                    messageFrame = (SendMessageFrame)MessageFrames.Dequeue();
                }
            }

            if (messageFrame != null)
            {
                int messageLength = messageFrame.Buffer.Length;
                
                if (!messageFrame.IsFragmented || messageFrame.FragmentSize > messageFrame.Buffer.Length)
                {
                    SendFrame(messageFrame);
                }
                else
                {
                    int offset = 0;
                    int numberOfFrames = messageFrame.Buffer.Length / messageFrame.FragmentSize + (messageFrame.Buffer.Length % messageFrame.FragmentSize > 0 ? 1 : 0); //number of frames to send message in
                    for (int i = 0; i < numberOfFrames; i++)
                    {
                        if (i == 0) //start frame fin = 0 Opcode normal
                        {
                            SendFrame(messageFrame, FragmentationType.FirstFragment, offset, messageFrame.FragmentSize);
                            offset += messageFrame.FragmentSize;

                        } //start frame
                        else if(i + 1  == numberOfFrames) //end frame set fin and opcode 0
                        {
                            SendFrame(messageFrame, FragmentationType.FinalFragment, offset, messageFrame.Buffer.Length - offset);

                        }
                        else //in between frames OpCode 0 fin = 1
                        {
                            SendFrame(messageFrame, FragmentationType.Fragment, offset, messageFrame.FragmentSize);
                            offset += messageFrame.FragmentSize;
                        }

                        //check controllerMessages each time
                        CheckControllerMessages();
                    }
                }

                return true;
            }

            return false;
        }

        private bool CheckControllerMessages()
        {
            bool itemSend = false;
            bool keepChecking = true;
            while (keepChecking)
            {
                SendMessageFrame controllerFrame = null;
                lock (_controllerMessageLock)
                {
                    if (ControllerMessages.Count > 0) //always do controller messages first in last-in first-out order
                    {
                        controllerFrame = (SendMessageFrame)ControllerMessages.Pop();
                    }
                    else keepChecking = false;
                }

                if (controllerFrame != null)
                {
                    SendFrame(controllerFrame);
                    if(controllerFrame.OpCode == OpCode.ConnectionCloseFrame) //close the connection
                    {
                        keepChecking = false;
                        CloseMessageSend = true;
                        are.WaitOne(); //wait until resouces can be released. 
                    }
                    itemSend = true;
                }

            }

            return itemSend;
            
        }


        private void SendFrame(SendMessageFrame frame, FragmentationType fragmentationType = FragmentationType.NotFragmented, int offset = 0, int length = 0)
        {
            int mesLength = fragmentationType == FragmentationType.NotFragmented ? frame.Buffer.Length : length;
            OpCode opcode = fragmentationType == FragmentationType.NotFragmented || fragmentationType == FragmentationType.FirstFragment ? frame.OpCode : OpCode.ContinuationFrame;
            int fin = fragmentationType == FragmentationType.NotFragmented || fragmentationType == FragmentationType.FinalFragment ? 128 : 0;
            int mask = !_isServer ? 128 : 0; 
            int messageOffset = 2;

            //set header
            int tempBufLength = 2;
            if (mask == 128) {
                tempBufLength += 4;
                messageOffset += 4;
            }
            if (mesLength > ushort.MaxValue)
            {
                tempBufLength += 8;
                messageOffset += 8;
            }
            else if (mesLength > 125)
            {
                tempBufLength += 2;
                messageOffset += 2;
            }
            tempBufLength += mesLength;

            byte[] tempBuf = new byte[tempBufLength];

            tempBuf[0] = (byte)opcode; //opcode is set
            tempBuf[0] += (byte)fin;
            tempBuf[1] = (byte)mask;
            if (mesLength < 126) //set 1 byte msglen
            {
                tempBuf[1] += (byte)mesLength;
            } else if (mesLength < ushort.MaxValue)
            {
                tempBuf[1] += 126;
                WebSocketHelpers.ReverseBytes(BitConverter.GetBytes((UInt16)mesLength)).CopyTo(tempBuf, 2);
            }
            else
            {
                tempBuf[1] += 127;
                WebSocketHelpers.ReverseBytes(BitConverter.GetBytes((UInt64)mesLength)).CopyTo(tempBuf, 2);
            }

            if (mask == 128) 
            {
                var masks = new byte[4];
                randomizer.NextBytes(masks);
                for (int i = offset; i < mesLength; ++i)
                {
                    frame.Buffer[i] = (byte)(frame.Buffer[i] ^ masks[i % 4]);
                }
                masks.CopyTo(tempBuf, messageOffset - 4);
            }

            Array.Copy(frame.Buffer, offset, tempBuf, messageOffset, mesLength);

            try
            {
                _outputStream.Write(tempBuf, 0, tempBuf.Length);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                frame.ErrorMessage = ex.Message;
                CloseMessageSend = true; //can not send a close message because something is wrong with the write stream so no need to await this. 
                _webSocketWriteErrorCallback?.Invoke(this, new WebSocketWriteErrorArgs() { frame = frame });
            }

        }

    }

    internal class WebSocketWriteErrorArgs : EventArgs
    {
        public SendMessageFrame frame { get; set; }
        public string ErrorMessage => frame.ErrorMessage;
    }
}
