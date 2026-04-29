// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleDrawing.cs
//
// Author: Mikael Danielsson
// Date Created: 29-01-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using SplineArchitect.CustomTools;
using SplineArchitect.Libraries;
using SplineArchitect.Utility;

namespace SplineArchitect
{
    internal static class EHandleDrawing
    {
        private static Vector3[] triangle = new Vector3[4];
        private static Vector3[] normalsContainer = new Vector3[3];
        private static List<Spline> allSelectedSplines = new List<Spline>();
        private static HashSet<Segment> segmentContainer = new HashSet<Segment>();
        private static Dictionary<Vector3Int, int> linksAtPoint = new Dictionary<Vector3Int, int>();

        internal static void OnSceneGUIGlobal(HashSet<Spline> splines, Event e)
        {
            if (e.type != EventType.Repaint)
                return;

            EHandleSpline.totalLinesDrawn = 0;

            SplineHideMode hideMode = EGlobalSettings.GetSplineHideMode();
            SceneView sceneView = EHandleSceneView.GetCurrent();

            allSelectedSplines.Clear();
            EHandleSelection.GetAllSelectedSplinesNonAlloc(allSelectedSplines);

            //First
            foreach (Spline spline in allSelectedSplines)
            {
                if (!spline.IsEnabled()) continue;
                if (!sceneView.drawGizmos) continue;
                if (!EHandleSelection.IsPrimiarySelection(spline) && !EHandleSelection.IsSecondarySelection(spline)) continue;

                if (EGlobalSettings.GetGridVisibility())
                    EHandleGrid.DrawGrid(spline.transform, EHandleGrid.GetGridBounds(spline), EGlobalSettings.GetGridSize(), spline.SplineType == SplineType.STATIC_2D);
            }

            //Second
            foreach (Spline spline in splines)
            {
                if (!spline.IsEnabled())
                    continue;

                if (!sceneView.drawGizmos)
                    continue;

                if (!spline.DistanceMap.IsCreated || spline.DistanceMap.Length == 0)
                    continue;

                bool primiarySelection = EHandleSelection.IsPrimiarySelection(spline);
                bool secondarySelection = EHandleSelection.IsSecondarySelection(spline);

                if (spline.IsHiddenInSceneView() && !primiarySelection && !secondarySelection)
                    continue;

                if (hideMode != SplineHideMode.NONE && !primiarySelection && !secondarySelection)
                    continue;

                DrawSpline(spline, primiarySelection, secondarySelection, hideMode);
            }

            //Third
            if (EHandleSelection.selectedSpline != null && EHandleSelection.selectedSpline.IsEnabled())
            {
                if (sceneView.drawGizmos)
                {
                    DrawControlPoints(EHandleSelection.selectedSpline, hideMode);
                    DrawNormals(EHandleSelection.selectedSpline);

                    if (EGlobalSettings.GetGridVisibility())
                        EHandleGrid.DrawLabels(EHandleSelection.selectedSpline);

                    if(EHandleSpline.controlPointCreationActive && EHandleSceneView.mouseInsideSceneView)
                        DrawSegmentIndicator(e, EHandleSelection.selectedSpline);
                }
            }

            //Fourth, none selected control point creation
            if (EHandleSpline.controlPointCreationActive)
            {
                if (PositionTool.activePart != PositionTool.ActivePart.NONE)
                    return;

                if (EHandleSelection.selectedSpline == null && EHandleSceneView.mouseInsideSceneView)
                {
                    if (EGlobalSettings.GetGridVisibility()) DrawIndicatorGrid(e);
                    else DrawIndicator(e);
                }
            }

            DrawLinkDots(splines, hideMode, sceneView);
            DrawLinkPreviewDots(splines, hideMode);
            PositionTool.Draw(e);
        }

