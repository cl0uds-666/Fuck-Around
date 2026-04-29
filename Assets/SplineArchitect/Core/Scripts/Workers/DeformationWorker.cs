// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: DeformationWorker.cs
//
// Author: Mikael Danielsson
// Date Created: 02-07-2024
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

using SplineArchitect.Utility;
using SplineArchitect.Jobs;

namespace SplineArchitect.Workers
{
    internal class DeformationWorker : BaseWorker
    {
        public const int MAX_VERTICES = 30000;

        public int totalVertices { get; private set; }

        private int oldTotalVertices = -1;
        private int oldsplineObjectsCount = -1;

        private NativeArray<Vector3> vertices;
        private NativeArray<Vector3> meshNormals;
        private NativeArray<Vector4> meshTangents;
        private NativeHashMap<int, float4x4> localSpaces;
        private NativeArray<int> localSpaceMap;
        private NativeArray<bool> mirrorMap;
        private NativeArray<NormalType> soNormalTypeMap;
        private NativeArray<bool> alignToEndMap;
        private NativeArray<SnapData> snapDatas;

        private NativeArray<Vector3> forwardDir;
        private NativeArray<Vector3> upDir;
        private NativeArray<Vector3> rightDir;

        private JobHandle jobHandle;
        private DeformJob deformJob;
        private List<SplineObject> splineObjects;
        private List<SplineObject> emptySplineObjects;
        private HashSet<SplineObject> splineObjectsSet;

        private List<Vector3> localSplinePositions;
        private List<Quaternion> localSplineRotations;
        private List<Vector3> emptyLocalSplinePositions;
        private List<Quaternion> emptyLocalSplineRotations;

        public DeformationWorker(Spline spline = null) : base(spline)
        {
            splineObjects = new List<SplineObject>();
            emptySplineObjects = new List<SplineObject>();
            splineObjectsSet = new HashSet<SplineObject>();

            localSplinePositions = new List<Vector3>();
            localSplineRotations = new List<Quaternion>();
            emptyLocalSplinePositions = new List<Vector3>();
            emptyLocalSplineRotations = new List<Quaternion>();
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
                Debug.LogWarning("DeforamtionWorker allready working! Can't add splineObject to worker.");
                return;
            }
#endif

            if (so.Type != SplineObjectType.DEFORMATION)
            {
                Debug.LogWarning($"Can't add {so.name} so Deformation Worker becouse becouse it's not a deformation.");
                return;
            }

            if (splineObjectsSet.Contains(so))
                return;

            int vertices = 0;
            Mesh meshFilterMesh = so.MeshContainerCount > 0 ? so.GetMeshContainerAtIndex(0).GetInstanceMesh() : null;

            for (int i = 0; i < so.MeshContainerCount; i++)
            {
                MeshContainer mc = so.GetMeshContainerAtIndex(i);
                Mesh instanceMesh = mc.GetInstanceMesh();

                if (instanceMesh == null)
                    continue;

                //If mesh colliders uses the same mesh as the mesh filter, skip.
                if (i > 0 && meshFilterMesh != null && meshFilterMesh == instanceMesh)
                    continue;

                vertices += instanceMesh.vertexCount;
            }

            if(vertices == 0) emptySplineObjects.Add(so);
            else splineObjects.Add(so);

            totalVertices += vertices;
            splineObjectsSet.Add(so);

            if (workerState == WorkerState.EMPTY)
                workerState = WorkerState.NOT_EMPTY;

            if (totalVertices > MAX_VERTICES)
                workerState = WorkerState.FULL;
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

