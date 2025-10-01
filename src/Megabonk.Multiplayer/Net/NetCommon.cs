using System;
using System.IO;
using Steamworks;


namespace Megabonk.Multiplayer.Net
{
public static class NetCommon
{
public const int Channel = 0; // P2P virtual channel
public const int MTU = 1200; // conservative packet size


public static HSteamListenSocket ListenP2P()
{
var opt = new SteamNetworkingConfigValue_t[0];
return SteamNetworkingSockets.CreateListenSocketP2P(Channel, (uint)opt.Length, opt);
}


public static HSteamNetConnection ConnectTo(CSteamID target)
{
SteamNetworkingIdentity id = new SteamNetworkingIdentity();
id.SetSteamID(target);
var opt = new SteamNetworkingConfigValue_t[0];
return SteamNetworkingSockets.ConnectP2P(ref id, Channel, (uint)opt.Length, opt);
}


public static void Send(HSteamNetConnection conn, byte[] data, bool reliable)
{
var flags = reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;
SteamNetworkingSockets.SendMessageToConnection(conn, data, (uint)data.Length, flags, out _);
}


public static int Receive(HSteamNetConnection conn, ref byte[] buffer)
{
IntPtr[] msgs = new IntPtr[8];
int received = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, msgs, msgs.Length);
if (received <= 0) return 0;
int count = 0;
for (int i = 0; i < received; i++)
{
var msg = new SteamNetworkingMessage_t(msgs[i]);
if (buffer == null || buffer.Length < msg.m_cbSize)
buffer = new byte[msg.m_cbSize];
System.Runtime.InteropServices.Marshal.Copy(msg.m_pData, buffer, 0, (int)msg.m_cbSize);
msg.Release();
count = (int)msg.m_cbSize; // return last size (process immediately in callers)
}
return count;
}
}
}
