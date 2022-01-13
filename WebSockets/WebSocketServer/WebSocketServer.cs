//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Net.WebSockets.WebSocketFrame;
using System.Text;
using System.Threading;
using System.Net;

namespace System.Net.WebSockets.Server
{
    //
    /// <summary>
    /// The WebSocketServer class is a WebSocket Server to which WebSocket Clients can connect.
    /// </summary>
    public class WebSocketServer : IDisposable
    {
        /// <summary>
        /// Describes the method that will handle the event that's fired when a message that has been subscribed to has been received.
        /// </summary>
        /// <param name="sender">The proximity device that received the message.</param>
        /// <param name="e">The message and related data that were passed to the event handler.</param>
        public delegate void MessageReceivedEventhandler(object sender, MessageReceivedEventArgs e);

        /// <summary>
        /// Describes the method that will handle the event that's fired when the <see cref="WebSocket"/> is opened.
        /// </summary>
        /// <param name="sender">The proximity device that received the message.</param>
        /// <param name="e">The opened event data that were passed to the event handler.</param>
        public delegate void WebSocketOpenedEventhandler(object sender, WebSocketOpenedEventArgs e);

        /// <summary>
        /// Describes the method that will handle the event that's fired when the <see cref="WebSocket"/> is closed.
        /// </summary>
        /// <param name="sender">The proximity device that received the message.</param>
        /// <param name="e">The closed event data that were passed to the event handler.</param>
        public delegate void WebSocketClosedEventhandler(object sender, WebSocketClosedEventArgs e);

        /// <summary>
        /// Occurs when a message is received by the server. The argument contains the received message frame. 
        /// </summary>
        ///<remarks>
        /// The WebSocketServer will stop to receiving any incoming messages including controller messages until 
        /// the included MessageStream is completely read till the end. 
        /// </remarks>
        public event MessageReceivedEventhandler MessageReceived;

        /// <summary>
        /// Occurs when a new Client is connected. The argument contains the connected clients endpoint.
        /// </summary>
        public event WebSocketOpenedEventhandler WebSocketOpened;

        /// <summary>
        /// Occurs when a Client is disconnected. The argument contains the disconnected clients endpoint.
        /// </summary>
        public event WebSocketClosedEventhandler WebSocketClosed;

        /// <summary>
        /// The maximum number of clients that can connect to the server.
        /// </summary>
        public int MaxClients => _options.MaxClients;

        /// <summary>
        /// The WebSocketServer timeout which specifies how long to wait for a message.
        /// </summary>
        public TimeSpan ServerTimeout => _options.ServerTimeout;

        /// <summary>
        /// The maximum allowed byte length of messages received by the WebSocket.
        /// </summary>
        public int MaxReceiveFrameSize => _options.MaxReceiveFrameSize;

        /// <summary>
        /// The WebSocketServer protocol keep-alive interval.
        /// </summary>
        public TimeSpan KeepAliveInterval => _options.KeepAliveInterval;

        /// <summary>
        /// The number of Clients connected to the WebSocketServer.
        /// </summary>
        public int ClientsCount { get => _webSocketClientsPool.Count; }

        /// <summary>
        /// Gets the maximum allowed byte length of a partial message send by the WebSocketServer.
        /// By default if a message that exceeds the size limit it will be broken up in smaller partial messages.
        /// </summary>
        public int FragmentSize => _options.MaxFragmentSize;

        /// <summary>
        /// The remote Prefix clients need to connect to.
        /// </summary>
        public string Prefix => _options.Prefix;

        /// <summary>
        /// The local Port to listen on.
        /// </summary>
        public int Port => _options.Port;

        /// <summary>
        /// The server name that is presented to the client during the handshake.
        /// </summary>
        public string ServerName => _options.ServerName;

        /// <summary>
        /// True, server is started. False, means server is not active.
        /// </summary>
        public bool Started { get; private set; } = false;

        /// <summary>
        /// Gets an array of all connected client IPEndPoints.
        /// </summary>
        public string[] ListClients { get => _webSocketClientsPool.List; }

        private readonly WebSocketClientsPool _webSocketClientsPool;
        private Thread _listnerThread;

