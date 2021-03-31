using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace nanoframework.System.Net.Websockets
{
    public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);

    /// <summary>
    /// The WebSocketClient can connect to a WebSocket Server.
    /// </summary>
    public class WebSocketClient : WebSocket, IDisposable
    {
        internal MessageReceivedEventHandler CallbacksMessageReceivedEventHandler;

        private NetworkStream _networkStream;
        private readonly object _syncLock = new object();
        private X509Certificate _certificate = null;

        /// <summary>
        /// If a secure connection is used.
        /// </summary>
        public bool IsSSL { get; private set; } = false;

        /// <summary>
        /// The remote Port to connect to.
        /// </summary>
        public int Port { get; private set; }

        public SslProtocols SslProtocol { get; private set; } = SslProtocols.Tls12;

        /// <summary>
        /// The remote Host name to connect to.
        /// </summary>
        public string Host { get; private set; }

        public bool UseCustomCertificate => _certificate != null;

        /// <summary>
        /// The remote Prefix to connect to.
        /// </summary>
        public string Prefix { get; private set; }

        /// <summary>
        /// The type of SslVerification to use.
        /// </summary>
        public SslVerification SslVerification { get; set; } = SslVerification.CertificateRequired;

        /// <summary>
        /// Gets or sets the root CA certificate used to authenticate with secure
        /// servers.  This certificate is used only for wss connections; ws connections do not require this.
        /// </summary>
        public X509Certificate CACertificate
        {
            get { return _certificate; }
            set { _certificate = value; }
        }

        /// <summary>
        /// Option to use the certificate stored in the device as client or server certificate. 
        /// The default option is false.
        /// </summary>
        public bool UseStoredDeviceCertificate { get; set; } = false;

        public override WebSocketState State { get; set; } = WebSocketState.Closed;

        private Socket _tcpSocket;

        public event MessageReceivedEventHandler MessageReceived
        {
            add
            {
                lock (_syncLock)
                {
                    MessageReceivedEventHandler callbacksOld = CallbacksMessageReceivedEventHandler;
                    MessageReceivedEventHandler callbacksNew = (MessageReceivedEventHandler)Delegate.Combine(callbacksOld, value);

                    try
                    {
                        CallbacksMessageReceivedEventHandler = callbacksNew;
                    }
                    catch
                    {
                        CallbacksMessageReceivedEventHandler = callbacksOld;

                        throw;
                    }
                }
            }

            remove
            {
                lock (_syncLock)
                {
                    MessageReceivedEventHandler callbacksOld = CallbacksMessageReceivedEventHandler;
                    MessageReceivedEventHandler callbacksNew = (MessageReceivedEventHandler)Delegate.Remove(callbacksOld, value);

                    try
                    {
                        CallbacksMessageReceivedEventHandler = callbacksNew;
                    }
                    catch
                    {
                        CallbacksMessageReceivedEventHandler = callbacksOld;

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Creates an instance of the System.Net.WebSockets.ClientWebSocket class.
        /// </summary>
        /// <param name="options">Optional ClientWebSocketOptions where extra options can be defined.</param>
        public WebSocketClient(ClientWebSocketOptions options = null) : base(options)
        {
            if(options != null)
            {
                SslProtocol = options.SslProtocol;
                 SslVerification = options.SslVerification;
                _certificate = options.Certificate;
            }
        }

        /// <summary>
        /// Connect to a WebSocket server.
        /// </summary>
        /// <param name="uri">The URI of the WebSocket server to connect to.</param>
        /// <param name="messageReceivedHandler">A handler that is called when the WebSocket server receives a message</param>
        public void Connect(string uri)
        {
            State = WebSocketFrame.WebSocketState.Connecting;
           
            var splitUrl = uri.ToLower().Split(new char[] { ':', '/', '/' }, 4);

            if (splitUrl.Length == 4 && splitUrl[0] == "ws")
            {
                IsSSL = false;
            }
            else if (splitUrl.Length == 4 && splitUrl[0] == "wss")
            {
                IsSSL = true;
            }
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

            Prefix = prefix;

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

            NetworkStream stream = null;
            try
            {
                _tcpSocket.Connect(ep);

                if (IsSSL)
                {
                    SslStream sslStream = new SslStream(_tcpSocket);
                    sslStream.SslVerification = SslVerification;
                    sslStream.UseStoredDeviceCertificate = UseStoredDeviceCertificate;

                    if (SslVerification != SslVerification.NoVerification && _certificate != null)
                    {
                        sslStream.AuthenticateAsClient(Host, null, _certificate, SslProtocol);
                    }
                    else
                    {
                        sslStream.AuthenticateAsClient(Host, SslProtocol);
                    }

                    _networkStream = sslStream;
                }
                else
                {
                    _networkStream = new NetworkStream(_tcpSocket, true);
                }

                WebSocketClientConnect(ep, prefix, Host);
            }
            catch (SocketException ex)
            {
                _tcpSocket.Close();
                State = WebSocketFrame.WebSocketState.Closed;
                Debug.WriteLine($"** Socket exception occurred: {ex.Message} error code {ex.ErrorCode}!**");
            }

            ConnectionClosed += WebSocket_ConnectionClosed;
        }

        private void WebSocket_ConnectionClosed(object sender, EventArgs e)
        {
            _tcpSocket.Close();
        }

        private void WebSocketClientConnect(IPEndPoint remoteEndPoint, string prefix = "/", string host = null )
        {
             if (prefix[0] != '/') throw new Exception("websocket prefix has to start with '/'");

            byte[] keyBuf = new byte[16];
            new Random().NextBytes(keyBuf);
            string swk = Convert.ToBase64String(keyBuf);

            byte[] sendBuffer = Encoding.UTF8.GetBytes($"GET {prefix} HTTP/1.1\r\nHost: {(host != null ? host : remoteEndPoint.Address.ToString())}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {swk}\r\nSec-WebSocket-Version: 13\r\n\r\n");
            _networkStream.Write(sendBuffer, 0, sendBuffer.Length);

            string beginHeader = ($"HTTP/1.1 101".ToLower());
            byte[] bufferStart = new byte[beginHeader.Length];
            byte[] buffer = new byte[600];

            int bytesRead = _networkStream.Read(bufferStart, 0, bufferStart.Length);

            bool correctHandshake = false;

            if (bytesRead == bufferStart.Length)
            {
                if (Encoding.UTF8.GetString(bufferStart, 0, bufferStart.Length).ToLower() == beginHeader)
                { 
                    //right http request
                    bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                    
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

            ConnectToStream(_networkStream, false, remoteEndPoint);

            ReceiveAndControllThread receiveThread = new ReceiveAndControllThread(this);
            new Thread(receiveThread.WorkerThread).Start();

            State = WebSocketState.Open;

        }

        /// <inheritdoc/>
        public new void Dispose()
        {
            base.Dispose();
            _tcpSocket.Close();
        }
    }
}