        private static void DrawSpline(Spline spline, bool primiarySelection, bool secondarySelection, SplineHideMode hideMode)
        {
            if (spline.segments.Count < 2)
                return;

            Camera editorCamera = EHandleSceneView.GetCamera();

            for (int i = 1; i < spline.segments.Count; i++)
            {
                Segment segmentFrom = spline.segments[i - 1];
                Segment segmentTo = spline.segments[i];
                bool segmentSelected = false;

                if(primiarySelection && spline.selectedControlPoint > 0)
                {
                    int segmentIndex = SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint);
                    if (segmentFrom == spline.segments[segmentIndex])
                        segmentSelected = true;

                    if(!segmentSelected)
                    {
                        foreach (int controlPoint in spline.selectedAnchors)
                        {
                            segmentIndex = SplineUtility.ControlPointIdToSegmentIndex(controlPoint);
                            if (segmentFrom == spline.segments[segmentIndex])
                            {
                                segmentSelected = true;
                                break;
                            }
                        }
                    }
                }

                int linesCount = SplineUtility.GetSegmentLinesCount(segmentFrom, segmentTo, editorCamera);
                EHandleSpline.totalLinesDrawn += linesCount;

                Vector3 anchor1 = segmentFrom.GetPosition(ControlHandle.ANCHOR);
                Vector3 tangent1A = segmentFrom.GetPosition(ControlHandle.TANGENT_A);
                Vector3 tangent2B = segmentTo.GetPosition(ControlHandle.TANGENT_B);
                Vector3 anchor2 = segmentTo.GetPosition(ControlHandle.ANCHOR);

                if ((primiarySelection || secondarySelection) && hideMode != SplineHideMode.SELECTED_OCCLUDED && !spline.Occluded)
                {
                    Handles.color = LibraryColor.black_A70;
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    DrawSegement(anchor1, tangent1A, tangent2B, anchor2, linesCount, 1);
                }

                Handles.color = spline.color;
                if (primiarySelection || secondarySelection)
                    Handles.color = Color.yellow;

                if (segmentSelected)
                    Handles.color = LibraryColor.lightOrange_A100;

                if (primiarySelection && EHandleSpline.controlPointCreationActive && spline.indicatorSegment == i && EHandleSpline.GetIndicatorActivationDistance(spline) > spline.indicatorDistanceToSpline)
                    Handles.color = Color.white;

                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

                float splineSize = spline.Width;
                if (spline == EHandleSelection.hoveredSpline) splineSize = spline.Width * 1.5f;
                else Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                DrawSegement(anchor1, tangent1A, tangent2B, anchor2, linesCount, splineSize);
                DrawTriangleOnSegment(spline, segmentFrom, primiarySelection || secondarySelection, hideMode);
            }
        }

        private static void DrawSegement(Vector3 anchorA, Vector3 tangnetA, Vector3 tangentB, Vector3 anchorB, int lines, float size)
        {
            for (int i = 0; i < lines; i++)
            {
                float startTime = (float)i / lines;
                float endTime = (float)(i + 1) / lines;

                Vector3 startPoint = BezierUtility.Cubic(anchorA, tangnetA, tangentB, anchorB, startTime);
                Vector3 endPoint = BezierUtility.Cubic(anchorA, tangnetA, tangentB, anchorB, endTime);

                Handles.DrawLine(startPoint, endPoint, size);
            }
        }

