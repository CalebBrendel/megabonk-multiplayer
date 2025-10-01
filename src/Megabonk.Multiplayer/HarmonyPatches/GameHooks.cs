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

            // 1) Common tag variants
            found = FindByTag("Player") ?? FindByTag("player") ?? FindByTag("LocalPlayer");
            if (Bind(found, "tag", verbose)) return true;

            // 2) Use the camera rig (common in FPS/3rd-person controllers)
            if (Camera.main != null)
            {
                var t = Camera.main.transform;
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

        private static Transform FindByTag(string tag)
        {
            try
            {
                var go = GameObject.FindWithTag(tag);
                return go ? go.transform : null;
            }
            catch
            {
                // If the tag doesn't exist in this scene, Unity throws — just ignore.
                return null;
            }
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

                // NOTE: We intentionally do NOT touch Collider here to avoid needing PhysicsModule.
                // If you want the avatar to be non-colliding, we can add a PhysicsModule reference
                // and disable the collider later.
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }
    }
}
