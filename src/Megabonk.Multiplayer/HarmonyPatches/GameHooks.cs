using HarmonyLib;
using UnityEngine;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Safe stubs for now — no IL2CPP-unsafe FindWithTag calls.
    /// Wire a cached Transform later via Harmony when we know Megabonk classes.
    /// </summary>
    public static class GameHooks
    {
        public static Vector3 LastPos;
        public static Quaternion LastRot;

        public static bool TryGetLocalPlayerPos(out Quaternion rot)
        {
            rot = Quaternion.identity;
            // TODO: cache player Transform when it spawns, then read it here.
            // Returning false prevents send loops from firing until we're ready.
            return false;
        }

        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            var tag = "RemotePlayer_" + who.m_SteamID;
            var avatar = GameObject.Find(tag);
            if (avatar == null)
            {
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = tag;
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }
    }
}
