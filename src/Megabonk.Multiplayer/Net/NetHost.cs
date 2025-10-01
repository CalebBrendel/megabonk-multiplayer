using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Steamworks;


namespace Megabonk.Multiplayer.Net
{
public class NetHost
{
public static NetHost Instance { get; private set; }


HSteamListenSocket _listen;
readonly List<HSteamNetConnection> _clients = new List<HSteamNetConnection>();
readonly Dictionary<HSteamNetConnection, CSteamID> _clientIds = new Dictionary<HSteamNetConnection, CSteamID>();
Callback<SteamNetConnectionStatusChangedCallback_t> _onConnChanged;
float _stateTimer;
byte[] _rx;
bool _everyoneReady;


public static void StartListening()
{
Instance?.Shutdown();
Instance = new NetHost();
Instance.Init();
}


void Init()
{
_listen = NetCommon.ListenP2P();
_onConnChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnChanged);
MelonLoader.MelonLogger.Msg("NetHost: listening P2P");
}


public void Shutdown()
{
foreach (var c in _clients)
SteamNetworkingSockets.CloseConnection(c, 0, "host shutdown", false);
_clients.Clear();
_clientIds.Clear();
if (_listen.m_HSteamListenSocket != 0)
SteamNetworkingSockets.CloseListenSocket(_listen);
_listen = default;
_onConnChanged = null;
Instance = null;
}


void OnConnChanged(SteamNetConnectionStatusChangedCallback_t ev)
{
var info = ev.m_info;
switch (info.m_eState)
{
case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
SteamNetworkingSockets.AcceptConnection(ev.m_hConn);
break;
case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
_clients.Add(ev.m_hConn);
_clientIds[ev.m_hConn] = info.m_identityRemote.GetSteamID();
SendHello(ev.m_hConn);
break;
case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
_clients.Remove(ev.m_hConn);
_clientIds.Remove(ev.m_hConn);
SteamNetworkingSockets.CloseConnection(ev.m_hConn, 0, "disconnect", false);
break;
}
}


public void ToggleReady()
{
_everyoneReady = !_everyoneReady;
BroadcastReady(_everyoneReady);
}


void SendHello(HSteamNetConnection c)
{
using (var ms = new MemoryStream())
using (var w = new BinaryWriter(ms))
{
MsgIO.WriteHeader(w, Op.Hello);
}
