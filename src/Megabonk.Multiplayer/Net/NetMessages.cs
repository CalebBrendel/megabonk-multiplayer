// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/Net/NetMessages.cs
using System;
using System.Text;

namespace Megabonk.Multiplayer.Net
{
    internal enum MsgId : byte
    {
        ReadyState = 1,
        StartGame  = 2,
    }

    internal static class NetMessages
    {
        // [ReadyState][ulong steamId][byte isReady]
        public static ArraySegment<byte> MakeReadyState(ulong steamId, bool isReady)
        {
            var buf = new byte[1 + 8 + 1];
            buf[0] = (byte)MsgId.ReadyState;
            WriteU64(buf, 1, steamId);
            buf[9] = isReady ? (byte)1 : (byte)0;
            return new ArraySegment<byte>(buf);
        }

        public static (ulong id, bool ready) ReadReadyState(ArraySegment<byte> data)
        {
            var arr = data.Array!;
            int off = data.Offset;
            ulong id = ReadU64(arr, off + 1);
            bool r = arr[off + 9] != 0;
            return (id, r);
        }

        // [StartGame][byte len][utf8 sceneName]
        public static ArraySegment<byte> MakeStartGame(string sceneName)
        {
            var name = Encoding.UTF8.GetBytes(sceneName ?? "");
            if (name.Length > 200) Array.Resize(ref name, 200);
            var buf = new byte[1 + 1 + name.Length];
            buf[0] = (byte)MsgId.StartGame;
            buf[1] = (byte)name.Length;
            Buffer.BlockCopy(name, 0, buf, 2, name.Length);
            return new ArraySegment<byte>(buf);
        }

        public static string ReadStartGame(ArraySegment<byte> data)
        {
            var arr = data.Array!;
            int off = data.Offset;
            int len = arr[off + 1];
            return len <= 0 ? "GeneratedMap" : Encoding.UTF8.GetString(arr, off + 2, len);
        }

        private static void WriteU64(byte[] b, int o, ulong v)
        {
            b[o+0]=(byte)v; b[o+1]=(byte)(v>>8); b[o+2]=(byte)(v>>16); b[o+3]=(byte)(v>>24);
            b[o+4]=(byte)(v>>32); b[o+5]=(byte)(v>>40); b[o+6]=(byte)(v>>48); b[o+7]=(byte)(v>>56);
        }
        private static ulong ReadU64(byte[] b, int o)
        {
            ulong v = 0;
            v |= b[o+0]; v |= ((ulong)b[o+1])<<8; v |= ((ulong)b[o+2])<<16; v |= ((ulong)b[o+3])<<24;
            v |= ((ulong)b[o+4])<<32; v |= ((ulong)b[o+5])<<40; v |= ((ulong)b[o+6])<<48; v |= ((ulong)b[o+7])<<56;
            return v;
        }
    }
}
