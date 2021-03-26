using nanoframework.System.Net.Websockets;
using nanoframework.System.Net.Websockets.WebSocketFrame;
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
    //
    // Summary:
    //     The WebSocketClient can connect to a WebSocket Server.
    public class WebSocketClient : WebSocket, IDisposable
    {
        //
        // Summary:
        //     If a secure connection is used.
        public bool IsSSL { get; private set; } = false;
        //
        // Summary:
        //     The remote Port to connect to.
        public int Port { get; private set; }
        public SslProtocols SslProtocol { get; private set; } = SslProtocols.Tls12;

        //
        // Summary:
        //     The remote Host name to connect to.
        public string Host { get; private set; }
        public bool UseCustomCertificate => _certificate != null;

        //
        // Summary:
        //     The remote Prefix to connect to.
        public string Prefix { get; private set; }

        //
        // Summary:
        //     The type of SslVerification to use.
        public SslVerification SslVerification { get; private set; } = SslVerification.NoVerification;
        
        private Socket _tcpSocket;
        private X509Certificate _certificate = null;



        //
        // Summary:
        //     Creates an instance of the System.Net.WebSockets.ClientWebSocket class.
        //
        // Parameters:
        //     options:
        //          Optional ClientWebSocketOptions where extra options can be defined.
        public WebSocketClient(ClientWebSocketOptions options = null) : base(options)
        {
            if(options != null)
            {
                SslProtocol = options.SslProtocol;
                 SslVerification = options.SslVerification;
                _certificate = options.Certificate;

            }
        }

        //
        // Summary:
        //     Connect to a WebSocket server.
        //
        // Parameters:
        //   uri:
        //     The URI of the WebSocket server to connect to.
        //
        //   messageReceivedHandler:
        //      A handler that is called when the WebSocket server receives a message
        public void Connect(string uri, MessageReceivedEventHandler messageReceivedHandler)
        {
            State = WebSocketFrame.WebSocketState.Connecting;
            var splitUrl = uri.ToLower().Split(new char[] { ':', '/', '/' }, 4);
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

            WebSocketStream stream = null;
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

                    WebSocketClientConnect(sslStream, ep, messageReceivedHandler, prefix, Host);
                    //stream = new WebSocketStream(sslStream);

                }
                else
                {
                    stream = new WebSocketStream(new NetworkStream(_tcpSocket));
                }
            }
            catch (SocketException ex)
            {
                _tcpSocket.Close();
                State = WebSocketFrame.WebSocketState.Closed;
                Debug.WriteLine($"** Socket exception occurred: {ex.Message} error code {ex.ErrorCode}!**");
            }

            //WebSocketClientConnect(stream, ep, messageReceivedHandler,prefix, Host);
            ConnectionClosed += WebSocket_ConnectionClosed;
        }

        private void WebSocket_ConnectionClosed(object sender, EventArgs e)
        {
            _tcpSocket.Close();
        }

        private void WebSocketClientConnect(SslStream stream, IPEndPoint remoteEndPoint, MessageReceivedEventHandler messageReceivedHandler, string prefix = "/", string host = null )
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
                            byte[] tempbuffer = new byte[] { 0x81, 0x85, 0x37, 0xfa, 0x21, 0x3d, 0x7f, 0x9f, 0x4d, 0x51, 0x58 };
                            stream.Write(tempbuffer, 0, tempbuffer.Length);
                            Thread.Sleep(300);
                            if (stream.DataAvailable)
                            {
                                Debug.WriteLine($"data available - number of bytes = {stream.Read(tempbuffer, 0, tempbuffer.Length)}");
                            }
                            else
                            {
                                Debug.WriteLine($"failed - number of bytes = { stream.Read(tempbuffer, 0, tempbuffer.Length)}");
                            }
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

            ConnectToStream(new WebSocketStream(stream), false, remoteEndPoint, messageReceivedHandler);
            
        }

        //
        // Summary:
        //     Releases the unmanaged resources used by the System.Net.WebSockets.ClientWebSocket
        //     instance.
        public new void Dispose()
        {
            base.Dispose();
            _tcpSocket.Close();
        }

    }
}
