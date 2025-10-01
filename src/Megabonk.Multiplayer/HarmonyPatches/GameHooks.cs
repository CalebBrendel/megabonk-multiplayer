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

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryAutoBind(verbose: true);
        }

        public static bool TryAutoBind(bool verbose = false)
        {
            if (LocalPlayer != null && LocalPlayer) return true;

            // throttle repeated attempts
            if (Time.unscaledTime - _lastBindAttemptTime < 0.75f)
                return false;
            _lastBindAttemptTime = Time.unscaledTime;

            Transform found = null;

            // 1) Common tag variants (safe helper wraps exceptions)
            found = FindByTag("Player") ?? FindByTag("player") ?? FindByTag("LocalPlayer");
            if (Bind(found, "tag", verbose)) return true;

            // 2) Use the camera rig (common in FPS/3rd-person controllers)
            if (Camera.main != null)
            {
                var t = Camera.main.transform;
                for (int i = 0; i < 3 && t.parent != null; i++) t = t.parent; // climb toward rig root
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

            if (verbose) MelonLoader.MelonLogger.Msg("[MP] TryAutoBind: no obvious player found yet.");
            return false;
        }

        private static Transform FindByTag(string tag)
        {
            try
            {
                var go = GameObject.FindWithTag(tag);
                return go != null ? go.transform : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool Bind(Transform t, string via, bool verbose)
        {
            if (t == null) return false;
            LocalPlayer = t;
            LastPos = LocalPlayer.position;
            LastRot = LocalPlayer.rotation;
            if (verbose) MelonLoader.MelonLogger.Msg($"[MP] Bound local player via {via}: '{LocalPlayer.name}'");
            return true;
        }

        /// <summary>
        /// Read cached local player transform (no heavy lookups at runtime).
        /// If not bound, attempts a throttled auto-bind in the background.
        /// </summary>
        public static bool TryGetLocalPlayerPos(out Quaternion rot)
        {
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
        /// (No Collider references to avoid needing PhysicsModule in the project.)
        /// </summary>
        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            var tag = "RemotePlayer_" + who.m_SteamID;
            var avatar = GameObject.Find(tag);
            if (avatar == null)
            {
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = tag;
                // If you want to remove colliders later, we can add PhysicsModule or do reflection.
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }
    }
}
