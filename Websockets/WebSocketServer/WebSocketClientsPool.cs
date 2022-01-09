//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections;

namespace System.Net.WebSockets.Server
{
    internal class WebSocketClientsPool
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


        public bool Add(WebSocketServerClient webSocket)
        {
            if (Count < Max)
            {
                lock (_poolLock)
                {
                    _webSocketClients.Add(webSocket.RemoteEndPoint.ToString(), webSocket);
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

            return _webSocketClients.Contains(endPoint.ToString());
            
        }

        public WebSocketServerClient Get(string endPoint)
        {

            return (WebSocketServerClient)_webSocketClients[endPoint];
  
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
