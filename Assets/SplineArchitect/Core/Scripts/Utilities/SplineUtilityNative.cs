// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: SplineUtilityNative.cs
//
// Author: Mikael Danielsson
// Date Created: 11-03-2025
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System;

using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace SplineArchitect.Utility
{
    public static class SplineUtilityNative
    {
        public static float TimeToFixedTime(NativeList<float> distanceMap, float splineResolution, float time, bool loop)
        {
            // bring time into [0,1) or [0,1] depending on loop
            time = SplineUtility.GetValidatedTime(time, loop);
            int count = distanceMap.Length;

            float rawIndex = time / splineResolution;
            rawIndex = Mathf.Clamp(rawIndex, 0f, count - 1);

            int i0 = Mathf.FloorToInt(rawIndex);
            int i1 = Mathf.Min(i0 + 1, count - 1);

            float frac = rawIndex - i0;

            i0 = Mathf.Clamp(i0, 0, distanceMap.Length);
            i1 = Mathf.Clamp(i1, 0, distanceMap.Length);

            return Mathf.Lerp(distanceMap[i0], distanceMap[i1], frac);
        }

        public static float FixedTimeToTime(NativeList<float> distanceMap, float splineResolution, float fixedTime, bool loop)
        {
            return TimeToFixedTime(distanceMap, splineResolution, fixedTime, loop);
        }

        public static Vector3 GetPosition(NativeArray<NativeSegment> nativeSegments, float time)
        {
            float anchorsCount = nativeSegments.Length;
            int segment = SplineUtility.GetSegmentIndex(nativeSegments.Length, time);
            return GetSegmentPosition(nativeSegments, segment - 1, SplineUtility.GetSegmentTime(segment, anchorsCount, time));
        }

        public static Vector3 GetPositionFast(NativeList<Vector3> positionMap, NativeArray<NativeSegment> nativeSegments, float resolution, float time)
        {
            int count = positionMap.Length;
            if (count == 0)
                return GetPosition(nativeSegments, time);

            float rawIndex = time / resolution;
            rawIndex = Mathf.Clamp(rawIndex, 0f, count - 1);

            int i0 = Mathf.FloorToInt(rawIndex);
            int i1 = Mathf.Min(i0 + 1, count - 1);
            float frac = rawIndex - i0;

            i0 = Mathf.Clamp(i0, 0, positionMap.Length);
            i1 = Mathf.Clamp(i1, 0, positionMap.Length);

            return Vector3.Lerp(positionMap[i0], positionMap[i1], frac);
        }

        public static Vector3 GetPositionExtended(NativeArray<NativeSegment> nativeSegments, float splineLength, float time)
        {
            int segementIndex = 0;
            Vector3 position = nativeSegments[0].anchor;
            if (time >= 1)
            {
                segementIndex = nativeSegments.Length - 1;
                position = nativeSegments[segementIndex].anchor;
            }

            Vector3 direction = -GetSegmentDirection(nativeSegments, segementIndex);

            if (time < 0)
            {
                direction = -direction;
                time = Mathf.Abs(time);
            }
            else if (time > 1)
                time -= 1;
            else
                return position;

            return position + direction * (time * splineLength);
        }

        public static Vector3 GetSegmentPosition(NativeArray<NativeSegment> nativeSegments, int segement, float time)
        {
            if (segement == nativeSegments.Length - 1)
                segement--;

            if (segement < 0)
                segement = 0;

            Vector3 a = nativeSegments[segement].anchor;
            Vector3 ata = nativeSegments[segement].tangentA;
            Vector3 b = nativeSegments[segement + 1].anchor;
            Vector3 btb = nativeSegments[segement + 1].tangentB;

            return BezierUtility.Cubic(a, ata, btb, b, time);
        }

        public static Vector3 GetSegmentDirection(NativeArray<NativeSegment> nativeSegments, int segment)
        {
            if(segment == 0) return (nativeSegments[segment].tangentB - nativeSegments[segment].anchor).normalized;
            else return (nativeSegments[segment].anchor - nativeSegments[segment].tangentA).normalized;
        }

        public static Vector3 GetDirection(NativeArray<NativeSegment> nativeSegments, float time)
        {
            if (time <= 0.00001f)
            {
                return -(nativeSegments[0].tangentB - nativeSegments[0].anchor).normalized;
            }
            else if (time >= 0.99999f)
            {
                return -(nativeSegments[nativeSegments.Length - 1].anchor - nativeSegments[nativeSegments.Length - 1].tangentA).normalized;
            }

            time = Mathf.Clamp(time, 0, 1);

            int segement = SplineUtility.GetSegmentIndex(nativeSegments.Length, time);
            if (segement < 1) segement = 1;

            float segementTime = SplineUtility.GetSegmentTime(segement, nativeSegments.Length, time);
            return BezierUtility.GetTangent(nativeSegments[segement - 1].anchor, nativeSegments[segement - 1].tangentA, 
                                            nativeSegments[segement].tangentB, nativeSegments[segement].anchor, segementTime);
        }

        public static Vector2 GetSadleSkew(NativeArray<NativeSegment> nativeSegments, int segment, float contrastedSegementTime)
        {
            Vector2 sadleSkew = new Vector2(Mathf.Lerp(nativeSegments[segment - 1].saddleSkew.x, nativeSegments[segment].saddleSkew.x, contrastedSegementTime),
                                            Mathf.Lerp(nativeSegments[segment - 1].saddleSkew.y, nativeSegments[segment].saddleSkew.y, contrastedSegementTime));

            return sadleSkew;
        }

        public static Vector2 GetScale(NativeArray<NativeSegment> nativeSegments, int segment, float contrastedSegementTime)
        {
            Vector2 scale = new Vector2(Mathf.Lerp(nativeSegments[segment - 1].scale.x, nativeSegments[segment].scale.x, contrastedSegementTime),
                                        Mathf.Lerp(nativeSegments[segment - 1].scale.y, nativeSegments[segment].scale.y, contrastedSegementTime));

            return scale;
        }

        public static float GetNoise(NativeArray<NativeSegment> nativeSegments, int segment, float contrastedSegementTime)
        {
            float noise = Mathf.Lerp(nativeSegments[segment - 1].noise, nativeSegments[segment].noise, contrastedSegementTime);

            return noise;
        }

        public static float GetContrastedSegementTime(NativeArray<NativeSegment> nativeSegments, int segment, float segmentTime)
        {
            //Apply contrast
            float contrast = nativeSegments[segment - 1].contrast - nativeSegments[segment].contrast;
            contrast = nativeSegments[segment].contrast + contrast - (contrast * segmentTime);
            float numerator = Mathf.Pow(segmentTime, contrast);
            float denominator = numerator + Mathf.Pow(1 - segmentTime, contrast);

            return numerator / denominator;
        }

        public static float GetZRotationDegrees(NativeArray<NativeSegment> nativeSegements, float time, float contrastedSegementTime)
        {
            int segement = SplineUtility.GetSegmentIndex(nativeSegements.Length, time);

            if (segement < 1)
                segement = 1;

            //Get rotation value
            float rotDif = nativeSegements[segement - 1].zRot - nativeSegements[segement].zRot;
            return math.radians(nativeSegements[segement].zRot + rotDif - (rotDif * contrastedSegementTime));
        }

        public static Quaternion GetZRotation(NativeArray<NativeSegment> nativeSegements, Vector3 splineDirection, float fixedTime, float contrastedSegementTime)
        {
            float degrees = math.degrees(-GetZRotationDegrees(nativeSegements, fixedTime, contrastedSegementTime));
            return Quaternion.AngleAxis(degrees, splineDirection);
        }

        public static float GetNearestTimeRough(NativeArray<NativeSegment> sgements,
                                  NativeList<Vector3> positionMap,
                                  float splineResolution,
                                  bool loop,
                                  Vector3 point,
                                  float fixedStep,
                                  bool ignoreYAxel = false)
        {
            float timeValue = -1;
            float distance = 999999;

            for (float t = 0; t < 1; t += fixedStep)
            {
                Vector3 bezierPoint = GetPositionFast(positionMap, sgements, splineResolution, t);
                float d2 = ignoreYAxel ? Vector2.Distance(new Vector2(bezierPoint.x, bezierPoint.z), new Vector2(point.x, point.z)) : Vector3.Distance(bezierPoint, point);

                if (d2 < distance)
                {
                    timeValue = t;
                    distance = d2;
                }
            }

            return timeValue;
        }

        public static float GetNearestTime(NativeArray<NativeSegment> segments,
                                        NativeList<Vector3> positionMap,
                                        float resolution,
                                        float splineLength,
                                        bool loop,
                                        Vector3 point,
                                        int precision,
                                        float steps = 5,
                                        bool ignoreYAxel = false)
        {
            float fixedStep = 100f / splineLength / steps;
            if (fixedStep > 0.2f) fixedStep = 0.2f;
            if (fixedStep < 0.0001f) fixedStep = 0.0001f;

            float timeValue = GetNearestTimeRough(segments, positionMap, resolution, loop, point, fixedStep, ignoreYAxel);

            for (int i = precision; i > 0; i--)
            {
                //Needs to be lower then 1.999f here.
                fixedStep = fixedStep / 1.66f;
                float timeForwards = timeValue + fixedStep;
                float timeBackwards = timeValue - fixedStep;
                timeForwards = SplineUtility.GetValidatedTime(timeForwards, loop);
                timeBackwards = SplineUtility.GetValidatedTime(timeBackwards, loop);

                Vector3 pForward = GetPositionFast(positionMap, segments, resolution, timeForwards);
                float dForward = ignoreYAxel ? Vector2.Distance(new Vector2(pForward.x, pForward.z), new Vector2(point.x, point.z)) : Vector3.Distance(pForward, point);

                Vector3 pBackwards = GetPositionFast(positionMap, segments, resolution, timeBackwards);
                float dBackwards = ignoreYAxel ? Vector2.Distance(new Vector2(pBackwards.x, pBackwards.z), new Vector2(point.x, point.z)) : Vector3.Distance(pBackwards, point);

                if (dForward > dBackwards)
                {
                    timeValue = timeBackwards;
                }
                else
                {
                    timeValue = timeForwards;
                }
            }

            return timeValue;
        }

        public static NativeSegment ToNative(Vector3 anchor,
                              Vector3 tangentA,
                              Vector3 tangentB)
        {
            return new NativeSegment(anchor,
                                     tangentA,
                                     tangentB);
        }

        public static NativeSegment ToNative(Vector3 anchor,
                                              Vector3 tangentA,
                                              Vector3 tangentB,
                                              float length,
                                              float rotation,
                                              float contrast,
                                              float noise,
                                              Vector2 sadleSkew,
                                              Vector2 scale)
        {
            return new NativeSegment(anchor,
                                     tangentA,
                                     tangentB,
                                     length,
                                     rotation,
                                     contrast,
                                     noise,
                                     sadleSkew,
                                     scale);
        }

        public static NativeArray<NativeSegment> ToNativeArray(List<Segment> segments, NativeArray<NativeSegment> nativeSegments, Space space)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                nativeSegments[i] = ToNative(segments[i].GetPosition(ControlHandle.ANCHOR, space),
                                           segments[i].GetPosition(ControlHandle.TANGENT_A, space),
                                           segments[i].GetPosition(ControlHandle.TANGENT_B, space),
                                           segments[i].length,
                                           segments[i].ZRotation,
                                           segments[i].Contrast,
                                           segments[i].Noise,
                                           segments[i].SaddleSkew,
                                           segments[i].Scale);
            }

            return nativeSegments;
        }

        public static void CopyToNativeArray(List<Segment> segments, NativeArray<NativeSegment> nativeSegments, Space space)
        {
            int count = segments.Count;
            if (nativeSegments.Length < count)
                throw new ArgumentException($"nativeSegments too small: {nativeSegments.Length} < {count}");

            for (int i = 0; i < count; i++)
            {
                Segment s = segments[i];

                Vector3 a = s.GetPosition(ControlHandle.ANCHOR, space);
                Vector3 ta = s.GetPosition(ControlHandle.TANGENT_A, space);
                Vector3 tb = s.GetPosition(ControlHandle.TANGENT_B, space);

                nativeSegments[i] = ToNative(a, ta, tb, s.length, s.ZRotation, s.Contrast, s.Noise, s.SaddleSkew, s.Scale);
            }
        }

        public static NativeArray<NativeSegment> CreateNativeArray(List<Segment> segments, Space space, Allocator allocator)
        {
            NativeArray<NativeSegment> nativeSegments = new NativeArray<NativeSegment>(segments.Count, allocator);

            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];

                Vector3 a = s.GetPosition(ControlHandle.ANCHOR, space);
                Vector3 ta = s.GetPosition(ControlHandle.TANGENT_A, space);
                Vector3 tb = s.GetPosition(ControlHandle.TANGENT_B, space);

                nativeSegments[i] = ToNative(a, ta, tb, s.length, s.ZRotation, s.Contrast, s.Noise, s.SaddleSkew, s.Scale);
            }

            return nativeSegments;
        }

        public static NativeHashMap<int, float4x4> ToNativeHashMap(Dictionary<int, float4x4> dic, Allocator allocator)
        {
            NativeHashMap<int, float4x4> nativeHashMap = new NativeHashMap<int, float4x4>(dic.Count, allocator);
            foreach (KeyValuePair<int, float4x4> item in dic)
            {
                nativeHashMap.Add(item.Key, item.Value);
            }

            return nativeHashMap;
        }
    }
}
