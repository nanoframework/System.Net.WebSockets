using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace nanoframework.System.Net.Websockets
{
    public class ClientWebSocketOptions : WebSocketOptions
    {
        
        //// Summary:
        ////     Adds a sub-protocol to be negotiated during the WebSocket connection handshake.
        ////
        //// Parameters:
        ////   subProtocol:
        ////     The WebSocket sub-protocol to add.
        //public void AddSubProtocol(string subProtocol);
        public SslProtocols SslProtocol { get; set; }

        //// Summary:
        ////     Gets or sets the form of SSL verification that is to performed 
        ////
        //// Returns:
        ////     the form of SSL verification that is to performed.
        public SslVerification SslVerification { get; set; } = SslVerification.NoVerification;
        
        //// Summary:
        ////     Gets or sets a collection of client side certificate. This certificate will automatically be used when connecting to a wss:// server 
        ////
        //// Returns:
        ////     the client side certificate.
        public X509Certificate Certificate { get; set; } = null;

        //// Summary:
        ////     Gets if a custom certificate is used when connecting to a secure server 
        ////
        //// Returns:
        ////     if a custom certificate is used when connecting to a secure server.
        public bool UseCustomCertificate => Certificate != null;
    }
}
