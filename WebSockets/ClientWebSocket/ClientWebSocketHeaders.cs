using System;
using System.Collections;
using System.Text;

namespace System.Net.WebSockets
{
    /// <summary>
    /// A Dictionary to store custom Http Headers to use with the ClientWebsocket
    /// </summary>
    public class ClientWebSocketHeaders
    {
        private Hashtable _headers = new Hashtable();

        /// <summary>
        /// Gets the number of headers.
        /// </summary>
        public int Count { get { return _headers.Count; } }

        /// <summary>
        /// Gets all header keys.
        /// </summary>
        public string[] Keys { get { return GetKeys(); } }

        /// <summary>
        /// Gets all header values.
        /// </summary>
        public string[] Values { get { return GetValues(); } }

        /// <summary>
        /// Gets a value header or Sets a header value
        /// </summary>
        public string this[string key]
        {
            get
            {
                return _headers[key] as string;
            }
            set
            {

                _headers[key] = value;
                
            }
        }

        /// <summary>
        /// Removes a header from the dictionary
        /// </summary>
        public void Remove(string value)
        {
            _headers.Remove(value);
            
        }

        private string[] GetKeys()
        {
            var keys = _headers.Keys;
            string[] returnkeys = new string[keys.Count];

            int i = 0;
            foreach (object key in keys)
            {
                returnkeys[i] = key as string;
                i++;
            }

            return returnkeys;
        }

        private string[] GetValues()
        {
            var values = _headers.Values;
            string[] returnkeys = new string[values.Count];

            int i = 0;
            foreach (object value in values)
            {
                returnkeys[i] = value as string;
                i++;
            }

            return returnkeys;
        }
    }
}
