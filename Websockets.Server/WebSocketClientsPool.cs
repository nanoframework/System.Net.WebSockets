using nanoframework.System.Net.Websockets;
using System;
using System.Collections;
using System.Diagnostics;
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
        public string[] List {get => GetList();}


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
                    if(!string.IsNullOrEmpty(webSocket.RemoteEndPoint.ToString()))
                    {
                        _webSocketClients.Add(webSocket.RemoteEndPoint.ToString(), webSocket);
                    }
                    else
                    {
                        Debug.WriteLine("heuh");
                    }
                    
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public bool Remove(string endPoint)
        {
            
            lock (_poolLock)
            {
                if (!Contains(endPoint.ToString())) return false;
                _webSocketClients.Remove(endPoint);
            }

            return true;           
        }

        public bool Contains(string endPoint)
        {
            lock (_poolLock)
            {
                return _webSocketClients.Contains(endPoint.ToString());
            }
        }

        public WebSocket Get(string endPoint)
        {
            lock (_poolLock)
            {
                return (WebSocket)_webSocketClients[endPoint];
            }
        }

        private string[] GetList()
        {
            lock (_poolLock)
            {
                string[] list = new string[_webSocketClients.Count];
                _webSocketClients.Keys.CopyTo(list, 0);
                return list;
            }
        }

    }
}
