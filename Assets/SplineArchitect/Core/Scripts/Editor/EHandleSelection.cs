// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleSelection.cs
//
// Author: Mikael Danielsson
// Date Created: 06-10-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

using SplineArchitect.CustomTools;
using SplineArchitect.Utility;
using SplineArchitect.Ui;

namespace SplineArchitect
{
    public static class EHandleSelection
    {
        //Selection Spline
        public static Spline selectedSpline { get; private set; }
        public static HashSet<Spline> selectedSplines { get; private set; } = new HashSet<Spline>();
        public static Spline hoveredSpline { get; private set; }
        private static Spline oldHoveredSpline;
        private static List<Object> selection = new List<Object>();

        //Selection SplineObject
        public static SplineObject selectedSplineObject;
        public static List<SplineObject> selectedSplineObjects = new List<SplineObject>();

        //Selection ControlPoint
        public static int hoveredCp { get; private set; }
        private static int oldHoveredCp;

        //Selected SplineConnector
        public static SplineConnector selectedSplineConnector { get; private set; }
        public static HashSet<SplineConnector> selectedSplineConnectors = new HashSet<SplineConnector>();

        //General
        public static bool stopNextUpdateSelection { private get; set; } = false;
        public static bool stopUpdateSelection { private get; set; } = false;
        private static Ray mouseRay;
        private static bool assemblyReload = true;
        private static bool markForForceUpdate = false;

        internal static void BeforeSceneGUIGlobal(SceneView sceneView, Event e)
        {
            //Need to check this in unity 2022, else we get errors only while creating splines in 2D.
            if (EHandleSpline.controlPointCreationActive)
                return;

            PositionTool.UpdateHoveredData(e, EMouseUtility.GetMouseRay(e.mousePosition));

            if (e.type == EventType.MouseDown && e.button == 0 && !EHandleModifier.AltActive(e))
            {
                if (PositionTool.Press(e, EMouseUtility.GetMouseRay(e.mousePosition)))
                {
#if UNITY_6000_0_OR_NEWER
                    e.Use();
#endif
                }
            }

            if (GUIUtility.hotControl == 0)
            {
                bool hovering = false;

                if (TryUpdateHoveredControlPoint(e, selectedSpline, sceneView))
                    hovering = true;

                if (!hovering && TryUpdateHoveredSpline(e, HandleRegistry.GetSplinesUnsafe(), EHandleModifier.CtrlActive(e)))
                    hovering = true;
            }
            else
            {
                hoveredCp = 0;
                hoveredSpline = null;
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                TrySelectHoveredControlPoint(selectedSpline, e, EHandleModifier.CtrlActive(e));
                TrySelectHoveredSpline(e, EHandleModifier.CtrlActive(e));
            }

            if (e.type == EventType.Layout)
            {
                if (EHandleEvents.updateSelection)
                {
                    EHandleEvents.updateSelection = false;
                    ForceUpdate();
                }
            }

            if (assemblyReload)
            {
                assemblyReload = false;
                //This is needed else Look rotation viewing vector is zero after assembly reload.
                EHandleTool.ActivatePositionToolForControlPoint(selectedSpline);
            }

            if(markForForceUpdate)
            {
                markForForceUpdate = false;
                ForceUpdate();
            }
        }

        internal static void OnSelectionChange()
        {
            UpdateSelection(TryGetSelectionTransform());
        }

