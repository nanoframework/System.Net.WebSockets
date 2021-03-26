using System;
using System.Net;
using System.Text;
using System.Collections;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using nanoframework.System.Net.Websockets;
using nanoframework.System.Net.Websockets.WebSocketFrame;

namespace nanoframework.System.Net.Websockets.Server
{
    //
    // Summary:
    //     The WebSocketServer class is a WebSocket Server to which WebSocket Clients can connect.
    public class WebSocketServer : IDisposable
    {

        public delegate void MessageReceivedEventhandler(object sender, MessageReceivedEventArgs e);
        public delegate void WebSocketOpenedEventhandler(object sender, WebSocketOpenedEventArgs e);
        public delegate void WebSocketClosedEventhandler(object sender, WebSocketClosedEventArgs e);

        //
        // Summary:
        //     Occurs when a message is received by the server. The argument contains the received message frame. 
        //
        // Remarks:
        //     The WebSocketServer will stop to receiving any incoming messages including controller messages until 
        //     the included MessageStream is completely read till the end. 
        public event MessageReceivedEventhandler MessageReceived;

        //
        // Summary:
        //     Occurs when a new Client is connected. The argument contains the connected clients endpoint.
        public event WebSocketOpenedEventhandler WebSocketOpened;

        //
        // Summary:
        //     Occurs when a Client is disconnected. The argument contains the disconnected clients endpoint.
        public event WebSocketClosedEventhandler WebSocketClosed;

        //
        // Summary:
        //     The maximum number of clients that can connect to the server.
        public int MaxClients => _options.MaxClients;

        //
        // Summary:
        //     The WebSocketServer timeout which specifies how long to wait for a message.
        public TimeSpan ServerTimeout => _options.ServerTimeout;

        // Summary:
        //     The maximum allowed byte length of messages received by the WebSocket .
        public int MaxReceiveFrameSize => _options.MaxReceiveFrameSize;

        // Summary:
        //     The WebSocketServer protocol keep-alive interval.
        public TimeSpan KeepAliveInterval => _options.KeepAliveInterval;

        // Summary:
        //     The number of Clients connected to the WebSocketServer.
        public int ClientsCount { get => _webSocketClientsPool.Count; }

        //
        // Summary:
        //     Gets the maximum allowed byte length of a partial message send by the WebSocketServer.
        //     By default if a message that exceeds the size limit it will be broken up in smaller partial messages
        public int FragmentSize => _options.MaxFragmentSize;

        //
        // Summary:
        //     The remote Prefix clients need to connect to.
        public string Prefix => _options.Prefix;

        //
        // Summary:
        //     The local Port to listen on.
        public int Port => _options.Port;

        //
        // Summary:
        //     The server name that is presented to the client during the handshake
        public string ServerName => _options.ServerName;

        //
        // Summary:
        //     True, server is started. False, means server is not active.
        public bool Started { get; private set; } = false;

        //
        // Summary:
        //     Gets an array of all connected client IPEndPoints.
        public string[] ListClients { get => _webSocketClientsPool.List; }

        private WebSocketClientsPool _webSocketClientsPool;
        private Thread _listnerThread;

        private WebSocketServerOptions _options = new WebSocketServerOptions();

        //
        // Summary:
        //     Creates an instance of the System.Net.WebSockets.WebSocketServer class.
        //
        // Parameters:
        //     options:
        //          Optional WebSocketServerOptions where extra options can be defined.
        public WebSocketServer(WebSocketServerOptions options = null)
        {
            if(options != null)
            {
                if (options.Prefix[0] != '/') throw new Exception("websocket prefix has to start with '/'");
                _options = options;
            }
            _webSocketClientsPool = new WebSocketClientsPool(MaxClients);
        }



        //
        // Summary:
        //     Starts the server.
        public void Start()
        {
            Started = true;
            _listnerThread = new Thread(ListenIncommingSocketRequest);
            _listnerThread.Start();
            

        }

        // Summary:
        //     Sends a text message to the specified client
        //
        // Parameters:
        //   endPoint:
        //     The IP endpoint of the connected client.
        //
        //   message:
        //     The text message.
        //
        //  fragmentSize:
        //      Override the maxFragmentSize used
        //      Default -1 will use the maxFragmentSize 
        //
        // Remarks:
        //     Messages are buffered send synchronously using a single send thread.
        //     The send message is not awaited. 

