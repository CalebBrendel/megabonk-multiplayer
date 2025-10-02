// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/UI/MpOverlay.cs
using System;
using UnityEngine;
using static Megabonk.Multiplayer.UI.GuiCompat;

namespace Megabonk.Multiplayer.UI
{
    internal class MpOverlay : MonoBehaviour
    {
        // If you already have state/fields, keep them; this is just illustrative.
        private bool _disableOverlay;

        private void OnGUI()
        {
            if (_disableOverlay) return;

            try
            {
                DrawPanel();
            }
            catch (Exception ex)
            {
                // Prevent repeated log spam and hard-locks if Unity throws every Event.
                _disableOverlay = true;
                Debug.LogError($"[Megabonk.Multiplayer] MpOverlay.OnGUI failed and overlay is disabled: {ex}");
            }
        }

        // Your actual UI layout. Replace any GUIContent.none / GUIStyle.none usage inside.
        private void DrawPanel()
        {
            // Example structure; keep your existing layout, only swap the 'none' usages.
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

            // BEFORE:
            // if (GUILayout.Button(GUIContent.none, someStyle)) { ... }
            // AFTER (use GuiCompat.Empty):
            if (GUILayout.Button(Empty, GUI.skin.button))
            {
                // Handle the "Ready" click or open your ready menu here
                OnReadyClicked();
            }

            // Any other places you used GUIContent.none, swap to GuiCompat.Empty.
            // Any GUIStyle.none usages can be replaced with GuiCompat.StyleNone.

            GUILayout.EndArea();
        }

        private void OnReadyClicked()
        {
            // Whatever your ready/menu logic is.
            Debug.Log("[Megabonk.Multiplayer] Ready clicked.");
        }
    }
}
