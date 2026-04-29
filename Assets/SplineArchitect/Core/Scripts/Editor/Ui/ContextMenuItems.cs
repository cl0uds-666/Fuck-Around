// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: ContextMenuItems.cs
//
// Author: Mikael Danielsson
// Date Created: 31-01-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEditor;

namespace SplineArchitect.Ui
{
    public static class ContextMenuItems
    {
        [MenuItem("GameObject/Spline Architect/Spline", false, 100)]
        public static void CreateSpline()
        {
            Spline spline = EHandleSpline.CreatedForContext(new GameObject());
            if (Selection.activeTransform != null)
                EHandleUndo.SetTransformParent(spline.transform, Selection.activeTransform);

            Selection.activeTransform = spline.transform;

            EHandleUndo.RecordNow(spline);
            if(EHandleSceneView.GetCurrent().in2DMode) spline.splineType = SplineType.STATIC_2D;
        }

        [MenuItem("GameObject/Spline Architect/Spline Connector", false, 101)]
        public static void CreateSplineConnector()
        {
            SplineConnector splineConnector = EHandleSplineConnector.CreatedForContext(new GameObject());
            if(Selection.activeTransform != null)
                EHandleUndo.SetTransformParent(splineConnector.transform, Selection.activeTransform);

            Selection.activeTransform = splineConnector.transform;
        }
    }
}
