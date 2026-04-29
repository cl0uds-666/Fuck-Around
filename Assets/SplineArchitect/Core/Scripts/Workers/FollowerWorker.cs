// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: FollowerWorker.cs
//
// Author: Mikael Danielsson
// Date Created: 23-12-2025
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Mathematics;

using SplineArchitect.Utility;
using SplineArchitect.Jobs;

namespace SplineArchitect.Workers
{
    internal class FollowerWorker : BaseWorker
    {
        private int oldsplineObjectsCount = -1;
        private NativeArray<Vector3> points;
        private NativeArray<int> localSpaceMap;
        private NativeArray<Vector3> forwardDir;
        private NativeArray<Vector3> upDir;
        private NativeArray<Vector3> rightDir;
        private NativeHashMap<int, float4x4> localSpaces;
        private NativeArray<bool> alignToEndMap;

        private NativeArray<Vector3> meshNormals;
        private NativeArray<Vector4> meshTangents;
        private NativeArray<bool> mirrorMap;
        private NativeArray<NormalType> soNormalTypeMap;
        private NativeArray<SnapData> snapDatas;

        private JobHandle jobHandle;
        private DeformJob deformJob;
        private List<SplineObject> splineObjects;
        private HashSet<SplineObject> splineObjectsSet;
        private Vector3[] normalsContainer;

        public FollowerWorker(Spline spline = null) : base(spline)
        {
            splineObjects = new List<SplineObject>();
            splineObjectsSet = new HashSet<SplineObject>();
            normalsContainer = new Vector3[3];
        }

        public void Add(SplineObject so)
        {
#if UNITY_EDITOR
            if (so.SplineParent == null)
            {
                Debug.LogWarning($"[Spline Architect] Tried to add SplineObject {so.name} with null splineParent.");
                return;
            }

            if(workerState == WorkerState.WORKING)
            {
                Debug.LogWarning($"FollowerWorker allready working! Can't add splineObject {so.name} to worker.");
                return;
            }
#endif

            if(so.Type != SplineObjectType.FOLLOWER)
            {
                Debug.LogWarning($"Can't add {so.name} so Follower Worker becouse becouse it's not a follower.");
                return;
            }

            if(splineObjectsSet.Contains(so))
                return;

            splineObjects.Add(so);
            splineObjectsSet.Add(so);
            workerState = WorkerState.NOT_EMPTY;
        }

        public void Remove(SplineObject so)
        {
            for (int i = 0; i < splineObjects.Count; i++)
            {
                SplineObject so2 = splineObjects[i];

                if (so2 == so)
                {
                    splineObjects[i] = null;
                    return;
                }
            }
        }

        public bool Contains(SplineObject so)
        {
            return splineObjectsSet.Contains(so);
        }

        public void Deform(SplineObject so)
        {
            Add(so);
            Complete();
        }

        public override void Start()
        {
            if (spline == null || spline.segments.Count < 2)
            {
                Reset();
                return;
            }

            if (workerState == WorkerState.EMPTY)
                return;

            if (workerState == WorkerState.WORKING)
                return;

            EnsureNativeCapacity();

            for (int i = 0; i < splineObjects.Count; i++)
            {
                SplineObject so = splineObjects[i];
                points[i] = so.localSplinePosition;

                if (so.LockPosition && so.SoParent != null)
                    points[i] = Vector3.zero;

                int combinedParentHashCodes = SplineObjectUtility.GetCombinedParentHashCodes(so);
                if (!localSpaces.ContainsKey(combinedParentHashCodes)) localSpaces.Add(combinedParentHashCodes, SplineObjectUtility.GetCombinedParentMatrixs(so.SoParent));
                localSpaceMap[i] = combinedParentHashCodes;
                alignToEndMap[i] = so.AlignToEnd;
            }

            deformJob = CreateDeformJob(splineObjects.Count,
                                        points,
                                        meshNormals,
                                        meshTangents,
                                        forwardDir,
                                        upDir,
                                        rightDir,
                                        spline.NativeSegmentsLocal,
                                        localSpaces,
                                        localSpaceMap,
                                        mirrorMap,
                                        SplineObjectType.FOLLOWER,
                                        soNormalTypeMap,
                                        alignToEndMap,
                                        snapDatas);

            jobHandle = deformJob.Schedule(splineObjects.Count, 1);
            workerState = WorkerState.WORKING;
        }

