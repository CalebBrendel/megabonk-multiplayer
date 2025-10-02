// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using MelonLoader;
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

        // NOTE: This version avoids *all* GUILayout.* calls (BeginArea/Label/Button/Space/etc.)
        private void DrawPanel_NoGUILayout()
        {
            // Window size & position
            const float w = 420f;
            const float h = 220f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Outer window
            var windowRect = new Rect(x, y, w, h);
            GUI.Box(windowRect, GUIContent.none); // simple frame

            // Title
            var titleRect = new Rect(x + 12f, y + 10f, w - 24f, 24f);
            GUI.Label(titleRect, "Megabonk Multiplayer", GUI.skin.label);

            // Divider line (simple 1px box)
            GUI.Box(new Rect(x + 10f, y + 36f, w - 20f, 1f), GUIContent.none);

            // Ready button (empty content – you can replace with text if you like)
            var readyRect = new Rect(x + 20f, y + 56f, w - 40f, 32f);
            if (GUI.Button(readyRect, Empty, GUI.skin.button))
                OnReadyClicked();

            // Close button
            var closeRect = new Rect(x + w - 100f, y + h - 42f, 80f, 28f);
            if (GUI.Button(closeRect, "Close", GUI.skin.button))
                Toggle(false);
        }

        private void OnReadyClicked()
        {
            MelonLogger.Msg("[Megabonk.Multiplayer] Ready clicked.");
            // TODO: your actual "ready" or menu-open logic here
        }
    }
}
