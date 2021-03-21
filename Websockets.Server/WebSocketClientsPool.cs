using nanoframework.System.Net.Websockets;
using System;
using System.Collections;

using System.Net;
using System.Text;

namespace nanoframework.System.Net.Websockets.Server
{
    public class WebSocketClientsPool
    {
        private Hashtable _webSocketClients;
        public int Max { get; private set; }
        private object _poolLock = new object();
        public int Count { get => _webSocketClients.Count; }
        public ICollection List => _webSocketClients.Keys;

        public WebSocketClientsPool(int maxClients)
        {
            _webSocketClients = new Hashtable(maxClients);
            Max = maxClients;
        }


        public bool Add(WebSocket webSocket)
        {
            if (Count < Max)
            {
                lock (_poolLock)
                {
                    _webSocketClients.Add(webSocket.RemotEndPoint, webSocket);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public bool Remove(IPEndPoint endPoint)
        {
            
            lock (_poolLock)
            {
                if (!Contains(endPoint)) return false;
                _webSocketClients.Remove(endPoint);
            }

            return true;           
        }

        public bool Contains(IPEndPoint endPoint)
        {
            return _webSocketClients.Contains(endPoint);
        }

        public WebSocket Get(IPEndPoint endPoint)
        {
            return (WebSocket)_webSocketClients[endPoint];
        }


    }
}
