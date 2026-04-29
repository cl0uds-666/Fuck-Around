// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleSegment.cs
//
// Author: Mikael Danielsson
// Date Created: 03-03-2024
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using SplineArchitect.Utility;
using SplineArchitect.Ui;

namespace SplineArchitect
{
    public class EHandleSegment
    {
        public struct SegmentIndicatorData
        {
            public Vector3 anchor;
            public Vector3 tangent;
            public Vector3 newAnchor;
            public Vector3 newTangentA;
            public Vector3 newTangentB;
            public bool originFromStart;

            //Only grid
            public Spline gridSpline;
        }

        private static List<int> markedSegments = new List<int>();
        private static Plane projectionPlane = new Plane(Vector3.up, Vector3.zero);
        private static Vector3[] normalsContainer = new Vector3[3];

        internal static void LinkMovementAll(Spline spline)
        {
            foreach (Segment s in spline.segments)
            {
                LinkMovement(s);
            }
        }

        internal static void LinkMovement(Segment s)
        {
            if (s.LinkCount == 0)
                return;

            Vector3 newPosition = s.GetPosition(ControlHandle.ANCHOR);

            for (int i = 0; i < s.LinkCount; i++)
            {
                Segment link = s.GetLinkAtIndex(i);

                if (link == s)
                    continue;

                if (link.localSpace == null)
                {
                    Debug.LogWarning("[Spline Architect] Segment has no local space set.");
                    continue;
                }

                Vector3 dif = link.GetPosition(ControlHandle.ANCHOR) - newPosition;

                EHandleUndo.RecordNow(link.SplineParent);
                link.Translate(ControlHandle.ANCHOR, dif);
                link.Translate(ControlHandle.TANGENT_A, dif);
                link.Translate(ControlHandle.TANGENT_B, dif);

                if (link.SplineParent.Loop && link.SplineParent.segments[0] == link)
                {
                    int last = link.SplineParent.segments.Count - 1;

                    link.SplineParent.segments[last].Translate(ControlHandle.ANCHOR, dif);
                    link.SplineParent.segments[last].Translate(ControlHandle.TANGENT_A, dif);
                    link.SplineParent.segments[last].Translate(ControlHandle.TANGENT_B, dif);
                }
            }
        }

        internal static void SegmentMovement(Spline spline, Segment segment, ControlHandle controlHandle, Vector3 newPosition)
        {
            //In some cases when working with prefabs the spline parent can be null. So we set it here before handling the segment.
            segment.splineParent = spline;

            ControlHandleType handleType = EGlobalSettings.GetHandleType();

            if (handleType == ControlHandleType.CONTINUOUS)
            {
                if (controlHandle == ControlHandle.ANCHOR)
                {
                    Vector3 dif = segment.GetPosition(ControlHandle.ANCHOR) - newPosition;
                    EHandleSelection.UpdateSelectedAnchors(spline, (selected) =>
                    {
                        selected.TranslateAnchor(dif);
                        LinkMovement(selected);
                    });
                }
                else
                {
                    segment.SetContinuousPosition(controlHandle, newPosition);
                }
            }
            else if (handleType == ControlHandleType.MIRRORED)
            {
                if (controlHandle == ControlHandle.ANCHOR)
                {
                    Vector3 dif = segment.GetPosition(ControlHandle.ANCHOR) - newPosition;
                    EHandleSelection.UpdateSelectedAnchors(spline, (selected) =>
                    {
                        selected.TranslateAnchor(dif);
                        LinkMovement(selected);
                    });
                }
                else
                {
                    segment.SetMirroredPosition(controlHandle, newPosition);
                }
            }
            else if (handleType == ControlHandleType.BROKEN)
            {
                if (controlHandle == ControlHandle.ANCHOR)
                {
                    Vector3 dif = segment.GetPosition(ControlHandle.ANCHOR) - newPosition;
                    EHandleSelection.UpdateSelectedAnchors(spline, (selected) =>
                    {
                        selected.TranslateAnchor(dif);
                        LinkMovement(selected);
                    });
                }
                else
                    segment.SetBrokenPosition(controlHandle, newPosition);
            }

            EHandleEvents.InvokeSegmentMovement(segment, controlHandle);
        }

