using System;
using System.Net;
using System.Text;
using System.Collections;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using nanoframework.System.Net.Websockets;


namespace nanoframework.System.Net.Websockets.Server
{

    public class WebSocketServer : IDisposable
    {

        public delegate void MessageReceivedEventhandler(object sender, MessageReceivedEventArgs e);
        public delegate void WebSocketOpenedEventhandler(object sender, WebSocketOpenedEventArgs e);
        public delegate void WebSocketClosedEventhandler(object sender, WebSocketClosedEventArgs e);
        public event MessageReceivedEventhandler MesageReceived;
        public event WebSocketOpenedEventhandler WebSocketOpened;
        public event WebSocketClosedEventhandler WebSocketClosed;
        public int MaxClients { get; private set; }
        public int ControllerMessageTimeoutSec { get; private set; }
        public int MessageReadTimeoutMS { get; private set; }
        public int MaxSizeReceivePackage { get; private set; }
        public int HeartBeatSec { get; private set; }
        public int ClientsCount { get => _webSocketClientsPool.Count; }
        public IPEndPoint[] ListClients { get => _webSocketClientsPool.List; }
        public int FragmentSize { get; private set; } = 0;


        public string Prefix { get; private set; }
        public int Port { get; private set; }
        public bool Stopped { get; private set; } = false;
        public string ServerName {get; private set;}

        private WebSocketClientsPool _webSocketClientsPool;
        private Thread _listnerThread;
        private bool _stopping = false;


        public WebSocketServer(int port = 80,  string prefix = "/", string serverName = "NFWebsocketServer", int maxClients = 10, int heartBeatSec = 30, int controllerMessageTimeoutSec = 10,  int fragmentSize = 0, int messageReadTimeoutMs = 100, int maxSizeReceivePackage = int.MaxValue)
        {
            if (prefix[0] != '/') throw new Exception("websocket prefix has to start with '/'");
            MaxClients = maxClients;
            _webSocketClientsPool = new WebSocketClientsPool(maxClients);
            HeartBeatSec = heartBeatSec;
            ControllerMessageTimeoutSec = controllerMessageTimeoutSec;
            MaxSizeReceivePackage = maxSizeReceivePackage;
            HeartBeatSec = heartBeatSec;
            MessageReadTimeoutMS = messageReadTimeoutMs;
            FragmentSize = fragmentSize;
            Prefix = prefix;
            Port = port;
            ServerName = serverName;
            _listnerThread = new Thread(ListenIncommingSocketRequest);
            _listnerThread.Start();
        
        }

        public void SendMessage(IPEndPoint endpoint, string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endpoint);
            if (client != null)
            {
                client.SendString(message, fragmentSize);
            }
        }
        public void SendMessage(IPEndPoint endpoint, byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            var client = _webSocketClientsPool.Get(endpoint);
            if (client != null)
            {
                client.SendBytes(buffer, fragmentSize);
            }
        }

        public void BroadCast(byte[] buffer, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0? FragmentSize : fragmentSize;
            foreach (IPEndPoint endPoint in _webSocketClientsPool.List)
            {
                SendMessage(endPoint, buffer, fragmentSize);
            }
        }

        public void BroadCast(string message, int fragmentSize = -1)
        {
            fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
            foreach (IPEndPoint endPoint in _webSocketClientsPool.List)
            {
                SendMessage(endPoint, message, fragmentSize);
            }
        }

        public void StopClient(IPEndPoint ipEndPoint)
        {
            var client = _webSocketClientsPool.Get(ipEndPoint);
            if (client != null)
            {
                client.Close();
            }
        }

        public void StopServer()
        {
            _stopping = true;
            if (!Stopped)
            {
                
                foreach (IPEndPoint endPoint in _webSocketClientsPool.List)
                {
                    StopClient(endPoint);
                }
            }

            Stopped = true;
        }


        private void ListenIncommingSocketRequest()
        {
            var listenPoint = new IPEndPoint(IPAddress.Any, Port);

            using (Socket mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                mySocket.Bind(listenPoint);
                Debug.WriteLine("websocket server Started!");
                while (!_stopping)
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

                            var webSocketClient = new WebSocket(networkStream, (IPEndPoint)networkSocket.RemoteEndPoint, onMessageReceived, true, prefix, MessageReadTimeoutMS, MaxSizeReceivePackage, ControllerMessageTimeoutSec, HeartBeatSec);
                            if(_webSocketClientsPool.Add(webSocketClient)) { //check if clients are not full again
                                WebSocketOpened?.Invoke(this, new WebSocketOpenedEventArgs() { EndPoint = webSocketClient.RemotEndPoint });
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

            var endPoint = ((WebSocket)sender).RemotEndPoint;
            WebSocketClosed?.Invoke(this, new WebSocketClosedEventArgs() { EndPoint = endPoint});
            _webSocketClientsPool.Remove(endPoint);
        }

        private void onMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            MesageReceived?.Invoke(this, e);
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
            StopServer();
        }
    }
}


//public bool HandleWesocketRequest(HttpListenerContext handshake)
//{
//    if (handshake.Request.Headers["Upgrade"].ToLower() == "websocket" && handshake.Request.Headers["Connection"].ToLower() == "upgrade")
//    {
//        string swk = handshake.Request.Headers["Sec-WebSocket-Key"];
//        if (swk != null) //a valid websocket request
//        {
//            if (_webSocketClientsPool.Count <= MaxClients)
//            {
//                handshake.AcceptWebSocketAsync("test");
//                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; //default signature for websocket
//                byte[] swkaSha1 = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
//                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

//                byte[] response = Encoding.UTF8.GetBytes($"HTTP/1.1 101 Web Socket Protocol Handshake\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {swkaSha1Base64}\r\nServer: Kaazing Gateway\r\nUpgrade: websocket\r\n\r\n");
//                var length = handshake.Response.OutputStream.Length;
//                handshake.Response.StatusCode = 101;
//                handshake.Response.Headers.Clear();
//                handshake.Response.OutputStream.Write(response, 0, response.Length);

//                _webSocketClientsPool.Add(handshake.Request.RemoteEndPoint, new WebSocket(handshake, true, MessageReadTimeoutMS, MaxSizeReceivePackage, ControllerMessageTimeoutSec, HeartBeatSec));
//                Console.WriteLine("connected  socket");
//                return true;

//            }
//            else
//            {

//                throw new NotImplementedException();
//                //error 503 server full?
//            }

//        }
//    }

//    return false;
//}