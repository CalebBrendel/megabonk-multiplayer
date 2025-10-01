using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Finds and caches the local player Transform in an IL2CPP-safe way.
    /// Avoids heavy per-frame searches; uses tag/name and MainCamera heuristics.
    /// </summary>
    public static class GameHooks
    {
        public static Transform LocalPlayer { get; private set; }

        public static Vector3 LastPos;
        public static Quaternion LastRot;

        private static float _lastBindAttemptTime;

        /// <summary>
        /// Called by mod on scene load; tries to (re)bind the local player.
        /// </summary>
        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryAutoBind(verbose: true);
        }

        /// <summary>
        /// Attempt to discover and cache the local player Transform.
        /// Returns true if bound this call or already bound.
        /// </summary>
        public static bool TryAutoBind(bool verbose = false)
        {
            if (LocalPlayer != null && LocalPlayer) return true;

            // throttle repeated attempts
            if (Time.unscaledTime - _lastBindAttemptTime < 0.75f)
                return false;
            _lastBindAttemptTime = Time.unscaledTime;

            Transform found = null;

            // 1) Common tag variants
            found = FindByTag("Player") ?? FindByTag("player") ?? FindByTag("LocalPlayer");
            if (Bind(found, "tag", verbose)) return true;

            // 2) Use the camera rig (common in FPS/3rd-person controllers)
            if (Camera.main != null)
            {
                var t = Camera.main.transform;
                // climb up a few levels to a plausible rig root
                for (int i = 0; i < 3 && t.parent != null; i++) t = t.parent;
                if (Bind(t, "camera-parent", verbose)) return true;
            }

            // 3) Root objects with "player" in name (case-insensitive)
            var scene = SceneManager.GetActiveScene();
            var roots = scene.IsValid() ? scene.GetRootGameObjects() : null;
            if (roots != null)
            {
                foreach (var go in roots)
                {
                    if (go == null) continue;
                    var name = go.name ?? "";
                    if (name.ToLowerInvariant().Contains("player"))
                    {
                        if (Bind(go.transform, "root-name", verbose)) return true;
                    }
                }
            }

            if (verbose) MelonLogger.Msg("[MP] TryAutoBind: no obvious player found yet.");
            return false;
        }

        private static bool Bind(Transform t, string via, bool verbose)
        {
            if (t == null) return false;
            LocalPlayer = t;
            LastPos = LocalPlayer.position;
            LastRot = LocalPlayer.rotation;
            if (verbose) MelonLogger.Msg($"[MP] Bound local player via {via}: '{LocalPlayer.name}'");
            return true;
        }

        /// <summary>
        /// Read cached local player transform (no heavy lookups at runtime).
        /// If not bound, attempts a throttled auto-bind in the background.
        /// </summary>
        public static bool TryGetLocalPlayerPos(out Quaternion rot)
        {
            // opportunistically try binding
            if (LocalPlayer == null || !LocalPlayer)
                TryAutoBind();

            if (LocalPlayer != null && LocalPlayer)
            {
                LastPos = LocalPlayer.position;
                LastRot = LocalPlayer.rotation;
                rot = LastRot;
                return true;
            }

            rot = Quaternion.identity;
            return false;
        }

        /// <summary>
        /// Spawns/updates a simple remote avatar (capsule) to visualize the other player.
        /// </summary>
        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            var tag = "RemotePlayer_" + who.m_SteamID;
            var avatar = GameObject.Find(tag);
            if (avatar == null)
            {
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = tag;
                var col = avatar.GetComponent<Collider>();
                if (col) col.enabled = false; // purely visual
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }

        // Example Harmony patch stubs (populate with real game types later if needed):
        // [HarmonyPatch(typeof(YourGamePlayerType), "OnSpawned")]
        // [HarmonyPostfix]
        // static void PlayerSpawned_Postfix(YourGamePlayerType __instance) {
        //     LocalPlayer = __instance.transform;
        //     MelonLogger.Msg("[MP] Bound local player from Harmony hook.");
        // }
    }
}
