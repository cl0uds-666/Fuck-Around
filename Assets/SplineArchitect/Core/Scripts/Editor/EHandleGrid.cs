// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleGrid.cs
//
// Author: Mikael Danielsson
// Date Created: 28-09-2024
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

using SplineArchitect.Utility;
using SplineArchitect.Libraries;

namespace SplineArchitect
{
    public class EHandleGrid
    {
        public static Bounds GetGridBounds(Vector3 anchor, Vector3 tangentA, Vector3 tangentB, bool in2DMode)
        {
            const int extraSize = 3;

            float gridSize = EGlobalSettings.GetGridSize();
            float lowestX = 99999;
            float highestX = -99999;
            float lowestY = 99999;
            float highestY = -99999;
            float lowestZ = 99999;
            float highestZ = -99999;

            if (anchor.x < lowestX) lowestX = anchor.x;
            if (anchor.x > highestX) highestX = anchor.x;
            if (tangentA.x < lowestX) lowestX = tangentA.x;
            if (tangentA.x > highestX) highestX = tangentA.x;
            if (tangentB.x < lowestX) lowestX = tangentB.x;
            if (tangentB.x > highestX) highestX = tangentB.x;

            if (anchor.y < lowestY) lowestY = anchor.y;
            if (anchor.y > highestY) highestY = anchor.y;
            if (tangentA.y < lowestY) lowestY = tangentA.y;
            if (tangentA.y > highestY) highestY = tangentA.y;
            if (tangentB.y < lowestY) lowestY = tangentB.y;
            if (tangentB.y > highestY) highestY = tangentB.y;

            if (anchor.z < lowestZ) lowestZ = anchor.z;
            if (anchor.z > highestZ) highestZ = anchor.z;
            if (tangentA.z < lowestZ) lowestZ = tangentA.z;
            if (tangentA.z > highestZ) highestZ = tangentA.z;
            if (tangentB.z < lowestZ) lowestZ = tangentB.z;
            if (tangentB.z > highestZ) highestZ = tangentB.z;

            lowestX -= gridSize * extraSize;
            highestX += gridSize * extraSize;
            lowestY -= gridSize * extraSize;
            highestY += gridSize * extraSize;
            lowestZ -= gridSize * extraSize;
            highestZ += gridSize * extraSize;

            Vector3 min = in2DMode ? new Vector3(lowestX, lowestY, anchor.z) : new Vector3(lowestX, anchor.y, lowestZ);
            Vector3 max = in2DMode ? new Vector3(highestX, highestY, anchor.z) : new Vector3(highestX, anchor.y, highestZ);

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);

            return bounds;
        }

        public static Bounds GetGridBounds(Spline spline)
        {
            const int extraSize = 3;

            float gridSize = EGlobalSettings.GetGridSize();

            float lowestX = 99999;
            float highestX = -99999;
            float lowestY = 99999;
            float highestY = -99999;
            float lowestZ = 99999;
            float highestZ = -99999;

            foreach (Segment s in spline.segments)
            {
                Vector3 anchor = s.GetPosition(ControlHandle.ANCHOR, Space.Self);
                Vector3 tangentA = s.GetPosition(ControlHandle.TANGENT_A, Space.Self);
                Vector3 tangentB = s.GetPosition(ControlHandle.TANGENT_B, Space.Self);

                if (anchor.x < lowestX) lowestX = anchor.x;
                if (anchor.x > highestX) highestX = anchor.x;
                if (tangentA.x < lowestX) lowestX = tangentA.x;
                if (tangentA.x > highestX) highestX = tangentA.x;
                if (tangentB.x < lowestX) lowestX = tangentB.x;
                if (tangentB.x > highestX) highestX = tangentB.x;

                if (anchor.y < lowestY) lowestY = anchor.y;
                if (anchor.y > highestY) highestY = anchor.y;
                if (tangentA.y < lowestY) lowestY = tangentA.y;
                if (tangentA.y > highestY) highestY = tangentA.y;
                if (tangentB.y < lowestY) lowestY = tangentB.y;
                if (tangentB.y > highestY) highestY = tangentB.y;

                if (anchor.z < lowestZ) lowestZ = anchor.z;
                if (anchor.z > highestZ) highestZ = anchor.z;
                if (tangentA.z < lowestZ) lowestZ = tangentA.z;
                if (tangentA.z > highestZ) highestZ = tangentA.z;
                if (tangentB.z < lowestZ) lowestZ = tangentB.z;
                if (tangentB.z > highestZ) highestZ = tangentB.z;
            }

            lowestX -= gridSize * extraSize;
            highestX += gridSize * extraSize;
            lowestY -= gridSize * extraSize;
            highestY += gridSize * extraSize;
            lowestZ -= gridSize * extraSize;
            highestZ += gridSize * extraSize;

            Vector3 min = spline.transform.TransformPoint(new Vector3(lowestX, spline.gridCenterPoint.y, lowestZ));
            if(spline.SplineType == SplineType.STATIC_2D) min = spline.transform.TransformPoint(new Vector3(lowestX, lowestY, spline.gridCenterPoint.z));
            min = SnapPoint(spline, min, true);
            Vector3 max = spline.transform.TransformPoint(new Vector3(highestX, spline.gridCenterPoint.y, highestZ));
            if (spline.SplineType == SplineType.STATIC_2D) max = spline.transform.TransformPoint(new Vector3(highestX, highestY, spline.gridCenterPoint.z));
            max = SnapPoint(spline, max, true);

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);

