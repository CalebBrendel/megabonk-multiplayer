// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using MelonLoader; // <-- use MelonLogger instead of UnityEngine.Debug.* for Il2Cpp safety
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using static Megabonk.Multiplayer.UI.GuiCompat;

namespace Megabonk.Multiplayer.UI
{
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
                DontDestroyOnLoad(_go);
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
                DrawPanel();
            }
            catch (Exception ex)
            {
                _overlayErrored = true;
                enabled = false; // avoid per-frame exceptions
                MelonLogger.Error($"[Megabonk.Multiplayer] MpOverlay.OnGUI failed; disabling overlay. {ex}");
            }
        }

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

            // Use GuiCompat.Empty instead of GUIContent.none
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
            MelonLogger.Msg("[Megabonk.Multiplayer] Ready clicked.");
            // TODO: hook your ready/menu action here
        }
    }
}
