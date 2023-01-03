using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Counterpact_Lobby
{
    internal class HTTPServer
    {
        #region Variables.
        private int serverPort = 21065;
        private bool serverLive = false;
        private TcpListener serverTcp;
        public const String serverVersion = "HTTP/1.1";
        public List<GameServer> serverList = new List<GameServer>();
        public X509Certificate serverCert = null;
        #endregion

        // Constructor.
        public HTTPServer()
        {
            serverTcp = new TcpListener(System.Net.IPAddress.Any, serverPort);
        }

        // Launch server.
        public void ServerLaunch()
        {
            Thread serverThread = new Thread(new ThreadStart(ServerRun));
            serverThread.Start();
        }

        // Run server.
        public void ServerRun()
        {
            // Starting.
            serverLive = true;
            serverTcp.Start();
            bool _clientWait = false;

            // Managing clients.
            while(serverLive)
            {
                // Checking registered servers.
                List<GameServer> _tempList = new List<GameServer>();
                _tempList.AddRange(serverList);
                foreach (GameServer i in _tempList)
                {
                    if (i.lobbyClose) ServerRemove(i);
                    else i.GameServerPing(this);
                }

                // Handling new clients.
                if (!_clientWait)
                {
                    Task.Run(async () =>
                    {
                        _clientWait = true;
                        TcpClient serverClient = await serverTcp.AcceptTcpClientAsync();
                        _clientWait = false;
                        bool _clientSuccess = await ServerClientAsync(serverClient);
                        serverClient.Close();
                        serverClient.Dispose();
                    });
                }

                // Resource management.
                Thread.Sleep(100);
            }

            // Shutting down server.
            serverLive = false;
            serverTcp.Stop();
            Console.WriteLine($"[{DateTime.Now}] Server closed.");
        }

        // Client management [ DEPRECATED ]
        public async void ServerClient(TcpClient _inClient)
        {
            // Initializing.
            //StreamReader _inRead = new StreamReader(_inClient.GetStream());
            Console.WriteLine("BEFORE GET");
            SslStream _inSSL = new SslStream(_inClient.GetStream(), false, new RemoteCertificateValidationCallback(ServerValidate), null);
            String _inStr = "";
            //_inClient.ReceiveBufferSize;

            // Reading.
            /*while (_inRead.Peek() != -1)
            {
                _inStr += _inRead.ReadLine() + "\n";
            }*/
            Console.WriteLine("BEFORE READ");
            byte[] _inBuffer = new byte[_inSSL.Length];
            await _inSSL.ReadAsync(_inBuffer);
            _inStr = _inBuffer.ToString();
            Console.WriteLine($"REQUEST: {_inStr}");
            Console.WriteLine("AFTER REQUEST");

            // Handling.
            Request _newRequest = Request.RequestGet(_inStr, ((IPEndPoint)(_inClient.Client.RemoteEndPoint)).Address.ToString());
            Response _newResponse = Response.ResponseInterpret(_newRequest, this);
            _newResponse.ResponseSend(_inClient.GetStream());
        }

        // Validate certificate.
        public static bool ServerValidate(object _inSend, X509Certificate _inCert, X509Chain _inChain, SslPolicyErrors _inSSLError)
        {
            return(true);
        }

        // Client management (asynchronous).
        public async Task<bool> ServerClientAsync(TcpClient _inClient)
        {
            // Initializing.
            int _taskTimeout = 10000;
            DateTime _taskStart = DateTime.Now;
            StreamReader _inRead = null;
            SslStream _inSSL = null;
            byte[] _inBuffer = null;
            String _inStr = "";

            // Waiting for a response.
            //while (_inSSL == null)
            while (_inRead == null)
            {
                // Data found.
                if (_inClient.Available > 0)
                {
                    _inRead = new StreamReader(_inClient.GetStream());
                    /*Console.WriteLine("BEFORE GET");
                    var _inStream = _inClient.GetStream();
                    Console.WriteLine("BEFORE NULL CHECK");
                    if (_inStream != null)
                    {
                        Console.WriteLine("BEFORE STREAM");
                        _inSSL = new SslStream(_inStream, false, new RemoteCertificateValidationCallback(ServerValidate), null);
                        _inBuffer = new byte[_inSSL.Length];
                        Console.WriteLine("AFTER STREAM");
                    }*/
                }

                // Timeout.
                else if (DateTime.Compare(DateTime.Now, _taskStart.AddMilliseconds(_taskTimeout)) > 0)
                {
                    return (false);
                }

                // Awaiting.
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            // Reading.
            while (_inRead.Peek() > 0)
            {
                String _strRead = _inRead.ReadLine();
                _inStr += _strRead + "\n";
            }

            // Reading (SSL).
            /*Console.WriteLine("BEFORE READ");
            await _inSSL.ReadAsync(_inBuffer);
            _inStr = _inBuffer.ToString();
            Console.WriteLine($"READ: {_inStr}");*/

            // Handling.
            Request _newRequest = Request.RequestGet(_inStr, ((IPEndPoint)(_inClient.Client.RemoteEndPoint)).Address.ToString());
            Response _newResponse = Response.ResponseInterpret(_newRequest, this);
            _newResponse.ResponseSend(_inClient.GetStream());

            // Successful.
            return (true);
        }

        // Requesting game server lobby.
        public String ServerGetLobby()
        {
            // Compiling return list.
            List<GameServer> _listReturn = new List<GameServer>();
            foreach(GameServer i in serverList)
            {
                if (i.lobbyValid)
                {
                    _listReturn.Add(i);
                }
            }

            // Preventing escape strings.
            JsonSerializerOptions _tempOpt = new JsonSerializerOptions();
            _tempOpt.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            // Returning.
            return (JsonSerializer.Serialize(_listReturn, _tempOpt));
        }

        // Registering new game server.
        public void ServerPostNew(String _inIp, String _inData)
        {
            #region Checking blacklist.
            if (File.Exists("blacklist.txt"))
            {
                // Reading.
                string _readStr = null;
                try
                {
                    using (StreamReader _inReader = new StreamReader("blacklist.txt"))
                    {
                        _readStr = _inReader.ReadToEnd();
                    }
                }

                // Issue.
                catch (Exception ex)
                {
                    //
                }

                // Reading info.
                if (_readStr != null)
                {
                    // Splitting.
                    string[] _readSplit = _readStr.Split('\r');

                    // Trimming.
                    for(int i = 0; i < _readSplit.Length; i++)
                    {
                        _readSplit[i] = _readSplit[i].Trim();
                    }

                    // Blacklisted.
                    if (_readSplit.Contains(_inIp))
                    {
                        Console.WriteLine($"[{DateTime.Now}] Refused to register server at {_inIp} as this address is blacklisted.\n");
                        return;
                    }
                }
            }
            #endregion

            // Deciphering data.
            GameServer _newServer = null;
            try
            {
                _newServer = GameServer.GameServerCreate(_inIp, _inData);
            }
            catch
            {
                // Just ignore it.
            }

            // Data is valid.
            if (_newServer != null)
            {
                // Initializing.
                int _newPort = _newServer.serverPort;
                GameServer _oldServer = null;

                // Checking existing servers.
                foreach(GameServer i in serverList)
                {
                    if (i.serverIp == _inIp && i.serverPort == _newServer.serverPort)
                    {
                        _oldServer = i;
                    }
                }

                // Server already exists; update it.
                if (_oldServer != null)
                {
                    _oldServer.GameServerUpdate(_inData);
                }

                // This is a new server.
                else serverList.Add(_newServer);
            }
        }

        // Removing game server.
        public void ServerRemove(GameServer _inServer)
        {
            if (serverList.Contains(_inServer))
            {
                if (_inServer.lobbyValid)
                {
                    //Console.WriteLine($"[{DateTime.Now}] Removing server \"{_inServer.serverName}\" at {_inServer.serverIp}:{_inServer.serverPort} (Websocket: {_inServer.serverWSIp}:{_inServer.serverWSPort})");
                    Console.WriteLine($"[{DateTime.Now}] Removing server \"{_inServer.serverName}\" at {_inServer.serverIp}:{_inServer.serverPort}\n");
                }
                else
                {
                    //Console.WriteLine($"[{DateTime.Now}] Failed to register server \"{_inServer.serverName}\" at {_inServer.serverIp}:{_inServer.serverPort} (Websocket: {_inServer.serverWSIp}:{_inServer.serverWSPort})");
                    Console.WriteLine($"[{DateTime.Now}] Failed to register server \"{_inServer.serverName}\" at {_inServer.serverIp}:{_inServer.serverPort}\n");
                }
                serverList.Remove(_inServer);
            }
        }

        // End class.
    }
}