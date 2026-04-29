// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: NormalsJob.cs
//
// Author: Mikael Danielsson
// Date Created: 05-11-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using Unity.Collections;

using SplineArchitect.Utility;

namespace SplineArchitect.Jobs
{
    [BurstCompile]
    public struct NormalsJob : IJob
    {
        public NativeList<Vector3> normals;

        [ReadOnly] public Vector3 splineUpDirection;
        [ReadOnly] public NativeArray<NativeSegment> nativeSegments;
        [ReadOnly] public float normalResolution;

        public void Execute()
        {
            normals.Clear();
            int steps = Mathf.CeilToInt(1f / normalResolution) + 1;
            float step = 1f / (steps - 1);

            Vector3 t0 = SplineUtilityNative.GetDirection(nativeSegments, 0f).normalized;
            Vector3 n0 = Vector3.Cross(t0, -splineUpDirection);
            n0 = n0.sqrMagnitude < 1e-6f ? Vector3.Cross(t0, Vector3.up).normalized : n0.normalized;
            Vector3 b0 = Vector3.Cross(t0, n0).normalized;

            normals.Add(n0);
            normals.Add(b0);
            normals.Add(t0);

            Vector3 prevT = t0;
            Vector3 prevN = n0;
            for (int i = 1; i < steps; i++)
            {
                float ti = Mathf.Min(i * step, 1f);
                Vector3 tiT = SplineUtilityNative.GetDirection(nativeSegments, ti).normalized;

                Vector3 v = (prevT + tiT);
                v.Normalize();

                Vector3 ni = prevN - 2f * Vector3.Dot(prevN, v) * v;

                Vector3 bi = Vector3.Cross(tiT, ni).normalized;

                normals.Add(ni);
                normals.Add(bi);
                normals.Add(tiT);

                prevT = tiT;
                prevN = ni;
            }
        }

        //public void Execute()
        //{
        //    int count = 0;
        //    Vector3 startUpDirection = splineUpDirection;

        //    for (float time = 0; time <= 1; time += normalResolution / 4)
        //    {
        //        float fixedTime = time;
        //        Vector3 zNormal = SplineUtilityNative.GetDirection(nativeSegments, fixedTime);

        //        Vector3 xNormal = Vector3.Cross(zNormal, -startUpDirection).normalized;
        //        startUpDirection = xNormal;
        //        Vector3 yNormal = Vector3.Cross(zNormal, xNormal).normalized;

        //        if (count == 0)
        //        {
        //            normals.Add(xNormal);
        //            normals.Add(yNormal);
        //            normals.Add(zNormal);
        //        }

        //        count++;
        //        if (count == 4)
        //            count = 0;
        //    }
        //}
    }
}
