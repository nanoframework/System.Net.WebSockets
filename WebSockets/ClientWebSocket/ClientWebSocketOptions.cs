﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Net.WebSockets.WebSocketFrame;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace System.Net.WebSockets
{
    /// <summary>
    /// Options to use with a <see cref="ClientWebSocket"/> object.
    /// </summary>
    public class ClientWebSocketOptions : WebSocketOptions
    {
        /// <summary>
        /// Gets or sets the TLS/SSL protocol used by the <see cref="WebSocket"/> class.
        /// </summary>
        /// <value>
        /// One of the values defined in the <see cref="SslProtocols"/> enumeration. Default is <see cref="SslProtocols.Tls12"/>.
        /// </value>
        /// <remarks>
        /// This property is specific to .NET nanoFramework. There is no equivalent in the .NET API.
        /// </remarks>
        public SslProtocols SslProtocol { get; set; } = SslProtocols.Tls12;

        /// <summary>
        /// Option for SSL verification.
        /// The default behavior is <see cref="SslVerification.CertificateRequired"/>.
        /// </summary>
        public SslVerification SslVerification { get; set; } = SslVerification.CertificateRequired;

        /// <summary>
        /// Gets or sets a collection of client side certificate. This certificate will automatically be used when connecting to a wss:// server 
        /// </summary>
        /// <value>
        /// The client side certificate.
        /// </value>
        public X509Certificate Certificate { get; set; } = null;
    }
}