        public override void Complete()
        {
            if ((int)workerState > 1)
                Start();

            if (workerState != WorkerState.WORKING)
            {
                Reset();
                return;
            }

            jobHandle.Complete();

            for (int i = 0; i < splineObjects.Count; i++)
            {
                SplineObject so = splineObjects[i];

                if (so == null)
                    continue;

                if (so.LockPosition)
                {
                    //deformJob.vertices[i] can be NaN in some rear cases in the editor. This fixes that.
                    Vector3 newPosition = Vector3.zero;
                    newPosition += deformJob.vertices[i];
                    newPosition += deformJob.forwardDir[i] * so.localSplinePosition.z;
                    newPosition += deformJob.upDir[i] * so.localSplinePosition.y;
                    newPosition += deformJob.rightDir[i] * so.localSplinePosition.x;

                    float fixedTime = spline.TimeToFixedTime(so.splinePosition.z / spline.Length);
                    spline.GetNormalsNonAlloc(normalsContainer, fixedTime);
                    //Quaternion parentRotations = SplineObjectUtility.GetCombinedParentRotations(so.soParent);
                    Quaternion localRotation = Quaternion.LookRotation(normalsContainer[2], normalsContainer[1]) *
                                               so.localSplineRotation;

                    so.transform.SetLocalPositionAndRotation(newPosition, localRotation);
                }
                else
                {
                    int axels = so.FollowAxels.x + so.FollowAxels.y + so.FollowAxels.z;
                    Quaternion newLocalRotation = so.transform.localRotation;
                    if (axels != 0)
                    {
                        Vector3 forward = deformJob.forwardDir[i];
                        Vector3 up = deformJob.upDir[i];
                        float combinedAxels = Mathf.Abs(forward.x) + Mathf.Abs(forward.y) + Mathf.Abs(forward.z);
                        if (GeneralUtility.IsZero(combinedAxels))
                        {
                            forward = Vector3.forward;
                            up = Vector3.up;
                        }
                        Quaternion localSplineRotation = Quaternion.LookRotation(forward, up);

                        //Set new local rotation. Order is relevant!
                        Quaternion parentRotations = SplineObjectUtility.GetCombinedParentRotations(so.SoParent);
                        newLocalRotation = Quaternion.Inverse(parentRotations) *         //1. Remove rotation from all so parents.
                                           localSplineRotation *                         //2. Add forward direction converted to rotation from Spline.
                                           (parentRotations * so.localSplineRotation);   //3. Add current parentRotations + localSplineRotation in that order!

                        if (axels != 3)
                        {
                            //Save old rotation euler
                            Vector3 euler = so.transform.localEulerAngles;

                            //Set world space rotation or splineSpace rotation
                            if (so.FollowAxels.x == 1)
                                euler.x = newLocalRotation.eulerAngles.x;
                            if (so.FollowAxels.y == 1)
                                euler.y = newLocalRotation.eulerAngles.y;
                            if (so.FollowAxels.z == 1)
                                euler.z = newLocalRotation.eulerAngles.z;

                            newLocalRotation = Quaternion.Euler(euler);
                        }
                    }

                    so.transform.SetLocalPositionAndRotation(deformJob.vertices[i], newLocalRotation);
                }
            }

            Reset();
        }

        public override void CompleteWithoutAssignData()
        {
            jobHandle.Complete();
            Reset();
        }

        public override int GetWorkCount()
        {
            return splineObjects.Count;
        }

        public override void DisposeNativeData()
        {
            if(points.IsCreated)
            {
                //Out data
                points.Dispose();
                forwardDir.Dispose();
                upDir.Dispose();
                rightDir.Dispose();
                //In data
                localSpaceMap.Dispose();
                alignToEndMap.Dispose();
                localSpaces.Dispose();
                //Empty
                mirrorMap.Dispose();
                soNormalTypeMap.Dispose();
                snapDatas.Dispose();
                meshNormals.Dispose();
                meshTangents.Dispose();
            }
        }

        private void Reset()
        {
            if(localSpaces.IsCreated)
                localSpaces.Clear();

            splineObjects.Clear();
            splineObjectsSet.Clear();
            workerState = WorkerState.EMPTY;
        }

        private void EnsureNativeCapacity()
        {
            if (oldsplineObjectsCount < splineObjects.Count)
            {
                oldsplineObjectsCount = splineObjects.Count;

                DisposeNativeData();

                //Out data
                points = new NativeArray<Vector3>(splineObjects.Count, Allocator.Persistent);
                forwardDir = new NativeArray<Vector3>(splineObjects.Count, Allocator.Persistent);
                upDir = new NativeArray<Vector3>(splineObjects.Count, Allocator.Persistent);
                rightDir = new NativeArray<Vector3>(splineObjects.Count, Allocator.Persistent);
                //In data
                localSpaceMap = new NativeArray<int>(splineObjects.Count, Allocator.Persistent);
                alignToEndMap = new NativeArray<bool>(splineObjects.Count, Allocator.Persistent);
                localSpaces = new NativeHashMap<int, float4x4>(0, Allocator.Persistent);
                //Empty
                mirrorMap = new NativeArray<bool>(0, Allocator.Persistent);
                soNormalTypeMap = new NativeArray<NormalType>(0, Allocator.Persistent);
                snapDatas = new NativeArray<SnapData>(0, Allocator.Persistent);
                meshNormals = new NativeArray<Vector3>(0, Allocator.Persistent);
                meshTangents = new NativeArray<Vector4>(0, Allocator.Persistent);
            }
        }
    }
}
