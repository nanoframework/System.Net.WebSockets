using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace nanoframework.System.Net.Websockets.Client
{
    public class ClientWebSocketOptions : WebSocketOptions
    {
        ////
        //// Summary:
        ////     Gets or sets a collection of client side certificates.
        ////
        //// Returns:
        ////     A collection of client side certificates.
        //public X509CertificateCollection ClientCertificates { get; set; }
        ////
        //// Summary:
        ////     Gets or sets the cookies associated with the request.
        ////
        //// Returns:
        ////     The cookies associated with the request.
        //public CookieContainer Cookies { get; set; }
        ////
        //// Summary:
        ////     Gets or sets the credential information for the client.
        ////
        //// Returns:
        ////     The credential information for the client.
        //public ICredentials Credentials { get; set; }
        ////
        //// Summary:
        ////     Creates a HTTP request header and its value.
        ////
        //// Parameters:
        ////   headerName:
        ////     The name of the HTTP header.
        ////
        ////   headerValue:
        ////     The value of the HTTP header.
        //public void SetRequestHeader(string headerName, string headerValue);

        ////
        //// Summary:
        ////     Gets or sets the proxy for WebSocket requests.
        ////
        //// Returns:
        ////     The proxy for WebSocket requests.
        //public IWebProxy Proxy { get; set; }
        //public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; }
        ////
        //// Summary:
        ////     Gets or sets a System.Boolean value that indicates if default credentials should
        ////     be used during WebSocket handshake.
        ////
        //// Returns:
        ////     true if default credentials should be used during WebSocket handshake; otherwise,
        ////     false. The default is true.
        //public bool UseDefaultCredentials { get; set; }

        ////
        //// Summary:
        ////     Adds a sub-protocol to be negotiated during the WebSocket connection handshake.
        ////
        //// Parameters:
        ////   subProtocol:
        ////     The WebSocket sub-protocol to add.
        //public void AddSubProtocol(string subProtocol);
        public SslProtocols SslProtocol { get; set; }
        public bool IsSSL { get; set; } = false;
        public SslVerification SslVerification { get; set; } = SslVerification.NoVerification;
        public  X509Certificate _certificate { get; set; } = null;
        public bool UseCustomCertificate => _certificate != null;
    }
}
