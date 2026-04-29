// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: BaseWorker.cs
//
// Author: Mikael Danielsson
// Date Created: 14-01-2026
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

using SplineArchitect.Jobs;

namespace SplineArchitect.Workers
{
    public abstract class BaseWorker
    {
        public WorkerState workerState { get; protected set; }
        protected Spline spline;

        protected BaseWorker(Spline spline)
        {
            this.spline = spline;
            spline?.allWorkers.Add(this);
        }

        public void SetSpline(Spline spline)
        {
            this.spline?.allWorkers.Remove(this);
            this.spline = spline;
            spline.allWorkers.Add(this);
        }

        public abstract void Start();
        public abstract void Complete();
        public abstract void CompleteWithoutAssignData();
        public abstract int GetWorkCount();
        public abstract void DisposeNativeData();

        protected DeformJob CreateDeformJob(int splineObjectCount,
                                  NativeArray<Vector3> vertices,
                                  NativeArray<Vector3> meshNormals,
                                  NativeArray<Vector4> meshTangents,
                                  NativeArray<Vector3> forwardDir,
                                  NativeArray<Vector3> upDir,
                                  NativeArray<Vector3> rightDir,
                                  NativeArray<NativeSegment> nativeSegments,
                                  NativeHashMap<int, float4x4> localSpaces,
                                  NativeArray<int> localSpaceMap,
                                  NativeArray<bool> mirrorMap,
                                  SplineObjectType deformationType,
                                  NativeArray<NormalType> soNormalTypeMap,
                                  NativeArray<bool> alignToEndMap,
                                  NativeArray<SnapData> snapDatas)
        {
            Vector3 splineUp = spline.SplineType == SplineType.STATIC_2D ? -Vector3.forward : Vector2.up;

            DeformJob deformJob = new DeformJob()
            {
                splineObjectCount = splineObjectCount,
                vertices = vertices,
                meshNormals = meshNormals,
                meshTangents = meshTangents,
                forwardDir = forwardDir,
                upDir = upDir,
                rightDir = rightDir,
                localSpaces = localSpaces,
                localSpaceMap = localSpaceMap,
                soNormalTypeMap = soNormalTypeMap,
                alignToEndMap = alignToEndMap,
                mirrorMap = mirrorMap,
                splineUpDirection = splineUp,
                nativeSegments = nativeSegments,
                noises = spline.NativeNoises,
                splineLength = spline.Length,
                distanceMap = spline.DistanceMap,
                normalsArray = spline.NormalsLocal,
                positionMap = spline.PositionMapLocal,
                splineResolution = spline.GetSplineResolution(),
                loop = spline.Loop,
                splineType = spline.SplineType,
                deformationType = deformationType,
                snapDatas = snapDatas
            };

            return deformJob;
        }
    }
}
