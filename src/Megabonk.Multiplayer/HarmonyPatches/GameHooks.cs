using HarmonyLib;
using UnityEngine;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Thin adapter between Megabonk's game objects and our netcode.
    /// For now we avoid IL2CPP paths that trigger ReadOnlySpan<> interop.
    /// We'll wire real game references later via Harmony patches.
    /// </summary>
    public static class GameHooks
    {
        public static Vector3 LastPos;
        public static Quaternion LastRot;

        /// <summary>
        /// Safe, non-throwing placeholder. Returns false until we wire a real player reference.
        /// Avoids GameObject.FindWithTag / Find() which was triggering MissingMethodException
        /// from IL2CPP interop on this build.
        /// </summary>
        public static bool TryGetLocalPlayerPos(out Quaternion rot)
        {
            rot = Quaternion.identity;

            // TODO (real wiring): cache the local player transform during scene/player spawn,
            // then read transform here and return true.
            // For now: return false so the net loops skip sending state.
            return false;
        }

        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            // Simple placeholder remote avatar (kept for later when we re-enable state send)
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

        // Example patch stubs — fill with real game types/methods later.
        //[HarmonyPatch(typeof(GameManager), "OnSceneLoaded")]
        //[HarmonyPostfix]
        public static void OnSceneLoaded_Postfix()
        {
            // When host loads a level, capture seed & broadcast via NetHost.
            if (Megabonk.Multiplayer.MegabonkMultiplayer.IsHost)
            {
                // TODO
            }
        }
    }
}
