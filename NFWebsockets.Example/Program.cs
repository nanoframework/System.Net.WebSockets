using nanoframework.System.Net.Websockets;
using nanoframework.System.Net.Websockets.Client;
using nanoframework.System.Net.Websockets.Server;
using System;
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

            string ip = CheckIP();
            //making sure your NFdevice has a network connetion
            if (ip != string.Empty)
            {
                //TODO implement extra options for headers, protocl etc


                //Lets create a server that listens on port 80 and excepts maximum of 5 clients
                WebSocketServer webSocketServer = new WebSocketServer();
                webSocketServer.Start();
                //Let's echo all incomming messages from clients to all connected clients including the sender. 
                webSocketServer.MessageReceived += WebSocketServer_MesageReceived;
                Debug.WriteLine($"Websocket server is up and running, connect on: ws://{ip}:{webSocketServer.Port}{webSocketServer.Prefix}");

                //Now let's also attach a local websocket client. Just because we can :-)
                //WebSocketClient client = new WebSocketClient("wss://echo.websocket.org/");
                WebSocketClient client = new WebSocketClient();
                client.Connect("ws://127.0.0.1", Client_MessageReceived);
                while (client.State == nanoframework.System.Net.Websockets.WebSocketFrame.WebSocketState.Open)
                {
                    client.SendString("hello");
                    Thread.Sleep(1000);
                }

                // Browse our samples repository: https://github.com/nanoframework/samples
                // Check our documentation online: https://docs.nanoframework.net/
                // Join our lively Discord community: https://discord.gg/gCyBu8T
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void Client_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Byte[] buffer = new Byte[e.Frame.MessageLength];
            e.Frame.MessageStream.Read(buffer, 0, buffer.Length);
            if (e.Frame.MessageType == nanoframework.System.Net.Websockets.WebSocketFrame.WebSocketMessageType.Text)
            {
                Debug.WriteLine($"received websocket message: {Encoding.UTF8.GetString(buffer, 0, buffer.Length)}");

            }
            else 
            {
                Debug.WriteLine("received websocket data");
            }
        }

        private static void WebSocketServer_MesageReceived(object sender, MessageReceivedEventArgs e)
            {
            
                var webSocketServer = (WebSocketServer)sender;
                Byte[] buffer = new Byte[e.Frame.MessageLength];
                e.Frame.MessageStream.Read(buffer, 0, buffer.Length);
                if (e.Frame.MessageType == nanoframework.System.Net.Websockets.WebSocketFrame.WebSocketMessageType.Text)
                {
                    webSocketServer.BroadCast(Encoding.UTF8.GetString(buffer, 0, buffer.Length));

                }
                else 
                {
                    webSocketServer.BroadCast(buffer);
                }
            }

            private static string CheckIP()
            {
                Debug.WriteLine("Checking for IP");

                NetworkInterface ni = NetworkInterface.GetAllNetworkInterfaces()[0];
                if (ni.IPv4Address != null && ni.IPv4Address.Length > 0)
                {
                    if (ni.IPv4Address[0] != '0')
                    {
                        Debug.WriteLine($"We have and IP: {ni.IPv4Address}");
                        return ni.IPv4Address;

                    }
                }

                return string.Empty;
            }
        }
    }

