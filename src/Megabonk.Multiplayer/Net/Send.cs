// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/Net/Send.cs
using System;
using MelonLoader;

namespace Megabonk.Multiplayer.Net
{
    internal static class Send
    {
        // TODO: replace bodies with your actual P2P send implementation

        public static void To(ulong peerId, ArraySegment<byte> payload)
        {
            // Example:
            // SteamNetworking.SendP2P(peerId, payload.Array, payload.Count);
            MelonLogger.Msg($"[MP][Net] → {peerId}  msg={ (MsgId)payload.Array[payload.Offset] } ({payload.Count} bytes)");
        }

        public static void All(ArraySegment<byte> payload)
        {
            // Send to every connected peer including host if your transport expects that.
            MelonLogger.Msg($"[MP][Net] → ALL  msg={ (MsgId)payload.Array[payload.Offset] } ({payload.Count} bytes)");
        }

        public static void AllExcept(ulong exceptPeer, ArraySegment<byte> payload)
        {
            // Iterate your peer list and send to all except 'exceptPeer'
            MelonLogger.Msg($"[MP][Net] → ALL-EXCEPT {exceptPeer}  msg={ (MsgId)payload.Array[payload.Offset] } ({payload.Count} bytes)");
        }
    }
}
