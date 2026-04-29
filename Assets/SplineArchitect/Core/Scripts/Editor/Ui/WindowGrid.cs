// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: WindowGrid.cs
//
// Author: Mikael Danielsson
// Date Created: 15-08-2025
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using SplineArchitect.Libraries;
using SplineArchitect.Utility;

namespace SplineArchitect.Ui
{
    public class WindowGrid : WindowBase
    {
        protected override void OnGUIExtended()
        {
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundHeader);
            EUiUtility.CreateLabelField("<b>Grid settings</b>", LibraryGUIStyle.textHeaderBlack, true);

            if (!EGlobalSettings.GetIsWindowsFloating())
            {
                //Close
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconClose, 19, 14, () =>
                {
                    EActionToSceneGUI.Add(() =>
                    {
                        CloseWindow();
                    }, EActionToSceneGUI.Type.LATE, EventType.Repaint);
                    EHandleSceneView.RepaintCurrent();
                });
            }

            GUILayout.EndHorizontal();

            EUiUtility.CreateColorField("Color:", EGlobalSettings.GetGridColor(), (Color newColor) =>
            {
                EGlobalSettings.SetGridColor(newColor);
                EHandleSceneView.RepaintCurrent();
            }, 70);

            EUiUtility.CreateFloatFieldWithLabel("Size:", EGlobalSettings.GetGridSize(), (newValue) =>
            {
                EGlobalSettings.SetGridSize(newValue);
                EHandleSceneView.RepaintCurrent();
            }, 70, 44);

            EUiUtility.CreateToggleField("Occluded:", EGlobalSettings.GetGridOccluded(), (newValue) =>
            {
                EGlobalSettings.SetGridOccluded(newValue);
                EHandleSceneView.RepaintCurrent();
            });

            EUiUtility.CreateToggleField("Distance labels:", EGlobalSettings.GetDrawGridDistanceLabels(), (newValue) =>
            {
                EGlobalSettings.SetDrawGridDistanceLabels(newValue);
                EHandleSceneView.RepaintCurrent();
            });
        }

        protected override void UpdateWindowSize()
        {
            cachedRect.size = new Vector2(125, itemHeight * 4 + 20);
        }
    }
}
