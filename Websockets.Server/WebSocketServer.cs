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

    public class WebSocketServer : IDisposable
    {

        public delegate void MessageReceivedEventhandler(object sender, MessageReceivedEventArgs e);
        public delegate void WebSocketOpenedEventhandler(object sender, WebSocketOpenedEventArgs e);
        public delegate void WebSocketClosedEventhandler(object sender, WebSocketClosedEventArgs e);
        public event MessageReceivedEventhandler MessageReceived;
        public event WebSocketOpenedEventhandler WebSocketOpened;
        public event WebSocketClosedEventhandler WebSocketClosed;
        public int MaxClients => _options.MaxClients;
        public TimeSpan ServerTimeout => _options.ServerTimeout;
        public int MaxReceiveFrameSize => _options.MaxReceiveFrameSize;
        public TimeSpan KeepAliveInterval => _options.KeepAliveInterval;
        public int ClientsCount { get => _webSocketClientsPool.Count; }
        public int FragmentSize => _options.MaxFragmentSize;
        public string Prefix => _options.Prefix;
        public int Port => _options.Port;
        
        public string ServerName => _options.ServerName;
        public bool Started { get; private set; } = false;
        public string[] ListClients { get => _webSocketClientsPool.List; }

        private WebSocketClientsPool _webSocketClientsPool;
        private Thread _listnerThread;

        private WebSocketServerOptions _options = new WebSocketServerOptions();

        public WebSocketServer(WebSocketServerOptions options = null)
        {
            if(options != null)
            {
                if (options.Prefix[0] != '/') throw new Exception("websocket prefix has to start with '/'");
                _options = options;
            }
            _webSocketClientsPool = new WebSocketClientsPool(MaxClients);
        }
        public void Start()
        {
            Started = true;
            _listnerThread = new Thread(ListenIncommingSocketRequest);
            _listnerThread.Start();
            

        }

        public void SendMessage(string endPoint, string message, int fragmentSize = -1)
        {
            if (string.IsNullOrEmpty(endPoint))
            {
                Debug.WriteLine("how?");
            }
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
            {
                client.SendString(message, fragmentSize);
            }
        }
        public void SendMessage(string endPoint, byte[] buffer, int fragmentSize = -1)
        {
            if (string.IsNullOrEmpty(endPoint))
            {
                Debug.WriteLine("how?");
            }
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endPoint);
            if (client != null)
            {
                client.SendBytes(buffer, fragmentSize);
            }
        }

        public void BroadCast(byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0? FragmentSize : fragmentSize;
            foreach (string endPoint in _webSocketClientsPool.List)
            {
                SendMessage(endPoint, buffer, fragmentSize);
            }
        }

        public void BroadCast(string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var list = _webSocketClientsPool.List;
            foreach (string endPoint in list)
            {
                if(!string.IsNullOrEmpty(endPoint)) SendMessage(endPoint, message, fragmentSize);
                else
                {
                    Debug.WriteLine("check it out");
                }
            }
        }

        public void DisconnectClient(string endPoint, WebSocketCloseStatus closeStatus, bool abort = false)
        {
            if (string.IsNullOrEmpty(endPoint))
            {
                Debug.WriteLine("how?");
            }
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
            
            NetworkStream networkStream = new NetworkStream(networkSocket);
            

            string beginHeader = ($"GET {prefix} HTTP/1.1".ToLower());
            byte[] bufferStart = new byte[beginHeader.Length];
            byte[] buffer = new byte[600];

            int bytesRead = networkStream.Read(bufferStart, 0, bufferStart.Length);
            if (bytesRead == bufferStart.Length)
            {
                if (Encoding.UTF8.GetString(bufferStart, 0 , bufferStart.Length).ToLower() == beginHeader)
                { //right http request
                    bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 20)
                    {
                        var headers = WebSocketHelpers.ParseHeaders(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                        if (((string)headers["connection"]).ToLower() == "upgrade" && ((string)headers["upgrade"]).ToLower() == "websocket" && headers["sec-websocket-key"] != null)
                        {
                            if (_webSocketClientsPool.Count >= _webSocketClientsPool.Max) {
                                byte[] serverFullResponse = Encoding.UTF8.GetBytes($"HTTP/1.1 503 Websocket Server is full\r\n\r\n");
                                networkStream.Write(serverFullResponse, 0, serverFullResponse.Length);
                                return false;
                            } 
                            //calculate sec-websocket-key and complete handshake
                            string swk = (string)headers["sec-websocket-key"];
                            string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; //default signature for websocket
                            byte[] swkaSha1 = WebSocketHelpers.ComputeHash(swka);
                            string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
                            byte[] response = Encoding.UTF8.GetBytes($"HTTP/1.1 101 Web Socket Protocol Handshake\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {swkaSha1Base64}\r\nServer: {ServerName}\r\nUpgrade: websocket\r\n\r\n");
                            networkStream.Write(response, 0, response.Length);

                            var webSocketClient = new WebSocketServerClient(_options);
                            webSocketClient.ConnectToStream(networkStream, (IPEndPoint)networkSocket.RemoteEndPoint, onMessageReceived);
                            if(_webSocketClientsPool.Add(webSocketClient)) { //check if clients are not full again
                                WebSocketOpened?.Invoke(this, new WebSocketOpenedEventArgs() { EndPoint = webSocketClient.RemoteEndPoint });
                                webSocketClient.ConnectionClosed += OnConnectionClosed;
                            }
                            else
                            {
                                webSocketClient.Dispose();
                                return false;
                            }
                        }

                    }
                    else
                    {
                        networkStream.Close();
                        return false;
                    }
                }
            }
            else
            {
                networkStream.Close();
                return false;
            }

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

        public class WebSocketOpenedEventArgs : EventArgs
        {
            public IPEndPoint EndPoint;
        }

        public class WebSocketClosedEventArgs : EventArgs
        {
            public IPEndPoint EndPoint;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}


