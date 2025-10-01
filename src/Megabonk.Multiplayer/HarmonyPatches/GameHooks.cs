using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

namespace Megabonk.Multiplayer.HarmonyPatches
{
    /// <summary>
    /// Finds and caches the local player Transform without tag lookups
    /// or Scene.GetRootGameObjects (stripped on some IL2CPP builds).
    /// Uses Camera.main ancestry and a root scan built from Resources.FindObjectsOfTypeAll.
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

            // 1) Use the camera rig (common in FPS/3rd-person controllers)
            if (Camera.main != null)
            {
                var t = Camera.main.transform;
                for (int i = 0; i < 3 && t.parent != null; i++) t = t.parent;
                if (Bind(t, "camera-parent", verbose)) return true;
            }

            // 2) Root objects scan (IL2CPP-safe) via Resources.FindObjectsOfTypeAll(typeof(Transform))
            var scn = SceneManager.GetActiveScene();
            var roots = GetSceneRootsSafe(scn);
            if (roots != null && roots.Length > 0)
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    var go = roots[i];
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

        public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
        {
            var tag = "RemotePlayer_" + who.m_SteamID;
            var avatar = GameObject.Find(tag);
            if (avatar == null)
            {
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.name = tag;
                // No collider toggles here to avoid needing PhysicsModule.
            }
            avatar.transform.position = pos;
            avatar.transform.rotation = rot;
        }

        /// <summary>
        /// IL2CPP-safe "get scene roots" using Resources.FindObjectsOfTypeAll instead of Scene.GetRootGameObjects().
        /// Filters out objects not in the active scene and those hidden from hierarchy.
        /// </summary>
        private static GameObject[] GetSceneRootsSafe(Scene scn)
        {
            try
            {
                var objs = Resources.FindObjectsOfTypeAll(typeof(Transform)); // returns UnityEngine.Object[]
                // First pass: count
                int count = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    var t = objs[i] as Transform;
                    if (t == null) continue;
                    var go = t.gameObject;
                    if (t.parent == null &&
                        go.scene.handle == scn.handle &&
                        (go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.HideAndDontSave)) == 0)
                        count++;
                }
                if (count == 0) return System.Array.Empty<GameObject>();

                // Second pass: collect
                var result = new GameObject[count];
                int idx = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    var t = objs[i] as Transform;
                    if (t == null) continue;
                    var go = t.gameObject;
                    if (t.parent == null &&
                        go.scene.handle == scn.handle &&
                        (go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.HideAndDontSave)) == 0)
                        result[idx++] = go;
                }
                return result;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[MP] GetSceneRootsSafe failed: {ex.GetType().Name}: {ex.Message}");
                return System.Array.Empty<GameObject>();
            }
        }
    }
}
