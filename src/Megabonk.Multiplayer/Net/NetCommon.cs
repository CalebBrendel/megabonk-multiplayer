using System;
using System.Runtime.InteropServices;
using Steamworks;

namespace Megabonk.Multiplayer.Net
{
    public static class NetCommon
    {
        public const int Channel = 0; // P2P virtual channel
        public const int MTU = 1200;

        public static HSteamListenSocket ListenP2P()
        {
            var opt = Array.Empty<SteamNetworkingConfigValue_t>();
            // Signature: CreateListenSocketP2P(int nLocalVirtualPort, int nOptions, SteamNetworkingConfigValue_t[] pOptions)
            return SteamNetworkingSockets.CreateListenSocketP2P(Channel, opt.Length, opt);
        }

        public static HSteamNetConnection ConnectTo(Steamworks.CSteamID target)
        {
            SteamNetworkingIdentity id = new SteamNetworkingIdentity();
            id.SetSteamID(target);
            var opt = Array.Empty<SteamNetworkingConfigValue_t>();
            // Signature: ConnectP2P(ref SteamNetworkingIdentity, int nRemoteVirtualPort, int nOptions, SteamNetworkingConfigValue_t[] pOptions)
            return SteamNetworkingSockets.ConnectP2P(ref id, Channel, opt.Length, opt);
        }

        public static void Send(HSteamNetConnection conn, byte[] data, bool reliable)
        {
            int flags = reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;

            // Signature: SendMessageToConnection(HSteamNetConnection, IntPtr, uint cb, int flags, out long outMessageNumber)
            unsafe
            {
                fixed (byte* p = data)
                {
                    SteamNetworkingSockets.SendMessageToConnection(
                        conn,
                        (IntPtr)p,
                        (uint)data.Length,
                        flags,
                        out long _);
                }
            }
        }

        public static int Receive(HSteamNetConnection conn, ref byte[] buffer)
        {
            IntPtr[] ptrs = new IntPtr[8];
            int received = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, ptrs, ptrs.Length);
            if (received <= 0) return 0;

            int size = 0;
            for (int i = 0; i < received; i++)
            {
                // Marshal the struct from the pointer
                var msg = (SteamNetworkingMessage_t)Marshal.PtrToStructure(
                    ptrs[i], typeof(SteamNetworkingMessage_t));

                int cb = (int)msg.m_cbSize;
                if (buffer == null || buffer.Length < cb)
                    buffer = new byte[cb];

                Marshal.Copy(msg.m_pData, buffer, 0, cb);

                // Important: release the message via static Release(ptr)
                SteamNetworkingMessage_t.Release(ptrs[i]);

                size = cb; // return last size processed (caller processes immediately)
            }
            return size;
        }
    }
}
