using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Player binder that avoids stripped APIs. Binds by following Camera.main up to a plausible rig root.
    /// Clears and rebinds on scene changes; F8 can force a rebind.
    /// </summary>
    public static class GameHooks
    {
        public static Transform LocalPlayer { get; private set; }

        public static Vector3 LastPos;
        public static Quaternion LastRot;

        private static float _lastBindAttemptTime;
        private static int _lastSceneHandle = -1;

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // If we switched scenes, clear any stale binding so we rebind to gameplay rig
            if (scene.handle != _lastSceneHandle)
            {
                _lastSceneHandle = scene.handle;
                if (LocalPlayer != null)
                    MelonLogger.Msg($"[MP] Scene changed to '{scene.name}'. Clearing previous player binding ('{LocalPlayer.name}').");
                LocalPlayer = null;
            }

            TryAutoBind(verbose: true);
        }

        /// <summary>
        /// Force a rebind regardless of current state (used by F8).
        /// </summary>
        public static bool ForceRebind(bool verbose = true)
        {
            LocalPlayer = null;
            return TryAutoBind(verbose);
        }

        /// <summary>
        /// Attempt to discover and cache the local player Transform.
        /// Returns true if bound this call or already bound.
        /// </summary>
        public static bool TryAutoBind(bool verbose = false)
        {
            // If we have a player but it belongs to a different (old) scene, drop it.
            if (LocalPlayer != null && LocalPlayer)
            {
                var active = SceneManager.GetActiveScene();
                if (LocalPlayer.gameObject.scene.handle != active.handle)
                {
                    if (verbose) MelonLogger.Msg($"[MP] Discarding stale player binding from scene '{LocalPlayer.gameObject.scene.name}'.");
                    LocalPlayer = null;
                }
            }

            if (LocalPlayer != null && LocalPlayer) return true;

            // throttle repeated attempts a bit to avoid spamming during load
            if (Time.unscaledTime - _lastBindAttemptTime < 0.75f)
                return false;
            _lastBindAttemptTime = Time.unscaledTime;

            // --- Heuristic: use the camera rig (works for menu and gameplay; we rebind on scene change) ---
            Transform candidate = null;
            if (Camera.main != null)
            {
                candidate = Camera.main.transform;
                // climb up a few levels to a plausible rig root
                for (int i = 0; i < 3 && candidate.parent != null; i++)
                    candidate = candidate.parent;

                if (Bind(candidate, "camera-parent", verbose))
                    return true;

                // If the camera has a parent, try siblings with "player"/"character" in the name
                var parent = Camera.main.transform.parent;
                if (parent != null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var s = parent.GetChild(i);
                        var n = (s.name ?? "").ToLowerInvariant();
                        if (n.Contains("player") || n.Contains("character") || n.Contains("pawn"))
                        {
                            if (Bind(s, "camera-sibling", verbose))
                                return true;
                        }
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
                // Collider untouched to avoid PhysicsModule ref.
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }
    }
}
