[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=nanoframework_nanoframework.System.Net.WebSockets&metric=alert_status)](https://sonarcloud.io/dashboard?id=nanoframework_nanoframework.System.Net.WebSockets) [![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=nanoframework_nanoframework.System.Net.WebSockets&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=nanoframework_nanoframework.System.Net.WebSockets) [![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE) [![NuGet](https://img.shields.io/nuget/dt/nanoFramework.System.Net.WebSockets.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.System.Net.WebSockets/) [![#yourfirstpr](https://img.shields.io/badge/first--timers--only-friendly-blue.svg)](https://github.com/nanoframework/Home/blob/main/CONTRIBUTING.md) [![Discord](https://img.shields.io/discord/478725473862549535.svg?logo=discord&logoColor=white&label=Discord&color=7289DA)](https://discord.gg/gCyBu8T)

![nanoFramework logo](https://raw.githubusercontent.com/nanoframework/Home/main/resources/logo/nanoFramework-repo-logo.png)

-----

# Welcome to the .NET **nanoFramework** System.Net.WebSockets Library repository

This API mirrors (as close as possible) the official .NET [System.Net.WebSockets](https://docs.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket). Exceptions are mainly derived from the lack of `async` and generics support in .NET **nanoFramework**.

## Build status

| Component | Build Status | NuGet Package |
|:-|---|---|
| System.Net.WebSockets | [![Build Status](https://dev.azure.com/nanoframework/System.Net.Websockets/_apis/build/status/nanoFramework.System.Net.Websockets?repoName=nanoframework%2FSystem.Net.WebSockets&branchName=main)](https://dev.azure.com/nanoframework/System.Net.Websockets/_build/latest?definitionId=70&repoName=nanoframework%2FSystem.Net.WebSockets&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/nanoFramework.System.Net.WebSockets.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.System.Net.WebSockets/) |
| System.Net.WebSockets.Client | [![Build Status](https://dev.azure.com/nanoframework/System.Net.Websockets/_apis/build/status/nanoFramework.System.Net.Websockets?repoName=nanoframework%2FSystem.Net.WebSockets&branchName=main)](https://dev.azure.com/nanoframework/System.Net.Websockets/_build/latest?definitionId=70&repoName=nanoframework%2FSystem.Net.WebSockets&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/nanoFramework.System.Net.WebSockets.Client.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.System.Net.WebSockets.Client/) |
| System.Net.WebSockets.Server | [![Build Status](https://dev.azure.com/nanoframework/System.Net.Websockets/_apis/build/status/nanoFramework.System.Net.Websockets?repoName=nanoframework%2FSystem.Net.WebSockets&branchName=main)](https://dev.azure.com/nanoframework/System.Net.Websockets/_build/latest?definitionId=70&repoName=nanoframework%2FSystem.Net.WebSockets&branchName=main) | [![NuGet](https://img.shields.io/nuget/v/nanoFramework.System.Net.WebSockets.Server.svg?label=NuGet&style=flat&logo=nuget)](https://www.nuget.org/packages/nanoFramework.System.Net.WebSockets.Server/) |

## Samples

### WebSockets Server Sample 

[Server.RgbSample](https://github.com/nanoframework/Samples/tree/main/samples/WebSockets/WebSockets.Server.RgbSample) shows howto use Websocket Server with a Webserver hosting a WebApp that controlls the rgb led on an Atom Lite ESP32.

### WebSockets Client Sample 

[Client.Sample](https://github.com/nanoframework/Samples/tree/main/samples/WebSockets/Websockets.ServerClient.Sample) shows how to use the Websocket Client.

### WebSockets Server and Client sample 

[ServerClient.Sample](https://github.com/nanoframework/Samples/tree/main/samples/WebSockets/Websockets.ServerClient.Sample) shows how to configure and start a WebSocket Server and (ssl) Client.

## Usage

This is a Websocket Client and Server library for .NET nanoFramework. Websockets are mainly used for creating interactive web apps that require a constant connection with the webserver. In the Internet of Things domain, some protocols require a WebSocket connection, like SignalR. Some IoT servers also support or require protocols like MQTT to run over websockets.  

### Client

#### Connect to a websocket server

To connect to a websocket server, create a `ClientWebsocket`. You can set extra websocket options by adding `ClientWebSocketOptions` upon initialization. These options can be used to set specific SSL options, change keep alive interval, server timeout and set maximum send and receive message size. 
You can start the connection by calling `Connect` with the uri of the websocket server. A websocket location always begins with `ws://` or `wss://`. You can use the optional `ClientWebSocketHeaders` to set specific headers. 

> Note: The ClientWebSocketOptions.MaxFragmentSize sets the max package size of the outgoing messages. When sending a message that exceeds the maximum package size. The message will be automatically chunked into smaller messages. 

```csharp
using System;
using System.Threading;
using System.Net.WebSockets;
using System.Net.WebSockets.WebSocketFrame;
using System.Text;

namespace NFWebsocketTestClient
{
    public class Program
    {
        public static void Main()
        {
            //setup WebSocketClient
            ClientWebSocket websocketClient = new ClientWebSocket(new ClientWebSocketOptions()
            {
                //Change the heart beat to a 30 second interval
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            //Handler for receiving websocket messages
            websocketClient.MessageReceived += WebsocketClient_MessageReceived;
            //Setup custom header
            var headers = new ClientWebSocketHeaders();
            headers["userId"] = "nano";

            //Connect the client to the websocket server with custom headers
            websocketClient.Connect("wss://websocket.nanoFramework.net", headers);

            //Send a message very 5 seconds
            while(websocketClient.State == System.Net.WebSockets.WebSocketFrame.WebSocketState.Open)
            {
                websocketClient.SendString("Hello nanoFramework Websocket!");
                Thread.Sleep(5000);
            }
        }

        private static void WebsocketClient_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var client = (ClientWebSocket)sender;

            //If message is of type Text, echo message back to client
            if(e.Frame.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(e.Frame.Buffer, 0, e.Frame.MessageLength);
                client.SendString(message);
            }
        }
    }
}
```

#### Connection state

The connection state can be monitored by checking the ClientWebSocket `State`. After the connection is established the state is set to `Open`. The client is only able to send messages if  the state is Open. 

#### Receiving messages

Messages can be received by setting an event handler for `MessageReceived`. This handler will be called every time a message is received. The  `MesageReceivedArguments` contains the `MessageReceivedFrame` with a buffer containing the message.  

##### Message frame

Websockets `MessageReceivedFrame` support two types of messages: `Text` and `Binary`. The property `MessageType` tells what type of message is received. `EndPoint` contains the IPEndPoind of the message sender. The `Buffer` contains the actual information that was send. 

> Note: To be able to receive fragmented messages the user needs to implement there own logic. By checking IsFragmented you are able to see if you're dealing with a fragmented message. The property Fragmentation tells if you are dealing with the begin, middle or end fragment of a message. 

#### Send messages

A message can be send by calling `SendString` for a text message or `SendBytes` for sending a binary message using a byte array. You can also call `Send` that takes a byte array and a `MessageType` as arguments. 

#### Closing a connection

The connection can be closed by calling `Close`. Calling this method will send a closing message over the line. You can optional specify a `WebSocketCloseStatus` and description on the reason for closing for debugging purposes. 
Whenever a connection is closed the event `Closed` is fired.  

### Server

The `WebSocketServer` is a websocket host for .NET nanoFramework that can handle multiple websocket connections. The server can be run stand alone or be integrated with the nanoFramework [HttpListner](https://github.com/nanoframework/System.Net.Http/blob/develop/nanoFramework.System.Net.Http/Http/System.Net.HttpListener.cs) or [WebServer](https://github.com/nanoframework/nanoFramework.WebServer). 
The server shares a common websocket base with the Client implementation. 
 
#### Creating a server

To start a new server, create a `WebsocketServer` with optional `WebSocketServerOptions`. By default this will start a selfhosted server on port 80, by setting the `Prefix` and `Port` options you can specify on what port and what prefix this server will listen. The default prefix is `/`. It's recommended to set the `MaxClients` to make sure the server does not run out of resources.

If you want to host a webapp to interact with the websocket server, it's best to integrate the websocket server directly with .NET nanoFramework [HttpListner](https://github.com/nanoframework/System.Net.Http/blob/develop/nanoFramework.System.Net.Http/Http/System.Net.HttpListener.cs) or [WebServer](https://github.com/nanoframework/nanoFramework.WebServer). To do this set the option `IsStandAlone` to `false`. 

To start the websocket server simply call `Start`.

```csharp
WebSocketServer wsServer = new WebSocketServer(new WebSocketServerOptions() { 
                MaxClients = 10,
                IsStandAlone = false
            });

wsServer.MessageReceived += WsServer_MessageReceived;
wsServer.Start();
```

#### Handling client connections

When the websocket server is selfhosted the client connections are handled automatically and added to the websocket server client pool. You can check the number of connected clients with `ClientsCount`. Calling `ListClients` will return an array of all Ip Endpoints of the connected clients. 

When using .NET nanoFramework [HttpListner](https://github.com/nanoframework/System.Net.Http/blob/develop/nanoFramework.System.Net.Http/Http/System.Net.HttpListener.cs) or [WebServer](https://github.com/nanoframework/nanoFramework.WebServer) you can upgrade a websocket request by passing the `HttpListnerContext` to the websocket server by calling `AddWebSocket`. If the connection is successful established `AddWebsocket` will return `true`. 

```csharp
//webserver receive message event handler
private static void WebServer_CommandReceived(object obj, WebServerEventArgs e)
{
    //check the path of the request
    if(e.Context.Request.RawUrl == "/ws")
    {
        //check if this is a websocket request or a page request 
        if(e.Context.Request.Headers["Upgrade"] == "websocket")
        {
            //Upgrade to a websocket
            _wsServer.AddWebSocket(e.Context);
        }
    }
}
```

##### Handling a new connection

When a client is connected the `WebsocketOpened` event is called. The `WebserverEventArgs` contains the endpoint of the client.

##### Handling connection closed

When a client connection is closed the `WebsocketClosed` event is called again containing the endpoint in the `webserverEventArgs`. 

#### Closing a client connection

You can close a specific client connection by calling `DisconnectClient`. You need to specify what client you want to disconnect by providing the client endpoint. Also you need to specify an appropriate `WebSocketCloseStatus`.

#### Receiving messages

When a message from any client is received the `MessageReceived` is raised. Please see the Client section [Receiving Messages](#receiving_messages) and [Message Frame](#message_frame) on how to handle messages. The client who send the message can be identified by checking `Endpoint` property of the `MessageFrame`.  

#### Sending messages

It's possible to send a messages to a specific client by calling `SendString` for a text message or `SendData` for sending a binary message using a byte array. You need to specify the specific client `EndPoint` that you want to send the message to. If you want to send a message to all clients you can simply use `Broadcast` and provide a byte array or a string. 

#### Stopping the server

You can stop the websocket server by calling `Stop`.

## Feedback and documentation

For documentation, providing feedback, issues and finding out how to contribute please refer to the [Home repo](https://github.com/nanoframework/Home).

Join our Discord community [here](https://discord.gg/gCyBu8T).

## Credits

The list of contributors to this project can be found at [CONTRIBUTORS](https://github.com/nanoframework/Home/blob/main/CONTRIBUTORS.md).

## License

The **nanoFramework** Class Libraries are licensed under the [MIT license](LICENSE.md).

## Code of Conduct

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behaviour in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

### .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).
