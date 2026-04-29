// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: Spline.cs
//
// Author: Mikael Danielsson
// Date Created: 25-03-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System;

using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Mathematics;

using SplineArchitect.Utility;
using SplineArchitect.Monitor;
using SplineArchitect.Workers;

using Vector3 = UnityEngine.Vector3;
using LineUtility = SplineArchitect.Utility.LineUtility;

namespace SplineArchitect
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public partial class Spline : MonoBehaviour
    {
        public const int dataUsage = 32 +
                                     1 + 4 + 1 + 40 + 40 + 1 + 1 + 4 + 1 + 1 + MonitorSpline.dataUsage + 40 + 72 +
                                     1 + 1 + 1 + 1 + 1 + 24 + 24 + 32 + 32 + 32 + 32 + 32 + 4 + 4 + 4 + 4 + 4 + 4;

        // Stored
        [HideInInspector, SerializeField] private float length;
        [HideInInspector, SerializeField] private bool loop;
        [HideInInspector, SerializeField] private bool renderInGame;
        [HideInInspector, SerializeField] private float width = 2;
        [HideInInspector, SerializeField] private float distanceScale = 100;
        [HideInInspector, SerializeField] private bool occluded;
        [HideInInspector, SerializeField] internal List<Segment> segments;
        [HideInInspector, SerializeField] internal SplineType splineType;
        [HideInInspector, SerializeField] internal ComponentMode componentMode = ComponentMode.NONE;

        // Runtime
        [NonSerialized] private MonitorSpline monitor;
        [NonSerialized] private List<SplineObject> allSplineObjects = new List<SplineObject>();
        [NonSerialized] private List<SplineObject> rootSplineObjects = new List<SplineObject>();
        [NonSerialized] private HashSet<SplineObject> splineObjectsSet = new HashSet<SplineObject>();
        [NonSerialized] private Vector3[] normalsContainer = new Vector3[3];
        [NonSerialized] private AnimationCurve widthCurve = null;
        [NonSerialized] private Vector3 oldRenderCameraPos = new Vector3(99999, 99999, 99999);
        [NonSerialized] internal List<(SplineObject, int)> detachList = new List<(SplineObject, int)>();
        [NonSerialized] internal List<SplineObject> attachList = new List<SplineObject>();

        // Values
        /// <summary>
        /// A list of noise layers that can be added or removed to affect spline deformation.
        /// The spline will automatically detect changes to this list and update accordingly.
        /// </summary>
        [HideInInspector] public List<NoiseLayer> noises;
        /// <summary>
        /// Set what camera the spline line should use for different calculations.
        /// </summary>
        [HideInInspector] public Camera lineRenderCamera;
        [HideInInspector] public Color color = new Color(0, 0, 0, 1);
        [HideInInspector] public NoiseGroup noiseGroup = NoiseGroup.A;

        // Properties
        internal MonitorSpline Monitor => monitor;
        /// <summary>
        /// Indicates whether the spline is looping. Use spline.SetLoop() to enable or disable looping.
        /// </summary>
        public bool Loop => loop;
        /// <summary>
        /// The width of the rendered spline in the editor and in game (when RenderInGame is true).
        /// </summary>
        public float Width
        {
            get => width;
            set
            {
                if (GeneralUtility.IsEqual(width, value))
                    return;

                if (width <= 0)
                {
                    Debug.LogWarning("[SplineArchitect] Render width can't be zero or less then zero!");
                    width = 0.001f;
                }

                width = value;
                RebuildSplineLine(true);
            }
        }
        public SplineType SplineType
        {
            get => splineType;
            set
            {
                if (splineType == value)
                    return;

                splineType = value;
                MarkCacheDirty();
            }
        }
        /// <summary>
        /// The distance scale used when rendering the spline in the editor and in game (when RenderInGame is true).
        /// </summary>
        public float DistanceScale
        {
            get => distanceScale;
            set
            {
                if (GeneralUtility.IsEqual(distanceScale, value))
                    return;

                if (distanceScale < 0.999f)
                {
                    Debug.LogWarning("[SplineArchitect] Render distance scale can't be less then one!");
                    distanceScale = 1;
                }

                distanceScale = value;
                RebuildSplineLine(true);
            }
        }
        /// <summary>
        /// Controls whether the spline is occluded in the editor and in game (when RenderInGame is true).
        /// </summary>
        public bool Occluded
        {
            get => occluded;
            set
            {
                if (occluded == value)
                    return;

                if (renderMaterial != null)
                {
                    if (value) renderMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                    else renderMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
                }

                occluded = value;
                RebuildSplineLine(true);
            }
        }
        /// <summary>
        /// The LineRenderer component used for drawing the spline in Play Mode and in the built game (when RenderInGame is true).
        /// </summary>
        public LineRenderer lineRenderer { get; private set; } = null;
        /// <summary>
        /// The material the LineRenderer uses for drawing the spline in Play Mode and in the built game (when RenderInGame is true).
        /// </summary>
        public Material renderMaterial { get; private set; } = null;
        public float Length => length;
        public bool RenderInGame
        {
            get => renderInGame;
            set
            {
                if (renderInGame == value)
                    return;

                renderInGame = value;
                RebuildSplineLine(true);
            }
        }
        public int SegmentCount => segments != null ? segments.Count : 0;
        public int AllSplineObjectCount => allSplineObjects != null ? allSplineObjects.Count : 0;
        public int RootSplineObjectCount => rootSplineObjects != null ? rootSplineObjects.Count : 0;

        public bool IsEnabled()
        {
            //Yes, this can happen
            if (this == null)
                return false;

            if (gameObject != null && gameObject.activeInHierarchy && enabled)
                return true;

            return false;
        }

        public Segment GetSegmentAtIndex(int index)
        {
            return segments[index];
        }

        public void AddSegment(Segment segment)
        {
            segments.Add(segment);
            MarkCacheDirty();
        }

        public void InsertSegment(int index, Segment segment)
        {
            segments.Insert(index, segment);
            MarkCacheDirty();
        }

        public Segment CreateSegment(Vector3 anchorPos, 
                                     Vector3 tangentAPos, 
                                     Vector3 tangentBPos, Space space = Space.World)
        {
            int indexInSpline = segments.Count;
            Segment segment = new Segment(this, anchorPos, tangentAPos, tangentBPos, space, indexInSpline);
            segments.Add(segment);

#if UNITY_EDITOR
            SetInterpolationModeForNewSegment(segment, segments.Count - 2);
            EHandleEvents.InvokeAfterSegmentCreated(segment);
#endif
            return segment;
        }

        public Segment CreateSegment(int index, Vector3 anchorPos, 
                                                Vector3 tangentAPos, 
                                                Vector3 tangentBPos, Space space = Space.World)
        {
            Segment segment = new Segment(this, anchorPos, tangentAPos, tangentBPos, space, index);
            segments.Insert(index, segment);

#if UNITY_EDITOR
            SetInterpolationModeForNewSegment(segment, index);
            EHandleEvents.InvokeAfterSegmentCreated(segment);
#endif
            return segment;
        }

        /// <summary>
        /// Creates a new segment at the specified time without changing the spline's shape.
        /// </summary>
        public Segment CreateSegmentSegmentAutoSmooth(float time)
        {
            AutoSmoothData segmentAutoSmooth = SplineUtility.ComputeAutoSmoothData(NativeSegmentsLocal, time);
            int index = segmentAutoSmooth.index;
            Vector3 newAnchor = segmentAutoSmooth.anchor;
            Vector3 newTangentA = segmentAutoSmooth.tangentA;
            Vector3 newTangentB = segmentAutoSmooth.tangentB;
            Vector3 prevTangentA = segmentAutoSmooth.prevTangentA;
            Vector3 nextTangentB = segmentAutoSmooth.nextTangentB;

            segments[index - 1].SetPosition(ControlHandle.TANGENT_A, prevTangentA, Space.Self);
            segments[index].SetPosition(ControlHandle.TANGENT_B, nextTangentB, Space.Self);
            if (loop && index == segments.Count - 1) segments[0].SetPosition(ControlHandle.TANGENT_B, nextTangentB, Space.Self);

            Segment segment = CreateSegment(index, newAnchor, newTangentA, newTangentB, Space.Self);
            return segment;
        }

        public bool ContainsSegment(Segment segment)
        {
            if (segments == null)
                return false;

            return segments.Contains(segment);
        }

        public void RemoveSegment(Segment segment)
        {
            segments.Remove(segment);
            MarkCacheDirty();
        }

        public void RemoveSegmentAt(int index)
        {
            segments.RemoveAt(index);
            MarkCacheDirty();
        }

        public void ReverseSegments()
        {
            segments.Reverse();
            foreach (Segment s in segments)
            {
                Vector3 tangetA = s.GetPosition(ControlHandle.TANGENT_A);
                Vector3 tangetB = s.GetPosition(ControlHandle.TANGENT_B);
                s.SetPosition(ControlHandle.TANGENT_A, tangetB);
                s.SetPosition(ControlHandle.TANGENT_B, tangetA);
            }

            MarkCacheDirty();
        }

        public int GetSegment(float time)
        {
            return SplineUtility.GetSegmentIndex(segments.Count, time);
        }

        public SplineObject GetSplineObjectAtIndex(int index)
        {
            return allSplineObjects[index];
        }

        public SplineObject GetRootSplineObjectAtIndex(int index)
        {
            return rootSplineObjects[index];
        }

        public bool ContainsSplineObject(SplineObject so)
        {
            if (so == null) return false;
            return splineObjectsSet.Contains(so);
        }

        // If you are looking for a way to create spline objects, you should not use this function.
        // Use spline.CreateDeformation() or spline.CreateFollower().
        internal void AddSplineObject(SplineObject so, int index = -1)
        {
            if (so == null)
                return;

            if (splineObjectsSet.Contains(so))
                return;

            splineObjectsSet.Add(so);
            if (so.transform.parent == transform) rootSplineObjects.Add(so);

            if (index == -1)
                allSplineObjects.Add(so);
            else
                allSplineObjects.Insert(index, so);

#if UNITY_EDITOR
            EHandleEvents.ForceUpdateSelection();
            EHandleEvents.MarkForInfoUpdate(this);
#endif
        }

        // If you are looking for a way to remove spline objects from the spline, you should not use this function.
        // Instead, use: splineObject.transform.parent = null
        // After that, the spline object is removed from the spline and you can pool or destroy it.
        internal void RemoveSplineObject(SplineObject so)
        {
            if (!splineObjectsSet.Contains(so))
                return;

            splineObjectsSet.Remove(so);
            allSplineObjects.Remove(so);
            rootSplineObjects.Remove(so);

#if UNITY_EDITOR
            EHandleEvents.ForceUpdateSelection();
            EHandleEvents.MarkForInfoUpdate(this);
#endif
        }

        // If you are looking for a way to remove spline objects from the spline, you should not use this function.
        // Instead, use: splineObject.transform.parent = null
        // After that, the spline object is removed from the spline and you can pool or destroy it.
        internal void RemoveAtSplineObject(int index)
        {
            SplineObject so = allSplineObjects[index];

            splineObjectsSet.Remove(so);
            rootSplineObjects.Remove(so);
            allSplineObjects.RemoveAt(index);

#if UNITY_EDITOR
            EHandleEvents.ForceUpdateSelection();
            EHandleEvents.MarkForInfoUpdate(this);
#endif
        }

        /// <summary>
        /// Gets the position on a specific segment of the spline.
        /// </summary>
        public Vector3 GetPositionOnSegment(int segment, float time, Space space = Space.World)
        {
            if (segment == segments.Count - 1) segment--;
            Vector3 a = segments[segment].GetPosition(ControlHandle.ANCHOR, space);
            Vector3 ata = segments[segment].GetPosition(ControlHandle.TANGENT_A, space);
            Vector3 b = segments[segment + 1].GetPosition(ControlHandle.ANCHOR, space);
            Vector3 btb = segments[segment + 1].GetPosition(ControlHandle.TANGENT_B, space);

            return BezierUtility.Cubic(a, ata, btb, b, time);
        }

        public Vector3 GetPosition(float time, Space space = Space.World)
        {
            int segment = GetSegment(time);
            return GetPositionOnSegment(segment - 1, SplineUtility.GetSegmentTime(segment, segments.Count, time), space);
        }

        /// <summary>
        /// Gets the local position from the cached position map. 
        /// This is faster than GetPosition(), but only works when useCachedPositions is enabled.
        /// </summary>
        public Vector3 GetPositionFastLocal(float time)
        {
            int count = PositionMapLocal.Length;
            if (count == 0)
                return GetPosition(time, Space.Self);

            float resolution = GetSplineResolution();
            float rawIndex = time / resolution;
            rawIndex = Mathf.Clamp(rawIndex, 0f, count - 1);

            int i0 = Mathf.FloorToInt(rawIndex);
            int i1 = Mathf.Min(i0 + 1, count - 1);
            float frac = rawIndex - i0;

            return Vector3.Lerp(PositionMapLocal[i0], PositionMapLocal[i1], frac);
        }

        /// <summary>
        /// Like GetPosition(), but also supports values below 0 and 
        /// above 1 by extending the spline from the start or end anchor.
        /// </summary>
        public Vector3 GetPositionExtended(float time)
        {
            int segmentIndex = GetSegment(time) - 1;
            Vector3 position = GetPosition(time);
            if (time >= 1)
            {
                segmentIndex = segments.Count - 1;
                position = segments[segmentIndex].GetPosition(ControlHandle.ANCHOR);
            }

            Vector3 direction = -segments[segmentIndex].GetDirection();

            if (time < 0)
            {
                direction = -direction;
                time = Mathf.Abs(time);
            }
            else if (time > 1)
                time -= 1;
            else
                return position;

            return position + direction * (time * length);
        }

        /// <summary>
        /// Gets the closest point on the spline to a 
        /// world position and outputs the corresponding spline time.
        /// </summary>
        public Vector3 GetNearestPoint(Vector3 worldPoint, out float timeValue, 
                                                          float startTime = 0, 
                                                          float endTime = 1, 
                                                          int precision = 8, 
                                                          float steps100Meter = 5, 
                                                          bool ignoreYAxel = false, 
                                                          bool useFixedTime = false)
        {
            steps100Meter = 100 / length / steps100Meter;
            if (steps100Meter > 0.2f) steps100Meter = 0.2f;
            if (steps100Meter < 0.0001f) steps100Meter = 0.0001f;

            Vector3 position = GetNearestPointRough(worldPoint, steps100Meter, out timeValue, startTime, endTime, ignoreYAxel, useFixedTime);

            for (int i = precision; i > 0; i--)
            {
                steps100Meter = steps100Meter / 1.66f;
                float timeForwards = timeValue + steps100Meter;
                float timeBackwards = timeValue - steps100Meter;

                //Only snap when not looping. Will prevent the position from getting stuck close to 0/1.
                if (!loop)
                {
                    if (timeForwards > 1) timeForwards = 1;
                    if (timeBackwards < 0) timeBackwards = 0;
                }

                Vector3 pForward = GetPosition(useFixedTime ? TimeToFixedTime(timeForwards) : timeForwards);
                float dForward = ignoreYAxel ? Vector2.Distance(new Vector2(pForward.x, pForward.z), new Vector2(worldPoint.x, worldPoint.z)) : Vector3.Distance(pForward, worldPoint);

                Vector3 pBackwards = GetPosition(useFixedTime ? TimeToFixedTime(timeBackwards) : timeBackwards);
                float dBackwards = ignoreYAxel ? Vector2.Distance(new Vector2(pBackwards.x, pBackwards.z), new Vector2(worldPoint.x, worldPoint.z)) : Vector3.Distance(pBackwards, worldPoint);

                if (dForward > dBackwards)
                {
                    position = pBackwards;
                    timeValue = timeBackwards;
                }
                else
                {
                    position = pForward;
                    timeValue = timeForwards;
                }
            }

            timeValue = Mathf.Clamp(timeValue, startTime, endTime);
            return position;

            Vector3 GetNearestPointRough(Vector3 point, float steps100Meter, out float timeValue, float startTime = 0,
                                                                                                  float endTime = 1,
                                                                                                  bool ignoreYAxel = false,
                                                                                                  bool useFixedTime = false)
            {
                timeValue = -1;
                float distance = 999999;
                Vector3 position = Vector3.zero;

                for (float t = startTime; t < endTime; t += steps100Meter)
                {
                    float dCheck;
                    float t2 = t;

                    //Calcuate closest point from fixed time instead.
                    //Remmeber that the regular time can still be needed for other calculations.
                    if (useFixedTime)
                        t2 = TimeToFixedTime(t);

                    Vector3 bezierPoint = GetPosition(t2, Space.World);

                    if (ignoreYAxel)
                        dCheck = Vector2.Distance(new Vector2(bezierPoint.x, bezierPoint.z), new Vector2(point.x, point.z));
                    else
                        dCheck = Vector3.Distance(bezierPoint, point);

                    if (dCheck < distance)
                    {
                        timeValue = t;
                        distance = dCheck;
                        position = bezierPoint;
                    }
                }

                return position;
            }
        }

        /// <summary>
        /// Finds the closest points between this spline and another spline, 
        /// and returns both points with their corresponding spline times.
        /// This can be used to detect whether two splines are overlapping.
        /// </summary>
        public SplineClosestResult GetNearestPoints(Spline spline, int precision = 7, 
                                                                   float stepsPer100Meter = 20)
        {
            NativeArray<NativeSegment> segments = SplineUtilityNative.CreateNativeArray(this.segments, Space.World, Allocator.TempJob);
            NativeArray<NativeSegment> segments2 = SplineUtilityNative.CreateNativeArray(spline.segments, Space.World, Allocator.TempJob);
            SplineClosestResult result = SplineUtility.GetNearestPoints(segments, length, segments2, spline.length, precision, stepsPer100Meter);

            segments.Dispose();
            segments2.Dispose();

            return result;
        }

        /// <summary>
        /// Gets the closest point on the spline to a world position.
        /// </summary>
        public Vector3 GetNearestPoint(Vector3 worldPoint)
        {
            return GetNearestPoint(worldPoint, out _);
        }

        /// <summary>
        /// Gets the closest point on the spline to a world position using fixed time, 
        /// and outputs the corresponding fixedTime.
        /// Fixed time maps to the exact distance along the spline.
        /// </summary>
        public Vector3 GetNearestPointFixedTime(Vector3 worldPoint, out float fixedTime, 
                                                                    int precision = 8, 
                                                                    float stepsPer100Meter = 5, 
                                                                    bool ignoreYAxel = false)
        {
            return GetNearestPoint(worldPoint, out fixedTime, 0, 1, precision, stepsPer100Meter, ignoreYAxel, true);
        }

        /// <summary>
        /// Gets the closest point on the spline to a world position using fixed time, and outputs the corresponding fixedTime.
        /// Fixed time maps to the exact distance along the spline.
        /// </summary>
        public Vector3 GetNearestPointFixedTime(Vector3 worldPoint, out float fixedTime)
        {
            return GetNearestPoint(worldPoint, out fixedTime, 0, 1, 8, 5, false, true); ;
        }

        /// <summary>
        /// Gets the nearest time on the spline from a world point.
        /// </summary>
        public float GetNearestTime(Vector3 worldPoint, float startTime, float endTime, 
                                                                         int precision = 8, 
                                                                         float steps100Meter = 5, 
                                                                         bool ignoreYAxel = false, 
                                                                         bool useFixedTime = false)
        {
            GetNearestPoint(worldPoint, out float timeValue, startTime, endTime, precision, steps100Meter, ignoreYAxel, useFixedTime);
            return timeValue;
        }

        /// <summary>
        /// Gets the nearest time on the spline from a world point.
        /// </summary>
        public float GetNearestTime(Vector3 worldPoint, int precision = 8, 
                                                        float steps100Meter = 5)
        {
            return GetNearestTime(worldPoint, 0, 1, precision, steps100Meter);
        }

        /// <summary>
        /// Gets the nearest fixed time on the spline from a world point.
        /// Fixed time maps to the exact distance along the spline.
        /// </summary>
        public float GetNearestFixedTime(Vector3 worldPoint, int precision = 8, 
                                                             float steps100Meter = 5)
        {
            return GetNearestTime(worldPoint, 0, 1, precision, steps100Meter, false, true);
        }

        public Vector3 GetDirection(float time, bool backwards = false, Space space = Space.World)
        {
            if (time <= 0.00001f) 
                return -(segments[0].GetPosition(ControlHandle.TANGENT_B) - segments[0].GetPosition(ControlHandle.ANCHOR)).normalized;
            else if (time >= 0.99999f) 
                return -(segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR) - segments[segments.Count - 1].GetPosition(ControlHandle.TANGENT_A)).normalized;

            int segment = GetSegment(time);
            if (segment < 1) segment = 1;

            float segementTime = SplineUtility.GetSegmentTime(segment, segments.Count, time);
            Vector3 forwardDirection = BezierUtility.GetTangent(segments[segment - 1].GetPosition(ControlHandle.ANCHOR, space),
                                                  segments[segment - 1].GetPosition(ControlHandle.TANGENT_A, space),
                                                  segments[segment].GetPosition(ControlHandle.TANGENT_B, space),
                                                  segments[segment].GetPosition(ControlHandle.ANCHOR, space), segementTime);

            return backwards ? -forwardDirection : forwardDirection;
        }

        /// <summary>
        /// Converts normalized time to fixed time. Fixed time is the corrected parameter that
        /// corresponds to the actual distance along the spline.
        /// For example, to get the world position exactly halfway along the spline length:
        /// float fixedTime = TimeToFixedTime(0.5f);
        /// Vector3 worldPoint = spline.GetPosition(fixedTime);
        /// </summary>
        public float TimeToFixedTime(float time)
        {
            return SplineUtilityNative.TimeToFixedTime(DistanceMap, GetSplineResolution(), time, loop);
        }

        /// <summary>
        /// Converts fixed time back to regular spline time, which is not correlated with distance.
        /// </summary>
        public float FixedTimeToTime(float fixedTime)
        {
            return SplineUtilityNative.FixedTimeToTime(DistanceMap, GetSplineResolution(), fixedTime, loop);
        }

        /// <summary>
        /// Gets the center point of all control point anchors on the spline.
        /// </summary>
        public Vector3 GetCenter(Space space = Space.World)
        {
            Vector3 center = Vector3.zero;
            foreach (Segment s in segments)
            {
                if (loop && s == segments[segments.Count - 1])
                    continue;

                center += s.GetPosition(ControlHandle.ANCHOR, space);
            }

            return center / (segments.Count - (loop ? 1 : 0));
        }

        /// <summary>
        /// Fills the provided array (size 3) with the spline normals at 
        /// the given fixedTime: X, Y, and Z directions.
        /// </summary>
        public void GetNormalsNonAlloc(Vector3[] normals, float fixedTime, 
                                                          Space space = Space.World, 
                                                          bool ignoreZRotation = false)
        {
            if (segments.Count == 1)
            {
                normals[1] = splineType == SplineType.STATIC_2D ? -transform.forward : transform.up;
                normals[2] = segments[0].GetDirection();
                normals[0] = Vector3.Cross(normals[2], -normals[1]).normalized;
            }
            else if (splineType != SplineType.DYNAMIC)
            {
                //Z
                normals[2] = GetDirection(fixedTime, false, space);
                //X
                normals[0] = Vector3.Cross(normals[2], splineType == SplineType.STATIC_2D ? transform.forward : -transform.up).normalized;
                //Y
                normals[1] = Vector3.Cross(normals[2], normals[0]).normalized;

                if (!ignoreZRotation)
                {
                    float degrees = math.degrees(-GetZRotationDegrees(fixedTime));
                    Quaternion rotation = Quaternion.AngleAxis(degrees, -normals[2]);
                    normals[0] = rotation * normals[0];
                    normals[1] = rotation * normals[1];
                }
            }
            else if(NormalsLocal.Length > 0)
            {
                //Get index
                float n = fixedTime * (NormalsLocal.Length / 3);
                int normalIndex = (int)math.floor(n);

                if (normalIndex < 0 || (normalIndex * 3) + 1 >= NormalsLocal.Length)
                    normalIndex = Mathf.Clamp(normalIndex, 0, (NormalsLocal.Length / 3) - 1);

                //Get directions
                normals[0] = NormalsLocal[normalIndex * 3];
                normals[1] = NormalsLocal[normalIndex * 3 + 1];
                normals[2] = NormalsLocal[normalIndex * 3 + 2];

                if(!ignoreZRotation)
                {
                    float degrees = math.degrees(-GetZRotationDegrees(fixedTime));
                    Quaternion rotation = Quaternion.Inverse(Quaternion.AngleAxis(degrees, -normals[2]));
                    normals[0] = rotation * normals[0];
                    normals[1] = rotation * normals[1];
                }

                if (space == Space.World)
                {
                    normals[0] = transform.TransformDirection(normals[0]);
                    normals[1] = transform.TransformDirection(normals[1]);
                    normals[2] = transform.TransformDirection(normals[2]);
                }
            }

            if (GeneralUtility.IsEqual(normals[2], normals[1]))
            {
                normals[1] = Vector3.up;
                normals[2] = Vector3.forward;
                normals[0] = Vector3.right;
            }
        }

        /// <summary>
        /// Gets the interpolated Z rotation (in degrees) along the spline at the specified time.
        /// </summary>
        public float GetZRotationDegrees(float time)
        {
            int segment = Mathf.Clamp(GetSegment(time), 1, segments.Count - 1);
            float segementTime = SplineUtility.GetSegmentTime(segment, segments.Count, time);
            float reversedSegmentTime = Mathf.Clamp01(1 - segementTime);

            float contrast = segments[segment - 1].Contrast - segments[segment].Contrast;
            contrast = segments[segment].Contrast + contrast - (contrast * segementTime);

            //Smooth the rotation closer to segment start/end.
            float numerator = Mathf.Pow(segementTime, contrast);
            float denominator = numerator + Mathf.Pow(reversedSegmentTime, contrast);

            segementTime = numerator / denominator;

            //Get rotation value
            float rotDif = segments[segment - 1].ZRotation - segments[segment].ZRotation;
            return math.radians(segments[segment].ZRotation + rotDif - (rotDif * segementTime));
        }

        public void SetLoop(bool enable, bool modifySegments = true)
        {
            if (loop == enable)
                return;

            loop = enable;

            if (!modifySegments)
                return;

            if (enable)
            {
                Segment first = segments[0];
                Segment last = CreateSegment(segments.Count,
                                            first.GetPosition(ControlHandle.ANCHOR),
                                            first.GetPosition(ControlHandle.TANGENT_A),
                                            first.GetPosition(ControlHandle.TANGENT_B));

                first.Contrast = last.Contrast;
                first.Scale = last.Scale;
                first.Noise = last.Noise;
                first.SaddleSkew = last.SaddleSkew;
                first.ZRotation = last.ZRotation;
            }
            else
            {
                RemoveSegmentAt(segments.Count - 1);
            }

            MarkCacheDirty();
        }

        public void Join(Spline spline, JoinType joinType)
        {
            float skipFirstDistance = 2.5f;

            if (joinType == JoinType.END_TO_START)
            {
                float distance = Vector3.Distance(segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR), spline.segments[0].GetPosition(ControlHandle.ANCHOR));
                for (int i = 0; i < spline.segments.Count; i++)
                {
                    if (GeneralUtility.IsZero(distance, skipFirstDistance) && i == 0)
                        continue;

                    Segment s = spline.segments[i];
                    s.SwitchSplineParent(this);
                    segments.Add(s);
                }
            }
            else if (joinType == JoinType.END_TO_END)
            {
                float distance = Vector3.Distance(segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR), spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR));
                for (int i = spline.segments.Count - 1; i >= 0; i--)
                {
                    if (GeneralUtility.IsZero(distance, skipFirstDistance) && i == spline.segments.Count - 1)
                        continue;

                    Segment s = spline.segments[i];
                    s.SwitchSplineParent(this);
                    segments.Add(s);
                    Vector3 tangentA = s.GetPosition(ControlHandle.TANGENT_A);
                    Vector3 tangentB = s.GetPosition(ControlHandle.TANGENT_B);
                    s.SetPosition(ControlHandle.TANGENT_A, tangentB);
                    s.SetPosition(ControlHandle.TANGENT_B, tangentA);
                }
            }
            else if (joinType == JoinType.START_TO_START)
            {
                float distance = Vector3.Distance(segments[0].GetPosition(ControlHandle.ANCHOR), spline.segments[0].GetPosition(ControlHandle.ANCHOR));
                for (int i = 0; i < spline.segments.Count; i++)
                {
                    if (GeneralUtility.IsZero(distance, skipFirstDistance) && i == 0)
                        continue;

                    Segment s = spline.segments[i];
                    s.SwitchSplineParent(this);
                    Vector3 tangentA = s.GetPosition(ControlHandle.TANGENT_A);
                    Vector3 tangentB = s.GetPosition(ControlHandle.TANGENT_B);
                    s.SetPosition(ControlHandle.TANGENT_A, tangentB);
                    s.SetPosition(ControlHandle.TANGENT_B, tangentA);
                    segments.Insert(0, s);
                }
            }
            else if (joinType == JoinType.START_TO_END)
            {
                float distance = Vector3.Distance(segments[0].GetPosition(ControlHandle.ANCHOR), spline.segments[spline.segments.Count - 1].GetPosition(ControlHandle.ANCHOR));
                for (int i = spline.segments.Count - 1; i >= 0; i--)
                {
                    if (GeneralUtility.IsZero(distance, skipFirstDistance) && i == spline.segments.Count - 1)
                        continue;

                    Segment s = spline.segments[i];
                    s.SwitchSplineParent(this);
                    segments.Insert(0, s);
                }
            }

            MarkCacheDirty();
        }

        public void Split(Spline newSpline, int segmentIndex)
        {
            newSpline.segments.Clear();

            for (int i = segmentIndex; i < segments.Count; i++)
            {
                Segment s = segments[i];

                if (i == segmentIndex)
                    newSpline.CreateSegment(0, s.GetPosition(ControlHandle.ANCHOR), s.GetPosition(ControlHandle.TANGENT_A), s.GetPosition(ControlHandle.TANGENT_B));
                else
                {
                    s.SwitchSplineParent(newSpline);
                    newSpline.segments.Add(s);
                }
            }

            for (int i = segments.Count - 1; i > segmentIndex; i--)
                segments.RemoveAt(i);

            if (transform.parent != null)
                newSpline.transform.parent = transform.parent;

            MarkCacheDirty();
            newSpline.MarkCacheDirty();
        }

        /// <summary>
        /// Detects if a spline object has crossed a segment that contains links 
        /// between the previous and current spline positions.
        /// Call this every frame and check if the links list contains any segments. 
        /// If it does, you can cross over to another
        /// spline by using: splineObject.transform.parent = newSegment.SplineParent.transform.
        /// For an exact position on the new spline without any visual lag, 
        /// use CalculateLinkCrossingZPosition() to set the spline object's new position.
        /// </summary>
        public void FindLinkCrossingsNonAlloc(List<Segment> links, Vector3 splinePosition, 
                                                            Vector3 previousSplinePosition, 
                                                            LinkFlags linkFlags, 
                                                            out Segment currentSegment)
        {
            //Handle loop
            if (loop && splinePosition.z > length)
            {
                int loops = Mathf.FloorToInt(splinePosition.z / length);
                splinePosition.z -= length * loops;
                previousSplinePosition.z -= length * loops;
            }

            float minZ = Mathf.Min(previousSplinePosition.z, splinePosition.z);
            float maxZ = Mathf.Max(previousSplinePosition.z, splinePosition.z);
            currentSegment = null;

            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];

                if (s.LinkCount == 0)
                    continue;

                if ((s.zPosition > minZ && s.zPosition < maxZ) || splinePosition.z == s.zPosition)
                {
                    for (int i2 = 0; i2 < s.LinkCount; i2++)
                    {
                        Segment s2 = s.GetLinkAtIndex(i2);

                        if (s2.ignoreLink)
                            continue;

                        if (linkFlags != LinkFlags.NONE)
                        {
                            Spline spline2 = s2.SplineParent;
                            Segment s2Link = s2;
                            Segment s2First = spline2.segments[0];
                            Segment s2Last = spline2.segments[spline2.segments.Count - 1];


                            if (linkFlags.HasFlag(LinkFlags.SKIP_LAST) && s2Last == s2Link)
                                continue;

                            if (linkFlags.HasFlag(LinkFlags.SKIP_FIRST) && s2First == s2Link)
                                continue;

                            if (linkFlags.HasFlag(LinkFlags.SKIP_SELF) && s == s2Link)
                                continue;
                        }

                        links.Add(s2);
                    }

                    currentSegment = s;
                    return;
                }
            }
        }

        /// <summary>
        /// Detects if a spline object has crossed a segment that contains 
        /// links between the previous and current spline positions.
        /// Call this every frame and check if the links list contains any segments. 
        /// If it does, you can cross over to another
        /// spline by using: splineObject.transform.parent = newSegment.SplineParent.transform.
        /// For an exact position on the new spline without any visual lag, 
        /// use CalculateLinkCrossingZPosition() to set the spline object's new position.
        /// </summary>
        public void FindLinkCrossingsNonAlloc(List<Segment> links, Vector3 splinePosition, 
                                                                   Vector3 previousSplinePosition)
        {
            FindLinkCrossingsNonAlloc(links, splinePosition, previousSplinePosition, LinkFlags.NONE, out _);
        }

        /// <summary>
        /// After detecting a link crossing with FindLinkCrossingsNonAlloc(),
        /// use this to calculate the spline object's new Z position on the spline it crossed to.
        /// This helps prevent visual lag when switching to a new spline.
        /// </summary>
        public float CalculateLinkCrossingZPosition(Vector3 splinePosition, Segment fromSegment, 
                                                                            Segment toSegment)
        {
            float validatedZPosition = SplineUtility.GetValidatedPosition(splinePosition, length, loop).z;
            float dif = validatedZPosition - fromSegment.zPosition;
            return toSegment.zPosition + dif;
        }

        /// <summary>
        /// Creates and initializes a SplineObject deformation on the 
        /// given GameObject and registers it to this spline.
        /// </summary>
        public SplineObject CreateDeformation(GameObject go, Vector3 localSplinePosition, 
                                                             Quaternion localSplineRotation, 
                                                             bool skipRuntimeUpdates = false,
                                                             Transform parent = null,
                                                             bool includeInNextJob = true,
                                                             bool worldPositionStays = true)
        {
#if UNITY_EDITOR
            editorDisableOnChildrenChanged = true;
#endif
            SplineObject.defaultType = SplineObjectType.DEFORMATION;

            if (parent == null) go.transform.SetParent(transform, worldPositionStays);
            else go.transform.SetParent(parent, worldPositionStays);

            SplineObject so = null;

            for(int i = 0; i < go.GetComponentCount(); i++)
            {
                Component c = go.GetComponentAtIndex(i);

                if(c is SplineObject)
                {
                    so = (SplineObject)c;
                    so.Initalize();
                    break;
                }
            }

            if(so == null) so = go.AddComponent<SplineObject>();
            so.localSplinePosition = localSplinePosition;
            so.localSplineRotation = localSplineRotation;
            so.componentMode = ComponentMode.ACTIVE;
            so.meshMode = MeshMode.GENERATE;

#if UNITY_EDITOR
            if(componentMode != ComponentMode.ACTIVE)
            {
                Debug.LogError($"[Spline Architect] Can't create deformation becouse the component setting is not set to active on {name}!");
                so.DestroyAllInstanceMeshes();
                Destroy(so);
                return null;
            }
            else
            {
                for (int i = 0; i < so.MeshContainerCount; i++)
                {
                    MeshContainer mc = so.GetMeshContainerAtIndex(i);
                    Mesh instanceMesh = mc.GetInstanceMesh();

                    if (instanceMesh != null && !instanceMesh.isReadable)
                    {
                        Debug.LogError($"[Spline Architect] Can't deformation mesh becouse no read/write access on '{so.name}' ({name}).");
                        so.DestroyAllInstanceMeshes();
                        Destroy(so);
                        return null;
                    }
                }
            }
#endif
            if (so.MeshContainerCount == 0)
            {
                so.transform.localPosition = localSplinePosition;
                so.transform.localRotation = localSplineRotation;
            }
            else if (skipRuntimeUpdates)
            {
                RemoveSplineObject(so);
                so.componentMode = ComponentMode.INACTIVE;
                if (includeInNextJob) mainSystemWorker.Add(so);
            }
            else if (includeInNextJob) mainSystemWorker.Add(so);

#if UNITY_EDITOR
            editorDisableOnChildrenChanged = false;
#endif

            return so;
        }

        /// <summary>
        /// Creates and initializes a SplineObject follower on the 
        /// given GameObject and registers it to this spline.
        /// </summary>
        public SplineObject CreateFollower(GameObject go, Vector3 localSplinePosition, 
                                                          Quaternion localSplineRotation, 
                                                          bool skipRuntimeUpdates = false,
                                                          Transform parent = null,
                                                          bool includeInNextJob = true, 
                                                          bool worldPositionStays = true)
        {
#if UNITY_EDITOR
            editorDisableOnChildrenChanged = true;
#endif
            SplineObject.defaultType = SplineObjectType.FOLLOWER;

            if (parent == null) go.transform.SetParent(transform, worldPositionStays);
            else go.transform.SetParent(parent, worldPositionStays);

            SplineObject so = null;

            for (int i = 0; i < go.GetComponentCount(); i++)
            {
                Component c = go.GetComponentAtIndex(i);

                if (c is SplineObject)
                {
                    so = (SplineObject)c;
                    so.Initalize();
                    break;
                }
            }

            if (so == null) so = go.AddComponent<SplineObject>();
            so.localSplinePosition = localSplinePosition;
            so.localSplineRotation = localSplineRotation;
            so.componentMode = ComponentMode.ACTIVE;

#if UNITY_EDITOR
            if(componentMode != ComponentMode.ACTIVE)
            {
                Debug.LogError($"[Spline Architect] Can't create follower becouse the component setting is not set to active on {name}!");
                so.DestroyAllInstanceMeshes();
                Destroy(so);
                return null;
            }          
#endif

            if (skipRuntimeUpdates)
            {
                RemoveSplineObject(so);
                so.componentMode = ComponentMode.INACTIVE;
                mainSystemWorker.Add(so);
            }
            else if (includeInNextJob) mainSystemWorker.Add(so);

#if UNITY_EDITOR
            editorDisableOnChildrenChanged = false;
#endif

            return so;
        }

        /// <summary>
        /// Finds the closest spline objects to a given position along 
        /// the spline and stores the results in the provided list.
        /// It can also search on linked splines and filter results 
        /// by direction and other options using the given flags.
        /// This is typically used for things like collision checks 
        /// in traffic or movement systems.
        /// </summary>
        public void DistanceToClosestSplineObjectNonAlloc(List<SplineSearchResult> searchResults, 
                                                          Vector3 searchSplinePosition, 
                                                          int maxResults, 
                                                          SplineSearchResultFlags searchFlags, 
                                                          bool includeNestedSplineObjects = true, 
                                                          bool includeIgnoredLinks = true)
        {
            searchSplinePosition = SplineUtility.GetValidatedPosition(searchSplinePosition, length, loop);
            Check(this, searchSplinePosition, 0, false);

            if (!searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_CLOSEST_LINK_FORWARD) && !searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_CLOSEST_LINK_BACKWARD))
                return;

            Segment closestSegment = null;
            float segmentDistanceCheck = 99999;
            float segmentOffset = 99999;

            //Get closest segment
            foreach (Segment s in segments)
            {
                float d = SplineUtility.GetValidatedZDistance(searchSplinePosition, new Vector3(0, 0, s.zPosition), length, loop);

                if (segmentDistanceCheck > Mathf.Abs(d))
                {
                    if (s.LinkCount == 0)
                        continue;

                    if (!searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_BACKWARD) && d <= 0)
                        continue;

                    if (!searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_FORWARD) && d >= 0)
                        continue;

                    closestSegment = s;
                    segmentDistanceCheck = Mathf.Abs(d);
                    segmentOffset = d;
                }
            }

            if (closestSegment == null)
                return;

            //Check all links on closest segment if it has any.
            for (int i = 0; i < closestSegment.LinkCount; i++)
            {
                Segment link = closestSegment.GetLinkAtIndex(i);

                if (link == null)
                    continue;

                if (link.SplineParent == this)
                    continue;

                if (!includeIgnoredLinks && link.ignoreLink)
                    continue;

                Check(link.SplineParent, new Vector3(0, 0, link.zPosition), segmentOffset, true);
            }

            void Check(Spline splineToCheck, Vector3 splinePositionToCheck, float offset, bool isLink)
            {
                List<SplineObject> splineObjectToSearch = splineToCheck.rootSplineObjects;
                if (!includeNestedSplineObjects) splineObjectToSearch = splineToCheck.allSplineObjects;

                for (int i = 0; i < splineObjectToSearch.Count; i++)
                {
                    SplineObject so = splineObjectToSearch[i];

                    if (!so.IsEnabled())
                        continue;

                    float d = SplineUtility.GetValidatedZDistance(splinePositionToCheck, so.splinePosition, length, loop);

                    if (searchFlags.HasFlag(SplineSearchResultFlags.NEED_SAME_X_POSITION) && !GeneralUtility.IsEqual(splinePositionToCheck.x, so.splinePosition.x))
                        continue;

                    if (searchFlags.HasFlag(SplineSearchResultFlags.NEED_SAME_Y_POSITION) && !GeneralUtility.IsEqual(splinePositionToCheck.y, so.splinePosition.y))
                        continue;

                    if (!isLink && !searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_BACKWARD) && d < 0)
                        continue;

                    if (!isLink && !searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_FORWARD) && d > 0)
                        continue;

                    if (isLink && !searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_CLOSEST_LINK_BACKWARD) && d < 0)
                        continue;

                    if (isLink && !searchFlags.HasFlag(SplineSearchResultFlags.SEARCH_CLOSEST_LINK_FORWARD) && d > 0)
                        continue;

                    float d2 = Mathf.Abs(d) + offset;

                    if (searchResults.Count == 0)
                    {
                        searchResults.Add(new SplineSearchResult(offset, d * -1, d2, so, isLink, d > 0));
                        continue;
                    }

                    bool inserted = false;
                    for (int i2 = 0; i2 < searchResults.Count; i2++)
                    {
                        if (Mathf.Abs(searchResults[i2].distanceFromSplinePoint) > d2)
                        {
                            searchResults.Insert(i2, new SplineSearchResult(offset, d * -1, d2, so, isLink, d > 0));
                            inserted = true;
                            break;
                        }
                    }

                    if (!inserted) searchResults.Add(new SplineSearchResult(offset, d * -1, d2, so, isLink, d > 0));
                    if (searchResults.Count > maxResults) searchResults.RemoveAt(searchResults.Count - 1);
                }
            }
        }

        public Vector3 SplinePositionToWorldPosition(Vector3 splinePosition, 
                                                     Transform parentTransform, 
                                                     float4x4 matrix, 
                                                     bool alignToEnd = false)
        {
            directPointWorker.Add(splinePosition, matrix, alignToEnd);
            directPointWorker.Complete(out PointWorkerData pwd);
            if (parentTransform == null) return pwd.point;
            return parentTransform.TransformPoint(pwd.point);
        }

        public Vector3 SplinePositionToWorldPosition(Vector3 splinePosition)
        {
            return SplinePositionToWorldPosition(splinePosition, transform, float4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one));
        }

        public Vector3 WorldPositionToSplinePosition(Vector3 worldPosition, int precision = 8, 
                                                                            float steps = 5)
        {
            if (segments.Count == 1)
                return worldPosition;

            //Closest point needs to be calculated in fixedTime and time.
            //Get fixedTime for z position
            Vector3 fixedPoint = GetNearestPoint(worldPosition, out float fixedTime, 0, 1, precision, steps, false, true);
            Vector3 point = GetNearestPoint(worldPosition, out float time, 0, 1, precision, steps, false, false);

            Vector3 backDirection = segments[0].GetDirection();
            Vector3 frontDirection = -segments[segments.Count - 1].GetDirection();
            float extendedDistance = 0;

            if (!loop)
            {
                Vector3 extendBackPoint = LineUtility.GetNearestPoint(segments[0].GetPosition(ControlHandle.ANCHOR),
                                                                  backDirection,
                                                                  worldPosition,
                                                                  out float extendBackLine);

                Vector3 extendFrontPoint = LineUtility.GetNearestPoint(segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR),
                                                                   frontDirection,
                                                                   worldPosition,
                                                                   out float extendFrontLine);

                float distanceToExtendBack = Vector3.Distance(worldPosition, extendBackPoint);
                float distanceToExtendFront = Vector3.Distance(worldPosition, extendFrontPoint);
                float distanceToPoint = Vector3.Distance(worldPosition, fixedPoint);

                bool extendingBack = extendBackLine >= 0 && distanceToExtendBack < distanceToPoint && distanceToExtendBack < distanceToExtendFront;
                bool extendingFront = extendFrontLine >= 0 && distanceToExtendFront < distanceToPoint && distanceToExtendFront < distanceToExtendBack;

                if (time == 0) extendingBack = true;
                if (time == 1) extendingFront = true;

                if (extendingBack)
                {
                    extendedDistance = -Vector3.Distance(extendBackPoint, segments[0].GetPosition(ControlHandle.ANCHOR));
                    fixedTime = 0;
                    time = 0;
                }
                else if (extendingFront)
                {
                    extendedDistance = Vector3.Distance(extendFrontPoint, segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR));
                    fixedTime = 1;
                    time = 1;
                }
            }

            //GetNormalsNonAlloc() allready calculates zRotation. Dont do it again.
            GetNormalsNonAlloc(normalsContainer, fixedTime, Space.World);
            //Needs to be time here else the position will be offseted and the x axel will be wrong.
            Vector3 p1 = GetPosition(Mathf.Clamp(time, 0, 1), Space.World);

            Vector3 xDirection = normalsContainer[0];
            Vector3 yDirection = normalsContainer[1];

            Vector3 closestXPos = LineUtility.GetNearestPoint(p1, xDirection, worldPosition, out float timeX);
            Vector3 closestYPos = LineUtility.GetNearestPoint(p1, yDirection, worldPosition, out float timeY);

            Vector3 splinePosition;
            splinePosition.x = Vector3.Distance(p1, closestXPos) * Mathf.Sign(timeX);
            splinePosition.y = Vector3.Distance(p1, closestYPos) * Mathf.Sign(timeY);
            splinePosition.z = length * fixedTime + extendedDistance;

            //Apply scale. This is perfect as long the Spline is scaled the same on all axels. Will still work if not but can be scewed.
            splinePosition.x /= transform.localScale.x;
            splinePosition.y /= transform.localScale.y;

            return splinePosition;
        }

        public Quaternion SplineRotationToWorldRotation(Quaternion rotation, float time)
        {
            float fixedTime = TimeToFixedTime(time);
            GetNormalsNonAlloc(normalsContainer, fixedTime);
            Quaternion splineForwardRot = Quaternion.LookRotation(normalsContainer[2], normalsContainer[1]);
            return splineForwardRot * rotation;
        }

        public Quaternion WorldRotationToSplineRotation(Quaternion rotation, float time)
        {
            //Rotation
            float fixedTime = TimeToFixedTime(time);
            GetNormalsNonAlloc(normalsContainer, fixedTime);
            Quaternion splineLocalRotation = Quaternion.LookRotation(normalsContainer[2], normalsContainer[1]);
            return Quaternion.Inverse(splineLocalRotation) * rotation;
        }

        public void TransformToCenter(out Vector3 dif)
        {
            dif = Vector3.zero;
            Vector3 center = GetCenter();

            if (GeneralUtility.IsEqual(center, transform.position, 0.01f))
                return;

            Vector3 oldPos = transform.position;
            transform.position = center;
            dif = oldPos - center;

            foreach (Segment s in segments)
            {
                s.Translate(ControlHandle.ANCHOR, -dif);
                s.Translate(ControlHandle.TANGENT_A, -dif);
                s.Translate(ControlHandle.TANGENT_B, -dif);
            }

            MarkCacheDirty();

#if UNITY_EDITOR
            Vector3 p = transform.TransformPoint(gridCenterPoint) + dif;
            gridCenterPoint = transform.InverseTransformPoint(p);

            Spline[] splines = transform.GetComponentsInChildren<Spline>();

            foreach (Spline s in splines)
            {
                if (s == this)
                    continue;

                UnityEditor.Undo.RecordObject(s.transform, "");
                s.transform.position += dif;
            }

            EHandleEvents.InvokeTransformToCenter(this, dif);
#endif
        }
    }
}