        private readonly WebSocketServerOptions _options = new WebSocketServerOptions();
        private HttpListener _httpListener;

        /// <summary>
        /// Creates an instance of the System.Net.WebSockets.WebSocketServer class.
        ///</summary>
        ///<param name="options"> Optional <see cref="WebSocketServerOptions"/> where extra options can be defined.</param>
        public WebSocketServer(WebSocketServerOptions options = null)
        {
            if (options != null)
            {
                if (options.Prefix[0] != '/') throw new Exception("websocket prefix has to start with '/'");
                if (options.IsStandAlone)
                {
                    _httpListener = new HttpListener("http", options.Port);
                }
                _options = options;
            }
            _webSocketClientsPool = new WebSocketClientsPool(MaxClients);

        }

        /// <summary>
        /// Add a websocket client to the Websocket.
        /// After this the HttpListnerContext should be discarded!
        ///</summary>
        ///<param name="context"> The HttpListnerContext
        public bool AddWebSocket(HttpListenerContext context)
        {
            //TODO check for limit number of clients. 

            var headerConnection = context.Request.Headers["Connection"];
            var headerUpgrade = context.Request.Headers["Upgrade"];
            var headerSwk = context.Request.Headers["Sec-WebSocket-Key"];
            var headerSecProtocol = context.Request.Headers["Sec-WebSocket-Protocol"];
            WebSocketContext websocketContext = context.GetWebsocketContext();

            if (headerConnection == "Upgrade" && headerUpgrade == "websocket" && !string.IsNullOrEmpty(headerSwk))
            {
                if (_webSocketClientsPool.Count >= _webSocketClientsPool.Max)
                {
                    byte[] serverFullResponse = Encoding.UTF8.GetBytes($"HTTP/1.1 503 WebSocket Server is full\r\n\r\n");
                    websocketContext.NetworkStream.Write(serverFullResponse, 0, serverFullResponse.Length);
                    return false;
                }

                //calculate sec-websocket-key and complete handshake
                string swk = headerSwk;
                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; //default signature for websocket
                byte[] swkaSha1 = WebSocketHelpers.ComputeHash(swka);
                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
                byte[] response = Encoding.UTF8.GetBytes($"HTTP/1.1 101 Web Socket Protocol Handshake\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {swkaSha1Base64}\r\nServer: {ServerName}\r\nUpgrade: websocket{(!string.IsNullOrEmpty(headerSecProtocol)? "\r\nSec-WebSocket-Protocol: " + headerSecProtocol : "")}\r\n\r\n");
                websocketContext.NetworkStream.Write(response, 0, response.Length);

                var webSocketClient = new WebSocketServerClient(_options);
                webSocketClient.ConnectToStream(websocketContext, onMessageReceived);
                if (_webSocketClientsPool.Add(webSocketClient))
                {
                    WebSocketOpened?.Invoke(this, new WebSocketOpenedEventArgs() { EndPoint = webSocketClient.RemoteEndPoint });
                    webSocketClient.ConnectionClosed += OnConnectionClosed;
                    return true;
                }
                else
                {
                    webSocketClient.Dispose();
                    return false;
                }
            }
            websocketContext.NetworkStream.Close();
            websocketContext.Socket.Close();
            return false;

        }

        //
        /// <summary>
        /// Starts the server.
        /// </summary>
        public void Start()
        {
            Started = true;

            if (_httpListener != null)
            {
                _listnerThread = new Thread(ListenIncommingSocketRequest);
                _listnerThread.Start();
                Debug.WriteLine($"websocket server Started at {IPAddress.GetDefaultLocalAddress()}:{Port}{Prefix}");
            }
            else
            {
                Debug.WriteLine("websocket server Started!");
            }

            
            
        }

        /// <summary>
        /// Sends a text message to the specified client
        /// </summary>
        /// <param name="endPoint">The IP endpoint of the connected client.Default -1 will use the maxFragmentSize.</param>
        /// <param name="message">The text message.</param>
        /// <param name="fragmentSize"> Override the maxFragmentSize used.</param>
        /// <remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited.
        /// </remarks>
        public void SendText(string endPoint, string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
            {
                client.SendString(message, fragmentSize);
            }
        }

