using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Websocket.Client;

namespace Counterpact_Lobby
{
    internal class GameServer
    {
        #region Variables.
        // Server info.
        public String? serverName { get; set; }
        public int serverPlayers { get; set; }
        public int serverMax { get; set; }
        public int serverMods { get; set; }
        public String? serverIp { get; set; }
        public String? serverWSIp { get; set; }
        public int serverPort { get; set; }
        public int serverWSPort { get; set; }
        public String? serverMap { get; set; }
        public String? serverVersion { get; set; }
        public String? serverPasskey { get; set; }

        // Lobby info.
        public bool lobbyValid = false;
        public bool lobbyPinging = false;
        public DateTime lobbyPing; // Time to ping.
        public int lobbyWait = 15;
        public bool lobbyClose = false;
        public bool lobbyRaw = true;
        #endregion

        // Register server.
        public static GameServer GameServerCreate(String _inIp, String _inData)
        {
            // Parsing data.
            try
            {
                GameServer _newServer = JsonSerializer.Deserialize<GameServer>(_inData);
                if (_newServer != null)
                {
                    if (_newServer.serverIp == "") _newServer.serverIp = _inIp;
                    if (_newServer.serverWSIp == "") _newServer.serverWSIp = _inIp;
                    _newServer.lobbyPing = DateTime.Now;
                    _newServer.serverPasskey = GameServer.GameServerDecipher(_newServer.serverPasskey);
                    return (_newServer);
                }
            }

            // Failed.
            catch
            {
                return (null);
            }

            // It won't stop bitching about this so whatever.
            return (null);
        }

        // Decipher passkey.
        public static String GameServerDecipher(String _inKey)
        {
            // Initializing.
            String _keyOrig = _inKey;
            String _keyOut = "";
            int _keyLen = _keyOrig.Length;
            int _keyMid = (int)Math.Ceiling((decimal)_keyLen / 2);

            // Deciphering.
            _keyOut += char.ConvertFromUtf32(65 + _keyLen);
            _keyOut += _keyOrig[_keyLen - 1];
            _keyOut += char.ConvertFromUtf32(122 - _keyLen);
            _keyOut += _keyOrig[_keyMid - 1];
            _keyOut += char.ConvertFromUtf32(97 + _keyLen);
            _keyOut += _keyOrig[0];
            _keyOut += $"{_keyMid - 1}";

            // Finalizing.
            //this.serverPasskey = _keyOut;
            return (_keyOut);
        }
        
