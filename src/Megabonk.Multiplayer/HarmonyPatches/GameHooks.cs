using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Player binder that avoids stripped APIs. Binds by following Camera.main up to a plausible rig root.
    /// Clears and rebinds on scene changes; F8 can force a rebind. Also renders a simple cyan capsule
    /// for remote players so you can verify movement sync.
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

            // --- Heuristic 1: use the camera rig (works for many games) ---
            if (Camera.main != null)
            {
                // climb up a few levels to a plausible rig root
                var t = Camera.main.transform;
                for (int i = 0; i < 3 && t.parent != null; i++)
                    t = t.parent;

                if (Bind(t, "camera-parent", verbose))
                    return true;

                // Heuristic 2: try siblings with "player"/"character"/"pawn" in the name
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
        /// Spawns/updates a simple remote avatar (cyan capsule) to visualize the other player.
        /// </summary>
        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            var tag = "RemotePlayer_" + who.m_SteamID;
            var avatar = GameObject.Find(tag);
            if (avatar == null)
            {
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = tag;

                // make it easy to spot
                var rend = avatar.GetComponent<Renderer>();
                if (rend != null)
                {
                    var shader = Shader.Find("Standard");
                    if (shader != null) rend.material = new Material(shader);
                    rend.material.color = Color.cyan;
                }
                avatar.transform.localScale = Vector3.one * 1.2f;

                MelonLogger.Msg($"[MP] Spawned remote avatar: {tag}");
            }

            // update transform each tick
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }
    }
}