        internal static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (selectionRect.Contains(Event.current.mousePosition))
                {
                    // Get the GameObject associated with the instance ID
                    GameObject go = GeneralUtility.GetObjectFromInstanceId(instanceID) as GameObject;

                    if (go == null || go.transform == null)
                        return;

                    Spline spline = go.transform.GetComponent<Spline>();

                    if (spline == null)
                        return;

                    //Dont record undo when no spline was selected. Else deformations/followers will get the wrong position after doing ctrl + z.
                    if(selectedSpline == null)
                    {
                        spline.selectedAnchors.Clear();
                        spline.selectedControlPoint = 0;
                    }
                    else
                    {
                        EHandleUndo.RecordNow(spline, "Selected spline");
                        //Also need to record the transform becouse of transform.position change
                        EHandleUndo.RecordNow(spline.transform);
                        spline.selectedAnchors.Clear();
                        spline.selectedControlPoint = 0;
                    }

                    EActionDelayed.Add(() =>
                    {
                        WindowBase.RepaintAll();
                    }, 0, 0, EActionDelayed.ActionFlag.FRAMES | EActionDelayed.ActionFlag.LATE);
                }
            }
            else if(Event.current != null && Event.current.type == EventType.DragPerform)
            {
                markForForceUpdate = true;
            }
        }

        internal static void OnUndo()
        {
            Transform selection = TryGetSelectionTransform();

            if (selection == null)
                return;

            UpdateSelection(selection);
        }

        private static void UpdateSelection(Transform newSelection)
        {
            WindowBase.RepaintAll();

            if (stopNextUpdateSelection)
            {
                stopNextUpdateSelection = false;
                return;
            }

            if (stopUpdateSelection)
                return;

            selectedSpline = null;
            selectedSplines.Clear();
            selectedSplineObject = null;
            selectedSplineObjects.Clear();
            selectedSplineConnector = null;
            selectedSplineConnectors.Clear();
            EHandleEvents.selectedSpline = null;
            EHandleEvents.isSplineObjectSelected = false;
            EHandleEvents.isSplineConnectorSelected = false;

            if (newSelection == null)
                return;

            Spline spline = TryFindSpline(newSelection);
            SplineObject so = newSelection.GetComponent<SplineObject>();
            SplineConnector sc = newSelection.GetComponent<SplineConnector>();

            //Select Spline
            if (so == null)
            {
                if (spline != null && spline.IsEnabled())
                {
                    selectedSpline = spline;
                    EHandleEvents.selectedSpline = selectedSpline;
                }

                foreach (Object o in Selection.objects)
                {
                    GameObject go = o as GameObject;

                    if(go == null)
                        continue;

                    Spline spline2 = go.GetComponent<Spline>();

                    if (spline2 != null && spline2 != selectedSpline)
                    {
                        if (selectedSplines.Contains(spline2))
                            continue;

                        selectedSplines.Add(spline2);
                    }
                }
            }
            //Select SplineObject
            else if (so != null)
            {
                if (!so.IsEnabled())
                    return;

                //Inactive SplineObjects can be created using scripts. We should not select them if the user trys to.
                if (so.SplineParent != null && !so.SplineParent.ContainsSplineObject(so))
                    return;

                selectedSplineObject = so;
                EHandleEvents.isSplineObjectSelected = true;

                if (spline != null)
                {
                    selectedSpline = spline;
                    EHandleEvents.selectedSpline = selectedSpline;

                    //Needs to deslect becouse if the user select an object in the hirarcy menu.
                    EHandleUndo.RecordNow(spline, "Selected spline object");
                    spline.selectedAnchors.Clear();
                    spline.selectedControlPoint = 0;

                    if (spline.segments.Count > 1)
                        EHandleTool.ActivatePositionToolForSplineObject(spline, so);
                }

                foreach (Object o in Selection.objects)
                {
                    GameObject go = o as GameObject;

                    if (go == null)
                        continue;

                    SplineObject so2 = go.GetComponent<SplineObject>();

                    if (so2 != null && so2 != selectedSplineObject)
                    {
                        selectedSplineObjects.Add(so2);

                        if (so2.SplineParent == null)
                            continue;

                        if(!selectedSplines.Contains(so2.SplineParent) && so2.SplineParent != selectedSpline)
                            selectedSplines.Add(so2.SplineParent);
                    }
                }
            }

            if(sc != null)
            {
                selectedSplineConnector = sc;
                EHandleEvents.isSplineConnectorSelected = true;

                foreach (Object o in Selection.objects)
                {
                    GameObject go = o as GameObject;

                    if (go == null)
                        continue;

                    SplineConnector sc2 = go.GetComponent<SplineConnector>();
                    if (sc2 != null && sc2 != selectedSplineConnector)
                        selectedSplineConnectors.Add(sc2);
                }
            }

            Spline TryFindSpline(Transform transform)
            {
                Spline spline2 = transform.GetComponent<Spline>();

                for (int i = 0; i < 25; i++)
                {
                    if (spline2 != null)
                        return spline2;

                    if (transform.parent == null)
                        break;

                    transform = transform.parent;
                    spline2 = transform.GetComponent<Spline>();
                }

                return null;
            }
        }

        internal static bool TryUpdateHoveredSpline(Event e, HashSet<Spline> splines, bool multiselectActive)
        {
            hoveredSpline = null;

            if (EGlobalSettings.GetSplineHideMode() > 0)
                return false;

            if (EHandleSpline.controlPointCreationActive)
                return false;

            mouseRay = EMouseUtility.GetMouseRay(e.mousePosition);
            float closestDistance = 99999999;
            Vector3 mousePoint = Vector3.zero;
            Spline hovered = null;

            foreach (Spline spline in splines)
            {
                if (spline == null)
                    continue;

                if (spline.transform == null)
                    continue;

                if (!spline.IsEnabled())
                    continue;

                if (spline.segments.Count < 2)
                    continue;

                if (spline.IsPickingDisabled() || spline.IsHiddenInSceneView())
                    continue;

                //If selected spline
                if (spline == selectedSpline)
                {
                    //Skip if ctrl is not hold down
                    if (!multiselectActive)
                        continue;

                    //Skip if so is selected
                    if (selectedSplineObject != null && selectedSplineObject.SplineParent == spline)
                        continue;

                    //Skip if control point is selected
                    if (selectedSpline == spline && spline.selectedControlPoint != 0)
                        continue;
                }

                if (!spline.bounds.IntersectRay(mouseRay))
                    continue;

                mousePoint = EHandleSpline.ClosestMousePoint(spline, mouseRay, 12, out float distance, out float time, 20);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    hovered = spline;

                    float distanceCheck = EHandleSegment.DistanceModifier(mousePoint) * (0.3f + (spline.Width * 0.1f));
                    if (distanceCheck < 0.025f) distanceCheck = 0.025f;

                    if (closestDistance < distanceCheck)
                    {
                        hoveredSpline = hovered;
                    }
                }
            }

            if (oldHoveredSpline != hoveredSpline)
            {
                oldHoveredSpline = hoveredSpline;
                EHandleSceneView.RepaintCurrent();
            }

            if (hoveredSpline != null)
                return true;

            return false;
        }

        internal static void TrySelectHoveredSpline(Event e, bool multiselectActive)
        {
            if (hoveredSpline == null)
                return;

            if (multiselectActive)
            {
                selection.Clear();
                selection.AddRange(Selection.objects);

                if (selectedSpline == hoveredSpline || selectedSplines.Contains(hoveredSpline))
                    selection.Remove(hoveredSpline.gameObject);
                else
                {
                    selection.Add(hoveredSpline.gameObject);
                    Selection.activeTransform = hoveredSpline.transform;
                }

                Selection.objects = selection.ToArray();
            }
            else
            {
                Selection.objects = null;
                Selection.activeTransform = hoveredSpline.transform;
            }

            e.Use();

            EHandleUndo.RecordNow(hoveredSpline, "Select Spline");
            hoveredSpline.selectedAnchors.Clear();
            hoveredSpline.selectedControlPoint = 0;

            if (selectedSpline == null)
                return;

            EHandleUndo.RecordNow(selectedSpline, "Select Spline");
            selectedSpline.selectedAnchors.Clear();
            selectedSpline.selectedControlPoint = 0;
        }

        internal static bool TryUpdateHoveredControlPoint(Event e, Spline spline, SceneView sceneView)
        {
            if (spline == null)
                return false;

            hoveredCp = 0;

            Ray mouseRay = EMouseUtility.GetMouseRay(e.mousePosition);

            //Bounds for control points
            if (!spline.controlPointsBounds.IntersectRay(mouseRay))
                return false;

            Vector3 editorCameraPosition = sceneView.camera.transform.position;
            List<int> intersectingControlPoint = EHandleSpline.GetIntersectingControlPoints(spline, mouseRay);

            float distanceCheck = 999999;

            foreach(int i in intersectingControlPoint)
            {
                if (spline.selectedControlPoint == i)
                    continue;

                int segmentId = SplineUtility.ControlPointIdToSegmentIndex(i);
                ControlHandle controlHandle = SplineUtility.GetControlHandleType(i);
                InterpolationType interpolationMode = spline.segments[segmentId].GetInterpolationType();

                if (interpolationMode == InterpolationType.LINE && controlHandle != ControlHandle.ANCHOR)
                    continue;

                float distanceToCamera = Vector3.Distance(spline.segments[segmentId].GetPosition(controlHandle), editorCameraPosition);

                if (distanceToCamera < distanceCheck)
                {
                    distanceCheck = distanceToCamera;
                    hoveredCp = i;
                }
            }

            if (oldHoveredCp != hoveredCp)
            {
                oldHoveredCp = hoveredCp;
                hoveredSpline = null;
                EHandleSceneView.RepaintCurrent();
            }

            if (hoveredCp != 0)
                return true;

            return false;
        }

        internal static void TrySelectHoveredControlPoint(Spline spline, Event e, bool multiselectActive)
        {
            if (hoveredCp == 0)
                return;

            if (spline == null || !spline.IsEnabled())
                return;

            //Record new control point selection
            EHandleUndo.RecordNow(spline, "Selected/Deselect control point: " + hoveredCp);
            if (multiselectActive && SplineUtility.GetControlHandleType(hoveredCp) == ControlHandle.ANCHOR &&
                            SplineUtility.GetControlHandleType(spline.selectedControlPoint) == ControlHandle.ANCHOR)
            {
                if (spline.selectedAnchors.Contains(hoveredCp))
                    spline.selectedAnchors.Remove(hoveredCp);
                else if (!spline.selectedAnchors.Contains(spline.selectedControlPoint))
                {
                    spline.selectedAnchors.Add(spline.selectedControlPoint);
                    spline.selectedControlPoint = hoveredCp;
                }
            }
            else
            {
                spline.selectedControlPoint = hoveredCp;
                spline.selectedAnchors.Clear();
            }

            Selection.objects = null;
            Selection.activeTransform = spline.transform;

            selectedSplines.Clear();
            selectedSplineObjects.Clear();
            selectedSplineObject = null;

            //Inactivate the next mouseDown and mouseUp event DuringSceneGUI.
            e.Use();

            EHandleTool.ActivatePositionToolForControlPoint(spline);

            WindowBase.RepaintAll();
        }

        public static Transform TryGetSelectionTransform()
        {
            if (Selection.activeTransform != null)
            {
                return Selection.activeTransform;
            }
            else if (Selection.activeObject != null)
            {
                GameObject gameObject = Selection.activeObject as GameObject;
                if (gameObject != null && gameObject.transform != null)
                {
                    return gameObject.transform;
                }
            }

            return null;
        }

        public static void MarkForForceUpdate()
        {
            markForForceUpdate = true;
        }

        public static void ForceUpdate()
        {
            UpdateSelection(TryGetSelectionTransform());
        }

        public static void SelectPrimaryControlPoint(Spline spline, int controlpointId)
        {
            selectedSplineObject = null;
            selectedSplineObjects.Clear();
            Selection.activeTransform = spline.transform;
            spline.selectedControlPoint = controlpointId;
            EHandleTool.ActivatePositionToolForControlPoint(spline);
        }

        public static void SelectSecondaryAnchors(Spline spline, int[] anchors)
        {
            spline.selectedAnchors.Clear();
            foreach (int a in anchors)
            {
                spline.selectedAnchors.Add(a);
            }
        }

        public static bool IsPrimiarySelection(Segment segment)
        {
            if (segment == null)
                return false;

            if (selectedSpline == null)
                return false;

            if (selectedSpline.selectedControlPoint == 0)
                return false;

            Segment selectedSegment = selectedSpline.segments[SplineUtility.ControlPointIdToSegmentIndex(selectedSpline.selectedControlPoint)];

            return selectedSegment == segment;
        }

        public static bool IsSecondarySelection(Segment segment)
        {
            if (segment == null)
                return false;

            if (selectedSpline == null)
                return false;

            if (selectedSpline.selectedAnchors.Count <= 0)
                return false;

            foreach (int segmentIndex in selectedSpline.selectedAnchors)
            {
                if (selectedSpline.segments.Count <= segmentIndex)
                    continue;

                Segment selectedSegment = selectedSpline.segments[SplineUtility.ControlPointIdToSegmentIndex(segmentIndex)];
                if (selectedSegment == segment)
                    return true;
            }

            return false;
        }

        public static bool IsPrimiarySelection(Spline spline)
        {
            if (spline == selectedSpline)
                return true;

            return false;
        }

        public static bool IsSecondarySelection(Spline spline)
        {
            if (selectedSplines.Contains(spline))
                return true;

            return false;
        }

        public static bool IsConnectedToSelection(Spline spline)
        {
            if(selectedSpline != null)
            {
                if (spline == selectedSpline)
                    return false;

                foreach(Segment s in selectedSpline.segments)
                {
                    if (s.linkTarget != LinkTarget.ANCHOR || s.LinkCount == 0)
                        continue;

                    for (int i = 0; i < s.LinkCount; i++)
                    {
                        Segment link = s.GetLinkAtIndex(i);

                        if (link.splineParent == selectedSpline)
                            continue;

                        if (link.splineParent == spline)
                        {
                            return true;
                        }
                    }
                }
            }

            foreach(Spline spline2 in selectedSplines)
            {
                if (spline == spline2)
                    return false;

                foreach (Segment s in spline2.segments)
                {
                    if (s.linkTarget != LinkTarget.ANCHOR || s.LinkCount == 0)
                        continue;

                    for (int i = 0; i < s.LinkCount; i++)
                    {
                        Segment link = s.GetLinkAtIndex(i);

                        if (link.splineParent == spline2)
                            continue;

                        if (link.splineParent == spline)
                            return true;
                    }
                }
            }
            
            if(selectedSplineConnector != null)
            {
                for (int i = 0; i < selectedSplineConnector.ConnectionCount; i++)
                {
                    Segment s = selectedSplineConnector.GetConnectionAtIndex(i);
                    if (s.splineParent == spline)
                        return true;
                }
            }

            if (selectedSplineConnectors.Count > 0)
            {
                foreach (SplineConnector sc in selectedSplineConnectors)
                {
                    for (int i = 0; i < sc.ConnectionCount; i++)
                    {
                        Segment s = sc.GetConnectionAtIndex(i);
                        if (s.splineParent == spline)
                            return true;
                    }
                }
            }

            return false;
        }

        public static bool IsChildOfSelected(Spline spline)
        {
            Transform selected = Selection.activeTransform;

            if (selected == null)
                return false;

            Transform splineTransformParent = spline.transform.parent;

            for(int i = 0; i < 25; i++)
            {
                if (splineTransformParent == null)
                    return false;

                if(selected == splineTransformParent)
                    return true;

                splineTransformParent = splineTransformParent.parent;
            }

            return false;
        }

        public static ControlHandle IsHovering(int segmentIndex)
        {
            if(SplineUtility.SegmentIndexToControlPointId(segmentIndex, ControlHandle.ANCHOR) == hoveredCp)
                return ControlHandle.ANCHOR;
            else if (SplineUtility.SegmentIndexToControlPointId(segmentIndex, ControlHandle.TANGENT_A) == hoveredCp)
                return ControlHandle.TANGENT_A;
            else if (SplineUtility.SegmentIndexToControlPointId(segmentIndex, ControlHandle.TANGENT_B) == hoveredCp)
                return ControlHandle.TANGENT_B;

            return ControlHandle.NONE;
        }

        public static void UpdatedSelectedSplinesRecordUndo(Action<Spline> action, string recordName, EHandleUndo.RecordType recordType = EHandleUndo.RecordType.RECORD_OBJECT)
        {
            EHandleUndo.RecordNow(selectedSpline, recordName, recordType);
            action.Invoke(selectedSpline);

            foreach (Spline spline2 in selectedSplines)
            {
                EHandleUndo.RecordNow(spline2, recordName, recordType);
                action.Invoke(spline2);
            }
        }

        public static void UpdatedSelectedSplines(Action<Spline> action)
        {
            action.Invoke(selectedSpline);
            foreach (Spline spline2 in selectedSplines) action.Invoke(spline2);
        }

        public static void UpdatedSelectedSplineObjectsRecordUndo(Action<SplineObject> action, string recordName, bool recordTransform = false, EHandleUndo.RecordType recordType = EHandleUndo.RecordType.RECORD_OBJECT)
        {
            Object objectToRecord = selectedSplineObject;

            if (recordTransform)
                objectToRecord = selectedSplineObject.transform;

            EHandleUndo.RecordNow(objectToRecord, recordName, recordType);
            action.Invoke(selectedSplineObject);

            foreach (SplineObject so2 in selectedSplineObjects)
            {
                Object objectToRecord2 = so2;

                if (recordTransform)
                    objectToRecord2 = so2.transform;

                EHandleUndo.RecordNow(objectToRecord2, recordName, recordType);
                action.Invoke(so2);
            }
        }

        public static void UpdatedSelectedSplineObjects(Action<SplineObject> action)
        {
            if (selectedSplineObject == null)
                return;

            action.Invoke(selectedSplineObject);

            foreach (SplineObject so2 in selectedSplineObjects)
            {
                action.Invoke(so2);
            }
        }

        public static void UpdateSelectedAnchors(Spline spline, Action<Segment> action)
        {
            action.Invoke(spline.segments[SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint)]);
            foreach (int i in spline.selectedAnchors)
                action.Invoke(spline.segments[SplineUtility.ControlPointIdToSegmentIndex(i)]);
        }

        public static void UpdateSelectedAnchorsRecordUndo(Spline spline, Action<Segment> action, string recordName, EHandleUndo.RecordType recordType = EHandleUndo.RecordType.RECORD_OBJECT)
        {
            EHandleUndo.RecordNow(spline, recordName, recordType);
            action.Invoke(spline.segments[SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint)]);
            foreach (int i in spline.selectedAnchors)
                action.Invoke(spline.segments[SplineUtility.ControlPointIdToSegmentIndex(i)]);
        }

        public static void GetAllSelectedSplinesNonAlloc(List<Spline> allSplines)
        {
            if(selectedSpline != null)
                allSplines.Add(selectedSpline);

            if (selectedSplines != null && selectedSplines.Count > 0)
                allSplines.AddRange(selectedSplines);
        }
    }
}