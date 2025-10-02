// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using MelonLoader;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using static Megabonk.Multiplayer.UI.GuiCompat;
using Megabonk.Multiplayer.Ready;

namespace Megabonk.Multiplayer.UI
{
    /// <summary>
    /// Il2Cpp-safe IMGUI overlay (no GUILayout; no GUIContent.none).
    /// Exposes Boot() and Toggle() for the main mod to call.
    /// </summary>
    internal class MpOverlay : MonoBehaviour
    {
        private static bool _registered;
        private static GameObject _go;
        private static MpOverlay _instance;

        public static bool IsVisible { get; private set; }

        public static void Boot()
        {
            EnsureRegistered();

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

        private static void EnsureRegistered()
        {
            if (_registered) return;
            ClassInjector.RegisterTypeInIl2Cpp<MpOverlay>();
            _registered = true;
        }

        // ---------- Instance ----------
        private bool _overlayErrored;

        private void Awake() => enabled = IsVisible;

        private void OnGUI()
        {
            if (_overlayErrored || !enabled) return;

            try
            {
                DrawPanel_NoGUILayout();
            }
            catch (Exception ex)
            {
                _overlayErrored = true;
                enabled = false;
                MelonLogger.Error($"[Megabonk.Multiplayer] MpOverlay.OnGUI failed; disabling overlay. {ex}");
            }
        }

        private void DrawPanel_NoGUILayout()
        {
            const float w = 420f;
            const float h = 220f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            var windowRect = new Rect(x, y, w, h);
            GUI.Box(windowRect, string.Empty);

            var titleRect = new Rect(x + 12f, y + 10f, w - 24f, 24f);
            GUI.Label(titleRect, "Megabonk Multiplayer", GUI.skin.label);

            GUI.Box(new Rect(x + 10f, y + 36f, w - 20f, 1f), string.Empty);

            // Ready/Unready button
            string label = ReadyManager.LocalReady ? "Unready" : "Ready";
            var readyRect = new Rect(x + 20f, y + 56f, w - 40f, 32f);
            if (GUI.Button(readyRect, label, GUI.skin.button))
            {
                ReadyManager.SetLocalReady(!ReadyManager.LocalReady);
            }

            // Status line: "Ready X/Y" and host hint
            var statusRect = new Rect(x + 20f, y + 96f, w - 40f, 22f);
            var status = $"Ready {ReadyManager.ReadyCount}/{ReadyManager.PlayerCount}" +
                         (ReadyManager.IsHost ? "  (Host will start automatically)" : "");
            GUI.Label(statusRect, status, GUI.skin.label);

            var closeRect = new Rect(x + w - 100f, y + h - 42f, 80f, 28f);
            if (GUI.Button(closeRect, "Close", GUI.skin.button))
                Toggle(false);
        }
    }
}