        /// <summary>
        /// Sends a binary message to the specified client
        /// </summary>
        ///<param name="buffer">The data content of the message.</param>
        ///<param name="endPoint">The IP endpoint of the connected client.</param>
        ///<param name="fragmentSize">Override the maxFragmentSize used.Default -1 will use the maxFragmentSize.</param>
        /// <remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited. 
        /// </remarks>
        public void SendData(string endPoint, byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
            {
                client.SendBytes(buffer, fragmentSize);
            }
        }

        /// <summary>
        ///  Broadcast a binary message to all connected clients
        /// </summary>
        /// <param name="fragmentSize">Override the maxFragmentSize used.Default -1 will use the maxFragmentSize .</param>
        /// <param name="buffer">The data content of the message.</param>
        ///<remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited. 
        /// </remarks>
        public void BroadCast(byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            foreach (string endPoint in _webSocketClientsPool.List)
            {
                SendData(endPoint, buffer, fragmentSize);
            }
        }

        /// <summary>
        /// Broadcast a text message to all connected clients
        ///</summary>
        /// <param name="fragmentSize">Override the maxFragmentSize used.Default -1 will use the maxFragmentSize .</param>
        /// <param name="message">The text message.</param>
        ///<remarks>
        /// Messages are buffered send synchronously using a single send thread.
        /// The send message is not awaited. 
        /// </remarks>
        public void BroadCast(string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var list = _webSocketClientsPool.List;
            foreach (string endPoint in list)
            {
                SendText(endPoint, message, fragmentSize);
            }
        }

        /// <summary>
        /// Will start closing the WebSocket connection to the client using
        /// handshake defined in the WebSocket protocol specification section 7.
        /// After connection is closed the client will be removed from the clientpool
        ///</summary>
        /// <param name="endPoint">The IP endpoint of the connected client.</param>
        /// <param name="closeStatus">Indicates the reason for closing the WebSocket connection.</param>
        /// <param name="abort">Optional, set to true to direct abort client connection in contrary to close connection.</param>
        ///<remarks>
        /// WebSocketCloseStatus.EndpointUnavailable will close the WebSocket synchronous without awaiting response.
        /// </remarks>
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

        /// <summary>
        /// Stops the server and disconnects all connected clients with code WebSocketCloseStatus.EndpointUnavailable. 
        /// </summary>
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

        /// <summary>
        /// Aborts the server and all WebSocket connections and any pending IO operations.
        /// </summary>
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
            _httpListener.Start();

            while (Started)
            {
                HttpListenerContext context = _httpListener.GetContext();
                if (context == null) return;

                new Thread(() =>
                {
                    try
                    {
                        if (context.Request.RawUrl == Prefix)
                        {
                            if (!AddWebSocket(context)) 
                            {

                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                            context.Response.ContentLength64 = 0;
                            context.Response.Close();
                            context.Close();
                        }


                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("* Error getting context: " + ex.Message + "\r\nSack = " + ex.StackTrace);
                    }
                }).Start();
            }
        }


        private void OnConnectionClosed(object sender, EventArgs e)
        {

            var endPoint = ((WebSocket)sender).RemoteEndPoint;
            WebSocketClosed?.Invoke(this, new WebSocketClosedEventArgs() { EndPoint = endPoint });
            var websocket = _webSocketClientsPool.Get(endPoint.ToString());
            _webSocketClientsPool.Remove(endPoint.ToString());

        }

        private void onMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }


        /// <summary>
        /// Releases the unmanaged resources used by the System.Net.WebSockets.WebSocketServer
        /// instance.
        /// </summary>
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

    /// <summary>
    /// EventArgs used for WebSocketOpenedEventHandler
    /// </summary>
    public class WebSocketOpenedEventArgs : EventArgs
    {
        /// <summary>
        /// The Remote Endpoint that is Opened.
        /// </summary>
        public IPEndPoint EndPoint;
    }

    /// <summary>
    /// EventArgs used for WebSocketClosedEventHandler
    /// </summary>
    public class WebSocketClosedEventArgs : EventArgs
    {
        /// <summary>
        /// The Remote Endpoint that is closed.
        /// </summary>
        public IPEndPoint EndPoint;
    }
}