        public void SendText(string endPoint, string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
            {
                client.SendString(message, fragmentSize);
            }
        }

        // Summary:
        //     Sends a binary message to the specified client
        //
        // Parameters:
        //   endPoint:
        //     The IP endpoint of the connected client.
        //
        //   buffer:
        //     The data content of the message.
        //
        //  fragmentSize:
        //      Override the maxFragmentSize used
        //      Default -1 will use the maxFragmentSize 
        //
        // Remarks:
        //     Messages are buffered send synchronously using a single send thread.
        //     The send message is not awaited. 
        public void SendData(string endPoint, byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
            {
                client.SendBytes(buffer, fragmentSize);
            }
        }

        // Summary:
        //      Broadcast a binary message to all connected clients
        //
        // Parameters:
        //   buffer:
        //     The data content of the message.
        //
        //  fragmentSize:
        //      Override the maxFragmentSize used
        //      Default -1 will use the maxFragmentSize 
        //
        // Remarks:
        //     Messages are buffered send synchronously using a single send thread.
        //     The send message is not awaited. 
        public void BroadCast(byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0? FragmentSize : fragmentSize;
            foreach (string endPoint in _webSocketClientsPool.List)
            {
                SendData(endPoint, buffer, fragmentSize);
            }
        }

        // Summary:
        //     Broadcast a text message to all connected clients
        //
        // Parameters:
        //   message:
        //     The text message.
        //
        //  fragmentSize:
        //      Override the maxFragmentSize used
        //      Default -1 will use the maxFragmentSize 
        //
        // Remarks:
        //     Messages are buffered send synchronously using a single send thread.
        //     The send message is not awaited. 
        public void BroadCast(string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var list = _webSocketClientsPool.List;
            foreach (string endPoint in list)
            {
                SendText(endPoint, message, fragmentSize);
            }
        }

        // Summary:
        //     Will start closing the WebSocket connection to the client using
        //     handshake defined in the WebSocket protocol specification section 7.
        //     After connection is closed the client will be removed from the clientpool
        //
        // Parameters:
        //   endPoint:
        //     The IP endpoint of the connected client.
        //
        //   closeStatus:
        //     Indicates the reason for closing the WebSocket connection.
        //
        //   abort:
        //     Optional, set to true to direct abort client connection in contrary to close connection
        //     using WebSocket protocol section 7.
        //
        // Remarks:
        //     WebSocketCloseStatus.EndpointUnavailable will close the WebSocket synchronous without awaiting response.
        public void DisconnectClient(string endPoint, WebSocketCloseStatus closeStatus, bool abort = false)
        {

            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
                if (abort)
                {
                    client.Abort();
                }
                else
                {
                    client.Close(closeStatus);
                }
            }



        //
        // Summary:
        //     Stops the server and disconnects all connected clients with code WebSocketCloseStatus.EndpointUnavailable. 
        public void Stop()
        {
            if (Started)
            {
                Started = false;

                foreach (string endPoint in _webSocketClientsPool.List)
                {
                    DisconnectClient(endPoint, WebSocketCloseStatus.EndpointUnavailable);
                }
            }
            
        }

        //
        // Summary:
        //     Aborts the server and all WebSocket connections and any pending IO operations.
        public void Abort()
        {
            Started = false;
            foreach (string endPoint in _webSocketClientsPool.List)
            {
                DisconnectClient(endPoint, WebSocketCloseStatus.EndpointUnavailable, true);
            }
        }


        private void ListenIncommingSocketRequest()
        {
            var listenPoint = new IPEndPoint(IPAddress.Any, Port);

            using (Socket mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                mySocket.Bind(listenPoint);
                Debug.WriteLine("websocket server Started!");
                while (Started)
                {
                    mySocket.Listen(2);

                    var socket = mySocket.Accept();
                    HandleTcpWebSocketRequest(socket, Prefix, ServerName);

                }

            }

            Debug.WriteLine("websocket server halted!");
        }

