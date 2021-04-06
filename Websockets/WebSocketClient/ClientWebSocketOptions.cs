using nanoframework.System.Net.Websockets.WebSocketFrame;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace nanoframework.System.Net.Websockets
{
    public class ClientWebSocketOptions : WebSocketOptions
    {
        /// <summary>
        /// Gets or sets the TLS/SSL protocol used by the <see cref="WebSocket"/> class.
        /// </summary>
        /// <value>
        /// One of the values defined in the <see cref="SslProtocols"/> enumeration.
        /// </value>
        /// <remarks>
        /// This property is specific to nanoFramework. There is no equivalent in the .NET API.
        /// </remarks>
        public SslProtocols SslProtocol { get; set; }

        /// <summary>
        /// Option for SSL verification.
        /// The default behavior is <see cref="SslVerification.NoVerification"/>.
        /// </summary>
        public SslVerification SslVerification { get; set; } = SslVerification.NoVerification;

        /// <summary>
        /// Gets or sets a collection of client side certificate. This certificate will automatically be used when connecting to a wss:// server 
        /// </summary>
        /// <value>
        /// The client side certificate.
        /// </value>
        public X509Certificate Certificate { get; set; } = null;
    }
}