        // Ping server.
        public void GameServerPing(HTTPServer _inServer)
        {
            if (!lobbyPinging && (DateTime.Compare(DateTime.Now, lobbyPing) > 0 || !lobbyValid))
            {
                // Initializing.
                lobbyPinging = true;
                //String _httpUrl = $"ws://{serverWSIp}:{serverWSPort}";
                //String _httpUrl = $"http://{serverIp}:{serverPort}/lobby?={serverPasskey}";
                //var _httpExit = new ManualResetEvent(false);
                //Console.WriteLine($"[{DateTime.Now}] ATTEMPT PING {serverIp}:{serverPort}");

                // Starting task.
                Task.Run(async () =>
                {
                    // Keeping track of time as to not deregister a rehosted server.
                    DateTime _pingTime = DateTime.Now;

                    // Initializing this here so we can properly close it.
                    Socket _tcpSocket = null;

                    // Try.
                    try
                    {
                        // Initializing.
                        byte[] _tcpBuffer = new byte[1024];
                        //IPHostEntry _tcpEntry = Dns.GetHostEntry(serverIp);
                        //IPAddress _tcpIp = _tcpEntry.AddressList[0];
                        IPAddress _tcpIp = IPAddress.Parse(serverIp);
                        IPEndPoint _tcpTarget = new IPEndPoint(_tcpIp, serverPort);
                        _tcpSocket = new Socket(_tcpIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        int _tcpTimeout = 10;

                        // Configuring socket.
                        _tcpSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                        _tcpSocket.SendTimeout = 15000;
                        _tcpSocket.ReceiveTimeout = 15000;

                        // Connecting.
                        await _tcpSocket.ConnectAsync(_tcpTarget).WaitAsync(TimeSpan.FromSeconds(_tcpTimeout));

                        // Sending request.
                        byte[] _tcpMessage = Encoding.ASCII.GetBytes($"GET /lobby?={serverPasskey}");
                        await _tcpSocket.SendAsync(_tcpMessage, SocketFlags.None);

                        // Getting data.
                        /*Console.WriteLine("BEFORE READ");
                        int _tcpTimeout = 10000;
                        int _tcpResponse = await _tcpSocket.ReceiveAsync(_tcpBuffer, SocketFlags.None);
                        String _tcpString = Encoding.ASCII.GetString(_tcpBuffer, 0, _tcpResponse);
                        Console.WriteLine("AFTER READ");*/

                        // Getting data.
                        int _tcpResponse = await _tcpSocket.ReceiveAsync(_tcpBuffer, SocketFlags.None).WaitAsync(TimeSpan.FromSeconds(_tcpTimeout));
                        String _tcpString = Encoding.ASCII.GetString(_tcpBuffer, 0, _tcpResponse);

                        // This isn't a raw packet; just mark it as okay and rely on data from the server.
                        if (_tcpString.Trim().StartsWith("GM:Studio") || !lobbyRaw)
                        {
                            // Validating.
                            if (!lobbyValid)
                            {
                                lobbyValid = true;
                                Console.WriteLine($"[{DateTime.Now}] Registered new server \"{serverName}\" at {serverIp}:{serverPort}\n");
                            }

                            // Set.
                            lobbyRaw = false;
                            lobbyPing = DateTime.Now.AddSeconds(lobbyWait);
                            lobbyPinging = false;
                        }

                        // A raw packet; handle it the old way.
                        else
                        {
                            // But do read the string properly since we added the checksum.
                            /*try
                            {
                                _tcpResponse = await _tcpSocket.ReceiveAsync(_tcpBuffer, SocketFlags.None);
                            }
                            catch (Exception ex)
                            {
                                // Don't do anything.
                            }*/
                            _tcpResponse = await _tcpSocket.ReceiveAsync(_tcpBuffer, SocketFlags.None);

                            // Checking for valid data.
                            while ((byte)_tcpString[_tcpString.Length - 1] == 0)
                            {
                                _tcpString = _tcpString.Remove(_tcpString.Length - 1);
                            }
                            GameServer _tcpCheck = JsonSerializer.Deserialize<GameServer>(_tcpString);

                            // Process the update.
                            if (_tcpCheck != null)
                            {
                                // Validating.
                                if (!lobbyValid)
                                {
                                    lobbyValid = true;
                                    Console.WriteLine($"[{DateTime.Now}] Registered new server \"{serverName}\" at {serverIp}:{serverPort}\n");
                                }

                                // Updating server.
                                GameServerUpdate(_tcpString);

                                // Resetting ping.
                                lobbyPing = DateTime.Now.AddSeconds(lobbyWait);
                                lobbyPinging = false;
                            }
                        }

                        // Close the connection.
                        /*await _tcpSocket.DisconnectAsync(false);
                        _tcpSocket.Close();
                        _tcpSocket.Dispose();*/

                        // End try.
                    }

                    // Catch.
                    catch (Exception _e)
                    {
                        if (DateTime.Compare(_pingTime, lobbyPing) > 0)
                        {
                            Console.WriteLine($"[{DateTime.Now}] Failed to ping \"{serverName}\" at {serverIp}:{serverPort} with reason:");
                            Console.WriteLine($"\t{_e.Message}");
                            _inServer.ServerRemove(this);
                        }
                    }

                    // Closing socket.
                    if (_tcpSocket != null)
                    {
                        try
                        {
                            await _tcpSocket.DisconnectAsync(false);
                            _tcpSocket.Close();
                            _tcpSocket.Dispose();
                        }
                        catch { }
                    }

                    // End task.
                });
            }

            // End method.
        }

        // Update server.
        public void GameServerUpdate(String _inData)
        {
            // Removing trailing data (because GMS2 adds string terminators that are fucking with the JSON data).
            while((byte)_inData[_inData.Length - 1] == 0)
            {
                _inData = _inData.Remove(_inData.Length - 1);
            }

            // Deciphering data.
            try
            {
                // Deciphering.
                GameServer _inUpdate = JsonSerializer.Deserialize<GameServer>(_inData);

                // Successful update.
                if (_inUpdate != null)
                {
                    serverName = _inUpdate.serverName;
                    serverPlayers = _inUpdate.serverPlayers;
                    serverMax = _inUpdate.serverMax;
                    serverMods = _inUpdate.serverMods;
                    serverMap = _inUpdate.serverMap;
                }
            }

            // Failed for some reason.
            catch
            {
                lobbyClose = true;
            }
        }

        // End class.
    }
}