            return bounds;
        }

        public static Vector3 SnapPoint(Spline spline, Vector3 worldPoint, bool keepLocal = false)
        {
            Vector3 gridPoint = spline.transform.InverseTransformPoint(worldPoint);
            gridPoint -= spline.gridCenterPoint;
            gridPoint = GeneralUtility.RoundToClosest(gridPoint, EGlobalSettings.GetGridSize());
            gridPoint += spline.gridCenterPoint;

            if (!keepLocal)
            {
                gridPoint = spline.transform.TransformPoint(gridPoint);
            }

            return gridPoint;
        }

        public static Vector3 SnapPoint(Vector3 worldPoint)
        {
            return GeneralUtility.RoundToClosest(worldPoint, EGlobalSettings.GetGridSize());
        }

        public static void AlignGrid(Spline primary, Spline secondary)
        {
            if (primary == null)
                return;

            Vector3 gridPoint = primary.transform.TransformPoint(primary.gridCenterPoint);
            secondary.gridCenterPoint = secondary.transform.InverseTransformPoint(gridPoint);

            for(int i  = 0; i < secondary.segments.Count; i++)
            {
                Segment s = secondary.segments[i];
                Vector3 pos = s.GetPosition(ControlHandle.ANCHOR);
                Vector3 newPos = SnapPoint(secondary, pos);
                s.SetPosition(ControlHandle.ANCHOR, newPos);
                s.Translate(ControlHandle.TANGENT_A, pos - newPos);
                s.Translate(ControlHandle.TANGENT_B, pos - newPos);
            }
        }

        public static void GridToCenter(Spline spline)
        {
            spline.gridCenterPoint = spline.transform.InverseTransformPoint(spline.GetCenter());

            for (int i = 0; i < spline.segments.Count; i++)
            {
                Segment s = spline.segments[i];
                Vector3 pos = s.GetPosition(ControlHandle.ANCHOR);
                Vector3 newPos = SnapPoint(spline, pos);
                s.SetPosition(ControlHandle.ANCHOR, newPos);
                s.Translate(ControlHandle.TANGENT_A, pos - newPos);
                s.Translate(ControlHandle.TANGENT_B, pos - newPos);
            }
        }

        internal static void DrawLabels(Spline spline)
        {
            bool drawGridDistanceLabels = EGlobalSettings.GetDrawGridDistanceLabels();
            Handles.color = Color.black;
            ControlHandle selectedType = SplineUtility.GetControlHandleType(spline.selectedControlPoint);
            bool in2DMode = EHandleSceneView.GetCurrent().in2DMode;

            for (int i = 0; i < spline.segments.Count; i++)
            {
                Segment s = spline.segments[i];
                ControlHandle hoveredControlHandle = EHandleSelection.IsHovering(i);

                bool isPrimarySelection = EHandleSelection.IsPrimiarySelection(s);
                ControlHandle primarySelectedlHandle = isPrimarySelection ? SplineUtility.GetControlHandleType(spline.selectedControlPoint) : ControlHandle.NONE;
                bool isSecondarySelection = EHandleSelection.IsSecondarySelection(s);

                Vector3 anchor = s.GetPosition(ControlHandle.ANCHOR, Space.Self);
                Vector3 tangentA = s.GetPosition(ControlHandle.TANGENT_A, Space.Self);
                Vector3 tangentB = s.GetPosition(ControlHandle.TANGENT_B, Space.Self);

                Vector3 labelPointAnchor = new Vector3(anchor.x, spline.gridCenterPoint.y, anchor.z);
                if (spline.SplineType == SplineType.STATIC_2D) labelPointAnchor = new Vector3(anchor.x, anchor.y, spline.gridCenterPoint.z);
                Vector3 worldLabelPointAnchor = spline.transform.TransformPoint(labelPointAnchor);
                Vector3 worldAnchor = spline.transform.TransformPoint(anchor);

                Vector3 labelPointTangentA = new Vector3(tangentA.x, spline.gridCenterPoint.y, tangentA.z);
                if (spline.SplineType == SplineType.STATIC_2D) labelPointTangentA = new Vector3(tangentA.x, tangentA.y, spline.gridCenterPoint.z);
                Vector3 worldLabelPointTangentA = spline.transform.TransformPoint(labelPointTangentA);
                Vector3 worldTangentA = spline.transform.TransformPoint(tangentA);

                Vector3 labelPointTangentB = new Vector3(tangentB.x, spline.gridCenterPoint.y, tangentB.z);
                if (spline.SplineType == SplineType.STATIC_2D) labelPointTangentB = new Vector3(tangentB.x, tangentB.y, spline.gridCenterPoint.z);
                Vector3 worldLabelPointTangentB = spline.transform.TransformPoint(labelPointTangentB);
                Vector3 worldTangentB = spline.transform.TransformPoint(tangentB);

                bool skipDraw = selectedType != ControlHandle.ANCHOR && (GeneralUtility.IsEqual(labelPointAnchor, labelPointTangentB) || GeneralUtility.IsEqual(labelPointAnchor, labelPointTangentA));
                if (!GeneralUtility.IsEqual(labelPointAnchor, anchor, 0.01f) && !skipDraw)
                {
                    Handles.DrawLine(worldLabelPointAnchor, worldAnchor);

                    float value = Mathf.Round((anchor.y - labelPointAnchor.y) * 100) / 100;
                    bool labelSelectionCheck = true;
                    if (spline.SplineType == SplineType.STATIC_2D)
                    {
                        value = Mathf.Round((anchor.z - labelPointAnchor.z) * 100) / 100;
                        if (in2DMode) labelSelectionCheck = hoveredControlHandle != ControlHandle.ANCHOR && !isSecondarySelection && primarySelectedlHandle != ControlHandle.ANCHOR;
                    }
                    if (drawGridDistanceLabels && labelSelectionCheck) Handles.Label(worldLabelPointAnchor, value.ToString(), LibraryGUIStyle.textSceneView);
                }

                if (s.GetInterpolationType() == InterpolationType.SPLINE)
                {
                    skipDraw = selectedType != ControlHandle.TANGENT_A && (GeneralUtility.IsEqual(labelPointTangentA, labelPointAnchor) || GeneralUtility.IsEqual(labelPointTangentA, labelPointTangentB));

                    if (!GeneralUtility.IsEqual(labelPointTangentA, tangentA) && !skipDraw)
                    {
                        Handles.DrawLine(worldLabelPointTangentA, worldTangentA);

                        float value = Mathf.Round((tangentA.y - labelPointTangentA.y) * 100) / 100;
                        bool labelSelectionCheck = true;
                        if (spline.SplineType == SplineType.STATIC_2D)
                        {
                            value = Mathf.Round((tangentA.z - labelPointTangentA.z) * 100) / 100;
                            if (in2DMode) labelSelectionCheck = hoveredControlHandle != ControlHandle.TANGENT_A && primarySelectedlHandle != ControlHandle.TANGENT_A;
                        }
                        if (drawGridDistanceLabels && labelSelectionCheck) Handles.Label(worldLabelPointTangentA, value.ToString(), LibraryGUIStyle.textSceneView);
                    }

                    skipDraw = selectedType != ControlHandle.TANGENT_B && (GeneralUtility.IsEqual(labelPointTangentB, labelPointAnchor) || GeneralUtility.IsEqual(labelPointTangentB, labelPointTangentA));

                    if (!GeneralUtility.IsEqual(labelPointTangentB, tangentB) && !skipDraw)
                    {
                        Handles.DrawLine(worldLabelPointTangentB, worldTangentB);

                        float value = Mathf.Round((tangentB.y - labelPointTangentB.y) * 100) / 100;
                        bool labelSelectionCheck = true;
                        if (spline.SplineType == SplineType.STATIC_2D)
                        {
                            value = Mathf.Round((tangentB.z - labelPointTangentB.z) * 100) / 100;
                            if (in2DMode) labelSelectionCheck = hoveredControlHandle != ControlHandle.TANGENT_B && primarySelectedlHandle != ControlHandle.TANGENT_B;
                        }
                        if (drawGridDistanceLabels && labelSelectionCheck) Handles.Label(worldLabelPointTangentB, value.ToString(), LibraryGUIStyle.textSceneView);
                    }
                }
            }
        }

        internal static void DrawGrid(Transform originTransform, Bounds bounds, float size, bool space2D = false)
        {
            int count = 0;
            bool is2D = EHandleSceneView.GetCurrent().in2DMode;

            Color color1 = EGlobalSettings.GetGridColor();
            Color color2 = new Color(color1.r, color1.g, color1.b, 0.33f);

            if (EGlobalSettings.GetGridOccluded())
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            else
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            float increment = bounds.min.x;
            if (space2D) increment = bounds.min.y;

            float length = bounds.max.x + size;
            if (space2D) length = bounds.max.y + size;

            while (increment < length)
            {
                Vector3 point1 = new Vector3(increment, bounds.max.y, bounds.min.z);
                Vector3 point2 = new Vector3(increment, bounds.max.y, bounds.max.z);

                if (space2D)
                {
                    point1 = new Vector3(bounds.min.x, increment, bounds.min.z);
                    point2 = new Vector3(bounds.max.x, increment, bounds.max.z);
                }

                if (originTransform != null)
                {
                    point1 = originTransform.TransformPoint(point1);
                    point2 = originTransform.TransformPoint(point2);
                }

                if (count != 0)
                {
                    Handles.color = color2;
                    Handles.DrawLine(point1, point2);
                }
                else
                {
                    Handles.color = color1;
                    Handles.DrawLine(point1, point2, is2D ? 2 : 1);
                }
                increment += size;

                if (count == 9) count = 0;
                else count++;
            }

            //Increment
            increment = bounds.min.z;
            if (space2D) increment = bounds.min.x;

            //Length
            length = bounds.max.z + size;
            if (space2D) length = bounds.max.x + size;

            count = 0;
            while (increment < length)
            {
                Vector3 point1 = new Vector3(bounds.min.x, bounds.max.y, increment);
                Vector3 point2 = new Vector3(bounds.max.x, bounds.max.y, increment);

                if (space2D)
                {
                    point1 = new Vector3(increment, bounds.min.y, bounds.min.z);
                    point2 = new Vector3(increment, bounds.max.y, bounds.max.z);
                }

                if (originTransform != null)
                {
                    point1 = originTransform.TransformPoint(point1);
                    point2 = originTransform.TransformPoint(point2);
                }

                if (count != 0)
                {
                    Handles.color = color2;
                    Handles.DrawLine(point1, point2);
                }
                else
                {
                    Handles.color = color1;
                    Handles.DrawLine(point1, point2, is2D ? 2 : 1);
                }
                increment += size;

                if (count == 9) count = 0;
                else count++;
            }
        }
    }
}
