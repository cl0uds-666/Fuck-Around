// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: PointWorker.cs
//
// Author: Mikael PointWorker
// Date Created: 14-01-2026
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

using SplineArchitect.Jobs;

namespace SplineArchitect.Workers
{
    public class PointWorker : BaseWorker
    {
        private NativeList<Vector3> splinePositions;
        private NativeList<bool> alignToEndMap;
        private NativeList<int> localSpaceMap;
        private NativeList<Vector3> forwardDir;
        private NativeList<Vector3> upDir;
        private NativeList<Vector3> rightDir;
        private NativeHashMap<int, float4x4> localSpaces;

        private NativeArray<Vector3> meshNormals;
        private NativeArray<Vector4> meshTangents;
        private NativeArray<bool> mirrorMap;
        private NativeArray<NormalType> soNormalTypeMap;
        private NativeArray<SnapData> snapDatas;

        private List<PointWorkerData> pointWorkerDataContainer = new List<PointWorkerData>();
        private JobHandle jobHandle;
        private DeformJob deformJob;

        public PointWorker(Spline spline = null) : base(spline)
        {

        }

        public void Add(Vector3 splinePosition)
        {
            Add(splinePosition, float4x4.identity, false);
        }

        public void Add(Vector3 splinePosition, float4x4 matrix, bool alignToEnd)
        {
            if (!splinePositions.IsCreated)
            {
                splinePositions = new NativeList<Vector3>(4, Allocator.Persistent);
                forwardDir = new NativeList<Vector3>(4, Allocator.Persistent);
                upDir = new NativeList<Vector3>(4, Allocator.Persistent);
                rightDir = new NativeList<Vector3>(4, Allocator.Persistent);
                localSpaceMap = new NativeList<int>(4, Allocator.Persistent);
                alignToEndMap = new NativeList<bool>(4, Allocator.Persistent);
                localSpaces = new NativeHashMap<int, float4x4>(4, Allocator.Persistent);

                mirrorMap = new NativeArray<bool>(0, Allocator.Persistent);
                soNormalTypeMap = new NativeArray<NormalType>(0, Allocator.Persistent);
                snapDatas = new NativeArray<SnapData>(0, Allocator.Persistent);
                meshNormals = new NativeArray<Vector3>(0, Allocator.Persistent);
                meshTangents = new NativeArray<Vector4>(0, Allocator.Persistent);
            }

            splinePositions.Add(splinePosition);
            alignToEndMap.Add(alignToEnd);
            localSpaces.Add(splinePositions.Length - 1, matrix);
            localSpaceMap.Add(splinePositions.Length - 1);
            forwardDir.Add(Vector3.zero);
            upDir.Add(Vector3.zero);
            rightDir.Add(Vector3.zero);

            workerState = WorkerState.NOT_EMPTY;
        }

        public override void Start()
        {
            if (spline == null || spline.segments.Count < 2)
            {
                Reset();
                return;
            }

#if UNITY_EDITOR
            if (workerState == WorkerState.EMPTY)
            {
                Debug.LogWarning("Can't start job becouse FollowerWorker is empty.");
                return;
            }

            if(workerState == WorkerState.WORKING)
            {
                Debug.LogWarning("FollowerWorker allready working! Can't start job.");
                return;
            }
#endif

            deformJob = CreateDeformJob(splinePositions.Length,
                                        splinePositions.AsArray(),
                                        meshNormals,
                                        meshTangents,
                                        forwardDir.AsArray(),
                                        upDir.AsArray(),
                                        rightDir.AsArray(),
                                        spline.NativeSegmentsLocal,
                                        localSpaces,
                                        localSpaceMap.AsArray(),
                                        mirrorMap,
                                        SplineObjectType.FOLLOWER,
                                        soNormalTypeMap,
                                        alignToEndMap.AsArray(),
                                        snapDatas);

            jobHandle = deformJob.Schedule(splinePositions.Length, 1);
            workerState = WorkerState.WORKING;
        }

        public override void Complete()
        {
            pointWorkerDataContainer.Clear();
            Complete(pointWorkerDataContainer, 1);
        }

        public void Complete(List<PointWorkerData> pointWorkerData, int maxAmount = int.MaxValue)
        {
            if ((int)workerState > 1)
                Start();

            if (workerState != WorkerState.WORKING)
            {
                Reset();
                return;
            }

            jobHandle.Complete();

            for (int i = 0; i < deformJob.vertices.Length; i++)
            {
                Vector3 point = deformJob.vertices[i];
                Vector3 forward = deformJob.forwardDir[i];
                Vector3 up = deformJob.upDir[i];
                Vector3 right = deformJob.rightDir[i];
                pointWorkerData.Add(new PointWorkerData(point, forward, up, right));

                if (i >= maxAmount)
                    break;
            }

            Reset();
        }

        public void Complete(out PointWorkerData p0)
        {
            pointWorkerDataContainer.Clear();
            Complete(pointWorkerDataContainer, 1);
            p0 = pointWorkerDataContainer[0];
        }

        public void Complete(out PointWorkerData p0, out PointWorkerData p1)
        {
            pointWorkerDataContainer.Clear();
            Complete(pointWorkerDataContainer, 2);
            p0 = pointWorkerDataContainer[0];
            p1 = pointWorkerDataContainer[1];
        }

        public void Complete(out PointWorkerData p0, out PointWorkerData p1, out PointWorkerData p2)
        {
            pointWorkerDataContainer.Clear();
            Complete(pointWorkerDataContainer, 3);
            p0 = pointWorkerDataContainer[0];
            p1 = pointWorkerDataContainer[1];
            p2 = pointWorkerDataContainer[2];
        }

        public void Complete(out PointWorkerData p0, out PointWorkerData p1, out PointWorkerData p2, out PointWorkerData p3)
        {
            pointWorkerDataContainer.Clear();
            Complete(pointWorkerDataContainer, 4);
            p0 = pointWorkerDataContainer[0];
            p1 = pointWorkerDataContainer[1];
            p2 = pointWorkerDataContainer[2];
            p3 = pointWorkerDataContainer[3];
        }

        public void Complete(out PointWorkerData p0, out PointWorkerData p1, out PointWorkerData p2, out PointWorkerData p3, out PointWorkerData p4)
        {
            pointWorkerDataContainer.Clear();
            Complete(pointWorkerDataContainer, 5);
            p0 = pointWorkerDataContainer[0];
            p1 = pointWorkerDataContainer[1];
            p2 = pointWorkerDataContainer[2];
            p3 = pointWorkerDataContainer[3];
            p4 = pointWorkerDataContainer[4];
        }

        public override int GetWorkCount()
        {
            return splinePositions.Length;
        }

        public override void CompleteWithoutAssignData()
        {
            jobHandle.Complete();
            Reset();
        }

        public override void DisposeNativeData()
        {
            if(splinePositions.IsCreated)
            {
                splinePositions.Dispose();
                localSpaceMap.Dispose();
                forwardDir.Dispose();
                upDir.Dispose();
                rightDir.Dispose();
                alignToEndMap.Dispose();
                localSpaces.Dispose();

                meshNormals.Dispose();
                meshTangents.Dispose();
                mirrorMap.Dispose();
                soNormalTypeMap.Dispose();
                snapDatas.Dispose();
            }
        }

        private void Reset()
        {
            if (splinePositions.IsCreated)
            {
                splinePositions.Clear();
                localSpaces.Clear();
                localSpaceMap.Clear();
                forwardDir.Clear();
                upDir.Clear();
                rightDir.Clear();
                alignToEndMap.Clear();
            }
            workerState = WorkerState.EMPTY;
        }
    }
}
