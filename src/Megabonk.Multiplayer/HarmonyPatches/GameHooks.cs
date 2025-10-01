using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Finds and caches the local player Transform without using tag lookups.
    /// Uses Camera.main ancestry and root-object name scanning (IL2CPP-safe).
    /// </summary>
    public static class GameHooks
    {
        public static Transform LocalPlayer { get; private set; }

        public static Vector3 LastPos;
        public static Quaternion LastRot;

        private static float _lastBindAttemptTime;

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Rebind on scene change (logs once if found)
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

            // 1) Use the camera rig (common in FPS/3rd-person controllers)
            if (Camera.main != null)
            {
                var t = Camera.main.transform;
                // climb up a few levels to a plausible rig root
                for (int i = 0; i < 3 && t.parent != null; i++) t = t.parent;
                if (Bind(t, "camera-parent", verbose)) return true;
            }

            // 2) Root objects with "player" in name (case-insensitive)
            var scene = SceneManager.GetActiveScene();
            var roots = scene.IsValid() ? scene.GetRootGameObjects() : null;
            if (roots != null)
            {
                foreach (var go in roots)
                {
                    if (go == null) continue;
                    var name = go.name ?? "";
                    var lower = name.ToLowerInvariant();
                    if (lower.Contains("player") || lower.Contains("pawn") || lower.Contains("character"))
                    {
                        if (Bind(go.transform, $"root-name:{name}", verbose)) return true;
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
        /// If not bound, attempts a throttled auto-bind.
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
        /// </summary>
        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            var tag = "RemotePlayer_" + who.m_SteamID;
            var avatar = GameObject.Find(tag);
            if (avatar == null)
            {
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = tag;
                // No collider toggles here to avoid requiring PhysicsModule.
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }
    }
}
