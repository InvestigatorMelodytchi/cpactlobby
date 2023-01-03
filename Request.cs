using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Counterpact_Lobby
{
    internal class Request
    {
        #region Variables.
        public String requestType { get; set; }
        public String requestUrl { get; set; }
        public String requestIp { get; set; }
        public Dictionary<String, String> requestMap { get; set; }
        #endregion

        // Constructor.
        private Request(String _inType, String _inUrl, String _inIp, Dictionary<String, String> _inMap)
        {
            requestType = _inType;
            requestUrl = _inUrl;
            requestIp = _inIp;
            requestMap = _inMap;
        }

        // Get request.
        public static Request RequestGet(String _inRequest, String _inIp)
        {
            // Null request.
            if (String.IsNullOrEmpty(_inRequest))
            {
                return (null);
            }

            // Valid request.
            else
            {
                // Reading header data.
                String[] _reqTokens = _inRequest.Split(' ');
                String _reqType = _reqTokens[0];
                String _reqUrl = _reqTokens[1];

                // Debugging.
                //Console.WriteLine($"\n[{DateTime.Now}] Received an HTTP request with the following data:\n{_inRequest.Trim()}\n");

                // Reading body data.
                Dictionary<String, String> _reqMap = new Dictionary<String, String>();
                String[] _reqData = _inRequest.Split('\n');
                foreach (String i in _reqData)
                {
                    if (i.Contains(':'))
                    {
                        String _iKey = i.Substring(0, i.IndexOf(':')).Trim();
                        String _iValue = i.Substring(i.IndexOf(':') + 1).Trim();
                        _reqMap.Add(_iKey, _iValue);
                    }
                }

                // Returning.
                return (new Request(_reqType, _reqUrl, _inIp, _reqMap));
            }
        }

        // End class.
    }
}