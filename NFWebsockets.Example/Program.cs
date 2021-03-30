using nanoframework.System.Net.Websockets;
using nanoframework.System.Net.Websockets.Server;
using nanoFramework.Networking;
using System;
using System.Collections;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace NFWebsockets.Example
{


    public class Program
    {
        public static void Main()
        {
            //Nanoframework makes it easy to create a websocket server. And to create a new WebSocket Client. Here's a quick example of how easy.
            //use the URL provided in the debug output and connect your own client. Or use your device to connect to your websocket server in the cloud.   

            NetworkHelpers.SetupAndConnectNetwork(true);

            Debug.WriteLine("Waiting for network up and IP address...");
            NetworkHelpers.IpAddressAvailable.WaitOne();

            Debug.WriteLine("Waiting for valid date&time...");
            NetworkHelpers.DateTimeAvailable.WaitOne();

            Debug.WriteLine("All set!");

            //Lets create a new default webserver.
            //WebSocketServer webSocketServer = new WebSocketServer();
            //webSocketServer.Start();
            //Let's echo all incomming messages from clients to all connected clients including the sender. 
            //webSocketServer.MessageReceived += WebSocketServer_MesageReceived;
            //Debug.WriteLine($"Websocket server is up and running, connect on: ws://{ip}:{webSocketServer.Port}{webSocketServer.Prefix}");

            //Now let's also attach a local websocket client. Just because we can :-)
            WebSocketClient client = new WebSocketClient();

            //connect to the local client and write the messages to the debug output
            client.Connect("wss://echo.websocket.org");
            client.MessageReceived += Client_MessageReceived;

            int helloCounter = 0;

            //While the client is connected will send a friendly hello every second.
            while (client.State == nanoframework.System.Net.Websockets.WebSocketFrame.WebSocketState.Open)
            {
                client.SendString($"hello {helloCounter++}");

                Thread.Sleep(100);
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void Client_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (e.Frame.MessageType == nanoframework.System.Net.Websockets.WebSocketFrame.WebSocketMessageType.Text)
            {
                Debug.WriteLine($"websocket message: {Encoding.UTF8.GetString(e.Frame.Buffer, 0, e.Frame.Buffer.Length)}");
            }
            else
            {
                Debug.WriteLine("websocket binary data");
            }
        }

        private static void WebSocketServer_MesageReceived(object sender, MessageReceivedEventArgs e)
        {

            //var webSocketServer = (WebSocketServer)sender;
            //Byte[] buffer = new Byte[e.Frame.MessageLength];
            //e.Frame.MessageStream.Read(buffer, 0, buffer.Length);
            //if (e.Frame.MessageType == nanoframework.System.Net.Websockets.WebSocketFrame.WebSocketMessageType.Text)
            //{
            //    webSocketServer.BroadCast(Encoding.UTF8.GetString(buffer, 0, buffer.Length));

            //}
            //else
            //{
            //    webSocketServer.BroadCast(buffer);
            //}
        }
    }
}
