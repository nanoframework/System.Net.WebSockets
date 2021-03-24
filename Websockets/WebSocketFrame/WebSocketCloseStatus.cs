using System;

using System.Text;

namespace nanoframework.System.Net.Websockets.WebSocketFrame
{
    //
    // Summary:
    //     Represents well known WebSocket close codes as defined in section 11.7 of the
    //     WebSocket protocol spec.
    public enum WebSocketCloseStatus
    {
        //
        // Summary:
        //     (1006) connection was closed abnormally - internal application use only
        ClosedAbnormally = 1006,
        //
        // Summary:
        //     (1000) The connection has closed after the request was fulfilled.
        NormalClosure = 1000,
        //
        // Summary:
        //     (1001) Indicates an endpoint is being removed. Either the server or client will
        //     become unavailable.
        EndpointUnavailable = 1001,
        //
        // Summary:
        //     (1002) The client or server is terminating the connection because of a protocol
        //     error.
        ProtocolError = 1002,
        //
        // Summary:
        //     (1003) The client or server is terminating the connection because it cannot accept
        //     the data type it received.
        InvalidMessageType = 1003,
        //
        // Summary:
        //     (1004) Reserved.  The specific meaning might be defined in the future
        //     the data type it received.
        Reserved = 1004,
        //
        // Summary:
        //     No error specified - internal application use only.
        Empty = 1005,
        //
        // Summary:
        //     (1007) The client or server is terminating the connection because it has received
        //     data inconsistent with the message type.
        InvalidPayloadData = 1007,
        //
        // Summary:
        //     (1008) The connection will be closed because an endpoint has received a message
        //     that violates its policy.
        PolicyViolation = 1008,
        //
        // Summary:
        //     (1009) The client or server is terminating the connection because it has received
        //     a message that is too big for it to process.
        MessageTooBig = 1009,
        //
        // Summary:
        //     (1010) The client is terminating the connection because it expected the server
        //     to negotiate an extension.
        MandatoryExtension = 1010,
        //
        // Summary:
        //     (1011) The connection will be closed by the server because of an error on the
        //     server.
        InternalServerError = 1011
    }
}