        private static void DrawTriangleOnSegment(Spline spline, Segment segment, bool selected, SplineHideMode hideMode)
        {
            float time = (segment.zPosition + segment.length * 0.5f) / spline.Length;
            float fixedTime = spline.TimeToFixedTime(time);
            spline.GetNormalsNonAlloc(normalsContainer, fixedTime);
            Vector3 position = spline.GetPosition(fixedTime);
            float size = EHandleSegment.GetControlPointSize(position) * 1.3f;

            triangle[0] = position - (normalsContainer[0] * size * 0) + (normalsContainer[2] * size);
            triangle[1] = position + (normalsContainer[0] * size * 0) + (normalsContainer[2] * size);
            triangle[2] = position + (normalsContainer[0] * size) - (normalsContainer[2] * size);
            triangle[3] = position - (normalsContainer[0] * size) - (normalsContainer[2] * size);

            Handles.color = Color.white;

            if (selected && hideMode != SplineHideMode.SELECTED_OCCLUDED)
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.DrawSolidRectangleWithOutline(triangle, LibraryColor.grey_RGB20_A50, Color.yellow);
            }
            else
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                Handles.DrawSolidRectangleWithOutline(triangle, LibraryColor.grey_RGB20_A50, spline.color);
            }
        }

        private static void DrawLinkDots(HashSet<Spline> splines, SplineHideMode hideMode, SceneView scneneView)
        {
            if (!scneneView.drawGizmos)
                return;

            Spline selectedSpline = EHandleSelection.selectedSpline;
            Vector3 selectedAnchorPos = new Vector3(99999, 99999, 99999);

            segmentContainer.Clear();
            linksAtPoint.Clear();

            if (selectedSpline != null)
            {
                foreach (Segment s in selectedSpline.segments)
                {
                    if(!segmentContainer.Contains(s)) 
                        segmentContainer.Add(s);
                }

                if(selectedSpline.selectedControlPoint != 0 && SplineUtility.GetControlHandleType(selectedSpline.selectedControlPoint) == ControlHandle.ANCHOR)
                {
                    int selectedSegmentId = SplineUtility.ControlPointIdToSegmentIndex(selectedSpline.selectedControlPoint);
                    selectedAnchorPos = selectedSpline.segments[selectedSegmentId].GetPosition(ControlHandle.ANCHOR);
                }
            }

            if(EGlobalSettings.GetShowLinkIndicators())
            {
                foreach (Spline spline in splines)
                {
                    if (spline == selectedSpline)
                        continue;

                    if (!spline.IsEnabled())
                        continue;

                    foreach (Segment s in spline.segments)
                    {
                        if (s.linkTarget != LinkTarget.NONE && !segmentContainer.Contains(s))
                            segmentContainer.Add(s);
                    }
                }
            }

            Handles.zTest = hideMode == SplineHideMode.SELECTED_OCCLUDED ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Always;

            foreach (Segment segment in segmentContainer)
            {
                float anchorSize = EHandleSegment.GetControlPointSize(segment.GetPosition(ControlHandle.ANCHOR));
                Vector3 anchorPos = segment.GetPosition(ControlHandle.ANCHOR);
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(anchorPos.x * 100), Mathf.RoundToInt(anchorPos.y * 100), Mathf.RoundToInt(anchorPos.z * 100));
                int count = 0;
                float yOffset = 0;

                if (GeneralUtility.IsEqual(selectedAnchorPos, anchorPos))
                    continue;

                if (linksAtPoint.TryGetValue(key, out int value))
                {
                    count = value;

                    yOffset = (anchorSize * 3f);
                    if (count > 1) yOffset += ((count - 1) * (anchorSize * 1.5f));
                }

                if (segment.linkTarget == LinkTarget.SPLINE_CONNECTOR && segment.SplineConnector != null)
                {
                    Handles.color = LibraryColor.orange_A100;
                    Handles.DotHandleCap(0, anchorPos + new Vector3(0, yOffset, 0), Quaternion.identity, anchorSize * 0.4f, EventType.Repaint);
                }
                else if(segment.linkTarget == LinkTarget.ANCHOR)
                {
                    Handles.color = segment.ignoreLink ? LibraryColor.lightBlue_A100 : Color.yellow;
                    Handles.DotHandleCap(0, anchorPos + new Vector3(0, yOffset, 0), Quaternion.identity, anchorSize * 0.4f, EventType.Repaint);
                }

                if(count == 0) linksAtPoint.Add(key, 1);
                else linksAtPoint[key] = count + 1;
            }
        }

        private static void DrawLinkPreviewDots(HashSet<Spline> splines, SplineHideMode hideMode)
        {
            Vector3 closestPoint = Vector3.zero;
            Spline closestSpline = null;
            SplineConnector closestConnector = null;
            float closestDistance = 999999;

            if (PositionTool.activePart == PositionTool.ActivePart.NONE)
                return;

            Spline selected = EHandleSelection.selectedSpline;

            if (selected == null || SplineUtility.GetControlHandleType(selected.selectedControlPoint) != ControlHandle.ANCHOR)
                return;

            if (selected.selectedAnchors.Count > 0)
                return;

            int selectedSegmentId = SplineUtility.ControlPointIdToSegmentIndex(selected.selectedControlPoint);
            Vector3 selectedAnchorPos = selected.segments[selectedSegmentId].GetPosition(ControlHandle.ANCHOR);
            float controlDistance = 75 * EHandleSegment.DistanceModifier(selectedAnchorPos);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            //Preview control points
            foreach (Spline spline in splines)
            {
                int dontDrawLastIfLoop = 0;
                if (spline.Loop) dontDrawLastIfLoop = 1;

                //Skip if to far away.
                if (Vector3.Distance(spline.controlPointsBounds.ClosestPoint(selectedAnchorPos), selectedAnchorPos) > controlDistance)
                    continue;

                for (int i = 0; i < spline.segments.Count - dontDrawLastIfLoop; i++)
                {
                    Segment s = spline.segments[i];

                    if (selected.segments[selectedSegmentId] == s)
                        continue;

                    Vector3 anchorPos = s.GetPosition(ControlHandle.ANCHOR);
                    float distance = Vector3.Distance(anchorPos, selectedAnchorPos);

                    if (distance > controlDistance)
                        continue;

                    if (GeneralUtility.IsEqual(selectedAnchorPos, anchorPos))
                        continue;

                    Handles.color = spline.color;
                    if(spline != selected) Handles.DotHandleCap(0, anchorPos, Quaternion.identity, EHandleSegment.GetControlPointSize(anchorPos) * 0.55f, EventType.Repaint);

                    if(closestDistance > distance)
                    {
                        closestDistance = distance;
                        closestSpline = spline;
                        closestPoint = anchorPos;
                        closestConnector = null;
                    }
                }
            }

            //Preview spline connectors
            foreach (SplineConnector sc in HandleRegistry.GetSplineConnectorsUnsafe())
            {
                float distance = Vector3.Distance(sc.transform.position, selectedAnchorPos);

                if (distance > controlDistance)
                    continue;

                float size = EHandleSegment.GetControlPointSize(sc.transform.position) * 0.55f;
                Handles.color = LibraryColor.orange_A100;
                Handles.DotHandleCap(0, sc.transform.position, Quaternion.identity, size, EventType.Repaint);

                if (closestDistance > distance)
                {
                    closestDistance = distance;
                    closestSpline = null;
                    closestPoint = sc.transform.position;
                    closestConnector = sc;
                }
            }

            if (closestSpline != null)
            {
                Handles.color = closestSpline.color;
                Handles.DotHandleCap(0, closestPoint, Quaternion.identity, EHandleSegment.GetControlPointSize(closestPoint) * 0.9f, EventType.Repaint);
                Handles.color = Color.white;
                Handles.DotHandleCap(0, closestPoint, Quaternion.identity, EHandleSegment.GetControlPointSize(closestPoint) * 0.45f, EventType.Repaint);
            }
            else if(closestConnector != null)
            {
                Handles.color = LibraryColor.orange_A100;
                Handles.DotHandleCap(0, closestPoint, Quaternion.identity, EHandleSegment.GetControlPointSize(closestPoint) * 0.9f, EventType.Repaint);
                Handles.color = Color.white;
                Handles.DotHandleCap(0, closestPoint, Quaternion.identity, EHandleSegment.GetControlPointSize(closestPoint) * 0.45f, EventType.Repaint);
            }
        }

        private static void DrawControlPoints(Spline spline, SplineHideMode hideMode)
        {
            int dontDrawLastIfLoop = 0;
            if (spline.Loop)
                dontDrawLastIfLoop = 1;

            int hoveredSegmentId = SplineUtility.ControlPointIdToSegmentIndex(EHandleSelection.hoveredCp);
            ControlHandle hoveredType = SplineUtility.GetControlHandleType(EHandleSelection.hoveredCp);

            //Draw lines between anchor and tangents
            for (int i = 0; i < spline.segments.Count - dontDrawLastIfLoop; i++)
            {
                Segment segment = spline.segments[i];

                int anchorId = SplineUtility.SegmentIndexToControlPointId(i, ControlHandle.ANCHOR);
                float anchorSize = EHandleSegment.GetControlPointSize(segment.GetPosition(ControlHandle.ANCHOR));
                float tangentASize = EHandleSegment.GetControlPointSize(segment.GetPosition(ControlHandle.TANGENT_A));
                float tangentBSize = EHandleSegment.GetControlPointSize(segment.GetPosition(ControlHandle.TANGENT_B));
                float lineAToAnchorSize = 2f;
                float lineBToAnchorSize = 2f;

                bool hoveringAnchor = false;
                bool hoveringTangentA = false;
                bool hoveringTangentB = false;

                bool isAnchorSelected = spline.selectedAnchors.Contains(anchorId);
                if (anchorId == spline.selectedControlPoint) isAnchorSelected = true;
                bool isTangentSelected = false;
                if (anchorId + 1 == spline.selectedControlPoint || anchorId + 2 == spline.selectedControlPoint) isTangentSelected = true;

                if (hoveredSegmentId == i)
                {
                    if (hoveredType == ControlHandle.ANCHOR)
                    {
                        hoveringAnchor = true;
                        lineAToAnchorSize = 3;
                        lineBToAnchorSize = 3;
                        anchorSize *= 1.1f;
                    }

                    if (hoveredType == ControlHandle.TANGENT_A)
                    {
                        hoveringTangentA = true;
                        lineAToAnchorSize = 3;
                        tangentASize *= 1.15f;
                    }

                    if (hoveredType == ControlHandle.TANGENT_B)
                    {
                        hoveringTangentB = true;
                        lineBToAnchorSize = 3;
                        tangentBSize *= 1.15f;
                    }
                }

                //Settings
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.color = LibraryColor.black_A50;

                if (hideMode != SplineHideMode.SELECTED_OCCLUDED || isAnchorSelected || isTangentSelected)
                {
                    //Shadow controlpoints
                    Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.ANCHOR), Quaternion.identity, anchorSize * 1.3f, EventType.Repaint);
                    if (segment.GetInterpolationType() == InterpolationType.SPLINE)
                    {
                        Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.TANGENT_A), Quaternion.identity, tangentASize, EventType.Repaint);
                        Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.TANGENT_B), Quaternion.identity, tangentBSize, EventType.Repaint);

                        //Settings
                        Handles.color = LibraryColor.black_A70;
                        //Shadow lines
                        Handles.DrawLine(segment.GetPosition(ControlHandle.ANCHOR), segment.GetPosition(ControlHandle.TANGENT_A));
                        Handles.DrawLine(segment.GetPosition(ControlHandle.ANCHOR), segment.GetPosition(ControlHandle.TANGENT_B));
                    }
                }

                //Settings
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                Handles.color = LibraryColor.grey_A100;

                if (segment.GetInterpolationType() == InterpolationType.SPLINE)
                {
                    //Lines
                    Handles.DrawLine(segment.GetPosition(ControlHandle.ANCHOR), segment.GetPosition(ControlHandle.TANGENT_A), lineAToAnchorSize);
                    Handles.DrawLine(segment.GetPosition(ControlHandle.ANCHOR), segment.GetPosition(ControlHandle.TANGENT_B), lineBToAnchorSize);
                }

                //Anchor
                if (!isAnchorSelected)
                {
                    float size = 1.3f;
                    Handles.color = Color.yellow;
                    if(isAnchorSelected || hideMode != SplineHideMode.SELECTED_OCCLUDED) Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    else Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                    Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.ANCHOR), Quaternion.identity, anchorSize * size, EventType.Repaint);

                    if (hoveringAnchor) Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    Handles.color = spline.color;
                    Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.ANCHOR), Quaternion.identity, anchorSize, EventType.Repaint);
                }
                if(segment.GetInterpolationType() == InterpolationType.SPLINE)
                {
                    //Tanget a
                    if(hoveringTangentA) Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    else Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                    if (anchorId + 1 != spline.selectedControlPoint)
                    {
                        Handles.color = spline.color;
                        Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.TANGENT_A), Quaternion.identity, tangentASize, EventType.Repaint);
                    }

                    //Tanget b
                    if (hoveringTangentB) Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    else Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                    if (anchorId + 2 != spline.selectedControlPoint)
                    {
                        Handles.color = spline.color;
                        Handles.DotHandleCap(0, segment.GetPosition(ControlHandle.TANGENT_B), Quaternion.identity, tangentBSize, EventType.Repaint);
                    }
                }
            }
        }

        private static void DrawNormals(Spline spline)
        {
            if (EGlobalSettings.GetShowNormals() == false)
                return;

            if (spline.segments.Count < 2)
                return;

            if (spline.NormalsLocal.Length <= 0 && spline.SplineType == SplineType.DYNAMIC)
                return;

            float spaceLength = 1 / (EGlobalSettings.GetNormalsSpacing() * (spline.Length / 100));
            float length = EGlobalSettings.GetNormalsLength();
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            Vector3 c1;
            for (float time = 0; time <= 1; time += spaceLength)
            {
                float fixedTime = spline.TimeToFixedTime(time);
                c1 = spline.GetPosition(fixedTime);
                spline.GetNormalsNonAlloc(normalsContainer, fixedTime, Space.World);

                Handles.color = Color.red;
                Handles.DrawLine(c1, c1 + normalsContainer[0] * length);

                Handles.color = Color.green;
                Handles.DrawLine(c1, c1 + normalsContainer[1] * length);
            }

            c1 = spline.GetPosition(1);
            spline.GetNormalsNonAlloc(normalsContainer, 1);

            Handles.color = Color.red;
            Handles.DrawLine(c1, c1 + normalsContainer[0] * length);

            Handles.color = Color.green;
            Handles.DrawLine(c1, c1 + normalsContainer[1] * length);
        }

        private static void DrawIndicator(Event e)
        {
            Vector3 anchor = EHandleSpline.segementIndicatorData.newAnchor;
            Vector3 tangentA = EHandleSpline.segementIndicatorData.newTangentA;
            Vector3 tangentB = EHandleSpline.segementIndicatorData.newTangentB;

            float size = EHandleSegment.GetControlPointSize(anchor);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = Color.yellow;

            Handles.DrawLine(anchor, tangentA, 1);
            Handles.DrawLine(anchor, tangentB, 1);

            Handles.color = Color.white;
            Handles.DotHandleCap(0, anchor, Quaternion.identity, size, EventType.Repaint);
            Handles.DotHandleCap(0, tangentA, Quaternion.identity, size, EventType.Repaint);
            Handles.DotHandleCap(0, tangentB, Quaternion.identity, size, EventType.Repaint);

            if (e.type == EventType.Repaint)
                EHandleSceneView.RepaintCurrent();
        }

        private static void DrawIndicatorGrid(Event e)
        {
            Vector3 anchor = EHandleSpline.segementIndicatorData.newAnchor;
            Vector3 tangentA = EHandleSpline.segementIndicatorData.newTangentA;
            Vector3 tangentB = EHandleSpline.segementIndicatorData.newTangentB;

            bool in2DMode = EHandleSceneView.GetCurrent().in2DMode;
            float size = EHandleSegment.GetControlPointSize(anchor);
            Bounds bounds = EHandleGrid.GetGridBounds(anchor, tangentA, tangentB, in2DMode);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = Color.white;
            EHandleGrid.DrawGrid(null, bounds, EGlobalSettings.GetGridSize(), in2DMode);

            Handles.color = Color.yellow;
            Handles.DrawLine(anchor, tangentA, 1);
            Handles.DrawLine(anchor, tangentB, 1);

            Handles.color = Color.white;
            Handles.DotHandleCap(0, anchor, Quaternion.identity, size, EventType.Repaint);
            Handles.DotHandleCap(0, tangentA, Quaternion.identity, size, EventType.Repaint);
            Handles.DotHandleCap(0, tangentB, Quaternion.identity, size, EventType.Repaint);

            if (e.type == EventType.Repaint)
                EHandleSceneView.RepaintCurrent();
        }

        private static void DrawSegmentIndicator(Event e, Spline spline)
        {
            Camera editorCamera = EHandleSceneView.GetCamera();

            Vector3 anchor = EHandleSpline.segementIndicatorData.anchor;
            Vector3 tangent = EHandleSpline.segementIndicatorData.tangent;
            Vector3 tangentA = EHandleSpline.segementIndicatorData.newTangentA;
            Vector3 tangentB = EHandleSpline.segementIndicatorData.newTangentB;
            Vector3 newAnchor = EHandleSpline.segementIndicatorData.newAnchor;
            Vector3 newTangentA = EHandleSpline.segementIndicatorData.newTangentA;
            Vector3 newTangentB = EHandleSpline.segementIndicatorData.newTangentB;
            bool originFromStart = EHandleSpline.segementIndicatorData.originFromStart;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = Color.white;
            float size = EHandleSegment.GetControlPointSize(spline.indicatorPosition);

            if (spline.indicatorDistanceToSpline < EHandleSpline.GetIndicatorActivationDistance(spline))
            {
                Handles.DotHandleCap(0, spline.indicatorPosition, Quaternion.identity, size, EventType.Repaint);
            }
            else if (!spline.Loop)
            {
                int precision = SplineUtility.GetSegmentLinesCount(anchor,
                                                                 tangent,
                                                                 newAnchor,
                                                                 newTangentB,
                                                                 75,
                                                                 editorCamera);
                DrawSegement(anchor,
                             tangent,
                             originFromStart ? newTangentA : newTangentB,
                             newAnchor,
                             precision,
                             2.5f);

                Handles.DotHandleCap(0, newAnchor, Quaternion.identity, size * 1.2f, EventType.Repaint);
                Handles.DotHandleCap(0, newTangentA, Quaternion.identity, size, EventType.Repaint);
                Handles.DotHandleCap(0, newTangentB, Quaternion.identity, size, EventType.Repaint);

                Handles.color = Color.black;
                Handles.DotHandleCap(0, newAnchor, Quaternion.identity, size * 0.8f, EventType.Repaint);
                Handles.DotHandleCap(0, newTangentA, Quaternion.identity, size * 0.6f, EventType.Repaint);
                Handles.DotHandleCap(0, newTangentB, Quaternion.identity, size * 0.6f, EventType.Repaint);
            }

            if (e.type == EventType.Repaint)
                EHandleSceneView.RepaintCurrent();
        }

        private static void DebugDrawSplineSpace(Spline spline)
        {
            Handles.color = Color.yellow;
            Handles.DrawLine(spline.transform.position, spline.transform.position + spline.transform.forward * spline.Length);

            Handles.color = LibraryColor.white_RGB80_A100;
            for (int i = 0; i < spline.Length; i += 10)
            {
                Handles.DotHandleCap(0, spline.transform.position + spline.transform.forward * i, Quaternion.identity, 0.1f, EventType.Repaint);
            }

            Handles.color = Color.blue;
            if (EHandleSelection.selectedSplineObject != null)
            {
                SplineObject selection = EHandleSelection.selectedSplineObject;
                Handles.DotHandleCap(0, spline.transform.TransformPoint(selection.splinePosition), Quaternion.identity, 0.25f, EventType.Repaint);

                Handles.color = Color.red;
                for (int i = 0; i < selection.transform.childCount; i++)
                {
                    SplineObject acoChild = selection.transform.GetChild(i).GetComponent<SplineObject>();

                    if (acoChild != null)
                        Handles.DotHandleCap(0, spline.transform.TransformPoint(acoChild.splinePosition), Quaternion.identity, 0.25f, EventType.Repaint);
                }
            }
        }
    }
}