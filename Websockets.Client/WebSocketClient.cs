using nanoframework.System.Net.Websockets;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets.Client
{
    public class WebSocketClient : WebSocket, IDisposable
    {
        //public event EventHandler SocketClosed;

        //public event MessageReceivedEventHandler MessageReceived;
        //public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);
        //public bool IsOpen { get => !_webSocket.Closing || !_webSocket.Closed; }
        public bool IsSSL { get; private set; } = false;
        public int Port { get; private set; }
        public SslProtocols SslProtocol { get; private set; } = SslProtocols.Tls12;
        public string Host { get; private set; }
        public SslVerification SslVerification { get; private set; } = SslVerification.NoVerification;
        //private WebSocket _webSocket;
        private Socket _tcpSocket;
        private X509Certificate _certificate = null;
        public bool UseCustomCertificate => _certificate != null;

        //
        // Summary:
        //     The timeout which specifies how long to wait for the handshake to respond
        //     before closing the connection. Default is 15 seconds.
        public static TimeSpan DefaultHandshakeTimeout { get; private set; }

        public WebSocketClient(ClientWebSocketOptions options = null) : base(options)
        {
            if(options != null)
            {
                SslProtocol = options.SslProtocol;
                IsSSL = options.IsSSL;
                SslVerification = options.SslVerification;
                _certificate = options._certificate;

            }
        }

        public void Connect(string url, MessageReceivedEventHandler messageReceivedHandler)
        {
            State = WebSocketFrame.WebSocketState.Connecting;
            var splitUrl = url.ToLower().Split(new char[] { ':', '/', '/' }, 4);
            if (splitUrl.Length == 4 && splitUrl[0] == "ws") IsSSL = false;
            else if (splitUrl.Length == 4 && splitUrl[0] == "wss") IsSSL = true;
            else
            {
                throw new Exception("websocket url should start with 'ws://' or 'wss://'");
            }

            string prefix = "/";

            splitUrl = splitUrl[3].Split(new char[] { '/' }, 2);
            if (splitUrl.Length == 2)
            {
                prefix += splitUrl[1];
            }

            Port = IsSSL ? 443 : 80;

            splitUrl = splitUrl[0].Split(new char[] { ':' }, 2);
            Host = splitUrl[0];
            if (splitUrl.Length == 2)
            {
                if (splitUrl[1].Length < 8)
                {
                    try
                    {
                        Port = int.Parse(splitUrl[1]);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Something is wrong with the port number of the websocket url");
                    }
                }

            }

            IPHostEntry hostEntry = Dns.GetHostEntry(Host);
            IPEndPoint ep = new IPEndPoint(hostEntry.AddressList[0], Port);

            byte[] buffer = new byte[1024];
            _tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Stream stream = null;
            try
            {

                _tcpSocket.Connect(ep);
                int datanum = _tcpSocket.Available;
                if (datanum > 0)
                    _tcpSocket.Receive(buffer);
                if (IsSSL)
                {
                    SslStream sslStream = new SslStream(_tcpSocket);
                    sslStream.SslVerification = SslVerification;
                    if (SslVerification != SslVerification.NoVerification && _certificate != null)
                    {
                        sslStream.AuthenticateAsClient(Host, null, _certificate, SslProtocol);
                    }
                    else
                    {
                        sslStream.AuthenticateAsClient(Host, SslProtocol);
                    }
                    Debug.WriteLine($"{sslStream.Length}  bytes to read");


                    stream = sslStream;

                }
                else
                {
                    stream = new NetworkStream(_tcpSocket);
                }
            }
            catch (SocketException ex)
            {
                _tcpSocket.Close();
                State = WebSocketFrame.WebSocketState.Closed;
                Debug.WriteLine($"** Socket exception occurred: {ex.Message} error code {ex.ErrorCode}!**");
            }

            WebSocketClientConnect(stream, ep, messageReceivedHandler,prefix, Host);
            ConnectionClosed += WebSocket_ConnectionClosed;
        }
        //public WebSocketClient(string url, SslProtocols sslProtocol = SslProtocols.Tls12, SslVerification sslVerification = SslVerification.NoVerification, X509Certificate certificate = null, int fragmentSize = 0)
        //{

        //    FragmentSize = fragmentSize;
        //    IsSSL = false;
        //    var splitUrl = url.ToLower().Split(new char[] { ':', '/', '/' },4);
        //    if (splitUrl.Length == 4 && splitUrl[0] == "ws") IsSSL = false;
        //    else if (splitUrl.Length == 4 && splitUrl[0] == "wss") IsSSL = true;
        //    else
        //    {
        //        throw new Exception("websocket url should start with 'ws://' or 'wss://'");
        //    }

        //    string prefix = "/";

        //    splitUrl = splitUrl[3].Split(new char[] { '/' }, 2);
        //    if(splitUrl.Length == 2) {
        //        prefix += splitUrl[1];
        //    }

        //    Port = IsSSL ? 443 : 80;

        //    splitUrl = splitUrl[0].Split(new char[] { ':' }, 2);
        //    Host = splitUrl[0];
        //    if(splitUrl.Length == 2)
        //    {
        //        if(splitUrl[1].Length < 8)
        //        {
        //            try
        //            {
        //                Port = int.Parse(splitUrl[1]);
        //            }catch(Exception ex)
        //            {
        //                throw new Exception("Something is wrong with the port number of the websocket url");
        //            }
        //        }

        //    }

        //    IPHostEntry hostEntry = Dns.GetHostEntry(Host);
        //    IPEndPoint ep = new IPEndPoint(hostEntry.AddressList[0], Port);

        //    byte[] buffer = new byte[1024];
        //    _tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //    Stream stream = null;
        //    try
        //    {

        //        _tcpSocket.Connect(ep);
        //        int datanum = _tcpSocket.Available;
        //        if(datanum > 0)
        //            _tcpSocket.Receive(buffer);
        //        if (IsSSL)
        //            {
        //                SslStream sslStream = new SslStream(_tcpSocket);
        //                sslStream.SslVerification = sslVerification;
        //                if (sslVerification != SslVerification.NoVerification && certificate != null)
        //                {
        //                    sslStream.AuthenticateAsClient(Host, null, certificate, sslProtocol);
        //                }
        //                else
        //                {
        //                    sslStream.AuthenticateAsClient(Host, sslProtocol);
        //                }
        //                Debug.WriteLine($"{sslStream.Length}  bytes to read");


        //            stream = sslStream;

        //            }
        //            else
        //            {
        //                stream = new NetworkStream(_tcpSocket);
        //            }
        //        }
        //        catch (SocketException ex)
        //        {
        //            Debug.WriteLine($"** Socket exception occurred: {ex.Message} error code {ex.ErrorCode}!**");
        //        }

        //    WebSocketClientConnect(stream, ep, prefix, Host);
        //    _webSocket.ConnectionClosed += WebSocket_ConnectionClosed;



        //}







        //public void SendMessage(string message, int fragmentSize = -1)
        //{
        //    fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
        //    _webSocket.SendString(message, fragmentSize);

        //}
        //public void SendMessage(byte[] buffer, int fragmentSize = -1)
        //{
        //    fragmentSize = fragmentSize < 0 ? FragmentSize : fragmentSize;
        //    _webSocket.SendBytes(buffer, fragmentSize);

        //}

        //public void Close()
        //{
        //    if(!_webSocket.Closing || !_webSocket.Closed)
        //    {
        //        _webSocket.Close(null, true);
        //        if(_tcpSocket != null) _tcpSocket.Close();

        //    }
        //}

        //private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        //{
        //    MessageReceived?.Invoke(this, e);
        //}

        private void WebSocket_ConnectionClosed(object sender, EventArgs e)
        {
            _tcpSocket.Close();
        }

        private void WebSocketClientConnect(Stream stream, IPEndPoint remoteEndPoint, MessageReceivedEventHandler messageReceivedHandler, string prefix = "/", string host = null )
        {
             if (prefix[0] != '/') throw new Exception("websocket prefix has to start with '/'");


            byte[] keyBuf = new byte[16];
            new Random().NextBytes(keyBuf);
            string swk = Convert.ToBase64String(keyBuf);

            byte[] sendBuffer = Encoding.UTF8.GetBytes($"GET {prefix} HTTP/1.1\r\nHost: {(host != null ? host : remoteEndPoint.Address.ToString())}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {swk}\r\nSec-WebSocket-Version: 13\r\n\r\n");
            stream.Write(sendBuffer, 0, sendBuffer.Length);

            string beginHeader = ($"HTTP/1.1 101".ToLower());
            byte[] bufferStart = new byte[beginHeader.Length];
            byte[] buffer = new byte[600];

            int bytesRead = stream.Read(bufferStart, 0, bufferStart.Length);

            bool correctHandshake = false;
            if (bytesRead == bufferStart.Length)
            {
                if (Encoding.UTF8.GetString(bufferStart, 0, bufferStart.Length).ToLower() == beginHeader)
                { //right http request
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 20)
                    {
                        var headers = WebSocketHelpers.ParseHeaders(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                        byte[] swkaSha1 = WebSocketHelpers.ComputeHash(swka);
                        string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                        if (((string)headers["connection"]).ToLower() == "upgrade" && ((string)headers["upgrade"]).ToLower() == "websocket" && (string)headers["sec-websocket-accept"] == swkaSha1Base64)
                        {
                            Debug.WriteLine("Websocket Client connected");
                            correctHandshake = true;
                            
                        }


                    }
                }
            }
            if (!correctHandshake)
            {
                State = WebSocketFrame.WebSocketState.Closed;
                _tcpSocket.Close();
                throw new Exception("Websocket did not receive right handshake");
            }

            ConnectToStream(stream, false, remoteEndPoint, messageReceivedHandler);
            //_webSocket = new WebSocket(stream, remoteEndPoint, OnMessageReceived, false);
            
        }

        public new void Dispose()
        {
            base.Dispose();
            _tcpSocket.Close();
        }

    }
}
