//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace System.Net.WebSockets
{
    internal class WebSocketSender
    {
        private readonly bool _isServer;
        private readonly NetworkStream _outputStream;
        private readonly Thread _sendThread;
        private readonly Random _randomizer = new Random();

        private readonly AutoResetEvent _shutdownEvent = new AutoResetEvent(false);
        private readonly WebSocketWriteErrorHandler _webSocketWriteErrorCallback;

        private readonly Stack _controlMessages = new Stack();
        private readonly Queue _messageFrames = new Queue();

        //public int MaxBufferSize = 0; //TODO: Cap the number of bytes that can live within the buffer. Else it can clug up and run out of memory. 

        internal delegate void WebSocketWriteErrorHandler(object sender, WebSocketWriteErrorEventArgs e);
        
        internal bool CloseMessageSent { get; private set; } = false;

        internal bool ControlMessagesPresent
        {
            get
            {
                lock (_controlMessages.SyncRoot)
                {
                    return _controlMessages.Count > 0;
                }
            }
        }

        internal WebSocketSender(NetworkStream outputStream, bool isServer, WebSocketWriteErrorHandler webSocketWriteErrorCallback)
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
                lock (_controlMessages.SyncRoot)
                {
                    _controlMessages.Push(sendMessage);
                }
            }
            else
            {
                lock (_messageFrames.SyncRoot)
                {
                    _messageFrames.Enqueue(sendMessage);
                }
            }

            _shutdownEvent.Set();
        }

        internal void StopSender()
        {
            CloseMessageSent = true;

            _shutdownEvent.Set();
        }

        private void SendMessageWorkerThread()
        {
            bool controlFrameSent;
            bool messageFrameSent = false;

            while (!CloseMessageSent) 
            {
                controlFrameSent = ProcessControlMessages();
            
                if (!CloseMessageSent)
                {
                    messageFrameSent = ProcessMessageFrames();
                }

                if(!controlFrameSent && !messageFrameSent)
                {
                    _shutdownEvent.WaitOne(); 
                }
            }

            _outputStream.Close();
            
            Debug.WriteLine($"SendThread stopped");
        }

        private bool ProcessMessageFrames()
        {
            SendMessageFrame messageFrame = null;

            lock (_messageFrames.SyncRoot)
            {
                // get next message from queue
                if (_messageFrames.Count > 0)
                {
                    messageFrame = (SendMessageFrame)_messageFrames.Dequeue();
                }
            }

            if (messageFrame != null)
            {
                if (!messageFrame.IsFragmented || messageFrame.FragmentSize > messageFrame.Buffer.Length)
                {
                    // message is not fragmented or fragment is smaller that the buffer size
                    // good to go in a single batch
                    SendFrame(messageFrame);
                }
                else
                {
                    // large message, need to fragment

                    // compute number of frames required to send the message
                    int offset = 0;

                    int numberOfFrames = messageFrame.Buffer.Length / messageFrame.FragmentSize + (messageFrame.Buffer.Length % messageFrame.FragmentSize > 0 ? 1 : 0); 

                    for (int i = 0; i < numberOfFrames; i++)
                    {
                        //start frame fin = 0 Opcode normal
                        if (i == 0) 
                        {
                            SendFrame(messageFrame, FragmentationType.FirstFragment, offset, messageFrame.FragmentSize);
                            
                            // update offset
                            offset += messageFrame.FragmentSize;
                        }
                        //end frame set fin and opcode 0
                        else if (i + 1  == numberOfFrames)
                        {
                            SendFrame(messageFrame, FragmentationType.FinalFragment, offset, messageFrame.Buffer.Length - offset);
                        }
                        // in between frames OpCode 0 fin = 1
                        else
                        {
                            SendFrame(messageFrame, FragmentationType.Fragment, offset, messageFrame.FragmentSize);

                            // update offset
                            offset += messageFrame.FragmentSize;
                        }

                        // check for controllerMessages each time
                        if(ControlMessagesPresent)
                        {
                            ProcessControlMessages();
                        }
                    }
                }

                return true;
            }

            return false;
        }

        private bool ProcessControlMessages()
        {
            bool itemSend = false;
            bool keepChecking = true;

            while (keepChecking)
            {
                SendMessageFrame controlFrame = null;

                lock (_controlMessages.SyncRoot)
                {
                    // always do controller messages first in last-in first-out order
                    if (_controlMessages.Count > 0)
                    {
                        controlFrame = (SendMessageFrame)_controlMessages.Pop();
                    }
                    else
                    {
                        keepChecking = false;
                    }
                }

                if (controlFrame != null)
                {
                    SendFrame(controlFrame);

                    if(controlFrame.OpCode == OpCode.ConnectionCloseFrame)
                    {
                        // request to close the connection

                        // no need to keep checking messages stack
                        keepChecking = false;

                        // close message was sent
                        CloseMessageSent = true;

                        // wait until resources can be released. 
                        _shutdownEvent.WaitOne();
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

            //opcode is set
            tempBuf[0] = (byte)opcode; 
            tempBuf[0] += (byte)fin;
            tempBuf[1] = (byte)mask;

            //set 1 byte msglen
            if (mesLength < 126) 
            {
                tempBuf[1] += (byte)mesLength;
            } 
            else if (mesLength < ushort.MaxValue)
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

                _randomizer.NextBytes(masks);

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

                //can not send a close message because something is wrong with the write stream so no need to await this
                CloseMessageSent = true; 

                _webSocketWriteErrorCallback?.Invoke(this, new WebSocketWriteErrorEventArgs() { Frame = frame });
            }
        }
    }

    internal class WebSocketWriteErrorEventArgs : EventArgs
    {
        public SendMessageFrame Frame { get; set; }

        public string ErrorMessage => Frame.ErrorMessage;
    }
}
