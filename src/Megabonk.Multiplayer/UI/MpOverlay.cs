// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using MelonLoader;                    // use MelonLogger for Il2Cpp-safe logging
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using static Megabonk.Multiplayer.UI.GuiCompat;

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

        // ---------- Static lifecycle ----------

        /// <summary>Registers Il2Cpp type and ensures singleton GameObject exists.</summary>
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

        /// <summary>Toggle visibility (flip current state).</summary>
        public static void Toggle() => Toggle(!IsVisible);

        /// <summary>Toggle visibility to a specific state.</summary>
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

        // ---------- Instance side ----------

        private bool _overlayErrored;

        private void Awake()
        {
            // Respect static visibility (Boot may have set it before creation)
            enabled = IsVisible;
        }

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
                enabled = false; // stop per-frame spam
                MelonLogger.Error($"[Megabonk.Multiplayer] MpOverlay.OnGUI failed; disabling overlay. {ex}");
            }
        }

        /// <summary>
        /// IMGUI panel without any GUILayout calls to avoid IL2CPP strip issues.
        /// Also avoids GUIContent.none entirely.
        /// </summary>
        private void DrawPanel_NoGUILayout()
        {
            // Window size & position
            const float w = 420f;
            const float h = 220f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Frame
            var windowRect = new Rect(x, y, w, h);
            GUI.Box(windowRect, string.Empty); // simple frame (no GUIContent.none)

            // Title
            var titleRect = new Rect(x + 12f, y + 10f, w - 24f, 24f);
            GUI.Label(titleRect, "Megabonk Multiplayer", GUI.skin.label);

            // Divider
            GUI.Box(new Rect(x + 10f, y + 36f, w - 20f, 1f), string.Empty);

            // Ready button — uses GuiCompat.Empty instead of GUIContent.none
            var readyRect = new Rect(x + 20f, y + 56f, w - 40f, 32f);
            if (GUI.Button(readyRect, Empty, GUI.skin.button))
            {
                OnReadyClicked();
            }

            // Close button
            var closeRect = new Rect(x + w - 100f, y + h - 42f, 80f, 28f);
            if (GUI.Button(closeRect, "Close", GUI.skin.button))
            {
                Toggle(false);
            }
        }

        private void OnReadyClicked()
        {
            MelonLogger.Msg("[Megabonk.Multiplayer] Ready clicked.");
            // TODO: hook your actual ready/menu logic here.
        }
    }
}