        internal static void UpdateIndicator3DGrid(Spline spline, Event e, ref SegmentIndicatorData segementIndicatorData)
        {
            Vector3 direction = Vector3.forward;
            Vector3 anchor = Vector3.zero;
            Vector3 tangent = Vector3.zero;
            bool start = false;

            Ray mouseRay = EMouseUtility.GetMouseRay(e.mousePosition);

            projectionPlane.SetNormalAndPosition(spline.transform.up, spline.transform.TransformPoint(spline.gridCenterPoint));
            if (projectionPlane.Raycast(mouseRay, out float enter))
            {
                Vector3 point = mouseRay.GetPoint(enter);

                direction = -spline.segments[spline.segments.Count - 1].GetDirection();
                anchor = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
                tangent = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.TANGENT_A);

                float distanceToStart = Vector3.Distance(spline.segments[0].GetPosition(ControlHandle.ANCHOR) + direction, point);
                float distanceToEnd = Vector3.Distance(spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR) - direction, point);
                start = distanceToStart < distanceToEnd;

                if (spline.segments.Count == 1)
                    start = !start;

                if (start)
                {
                    direction = -spline.segments[0].GetDirection();
                    anchor = spline.segments[0].GetPosition(ControlHandle.ANCHOR);
                    tangent = spline.segments[0].GetPosition(ControlHandle.TANGENT_B);
                }
            }

