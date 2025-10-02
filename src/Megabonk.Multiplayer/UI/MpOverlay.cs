// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using MelonLoader;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Megabonk.Multiplayer.Ready;

namespace Megabonk.Multiplayer.UI
{
    /// <summary>Il2Cpp-safe overlay (no GUILayout, no GUIContent.none).</summary>
    internal class MpOverlay : MonoBehaviour
    {
        private static bool _registered;
        private static GameObject _go;
        private static MpOverlay _instance;

        public static bool IsVisible { get; private set; }

        public static void Boot()
        {
            if (!_registered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<MpOverlay>();
                _registered = true;
            }
            if (_instance == null)
            {
                _go = new GameObject("Megabonk.Multiplayer.Overlay");
                _go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_go);
                _instance = _go.AddComponent<MpOverlay>();
                _instance.enabled = IsVisible;
            }
        }

        public static void Toggle() => Toggle(!IsVisible);
        public static void Toggle(bool show)
        {
            Boot();
            IsVisible = show;
            if (_instance != null) _instance.enabled = show;
        }

        private bool _errored;
        private void Awake() => enabled = IsVisible;

        private void OnGUI()
        {
            if (_errored || !enabled) return;
            try { Draw(); }
            catch (Exception ex)
            {
                _errored = true; enabled = false;
                MelonLogger.Error($"[Megabonk.Multiplayer] MpOverlay.OnGUI failed; disabling overlay. {ex}");
            }
        }

        private void Draw()
        {
            const float w = 420f, h = 220f;
            float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), string.Empty);
            GUI.Label(new Rect(x + 12, y + 10, w - 24, 24), "Megabonk Multiplayer", GUI.skin.label);
            GUI.Box(new Rect(x + 10, y + 36, w - 20, 1), string.Empty);

            string label = ReadyManager.LocalReady ? "Unready" : "Ready";
            if (GUI.Button(new Rect(x + 20, y + 56, w - 40, 32), label, GUI.skin.button))
                ReadyManager.SetLocalReady(!ReadyManager.LocalReady);

            var status = $"Ready {ReadyManager.ReadyCount}/{ReadyManager.PlayerCount}" +
                         (ReadyManager.IsHost ? "  (Host will start automatically)" : "");
            GUI.Label(new Rect(x + 20, y + 96, w - 40, 22), status, GUI.skin.label);

            if (GUI.Button(new Rect(x + w - 100, y + h - 42, 80, 28), "Close", GUI.skin.button))
                Toggle(false);
        }
    }
}