            for (int i = 0; i < emptySplineObjects.Count; i++)
            {
                SplineObject so2 = emptySplineObjects[i];

                if (so2 == so)
                {
                    emptySplineObjects[i] = null;
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

            if(workerState == WorkerState.WORKING)
                return;

            EnsureNativeCapacity();

            foreach (SplineObject emptySo in emptySplineObjects)
            {
                emptyLocalSplinePositions.Add(emptySo.localSplinePosition);
                emptyLocalSplineRotations.Add(emptySo.localSplineRotation);
            }

            if (splineObjects.Count > 0)
            {
                int offset = 0;

                for (int i = 0; i < splineObjects.Count; i++)
                {
                    SplineObject so = splineObjects[i];
                    localSplinePositions.Add(so.localSplinePosition);
                    localSplineRotations.Add(so.localSplineRotation);
#if UNITY_EDITOR
                    if (so == null)
                    {
                        Reset();
                        Debug.LogWarning("[Spline Architect] Found null splineObject, deformation job aborted!");
                        return;
                    }
#endif
                    localSpaces.Add(i, SplineObjectUtility.GetCombinedParentMatrixs(so));
                    Mesh meshFilterMesh = so.MeshContainerCount > 0 ? so.GetMeshContainerAtIndex(0).GetInstanceMesh() : null;

                    //MeshContainers
                    for (int i2 = 0; i2 < so.MeshContainerCount; i2++)
                    {
                        MeshContainer mc = so.GetMeshContainerAtIndex(i2);
                        Mesh instanceMesh = mc.GetInstanceMesh();

                        if (instanceMesh == null)
                            continue;

                        if (i2 > 0 && meshFilterMesh != null && meshFilterMesh == mc.GetInstanceMesh())
                            continue;

                        Vector3[] originVertices = HandleCachedResources.FetchOriginVertices(mc);
                        NativeArray<Vector3>.Copy(originVertices, 0, vertices, offset, originVertices.Length);

                        Vector3[] originNormals = HandleCachedResources.FetchOriginNormals(mc);
                        NativeArray<Vector3>.Copy(originNormals, 0, meshNormals, offset, originNormals.Length);

                        Vector4[] originTangents = HandleCachedResources.FetchOriginTangents(mc);
                        NativeArray<Vector4>.Copy(originTangents, 0, meshTangents, offset, originTangents.Length);

                        offset += originVertices.Length;
                    }

                    mirrorMap[i] = so.MirrorDeformation;
                    alignToEndMap[i] = so.AlignToEnd;
                    soNormalTypeMap[i] = so.NormalType;
                    localSpaceMap[i] = offset;

                    if (so.SnapSettings.snapMode != SnapMode.NONE) snapDatas[i] = so.CalculateSnapData();
                    else snapDatas[i] = new SnapData();
                }

                deformJob = CreateDeformJob(splineObjects.Count,
                            vertices,
                            meshNormals,
                            meshTangents,
                            forwardDir,
                            upDir,
                            rightDir,
                            spline.NativeSegmentsLocal,
                            localSpaces,
                            localSpaceMap,
                            mirrorMap,
                            SplineObjectType.DEFORMATION,
                            soNormalTypeMap,
                            alignToEndMap,
                            snapDatas);

                int batchCount = Mathf.Max(deformJob.vertices.Length / 1500, 1);
                jobHandle = deformJob.Schedule(deformJob.vertices.Length, batchCount);
            }

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

            int verticesId = 0;

            for (int i = 0; i < splineObjects.Count; i++)
            {
                SplineObject so = splineObjects[i];

                if (so == null)
                    continue;
#if UNITY_EDITOR
                Vector3 combinedScale = SplineObjectUtility.GetCombinedParentScales(so);
                if (GeneralUtility.IsZero(combinedScale.x) ||
                    GeneralUtility.IsZero(combinedScale.y) ||
                    GeneralUtility.IsZero(combinedScale.z))
                    continue;
#endif
                so.transform.SetLocalPositionAndRotation(localSplinePositions[i], localSplineRotations[i]);

                Mesh meshFilterMesh = so.MeshContainerCount > 0 ? so.GetMeshContainerAtIndex(0).GetInstanceMesh() : null;

                for (int i2 = 0; i2 < so.MeshContainerCount; i2++)
                {
                    MeshContainer mc = so.GetMeshContainerAtIndex(i2);
                    Mesh instanceMesh = mc.GetInstanceMesh();

                    if (instanceMesh == null)
                        continue;

                    //If mesh colliders uses the same mesh as the mesh filter, skip.
                    if (i2 > 0 && meshFilterMesh != null && meshFilterMesh == instanceMesh)
                        continue;

                    instanceMesh.MarkDynamic();
                    Vector3[] container = HandleCachedResources.FetchVerticeNormalContainer(mc);

                    //Vertices
                    NativeArray<Vector3>.Copy(deformJob.vertices, verticesId, container, 0, container.Length);
                    instanceMesh.SetVertices(container);
                    //Bounds
                    instanceMesh.RecalculateBounds();

                    //Normals
                    if ((int)so.NormalType < 2)
                    {
                        NativeArray<Vector3>.Copy(deformJob.meshNormals, verticesId, container, 0, container.Length);
                        instanceMesh.SetNormals(container);

                        Vector4[] tangentContainer = HandleCachedResources.FetchOriginTangentsContainer(mc);
                        NativeArray<Vector4>.Copy(deformJob.meshTangents, verticesId, tangentContainer, 0, tangentContainer.Length);
                        instanceMesh.SetTangents(tangentContainer);
                    }
                    else if(so.NormalType == NormalType.UNITY_CALCULATED || so.NormalType == NormalType.UNITY_CALCULATED_SEAMLESS)
                    {
                        instanceMesh.RecalculateNormals();
                        instanceMesh.RecalculateTangents();
                    }

                    //Updated colliders using the same mesh as meshFilter.
                    if (mc.IsMeshFilter())
                    {
                        for(int i3 = 0; i3 < so.MeshContainerCount; i3++)
                        {
                            MeshContainer mc2 = so.GetMeshContainerAtIndex(i3);
                            Mesh instanceMesh2 = mc2.GetInstanceMesh();
                            if (instanceMesh2 == null) continue;
                            //Need to set mesh like this else MeshColliders will not update properly. MeshFilter will work fine without this.
                            if (instanceMesh2 == instanceMesh) mc2.SetInstanceMesh(instanceMesh);
                        }
                    }
                    else
                    {
                        mc.SetInstanceMesh(instanceMesh);
                    }

                    verticesId += container.Length;
                }
            }

            for (int i = 0; i < emptySplineObjects.Count; i++)
            {
                SplineObject emptySo = emptySplineObjects[i];

                if (emptySo == null)
                    continue;

                emptySo.transform.SetLocalPositionAndRotation(emptyLocalSplinePositions[i], emptyLocalSplineRotations[i]);
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
            return splineObjects.Count + emptySplineObjects.Count;
        }

        public override void DisposeNativeData()
        {
            if(vertices.IsCreated)
            {
                vertices.Dispose();
                meshNormals.Dispose();
                meshTangents.Dispose();
                localSpaces.Dispose();
                localSpaceMap.Dispose();
                mirrorMap.Dispose();
                soNormalTypeMap.Dispose();
                alignToEndMap.Dispose();
                snapDatas.Dispose();

                forwardDir.Dispose();
                upDir.Dispose();
                rightDir.Dispose();
            }
        }

        private void Reset()
        {
            workerState = WorkerState.EMPTY;
            totalVertices = 0;

            if (localSpaces.IsCreated)
                localSpaces.Clear();

            splineObjects.Clear();
            emptySplineObjects.Clear();
            splineObjectsSet.Clear();
            localSplinePositions.Clear();
            localSplineRotations.Clear();
            emptyLocalSplinePositions.Clear();
            emptyLocalSplineRotations.Clear();
        }

        private void EnsureNativeCapacity()
        {
            if(totalVertices > oldTotalVertices || splineObjects.Count > oldsplineObjectsCount)
            {
                oldsplineObjectsCount = splineObjects.Count;
                oldTotalVertices = totalVertices;

                DisposeNativeData();

                //Out data
                vertices = new NativeArray<Vector3>(totalVertices, Allocator.Persistent);
                meshNormals = new NativeArray<Vector3>(totalVertices, Allocator.Persistent);
                meshTangents = new NativeArray<Vector4>(totalVertices, Allocator.Persistent);

                //In data
                localSpaceMap = new NativeArray<int>(splineObjects.Count, Allocator.Persistent);
                mirrorMap = new NativeArray<bool>(splineObjects.Count, Allocator.Persistent);
                soNormalTypeMap = new NativeArray<NormalType>(splineObjects.Count, Allocator.Persistent);
                alignToEndMap = new NativeArray<bool>(splineObjects.Count, Allocator.Persistent);
                snapDatas = new NativeArray<SnapData>(splineObjects.Count, Allocator.Persistent);
                localSpaces = new NativeHashMap<int, float4x4>(splineObjects.Count, Allocator.Persistent);

                forwardDir = new NativeArray<Vector3>(0, Allocator.Persistent);
                upDir = new NativeArray<Vector3>(0, Allocator.Persistent);
                rightDir = new NativeArray<Vector3>(0, Allocator.Persistent);
            }
        }
    }
}
