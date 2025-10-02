// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/Net/NetMessages.cs
using System;
using System.IO;
using System.Text;

namespace Megabonk.Multiplayer.Net
{
    // One-byte message IDs for tiny P2P packets
    internal enum MsgId : byte
    {
        ReadyState = 1,
        StartGame  = 2,
    }

    internal static class NetMessages
    {
        // Payload layout:
        // [MsgId.ReadyState][ulong steamId][byte isReady]
        public static ArraySegment<byte> MakeReadyState(ulong steamId, bool isReady)
        {
            var buf = new byte[1 + 8 + 1];
            buf[0] = (byte)MsgId.ReadyState;
            WriteUInt64(buf, 1, steamId);
            buf[1 + 8] = isReady ? (byte)1 : (byte)0;
            return new ArraySegment<byte>(buf);
        }

        public static (ulong steamId, bool isReady) ReadReadyState(ArraySegment<byte> data)
        {
            var off = data.Offset;
            var arr = data.Array;
            ulong id = ReadUInt64(arr, off + 1);
            bool r = arr[off + 1 + 8] != 0;
            return (id, r);
        }

        // Payload layout:
        // [MsgId.StartGame][byte len][utf8 sceneName]
        public static ArraySegment<byte> MakeStartGame(string sceneName)
        {
            var nameBytes = Encoding.UTF8.GetBytes(sceneName ?? "");
            if (nameBytes.Length > 200) Array.Resize(ref nameBytes, 200); // cap
            var buf = new byte[1 + 1 + nameBytes.Length];
            buf[0] = (byte)MsgId.StartGame;
            buf[1] = (byte)nameBytes.Length;
            Buffer.BlockCopy(nameBytes, 0, buf, 2, nameBytes.Length);
            return new ArraySegment<byte>(buf);
        }

        public static string ReadStartGame(ArraySegment<byte> data)
        {
            var arr = data.Array;
            int off = data.Offset;
            int len = arr[off + 1];
            if (len <= 0) return "GeneratedMap";
            return Encoding.UTF8.GetString(arr, off + 2, len);
        }

        // --- little helpers (no BinaryReader to avoid allocs) ---
        private static void WriteUInt64(byte[] buf, int offset, ulong v)
        {
            buf[offset + 0] = (byte)(v);
            buf[offset + 1] = (byte)(v >> 8);
            buf[offset + 2] = (byte)(v >> 16);
            buf[offset + 3] = (byte)(v >> 24);
            buf[offset + 4] = (byte)(v >> 32);
            buf[offset + 5] = (byte)(v >> 40);
            buf[offset + 6] = (byte)(v >> 48);
            buf[offset + 7] = (byte)(v >> 56);
        }
        private static ulong ReadUInt64(byte[] buf, int offset)
        {
            ulong v = 0;
            v |= buf[offset + 0];
            v |= ((ulong)buf[offset + 1]) << 8;
            v |= ((ulong)buf[offset + 2]) << 16;
            v |= ((ulong)buf[offset + 3]) << 24;
            v |= ((ulong)buf[offset + 4]) << 32;
            v |= ((ulong)buf[offset + 5]) << 40;
            v |= ((ulong)buf[offset + 6]) << 48;
            v |= ((ulong)buf[offset + 7]) << 56;
            return v;
        }
    }
}