            projectionPlane.SetNormalAndPosition(spline.transform.up, anchor);
            if (projectionPlane.Raycast(mouseRay, out float enter2))
            {
                Vector3 point = mouseRay.GetPoint(enter2);

                point = EHandleGrid.SnapPoint(spline, point);

                Vector3 direction90 = Vector3.Cross(direction, spline.transform.up);
                Vector3 closestPoint = Utility.LineUtility.GetNearestPoint(anchor, direction, point, out _);
                Utility.LineUtility.GetNearestPoint(anchor, direction90, point, out float time);
                float sign = Mathf.Sign(time) * (start ? -1 : 1);
                direction90 = direction90 * sign;

                segementIndicatorData.gridSpline = spline;
                segementIndicatorData.anchor = anchor;
                segementIndicatorData.tangent = tangent;
                segementIndicatorData.newAnchor = point;
                segementIndicatorData.originFromStart = start;

                if (GeneralUtility.IsEqual(closestPoint, point, 0.1f))
                {
                    Vector3 newTa = point + direction * Vector3.Distance(anchor, tangent);
                    newTa = EHandleGrid.SnapPoint(spline, newTa);

                    Vector3 newTb = point - direction * Vector3.Distance(anchor, tangent);
                    newTb = EHandleGrid.SnapPoint(spline, newTb);

                    segementIndicatorData.newTangentA = newTa;
                    segementIndicatorData.newTangentB = newTb;
                }
                else
                {
                    Vector3 newTa = point + direction90 * Vector3.Distance(anchor, tangent);
                    newTa = EHandleGrid.SnapPoint(spline, newTa);

                    Vector3 newTb = point - direction90 * Vector3.Distance(anchor, tangent);
                    newTb = EHandleGrid.SnapPoint(spline, newTb);

                    segementIndicatorData.newTangentA = newTa;
                    segementIndicatorData.newTangentB = newTb;
                }

                if ((start && spline.segments[0].GetInterpolationType() == InterpolationType.LINE) ||
                    (!start && spline.segments[spline.segments.Count - 1].GetInterpolationType() == InterpolationType.LINE))
                {
                    segementIndicatorData.tangent = anchor;
                    segementIndicatorData.newTangentA = point;
                    segementIndicatorData.newTangentB = point;
                }
            }
        }

        internal static void UpdateIndicator3D(Spline spline, Event e, ref SegmentIndicatorData segementIndicatorData)
        {
            //Data
            float disBetweenTangents = 10f;
            Vector3 direction = -spline.segments[spline.segments.Count - 1].GetDirection();
            Vector3 anchor = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
            Vector3 tangent = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.TANGENT_A) + direction * DistanceModifier(spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.TANGENT_A), disBetweenTangents);

            //Try hit terrain
            Vector3 anchorHitPoint = Vector3.zero;
            bool hitTerrain = false;
            RaycastHit hit;
            Ray mouseRay = EMouseUtility.GetMouseRay(e.mousePosition);
            if (Physics.Raycast(mouseRay, out hit))
            {
                anchorHitPoint = hit.point;
                hitTerrain = true;
            }
            else
            {
                Vector3 center = spline.GetCenter();
                projectionPlane.SetNormalAndPosition(Vector3.up, new Vector3(0, center.y, 0));
                if (!hitTerrain && projectionPlane.Raycast(mouseRay, out float enter))
                    anchorHitPoint = mouseRay.GetPoint(enter);
            }

            //Closest to start or end
            float disLastSegement = Vector3.Distance(spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR) + direction, anchorHitPoint);
            float disFirstSegement = Vector3.Distance(spline.segments[0].GetPosition(ControlHandle.ANCHOR) - direction, anchorHitPoint);
            bool start = disLastSegement > disFirstSegement;

            //Get y height of start or end for project plane
            Vector3 planPos = start ? spline.segments[0].GetPosition(ControlHandle.ANCHOR) : spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
            projectionPlane.SetNormalAndPosition(Vector3.up, new Vector3(0, planPos.y, 0));
            if (!hitTerrain && projectionPlane.Raycast(mouseRay, out float enter2))
                anchorHitPoint = mouseRay.GetPoint(enter2);

            //If start switch data direction
            if (start)
            {
                direction = -spline.segments[0].GetDirection();
                anchor = spline.segments[0].GetPosition(ControlHandle.ANCHOR);
                tangent = spline.segments[0].GetPosition(ControlHandle.TANGENT_B) - direction * DistanceModifier(spline.segments[0].GetPosition(ControlHandle.TANGENT_B), disBetweenTangents);
            }

            //Set control point data
            Vector3 newAnchor = anchorHitPoint;
            Vector3 cubicLerpPos1 = BezierUtility.Cubic(anchor, tangent, newAnchor, newAnchor, 0.99f);
            Vector3 cubicLerpPos2 = BezierUtility.Cubic(anchor, tangent, newAnchor, newAnchor, 1);
            Vector3 newEndDirection = (cubicLerpPos1 - cubicLerpPos2).normalized;
            Vector3 newFlatEndDirection = (new Vector3(cubicLerpPos1.x, newAnchor.y, cubicLerpPos1.z) - new Vector3(cubicLerpPos2.x, newAnchor.y, cubicLerpPos2.z)).normalized;

            //Set tangents
            float tangentDistance = DistanceModifier(newAnchor, disBetweenTangents);
            Vector3 newTangentA = newAnchor + newFlatEndDirection * (tangentDistance * (start ? 1 : -1));
            Vector3 newTangentB = newAnchor + newFlatEndDirection * (tangentDistance * (start ? -1 : 1));

            if (hitTerrain)
            {
                Vector3 editorCameraPosition = EHandleSceneView.GetCurrent().camera.transform.position;
                Vector3 origin = newAnchor + newFlatEndDirection * tangentDistance;
                Ray tangentRay = new Ray(new Vector3(origin.x, editorCameraPosition.y, origin.z), -Vector3.up);
                if (Physics.Raycast(tangentRay, out hit))
                {
                    if (start)
                    {
                        newTangentA = hit.point;
                        newTangentB = newAnchor + (newAnchor - newTangentA).normalized * tangentDistance;
                    }
                    else
                    {
                        newTangentB = hit.point;
                        newTangentA = newAnchor + (newAnchor - newTangentB).normalized * tangentDistance;
                    }
                }
            }

            //Set segment to closest spline direction
            if (EHandleModifier.CtrlActive(e))
            {
                Spline closest = SplineUtility.GetNearestSpline(anchorHitPoint, HandleRegistry.GetSplinesUnsafe(), 20 * DistanceModifier(anchorHitPoint), out float time, out _);

                if (closest != null)
                {
                    closest.GetNormalsNonAlloc(normalsContainer, time);
                    float dot = Vector3.Dot(normalsContainer[2], newEndDirection);
                    if (dot < 0)
                    {
                        newTangentA = newAnchor - normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                        newTangentB = newAnchor + normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                    }
                    else
                    {
                        newTangentA = newAnchor + normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                        newTangentB = newAnchor - normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                    }
                }
            }

            //Set indicator data
            segementIndicatorData.anchor = anchor;
            segementIndicatorData.tangent = tangent;
            segementIndicatorData.newAnchor = newAnchor;
            segementIndicatorData.newTangentA = newTangentA;
            segementIndicatorData.newTangentB = newTangentB;
            segementIndicatorData.originFromStart = start;

            //If line
            if ((start && spline.segments[0].GetInterpolationType() == InterpolationType.LINE) ||
               (!start && spline.segments[spline.segments.Count - 1].GetInterpolationType() == InterpolationType.LINE))
            {
                segementIndicatorData.tangent = anchor;
                segementIndicatorData.newTangentA = newAnchor;
                segementIndicatorData.newTangentB = newAnchor;
            }
        }

        internal static void UpdateIndicator2D(Spline spline, Event e, ref SegmentIndicatorData segementIndicatorData)
        {
            float disBetweenTangents = 10f;
            Vector3 direction = Vector3.forward;
            Vector3 anchor = Vector3.zero;
            Vector3 tangent = Vector3.zero;
            bool start = false;

            Ray mouseRay = EMouseUtility.GetMouseRay(e.mousePosition);

            Vector3 center = (spline.segments[0].GetPosition(ControlHandle.ANCHOR) + spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR)) / 2;
            projectionPlane.SetNormalAndPosition(-spline.transform.forward, center);
            if (projectionPlane.Raycast(mouseRay, out float enter))
            {
                Vector3 point = mouseRay.GetPoint(enter);

                direction = -spline.segments[spline.segments.Count - 1].GetDirection();
                anchor = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
                tangent = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.TANGENT_A);

                float distanceToStart = Vector3.Distance(spline.segments[0].GetPosition(ControlHandle.ANCHOR) + direction, point);
                float distanceToEnd = Vector3.Distance(spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR) - direction, point);
                start = distanceToStart < distanceToEnd;

                if (spline.segments.Count == 1)
                    start = !start;

                if (start)
                {
                    direction = -spline.segments[0].GetDirection();
                    anchor = spline.segments[0].GetPosition(ControlHandle.ANCHOR);
                    tangent = spline.segments[0].GetPosition(ControlHandle.TANGENT_B);
                }
            }

            Vector3 anchorHitPoint = Vector3.zero;
            projectionPlane.SetNormalAndPosition(-spline.transform.forward, anchor);
            if (projectionPlane.Raycast(mouseRay, out float enter2))
                anchorHitPoint = mouseRay.GetPoint(enter2);

            //Set control point data
            Vector3 newAnchor = anchorHitPoint;
            Vector3 cubicLerpPos1 = BezierUtility.Cubic(anchor, tangent, newAnchor, newAnchor, 0.99f);
            Vector3 cubicLerpPos2 = BezierUtility.Cubic(anchor, tangent, newAnchor, newAnchor, 1);
            Vector3 newEndDirection = (cubicLerpPos1 - cubicLerpPos2).normalized;

            newAnchor = SnapPointToSplinePlane(newAnchor, spline.transform);
            newEndDirection = SnapDirToSplinePlane(newEndDirection, spline.transform);

            //Set tangents
            float tangentDistance = DistanceModifier(newAnchor, disBetweenTangents);
            Vector3 newTangentA = newAnchor + newEndDirection * (tangentDistance * (start ? 1 : -1));
            Vector3 newTangentB = newAnchor + newEndDirection * (tangentDistance * (start ? -1 : 1));

            newTangentA = SnapPointToSplinePlane(newTangentA, spline.transform);
            newTangentB = SnapPointToSplinePlane(newTangentB, spline.transform);

            //Set segment to closest spline direction
            if (EHandleModifier.CtrlActive(e))
            {
                Spline closest = SplineUtility.GetNearestSpline(anchorHitPoint, HandleRegistry.GetSplinesUnsafe(), DistanceModifier(anchorHitPoint) * 20, out float time, out _);

                if (closest != null)
                {
                    closest.GetNormalsNonAlloc(normalsContainer, time);
                    float dot = Vector3.Dot(normalsContainer[2], newEndDirection);
                    if (dot < 0)
                    {
                        newTangentA = newAnchor - normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                        newTangentB = newAnchor + normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                    }
                    else
                    {
                        newTangentA = newAnchor + normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                        newTangentB = newAnchor - normalsContainer[2] * DistanceModifier(newAnchor, disBetweenTangents);
                    }
                }
            }

            //Set indicator data
            segementIndicatorData.anchor = anchor;
            segementIndicatorData.tangent = tangent;
            segementIndicatorData.newAnchor = newAnchor;
            segementIndicatorData.newTangentA = newTangentA;
            segementIndicatorData.newTangentB = newTangentB;
            segementIndicatorData.originFromStart = start;

            //If line
            if ((start && spline.segments[0].GetInterpolationType() == InterpolationType.LINE) ||
               (!start && spline.segments[spline.segments.Count - 1].GetInterpolationType() == InterpolationType.LINE))
            {
                segementIndicatorData.tangent = anchor;
                segementIndicatorData.newTangentA = newAnchor;
                segementIndicatorData.newTangentB = newAnchor;
            }

            Vector3 SnapPointToSplinePlane(Vector3 worldPoint, Transform t)
            {
                var lp = t.InverseTransformPoint(worldPoint);
                lp.z = 0f;
                return t.TransformPoint(lp);
            }

            Vector3 SnapDirToSplinePlane(Vector3 worldDir, Transform t)
            {
                var ld = t.InverseTransformDirection(worldDir);
                ld.z = 0f;
                ld.Normalize();
                return t.TransformDirection(ld);
            }
        }

        internal static void UpdateIndicator2DGrid(Spline spline, Event e, ref SegmentIndicatorData segementIndicatorData)
        {
            Vector3 direction = Vector3.forward;
            Vector3 anchor = Vector3.zero;
            Vector3 tangent = Vector3.zero;
            bool start = false;

            Ray mouseRay = EMouseUtility.GetMouseRay(e.mousePosition);

            projectionPlane.SetNormalAndPosition(-spline.transform.forward, spline.transform.TransformPoint(spline.gridCenterPoint));
            if (projectionPlane.Raycast(mouseRay, out float enter))
            {
                Vector3 point = mouseRay.GetPoint(enter);

                direction = -spline.segments[spline.segments.Count - 1].GetDirection();
                anchor = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
                tangent = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.TANGENT_A);

                float distanceToStart = Vector3.Distance(spline.segments[0].GetPosition(ControlHandle.ANCHOR) + direction, point);
                float distanceToEnd = Vector3.Distance(spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR) - direction, point);
                start = distanceToStart < distanceToEnd;

                if (spline.segments.Count == 1)
                    start = !start;

                if (start)
                {
                    direction = -spline.segments[0].GetDirection();
                    anchor = spline.segments[0].GetPosition(ControlHandle.ANCHOR);
                    tangent = spline.segments[0].GetPosition(ControlHandle.TANGENT_B);
                }
            }

            projectionPlane.SetNormalAndPosition(-spline.transform.forward, anchor);
            if (projectionPlane.Raycast(mouseRay, out float enter2))
            {
                Vector3 point = mouseRay.GetPoint(enter2);

                point = EHandleGrid.SnapPoint(spline, point);

                Vector3 direction90 = Vector3.Cross(direction, spline.transform.forward);
                Vector3 closestPoint = Utility.LineUtility.GetNearestPoint(anchor, direction, point, out _);
                Utility.LineUtility.GetNearestPoint(anchor, direction90, point, out float time);
                float sign = Mathf.Sign(time) * (start ? -1 : 1);
                direction90 = direction90 * sign;

                segementIndicatorData.gridSpline = spline;
                segementIndicatorData.anchor = anchor;
                segementIndicatorData.tangent = tangent;
                segementIndicatorData.newAnchor = point;
                segementIndicatorData.originFromStart = start;

                if (GeneralUtility.IsEqual(closestPoint, point, 0.1f))
                {
                    Vector3 newTa = point + direction * Vector3.Distance(anchor, tangent);
                    newTa = EHandleGrid.SnapPoint(spline, newTa);

                    Vector3 newTb = point - direction * Vector3.Distance(anchor, tangent);
                    newTb = EHandleGrid.SnapPoint(spline, newTb);

                    segementIndicatorData.newTangentA = newTa;
                    segementIndicatorData.newTangentB = newTb;
                }
                else
                {
                    Vector3 newTa = point + direction90 * Vector3.Distance(anchor, tangent);
                    newTa = EHandleGrid.SnapPoint(spline, newTa);

                    Vector3 newTb = point - direction90 * Vector3.Distance(anchor, tangent);
                    newTb = EHandleGrid.SnapPoint(spline, newTb);

                    segementIndicatorData.newTangentA = newTa;
                    segementIndicatorData.newTangentB = newTb;
                }

                if ((start && spline.segments[0].GetInterpolationType() == InterpolationType.LINE) ||
                    (!start && spline.segments[spline.segments.Count - 1].GetInterpolationType() == InterpolationType.LINE))
                {
                    segementIndicatorData.tangent = anchor;
                    segementIndicatorData.newTangentA = point;
                    segementIndicatorData.newTangentB = point;
                }
            }
        }

        internal static void HandleDeletion(Spline spline, Event e)
        {
            if (EHandleModifier.DeleteActive(e))
            {
                MarkForDeletion(SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint));

                foreach (int i in spline.selectedAnchors)
                {
                    MarkForDeletion(SplineUtility.ControlPointIdToSegmentIndex(i));
                }

                DeleteAndUnlinkMarked(spline, true);
                spline.selectedAnchors.Clear();
            }


            //Dont delete Selection.activeTransform if controlHandle is selected.
            if (spline.selectedControlPoint > 0 && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
                e.Use();

#if UNITY_EDITOR_OSX
            //Dont delete Selection.activeTransform if controlHandle is selected.
            if (spline.selectedControlPoint > 0 && e.command && e.keyCode == KeyCode.Backspace && e.type == EventType.KeyDown)
                e.Use();
#endif
        }

        public static void MarkForDeletion(int segement)
        {
            markedSegments.Add(segement);
        }

        public static void DeleteAndUnlinkMarked(Spline spline, bool updateControlPointSelection)
        {
            markedSegments.Sort();

            for (int i = markedSegments.Count - 1; i >= 0; i--)
            {
                int segmentIndex = markedSegments[i];

                if (segmentIndex < 0 || segmentIndex >= spline.segments.Count)
                    continue;

                Segment s = spline.segments[segmentIndex];

                if(s.linkTarget != LinkTarget.NONE)
                {
                    if(s.LinkCount > 0)
                    {
                        //Unlink on other segments
                        for (int i2 = 0; i2 < s.LinkCount; i2++)
                        {
                            Segment s2 = s.GetLinkAtIndex(i2);

                            if (s2 == s)
                                continue;

                            if (s2.LinkCount > 2)
                                continue;

                            EHandleUndo.RecordNow(s2.SplineParent, "Delete segement: " + segmentIndex);
                            s2.linkTarget = LinkTarget.NONE;
                        }
                    }

                    if(s.SplineConnector != null)
                    {
                        s.SplineConnector.RemoveConnection(s);
                    }
                }

                //If deleteing an Spline very fast after selecting it, the segement will be -333 and it will go into this if statement if "segement >= 0" is not here.
                //In this case the Spline should be deleted.
                if (spline.segments.Count > 1 && segmentIndex >= 0)
                {
                    EHandleEvents.InvokeSegmentRemoved(s);

                    EHandleUndo.RecordNow(spline, "Delete segement: " + segmentIndex);
                    if (segmentIndex > spline.segments.Count - 1)
                        spline.RemoveSegmentAt(spline.segments.Count - 1);
                    else
                        spline.RemoveSegmentAt(segmentIndex);

                    if (spline.Loop)
                    {
                        if(segmentIndex == 0)
                        {
                            spline.segments[spline.segments.Count - 1].SetPosition(ControlHandle.ANCHOR, spline.segments[0].GetPosition(ControlHandle.ANCHOR));
                            spline.segments[spline.segments.Count - 1].SetPosition(ControlHandle.TANGENT_A, spline.segments[0].GetPosition(ControlHandle.TANGENT_A));
                            spline.segments[spline.segments.Count - 1].SetPosition(ControlHandle.TANGENT_B, spline.segments[0].GetPosition(ControlHandle.TANGENT_B));
                        }

                        if (spline.segments.Count == 2)
                        {
                            spline.RemoveSegmentAt(spline.segments.Count - 1);
                            spline.SetLoop(false, false);
                        }
                    }

                    if (updateControlPointSelection)
                    {
                        //If last selected cp was deleted and the spline is looped we need to select the second last cp.
                        if (spline.Loop && segmentIndex >= spline.segments.Count - 1)
                            EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(spline.segments.Count - 2, ControlHandle.ANCHOR));
                        else if (segmentIndex == 0)
                            EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(0, ControlHandle.ANCHOR));
                        else if (segmentIndex < spline.segments.Count && segmentIndex > 0)
                            EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(segmentIndex, ControlHandle.ANCHOR));
                        else if (segmentIndex >= spline.segments.Count - 1)
                            EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(spline.segments.Count - 1, ControlHandle.ANCHOR));
                    }

                    EHandleEvents.InvokeAfterSegmentRemoved(spline);
                }
                else
                {
                    EHandleUndo.RecordNow(spline);
                    spline.RemoveSegmentAt(0);

                    EHandleUndo.MarkSplineForDestroy(spline);
                }
            }

            WindowBase.RepaintAll();
            markedSegments.Clear();
        }

        public static void HandleLinking(Spline spline)
        {
            for (int i = 0; i < spline.segments.Count; i++)
            {
                Segment s = spline.segments[i];

                if (s.linkTarget != s.oldLinkTarget)
                {
                    s.oldLinkTarget = s.linkTarget;

                    //In unity 2022 when appying changes to a prefab, the spline parent will be null.
                    s.splineParent = spline;

                    if (s.linkTarget == LinkTarget.ANCHOR)
                    {
                        s.LinkToAnchor(s.GetPosition(ControlHandle.ANCHOR));
                    }
                    else if (s.linkTarget == LinkTarget.SPLINE_CONNECTOR)
                    {
                        s.LinkToConnector(s.GetPosition(ControlHandle.ANCHOR));
                    }
                    else
                    {
                        s.Unlink();
                    }
                }
            }
        }

        public static void GetIndicatorExtendedData(Spline spline, Vector3 startLinePos, Vector3 endLinePos, Ray mouseRay, out Vector3 closestPoint, out float distance, out float time)
        {
            float lineLength = Vector3.Distance(spline.segments[0].GetPosition(ControlHandle.ANCHOR), spline.segments[0].GetPosition(ControlHandle.TANGENT_B));
            Vector3 startDirection = spline.segments[0].GetDirection();
            Vector3 newClosestPoint = Utility.LineUtility.GetNearestPointOnLineFromLine(startLinePos, -startDirection, mouseRay.origin, mouseRay.direction, lineLength, true);
            distance = EMouseUtility.MouseDistanceToPoint(newClosestPoint, mouseRay);
            closestPoint = newClosestPoint;
            time = 0;

            lineLength = Vector3.Distance(spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR), spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.TANGENT_A));
            Vector3 endDirection = spline.segments[spline.segments.Count - 1].GetDirection();
            newClosestPoint = Utility.LineUtility.GetNearestPointOnLineFromLine(endLinePos, endDirection, mouseRay.origin, mouseRay.direction, lineLength, true);
            float distanceToExtendedEnd = EMouseUtility.MouseDistanceToPoint(newClosestPoint, mouseRay);
            if (distanceToExtendedEnd < distance)
            {
                distance = distanceToExtendedEnd;
                closestPoint = newClosestPoint;
                time = 1;
            }
        }

        public static void CreateWithAutoSmooth(Spline spline, float time)
        {
            EHandleUndo.RecordNow(spline, "Create segement: " + spline.indicatorSegment);
            Segment segment = spline.CreateSegmentSegmentAutoSmooth(time);
            EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(segment.IndexInSpline, ControlHandle.ANCHOR));
        }

        public static void CreateFromWorldPoint(Spline spline, SegmentIndicatorData segementIndicatorData)
        {
            int segementId = 0;
            if (!segementIndicatorData.originFromStart)
                segementId = spline.segments.Count;

            EHandleUndo.RecordNow(spline, "Create segement: " + spline.indicatorSegment);
            spline.CreateSegment(segementId, segementIndicatorData.newAnchor, segementIndicatorData.newTangentA, segementIndicatorData.newTangentB);
            EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(segementId, ControlHandle.ANCHOR));

            if (!segementIndicatorData.originFromStart)
                spline.segments[spline.segments.Count - 2].SetPosition(ControlHandle.TANGENT_A, segementIndicatorData.tangent);
            else
                spline.segments[1].SetPosition(ControlHandle.TANGENT_B, segementIndicatorData.tangent);
        }

        public static void CreateExtended(Spline spline, bool createAtStart)
        {
            EHandleUndo.RecordNow(spline, "Create segement: " + spline.indicatorSegment);

            if (createAtStart)
            {
                Vector3 firstAnchor = spline.segments[0].GetPosition(ControlHandle.ANCHOR);
                Vector3 correctedTangentA = firstAnchor - (firstAnchor - spline.indicatorPosition) / 2;
                spline.segments[0].SetPosition(ControlHandle.TANGENT_B, correctedTangentA);

                Vector3 anchor = spline.indicatorPosition;
                Vector3 tangentA = correctedTangentA;
                Vector3 tangentB = spline.indicatorPosition - (spline.indicatorDirection * DistanceModifier(spline.indicatorPosition) * 12); ;

                spline.CreateSegment(0, anchor, tangentA, tangentB);
                EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(0, ControlHandle.ANCHOR));
            }
            else
            {
                Vector3 lastAnchor = spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
                Vector3 correctedTangentB =  lastAnchor - (lastAnchor - spline.indicatorPosition) / 2;
                spline.segments[spline.segments.Count - 1].SetPosition(ControlHandle.TANGENT_A, correctedTangentB);

                Vector3 anchor = spline.indicatorPosition;
                Vector3 tangentA = spline.indicatorPosition + (spline.indicatorDirection * DistanceModifier(spline.indicatorPosition) * 12);
                Vector3 tangentB = correctedTangentB;

                spline.CreateSegment(spline.segments.Count, anchor, tangentA, tangentB);
                EHandleSelection.SelectPrimaryControlPoint(spline, SplineUtility.SegmentIndexToControlPointId(spline.segments.Count - 1, ControlHandle.ANCHOR));
            }
        }

        public static void CreateSegmentsFromEditorCameraDirection(Spline spline)
        {
            Transform editorCamera = EHandleSceneView.GetCurrent().camera.transform;

            spline.transform.position = editorCamera.position + editorCamera.forward * 50;
            Vector3 anchor1 = spline.transform.position + editorCamera.transform.right * 12;
            Vector3 anchor2 = spline.transform.position - editorCamera.transform.right * 12;

            spline.CreateSegment(0, anchor1, anchor1 - editorCamera.transform.right * 5, anchor1 + editorCamera.transform.right * 5);
            spline.CreateSegment(1, anchor2, anchor2 - editorCamera.transform.right * 5, anchor2 + editorCamera.transform.right * 5);

            spline.RebuildCache();
        }

        public static float DistanceModifier(Vector3 position, float strength = 1)
        {
            SceneView sceneView = EHandleSceneView.GetCurrent();
            if (sceneView == null) return 1;

            if (sceneView.orthographic)
            {
                float orthoSize = sceneView.camera.orthographicSize;
                return 0.0133f * orthoSize * strength;
            }
            else
            {
                float distance = Vector3.Distance(sceneView.camera.transform.position, position);
                return 0.0066f * distance * strength;
            }
        }

        public static float GetControlPointSize(Vector3 position)
        {
            float controlPointSize = EGlobalSettings.GetControlPointSize();
            float distance = -1;

            SceneView sceneView = EHandleSceneView.GetCurrent();
            if (sceneView != null)
            {
                if (sceneView.orthographic)
                {
                    distance = sceneView.camera.orthographicSize;
                    controlPointSize *= 0.0133f * distance;
                }
                else
                {
                    distance = Vector3.Distance(sceneView.camera.transform.position, position);
                    float controlPointScaleDistance = EGlobalSettings.GetControlPointScaleDistance();
                    if (controlPointScaleDistance < distance) distance = controlPointScaleDistance;
                    controlPointSize *= 0.0066f * distance;
                }
            }

            return controlPointSize;
        }
    }
}