        private bool HandleTcpWebSocketRequest(Socket networkSocket, string prefix = "/", string serverName = "NFWebsocketServer") 
        {

            //WebSocketStream networkStream = new WebSocketStream(new NetworkStream(networkSocket));
            

            //string beginHeader = ($"GET {prefix} HTTP/1.1".ToLower());
            //byte[] bufferStart = new byte[beginHeader.Length];
            //byte[] buffer = new byte[600];

            //int bytesRead = networkStream.Read(bufferStart, 0, bufferStart.Length);
            //if (bytesRead == bufferStart.Length)
            //{
            //    if (Encoding.UTF8.GetString(bufferStart, 0 , bufferStart.Length).ToLower() == beginHeader)
            //    { //right http request
            //        bytesRead = networkStream.Read(buffer, 0, buffer.Length);
            //        if (bytesRead > 20)
            //        {
            //            var headers = WebSocketHelpers.ParseHeaders(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            //            if (((string)headers["connection"]).ToLower() == "upgrade" && ((string)headers["upgrade"]).ToLower() == "websocket" && headers["sec-websocket-key"] != null)
            //            {
            //                if (_webSocketClientsPool.Count >= _webSocketClientsPool.Max) {
            //                    byte[] serverFullResponse = Encoding.UTF8.GetBytes($"HTTP/1.1 503 Websocket Server is full\r\n\r\n");
            //                    networkStream.Write(serverFullResponse, 0, serverFullResponse.Length);
            //                    return false;
            //                } 
            //                //calculate sec-websocket-key and complete handshake
            //                string swk = (string)headers["sec-websocket-key"];
            //                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; //default signature for websocket
            //                byte[] swkaSha1 = WebSocketHelpers.ComputeHash(swka);
            //                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
            //                byte[] response = Encoding.UTF8.GetBytes($"HTTP/1.1 101 Web Socket Protocol Handshake\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {swkaSha1Base64}\r\nServer: {ServerName}\r\nUpgrade: websocket\r\n\r\n");
            //                networkStream.Write(response, 0, response.Length);

            //                var webSocketClient = new WebSocketServerClient(_options);
            //                webSocketClient.ConnectToStream(networkStream, (IPEndPoint)networkSocket.RemoteEndPoint, onMessageReceived);
            //                if(_webSocketClientsPool.Add(webSocketClient)) { //check if clients are not full again
            //                    WebSocketOpened?.Invoke(this, new WebSocketOpenedEventArgs() { EndPoint = webSocketClient.RemoteEndPoint });
            //                    webSocketClient.ConnectionClosed += OnConnectionClosed;
            //                }
            //                else
            //                {
            //                    webSocketClient.Dispose();
            //                    return false;
            //                }
            //            }

            //        }
            //        else
            //        {
            //            networkStream.Close();
            //            return false;
            //        }
            //    }
            //}
            //else
            //{
            //    networkStream.Close();
            //    return false;
            //}

            return true;

        }

        private void OnConnectionClosed(object sender, EventArgs e)
        {

            var endPoint = ((WebSocket)sender).RemoteEndPoint;
            WebSocketClosed?.Invoke(this, new WebSocketClosedEventArgs() { EndPoint = endPoint});
            _webSocketClientsPool.Remove(endPoint.ToString());
        }

        private void onMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }


        // Summary:
        //     Releases the unmanaged resources used by the System.Net.WebSockets.WebSocketServer
        //     instance.
        public void Dispose()
        {
            if (Started)
            {
                Started = false;

                foreach (string endPoint in _webSocketClientsPool.List)
                {
                    DisconnectClient(endPoint, WebSocketCloseStatus.EndpointUnavailable, true);
                }
            }
        }
    }

    //
    // Summary:
    //     EventArgs used for WebSocketOpenedEventHandler
    public class WebSocketOpenedEventArgs : EventArgs
    {
        // Summary:
        //     The Remote Endpoint that is Opened.
        public IPEndPoint EndPoint;
    }

    //
    // Summary:
    //     EventArgs used for WebSocketClosedEventHandler
    public class WebSocketClosedEventArgs : EventArgs
    {
        // Summary:
        //     The Remote Endpoint that is closed.
        public IPEndPoint EndPoint;
    }
}


