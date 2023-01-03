using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Counterpact_Lobby
{
    internal class Response
    {
        // Variables.
        private String _responseData = null;
        public HTTPServer _responseServer = null;

        // Constructor.
        private Response(String _inData, HTTPServer _inServer)
        {
            this._responseData = _inData;
            this._responseServer = _inServer;
        }

        // Interpreting the response.
        public static Response ResponseInterpret(Request _inRequest, HTTPServer _inServer)
        {
            // Initializing.
            var _inType = _inRequest.requestType;
            var _inMap = _inRequest.requestMap;
            var _outStr = "NULL";

            #region Attempting to interpret URL.
            if (_inRequest.requestUrl.Length > 1)
            {
                // Initialize.
                string _switchStr = _inRequest.requestUrl.Remove(0, 1);
                if (_switchStr[_switchStr.Length - 1] == '/') _switchStr = _switchStr.Remove(_switchStr.Length - 1, 1);

                // Switch.
                switch (_switchStr)
                {
                    // Lobby.
                    case "lobby":
                        _outStr = _inServer.ServerGetLobby();
                        break;

                    // Invalid request.
                    default:
                        _outStr = "Invalid HTTP request.";
                        break;
                }
            }
            #endregion

            #region Attempting to interpret header data.
            else
            {
                switch (_inType)
                {
                    // GET
                    case "GET":
                        // Requesting lobby data.
                        if (_inMap.ContainsKey("Lobby"))
                        {
                            if (_inMap["Lobby"] == "True")
                            {
                                _outStr = _inServer.ServerGetLobby();
                                //Console.WriteLine("Lobby data was requested.");
                            }
                        }

                        // End.
                        break;

                    // POST
                    case "POST":
                        // Requesting lobby data.
                        if (_inMap.ContainsKey("Register"))
                        {
                            _inServer.ServerPostNew(_inRequest.requestIp, _inMap["Register"]);
                        }

                        // Closing server.
                        else if (_inMap.ContainsKey("Deregister") && _inMap.ContainsKey("Port"))
                        {
                            // Finding the related server.
                            GameServer _serverTarget = null;
                            foreach (GameServer i in _inServer.serverList)
                            {
                                if (i.serverIp == _inRequest.requestIp && i.serverPort.ToString() == _inMap["Port"] && i.serverPasskey == _inMap["Deregister"])
                                {
                                    _serverTarget = i;
                                    break;
                                }
                            }

                            // Removing the server.
                            if (_serverTarget != null)
                            {
                                _inServer.ServerRemove(_serverTarget);
                            }
                        }
                        break;
                }
            }
            #endregion

            // Calculating TRUE size (due to 16 bit characters).
            /*int _tempSize = 0;
            int _tempSizeLast = 0;
            foreach (char i in _outStr)
            {
                int _tempBytes = (i > 255 ? 2 : 1);
                if (_tempSizeLast < _tempBytes) _tempSizeLast = _tempBytes;
                _tempSize += _tempSizeLast;
            }*/

            // Converting string to UTF-8 because Android can't handle HTTP responses with UTF-16 apparently.
            int _tempSize = 0;
            //if (_keyUTF8 != null)
            if (true)
            {
                byte[] _tempBytes = Encoding.Default.GetBytes(_outStr);
                _outStr = Encoding.UTF8.GetString(_tempBytes);
                _tempSize = _outStr.Length;
            }

            // Calculating TRUE size (due to 16 bit characters).
            else _tempSize = Encoding.Unicode.GetByteCount(_outStr);

            // Finalizing.
            string _outHeader = "HTTP/1.1 200 OK\n";
            _outHeader += "Connection: close\n";
            _outHeader += $"Content-Length: {_tempSize}\n";
            _outHeader += "Content-Type: text/html; charset=utf-16\n";

            // Returning.
            return (new Response($"{_outHeader}\n{_outStr}", _inServer));
        }

        // Sending response.
        public void ResponseSend(NetworkStream _inStream)
        {
            // Initializing.
            StreamWriter _outStream = new StreamWriter(_inStream);

            // Compiling HTTP 1.1 headers.
            /*string _outHeader = "HTTP/1.1 200 OK\n";
            _outHeader += "Connection: close\n";
            _outHeader += $"Content-Length: {_responseData.Length}\n";
            _outHeader += "Content-Type: text/html\n";
            string _outFinal = $"{_outHeader}\n{_responseData}";

            // Writing.
            _outFinal = _outFinal.Replace("{", "{{").Replace("}", "}}");
            _outStream.Write(_outFinal, 0, _outFinal.Length);*/

            // Writing.
            _responseData = _responseData.Replace("{", "{{").Replace("}", "}}");
            _outStream.Write(_responseData, 0, _responseData.Length);

            // Sending.
            _outStream.Flush();
        }

        // End class.
    }
}