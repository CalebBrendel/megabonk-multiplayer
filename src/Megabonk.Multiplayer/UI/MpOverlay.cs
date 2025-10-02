// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using static Megabonk.Multiplayer.UI.GuiCompat;

namespace Megabonk.Multiplayer.UI
{
    /// <summary>
    /// Multiplayer overlay (IMGUI) with Il2Cpp-safe bootstrap and toggling.
    /// Exposes Boot() and Toggle() so callers in MegabonkMultiplayer.cs compile.
    /// </summary>
    internal class MpOverlay : MonoBehaviour
    {
        private static bool _registered;
        private static GameObject _go;
        private static MpOverlay _instance;

        // Visible state the rest of the mod can query if needed
        public static bool IsVisible { get; private set; }

        /// <summary>
        /// Ensure the Il2Cpp type is registered and the singleton overlay exists.
        /// Safe to call multiple times.
        /// </summary>
        public static void Boot()
        {
            EnsureRegistered();

            if (_instance == null)
            {
                _go = new GameObject("Megabonk.Multiplayer.Overlay");
                _go.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(_go);
                _instance = _go.AddComponent<MpOverlay>();
                _instance.enabled = IsVisible; // match current visibility
            }
        }

        /// <summary>
        /// Toggle visibility (no-arg flips current state).
        /// </summary>
        public static void Toggle()
        {
            Toggle(!IsVisible);
        }

        /// <summary>
        /// Toggle visibility to a specific state.
        /// </summary>
        public static void Toggle(bool show)
        {
            Boot(); // make sure we exist
            IsVisible = show;
            if (_instance != null) _instance.enabled = show;
        }

        private static void EnsureRegistered()
        {
            if (_registered) return;
            // Register MonoBehaviour so AddComponent works under Il2Cpp
            ClassInjector.RegisterTypeInIl2Cpp<MpOverlay>();
            _registered = true;
        }

        // ---------- Instance side ----------

        private bool _overlayErrored; // stops spam if IMGUI throws

        private void Awake()
        {
            // Respect the static visibility when created (Boot may be called before Toggle)
            enabled = IsVisible;
        }

        private void OnGUI()
        {
            if (_overlayErrored || !enabled) return;

            try
            {
                DrawPanel();
            }
            catch (Exception ex)
            {
                _overlayErrored = true;
                enabled = false; // disable to avoid per-frame exceptions
                Debug.LogError($"[Megabonk.Multiplayer] MpOverlay.OnGUI failed; disabling overlay. {ex}");
            }
        }

        // Keep this light; IMGUI runs multiple times per frame for different events.
        private void DrawPanel()
        {
            const float width = 420f;
            const float height = 260f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height
            );

            GUILayout.BeginArea(rect, GUI.skin.window);
            GUILayout.Label("Megabonk Multiplayer", GUI.skin.label);
            GUILayout.Space(6);

            // Use GuiCompat.Empty instead of GUIContent.none (avoids MissingFieldException on some Unity/Il2Cpp combos)
            if (GUILayout.Button(Empty, GUI.skin.button))
            {
                OnReadyClicked();
            }

            if (GUILayout.Button("Close"))
            {
                Toggle(false);
            }

            GUILayout.EndArea();
        }

        private void OnReadyClicked()
        {
            Debug.Log("[Megabonk.Multiplayer] Ready clicked.");
            // TODO: hook your ready/menu action here
        }
    }
}
