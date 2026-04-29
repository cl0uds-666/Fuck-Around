// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: BezierUtility.cs
//
// Author: Mikael Danielsson
// Date Created: 11-06-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace SplineArchitect.Utility
{
    public static class BezierUtility
    {
        public static Vector3 Linear(Vector3 a, Vector3 b, float t)
        {
            float u = 1f - t;
            return a * u + b * t;
        }

        public static Vector3 Quadratic(Vector3 a, Vector3 at, Vector3 b, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            return a * uu + at * (2f * u * t) + b * tt;
        }

        public static Vector3 Cubic(Vector3 a, Vector3 ata, Vector3 btb, Vector3 b, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return a * uuu
                   + ata * (3f * uu * t)
                   + btb * (3f * u * tt)
                   + b * ttt;
        }

        public static Vector3 GetTangent(Vector3 a, Vector3 ata, Vector3 btb, Vector3 b, float t)
        {
            float u = 1 - t;
            Vector3 tangent = 3 * u * u * (ata - a);
            tangent += 6 * u * t * (btb - ata);
            tangent += 3 * t * t * (b - btb);
            return Vector3.Normalize(tangent);
        }
    }
}
