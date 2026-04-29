// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleEvents.cs
//
// Author: Mikael Danielsson
// Date Created: 04-02-2024
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SplineArchitect
{
    internal static class EHandleEvents
    {
        internal static bool updateSelection;
        internal static bool sceneIsClosing;
        internal static bool sceneIsLoadedPlaymode;
        internal static bool buildRunning;
        internal static bool dragActive;
        internal static bool undoActive;
        internal static bool undoWasRedo;
        internal static bool controlPointCreationActive;
        internal static bool isSplineConnectorSelected;
        internal static bool isSplineObjectSelected;
        internal static Spline selectedSpline;
        internal static PlayModeStateChange playModeStateChange;

        private static List<Spline> markedInfoUpdates = new List<Spline>();
        private static List<Spline> InitalizeAfterDragSplines = new List<Spline>();
        private static List<SplineConnector> InitalizeAfterDragSplineConnectors = new List<SplineConnector>();
        private static List<SplineObject> InitalizeAfterDragSplineObjects = new List<SplineObject>();

        //Events
        internal static event Action OnFirstUpdate;
        internal static event Action OnUpdateEarly;
        internal static event Action<Event, bool> OnWindowSplineGUI;
        internal static event Action<Event, bool> OnWindowControlPointGUI;
        internal static event Action<Spline, Vector3> OnTransformToCenter;
        internal static event Action<Spline> OnUndoSelectedSplines;
        internal static event Action<Spline> OnInitalizeSplineEditor;
        internal static event Action<Spline> OnDestroySpline;
        internal static event Action<Spline> OnSplineJoin;
        internal static event Action<Spline> OnSplineReverse;
        internal static event Action<Spline> OnSplineLoop;
        internal static event Action<Spline> OnSplineFlatten;
        internal static event Action<Spline, Spline> OnSplineSplit;
        internal static event Action<Spline> OnSplineCopied;
        internal static event Action<Spline, SplineObject> OnSplineObjectSCeneGUI;
        internal static event Action<Spline> AfterSegmentRemoved;
        internal static event Action<Segment> AfterSegmentCreated;
        internal static event Action<Segment, ControlHandle> OnSegmentMovement;
        internal static event Action<Segment> OnSegmentRemoved;
        internal static event Action<Segment> OnSegmentLinked;
        internal static event Action<Segment> OnSegmentFlatten;
        internal static event Action<SplineConnector, Segment> OnSplineConnectorAlignSegment;
        internal static event Action<SplineObject> AfterSplineObjectActivatePositionTool;
        internal static event Action<SplineObject> OnSplineObjectParentChanged;
        internal static event Action<SplineObject> AfterSplineOnbjectSetPositionInUi;
        internal static event Action<Object> OnAssetImported;

        internal static void InitAfterDrag(SceneView sceneView)
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                dragActive = true;
            }
            else if (Event.current.type == EventType.DragPerform || Event.current.type == EventType.DragExited)
            {
                dragActive = false;
            }

            if (Event.current.type == EventType.DragPerform)
            {
                foreach (Spline spline in InitalizeAfterDragSplines)
                {
                    if (spline == null)
                        continue;

                    spline.Initalize();
                }

                foreach (SplineConnector sc in InitalizeAfterDragSplineConnectors)
                {
                    if (sc == null)
                        continue;

                    sc.Initalize();
                }

                foreach (SplineObject so in InitalizeAfterDragSplineObjects)
                {
                    if (so == null)
                        continue;

                    so.Initalize();
                }

                InitalizeAfterDragSplines.Clear();
                InitalizeAfterDragSplineObjects.Clear();
            }
        }

        internal static void InvokeFirstUpdate()
        {
            OnFirstUpdate?.Invoke();
        }

        internal static void InvokeUpdateEarly()
        {
            OnUpdateEarly?.Invoke();
        }

        internal static void InvokeSegmentRemoved(Segment segment)
        {
            OnSegmentRemoved?.Invoke(segment);
        }

        internal static void InvokeSegmentFlatten(Segment segment)
        {
            OnSegmentFlatten?.Invoke(segment);
        }

        internal static void InvokeSplineConnectorAlignSegment(SplineConnector splineConnector, Segment segment)
        {
            OnSplineConnectorAlignSegment?.Invoke(splineConnector, segment);
        }

        internal static void InvokeSegmentLinked(Segment segment)
        {
            OnSegmentLinked?.Invoke(segment);
        }

        internal static void InvokeAfterSegmentRemoved(Spline spline)
        {
            AfterSegmentRemoved?.Invoke(spline);
        }

        internal static void InvokeAfterSegmentCreated(Segment segment)
        {
            AfterSegmentCreated?.Invoke(segment);
        }

        internal static void InvokeTransformToCenter(Spline spline, Vector3 dif)
        {
            OnTransformToCenter?.Invoke(spline, dif);
        }

        internal static void InvokeUndoSelection(Spline spline)
        {
            OnUndoSelectedSplines?.Invoke(spline);
        }

        internal static void InvokeInitalizeSplineEditor(Spline spline)
        {
            OnInitalizeSplineEditor?.Invoke(spline);
        }

        internal static void InvokeSplineSplit(Spline spline, Spline newSpline)
        {
            OnSplineSplit?.Invoke(spline, newSpline);
        }

        internal static void InvokeSegmentMovement(Segment segment, ControlHandle controlHandle)
        {
            OnSegmentMovement?.Invoke(segment, controlHandle);
        }

        internal static void InvokeSplineJoin(Spline spline)
        {
            OnSplineJoin?.Invoke(spline);
        }

        internal static void InvokeSplineLoop(Spline spline)
        {
            OnSplineLoop?.Invoke(spline);
        }

        internal static void InvokeSplineFlatten(Spline spline)
        {
            OnSplineFlatten?.Invoke(spline);
        }

        internal static void InvokeSplineReverse(Spline spline)
        {
            OnSplineReverse?.Invoke(spline);
        }

        internal static void InvokeDestroySpline(Spline spline)
        {
            OnDestroySpline?.Invoke(spline);
        }

        internal static void InvokeSplineCopied(Spline spline)
        {
            OnSplineCopied?.Invoke(spline);
        }

        internal static void InvokeSplineObjectParentChanged(SplineObject so)
        {
            OnSplineObjectParentChanged?.Invoke(so);
        }

        internal static void InvokeSplineObjectSceneGUI(Spline spline, SplineObject splineObject)
        {
            OnSplineObjectSCeneGUI?.Invoke(spline, splineObject);
        }

        internal static void InvokeAfterSplineObjectActivatePositionTool(SplineObject splineObject)
        {
            AfterSplineObjectActivatePositionTool?.Invoke(splineObject);
        }

        internal static void InvokeWindowSplineGUI(Event e, bool leftMouseUp)
        {
            OnWindowSplineGUI?.Invoke(e, leftMouseUp);
        }

        internal static void InvokeWindowControlPointGUI(Event e,bool leftMouseUp)
        {
            OnWindowControlPointGUI?.Invoke(e, leftMouseUp);
        }

        internal static void InvokeAfterSplineObjectSetPositionInUi(SplineObject splineObject)
        {
            AfterSplineOnbjectSetPositionInUi?.Invoke(splineObject);
        }

        internal static void InvokeAssetImported(Object obj)
        {
            OnAssetImported?.Invoke(obj);
        }

        internal static void MarkForInfoUpdate(Spline spline)
        {
            if (markedInfoUpdates.Contains(spline))
                return;

            markedInfoUpdates.Add(spline);
        }

        internal static List<Spline> GetMarkedForInfoUpdates()
        {
            return markedInfoUpdates;
        }

        internal static void ClearMarkedForInfoUpdates()
        {
            markedInfoUpdates.Clear();
        }

        internal static void ForceUpdateSelection()
        {       
            updateSelection = true;
        }

        internal static void InitalizeAfterDrag(Spline spline)
        {
            if (InitalizeAfterDragSplines.Contains(spline))
                return;

            InitalizeAfterDragSplines.Add(spline);
        }

        internal static void InitalizeAfterDrag(SplineConnector splineConnector)
        {
            if (InitalizeAfterDragSplineConnectors.Contains(splineConnector))
                return;

            InitalizeAfterDragSplineConnectors.Add(splineConnector);
        }

        internal static void InitalizeAfterDrag(SplineObject splineObject)
        {
            if (InitalizeAfterDragSplineObjects.Contains(splineObject))
                return;

            InitalizeAfterDragSplineObjects.Add(splineObject);
        }
    }
}

#endif
