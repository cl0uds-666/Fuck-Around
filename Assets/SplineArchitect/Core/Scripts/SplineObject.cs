// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: SplineObject.cs
//
// Author: Mikael Danielsson
// Date Created: 28-03-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections;
using System;

using UnityEngine;
using Unity.Mathematics;

using SplineArchitect.Utility;
using SplineArchitect.Monitor;
using SplineArchitect.Workers;

namespace SplineArchitect
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public partial class SplineObject : MonoBehaviour
    {
        public const int dataUsage = 32 + 
                                     1 + 12 + 16 + 12 + 1 + 1 + 1 + 8 + 8 + 40 + 40 + 1 + 4 + 1 + MonitorSplineObject.dataUsage + 1 + 1;

        // Static data
        public static SplineObjectType defaultType = SplineObjectType.DEFORMATION;
        private static List<Vector3> normalsContainer = new List<Vector3>();
        private static List<Vector4> tangentsContainer = new List<Vector4>();
        private static List<int> trianglesContainer = new List<int>();

        // Public stored data
        [HideInInspector] public Vector3 localSplinePosition;
        [HideInInspector] public Quaternion localSplineRotation = Quaternion.identity;

        // Private stored data
        [SerializeField, HideInInspector] internal ComponentMode componentMode = ComponentMode.REMOVE_FROM_BUILD;
        [SerializeField, HideInInspector] internal MeshMode meshMode = MeshMode.SAVE_IN_BUILD;
        [SerializeField, HideInInspector] internal SnapSettings snapSettings;
        [SerializeField, HideInInspector] private bool lockPosition;
        [SerializeField, HideInInspector] private SplineObjectType type = SplineObjectType.NOT_SET;
        [SerializeField, HideInInspector] private NormalType normalType;
        [SerializeField, HideInInspector] private bool mirrorDeformation;
        [SerializeField, HideInInspector] private bool alignToEnd;
        [SerializeField, HideInInspector] private Vector3Int followAxels = Vector3Int.one;
        [SerializeField, HideInInspector] private List<MeshContainer> meshContainers;
        [SerializeField, HideInInspector] private Spline splineParent;
        [SerializeField, HideInInspector] private SplineObject soParent;
        [SerializeField, HideInInspector] private int version;

        // Private runtime data
        [NonSerialized] private MonitorSplineObject monitor;
        [NonSerialized] private int oldVersion;

        // Properties
        internal MonitorSplineObject Monitor => monitor;
        internal int MeshContainerCount => meshContainers != null ? meshContainers.Count : 0;
        public Vector3 splinePosition
        {
            get
            {
                if (soParent == null) return localSplinePosition;
                return math.transform(SplineObjectUtility.GetCombinedParentMatrixs(soParent), localSplinePosition);
            }
            set
            {
                localSplinePosition = math.transform(math.inverse(SplineObjectUtility.GetCombinedParentMatrixs(soParent)), value);
            }
        }
        public Quaternion splineRotation
        {
            get
            {
                if (soParent == null) return localSplineRotation;
                return SplineObjectUtility.GetCombinedParentRotations(soParent) * localSplineRotation;
            }
            set
            {
                localSplineRotation = Quaternion.Inverse(SplineObjectUtility.GetCombinedParentRotations(soParent)) * value;
            }
        }
        public Spline SplineParent => splineParent;
        public SplineObject SoParent => soParent;
        public Vector3Int FollowAxels
        {
            get => followAxels;
            set
            {
                if (value == followAxels)
                    return;

                followAxels = value;
            }
        }
        public bool AlignToEnd
        {
            get => alignToEnd;
            set
            {
                if (value == alignToEnd)
                    return;

                alignToEnd = value;
                version++;
            }
        }
        public bool MirrorDeformation
        {
            get => mirrorDeformation;
            set
            {
                if (mirrorDeformation == value)
                    return;

                mirrorDeformation = value;
                version++;
            }
        }
        public NormalType NormalType
        {
            get => normalType;
            set
            {
                if (normalType == value)
                    return;

                normalType = value;
                version++;
            }
        }
        public SplineObjectType Type
        {
            get => type;
            set
            {
                if (type == value)
                    return;

                if (value == SplineObjectType.NOT_SET)
                    throw new ArgumentException("SplineObjectType cannot be NOT_SET.", nameof(value));

                type = value;
                version++;
            }
        }
        public SnapSettings SnapSettings
        {
            get => snapSettings;
            set
            {
                if (GeneralUtility.IsEqual(snapSettings.startSnapDistance, value.startSnapDistance) &&
                    GeneralUtility.IsEqual(snapSettings.startSnapOffset, value.startSnapOffset) &&
                    GeneralUtility.IsEqual(snapSettings.endSnapDistance, value.endSnapDistance) &&
                    GeneralUtility.IsEqual(snapSettings.endSnapOffset, value.endSnapOffset) &&
                    GeneralUtility.IsEqual(snapSettings.snapTargetPoint, value.snapTargetPoint) &&
                    snapSettings.snapMode == value.snapMode)
                    return;

                snapSettings = value;
                version++;
            }
        }
        public bool LockPosition
        {
            get => lockPosition;
            set
            {
                if (lockPosition == value)
                    return;

                lockPosition = value;
                version++;
            }
        }

        public bool IsEnabled()
        {
#if UNITY_EDITOR
            if (gameObject == null)
                return false;
#endif
            if (gameObject.activeInHierarchy && enabled)
                return true;

            return false;
        }

        /// <summary>
        /// Enables or disables rendering for all meshes on this spline object.
        /// If the object uses colliders, they are also enabled or disabled, affecting collision.
        /// </summary>
        public void RenderMeshes(bool value)
        {
            if(type == SplineObjectType.DEFORMATION)
            {
                foreach (MeshContainer mc in meshContainers)
                {
                    mc.RenderMesh(value);
                }
            }
            else
            {
                TryGetComponent(out MeshRenderer meshRenderer);
                if(meshRenderer != null) meshRenderer.enabled = value;

                TryGetComponent(out MeshCollider meshCollider);
                if (meshCollider != null) meshCollider.enabled = value;
            }
        }

        /// <summary>
        /// Returns true if the meshes are currently visible and their colliders (if any) are enabled.
        /// </summary>
        public bool AreMeshesRendered()
        {
            if (type == SplineObjectType.DEFORMATION)
            {
                foreach (MeshContainer mc in meshContainers)
                {
                    if (!mc.IsMeshRendered())
                        return false;
                }
            }
            else
            {
                TryGetComponent(out MeshRenderer meshRenderer);
                if (meshRenderer != null && !meshRenderer.enabled) return false;

                TryGetComponent(out MeshCollider meshCollider);
                if (meshCollider != null && !meshCollider.enabled) return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if this spline object is a parent, grandparent, 
        /// or any higher-level ancestor of the given spline object.
        /// </summary>
        public bool IsAncestorOf(SplineObject so)
        {
            SplineObject parent = so.soParent;

            for (int i = 0; i < 25; i++)
            {
                if (parent == null)
                    return false;

                if (parent == this)
                    return true;

                parent = parent.soParent;
            }

            return false;
        }

        /// <summary>
        /// Scans the GameObject for MeshFilter and MeshCollider components 
        /// and creates MeshContainer entries for any that are not yet registered.
        /// Call SyncInstanceMeshesFromCache() after this to create 
        /// instance meshes for each MeshContainer created by this function.
        /// </summary>
        public void SyncMeshContainers()
        {
            for (int i = 0; i < gameObject.GetComponentCount(); i++)
            {
                Component component = gameObject.GetComponentAtIndex(i);
                MeshFilter meshFilter = component as MeshFilter;
                MeshCollider meshCollider = component as MeshCollider;

                if (meshFilter == null && meshCollider == null)
                    continue;

                Mesh sharedMesh = null;
                if (meshFilter != null) sharedMesh = meshFilter.sharedMesh;
                else if (meshCollider != null) sharedMesh = meshCollider.sharedMesh;

                if (sharedMesh == null)
                    continue;

                bool allreadyExists = false;
                foreach (MeshContainer mc2 in meshContainers)
                {
                    if (mc2 != null && mc2.Contains(component))
                    {
                        allreadyExists = true;
                        break;
                    }
                }

                if (allreadyExists)
                    continue;

                AddMeshContainer(new MeshContainer(component));
            }
        }

        /// <summary>
        /// Updates the instance meshes for all MeshContainers.
        /// Call this after changing the mesh on a MeshFilter 
        /// or MeshCollider to fetch or create a new cached instance mesh
        /// and ensure it is properly deformed by the spline.
        /// </summary>
        public void SyncInstanceMeshesFromCache()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                if (mc == null)
                    continue;

                Mesh oldInstanceMesh = mc.GetInstanceMesh();
                Mesh instanceMesh = HandleCachedResources.FetchInstanceMesh(mc);

                mc.SetInstanceMesh(instanceMesh);

                if (oldInstanceMesh == instanceMesh)
                    continue;

                MarkVersionDirty();
            }
        }

        internal void MarkVersionDirty()
        {
            version++;
        }

        internal bool IsVersionDirty()
        {
            bool value = oldVersion != version;
            oldVersion = version;

            return value;
        }

        internal MeshContainer GetMeshContainerAtIndex(int index)
        {
            return meshContainers[index];
        }

        internal void AddMeshContainer(MeshContainer mc)
        {
            if (mc.IsMeshFilter()) meshContainers.Insert(0, mc);
            else meshContainers.Add(mc);
        }

        internal void RemoveMeshContainer(MeshContainer mc)
        {
            meshContainers.Remove(mc);
        }

        internal void RemoveMeshContainerAt(int index)
        {
            meshContainers.RemoveAt(index);
        }

        internal void UpdateExternalComponents(bool useOriginMesh = false)
        {
            for (int i = 0; i < gameObject.GetComponentCount(); i++)
            {
                Component component = gameObject.GetComponentAtIndex(i);

                if (component == null)
                    continue;

                //Update primitive colliders
                if (component is Collider)
                {
                    Mesh mesh = null;

                    if (meshContainers != null && meshContainers.Count > 0)
                    {
                        mesh = meshContainers[0].GetInstanceMesh();

                        if (useOriginMesh)
                            mesh = meshContainers[0].GetOriginMesh();

                        if (mesh == null)
                        {
                            for (int i2 = 0; i2 < gameObject.GetComponentCount(); i2++)
                            {
                                MeshFilter meshFilter = gameObject.GetComponentAtIndex(i) as MeshFilter;
                                MeshCollider meshCollider = gameObject.GetComponentAtIndex(i) as MeshCollider;

                                if (meshFilter != null)
                                {
                                    mesh = meshFilter.sharedMesh;
                                    break;
                                }

                                if (meshCollider != null)
                                {
                                    mesh = meshCollider.sharedMesh;
                                    break;
                                }
                            }
                        }
                    }

                    Collider collider = component as Collider;

                    if (collider is BoxCollider)
                    {
                        BoxCollider boxCollider = collider as BoxCollider;
                        if (mesh != null)
                        {
                            boxCollider.center = mesh.bounds.center;
                            boxCollider.size = mesh.bounds.size;
                        }
                        else
                        {
                            Vector3 center = transform.position;
                            if (splineParent != null) center = transform.InverseTransformPoint(splineParent.SplinePositionToWorldPosition(splinePosition));
                            boxCollider.center = center;
                        }
                    }
                    else if (collider is SphereCollider)
                    {
                        SphereCollider sphereCollider = collider as SphereCollider;
                        if (mesh != null)
                        {
                            sphereCollider.center = mesh.bounds.center;
                            float r = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y);
                            sphereCollider.radius = Mathf.Max(r, mesh.bounds.extents.z);

                        }
                        else
                        {
                            Vector3 center = transform.position;
                            if (splineParent != null) center = transform.InverseTransformPoint(splineParent.SplinePositionToWorldPosition(splinePosition));
                            sphereCollider.center = center;
                        }
                    }
                    else if (collider is CapsuleCollider)
                    {
                        CapsuleCollider capsuleCollider = collider as CapsuleCollider;
                        if (mesh != null)
                        {
                            capsuleCollider.center = mesh.bounds.center;
                            capsuleCollider.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z);
                            capsuleCollider.height = Mathf.Max(mesh.bounds.size.y, capsuleCollider.radius);
                        }
                        else
                        {
                            Vector3 center = transform.position;
                            if (splineParent != null) center = transform.InverseTransformPoint(splineParent.SplinePositionToWorldPosition(splinePosition));
                            capsuleCollider.center = center;
                        }
                    }
                }
                //Update LODGroup
                else if (component is LODGroup)
                {
                    LODGroup lodGroup = component as LODGroup;
                    lodGroup.RecalculateBounds();

                    if (lodGroup.animateCrossFading && lodGroup.fadeMode != LODFadeMode.None && type == SplineObjectType.DEFORMATION)
                        Debug.LogWarning("[Spline Architect] Using Animate Cross-fading on a Deformation with LOD Group may have undesired consequences.");
                }
            }
        }

        internal void ClearMeshContainers()
        {
            meshContainers.Clear();
        }

        internal void CacheUntrackedInstanceMeshes()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                if (mc == null)
                    continue;

                Mesh instanceMesh = mc.GetInstanceMesh();
                Mesh originMesh = mc.GetOriginMesh();

                if (instanceMesh == null || originMesh == null)
                    continue;

                if (instanceMesh == originMesh)
                    continue;

                if (HandleCachedResources.IsInstanceMeshCached(instanceMesh))
                    continue;

                HandleCachedResources.AddOrUpdateInstanceMesh(mc);
            }
        }

        internal void SetInstanceMeshesToOriginMesh()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                if (mc == null)
                    continue;

                Mesh originMesh = mc.GetOriginMesh();
                if (originMesh == null) continue;

                mc.SetInstanceMeshToOriginMesh();
            }
        }

        internal void DestroyAllInstanceMeshes()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                Mesh instanceMesh = mc.GetInstanceMesh();

                if (instanceMesh == null)
                    continue;

                Destroy(instanceMesh);
            }
        }

        internal void SetOriginNormalsOnAll()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                Mesh sharedMesh = mc.GetInstanceMesh();
                Mesh originMesh = mc.GetOriginMesh();

                if (sharedMesh == null)
                    continue;

                if (sharedMesh == originMesh)
                    continue;

                normalsContainer.Clear();
                originMesh.GetNormals(normalsContainer);
                sharedMesh.SetNormals(normalsContainer);
            }
        }

        internal void SetOriginTangentsOnAll()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                Mesh sharedMesh = mc.GetInstanceMesh();
                Mesh originMesh = mc.GetOriginMesh();

                if (sharedMesh == null)
                    continue;

                if (sharedMesh == originMesh)
                    continue;

                tangentsContainer.Clear();
                originMesh.GetTangents(tangentsContainer);
                sharedMesh.SetTangents(tangentsContainer);
            }
        }

        internal void SetOriginTrianglesOnAll()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                Mesh sharedMesh = mc.GetInstanceMesh();
                Mesh originMesh = mc.GetOriginMesh();

                if (sharedMesh == null)
                    continue;

                if (sharedMesh == originMesh)
                    continue;

                for (int i = 0; i < originMesh.subMeshCount; i++)
                {
                    trianglesContainer.Clear();
                    originMesh.GetTriangles(trianglesContainer, i);
                    sharedMesh.SetTriangles(trianglesContainer, i);
                }
            }
        }

        internal void SetSeamlessTrianglesOnAll()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                MeshUtility.SetSeamlessTriangles(mc);
            }
        }

        internal void ReverseTrianglesOnAll()
        {
            foreach (MeshContainer mc in meshContainers)
            {
                Mesh sharedMesh = mc.GetInstanceMesh();
                Mesh originMesh = mc.GetOriginMesh();

                if (sharedMesh == null)
                    continue;

                if (sharedMesh == originMesh)
                    continue;

                MeshUtility.ReverseTriangles(sharedMesh);
            }
        }

        internal SnapData CalculateSnapData()
        {
            SnapData snapData = new SnapData();

            if (meshContainers == null || meshContainers.Count == 0)
            {
                return snapData;
            }

            Bounds localBounds = meshContainers[0].GetOriginMesh().bounds;
            Bounds transformedBounds = GeneralUtility.TransformBounds(localBounds, SplineObjectUtility.GetCombinedParentMatrixs(this));

            snapData.soStartPoint = splinePosition.z - transformedBounds.extents.z + localBounds.center.z;
            snapData.soEndPoint = splinePosition.z + transformedBounds.extents.z + localBounds.center.z;

            snapData.snapStartPoint = GetClosestPoint(snapSettings.snapMode, snapData.soStartPoint, out float startDistance);
            snapData.snapEndPoint = GetClosestPoint(snapSettings.snapMode, snapData.soEndPoint, out float endDistance);

            float midPoint = (snapData.soStartPoint + snapData.soEndPoint) / 2;

            if (midPoint > snapData.snapStartPoint && startDistance < snapSettings.startSnapDistance)
            {
                snapData.start = true;
            }

            if (midPoint < snapData.snapEndPoint && endDistance < snapSettings.endSnapDistance)
            {
                snapData.end = true;
            }

            snapData.snapStartPoint += snapSettings.startSnapOffset;
            snapData.snapEndPoint += snapSettings.endSnapOffset;

            return snapData;

            float GetClosestPoint(SnapMode snapMode, float point, out float distance)
            {
                float closestPoint = 0;
                float dCheck = 99999;

                distance = dCheck;

                if (snapMode == SnapMode.CONTROL_POINTS)
                {
                    foreach (Segment s in splineParent.segments)
                    {
                        if (s.IgnoreSnapping)
                            continue;

                        float zPoint = s.zPosition;
                        if (alignToEnd) zPoint = s.SplineParent.Length - s.zPosition;
                        float d = Mathf.Abs(zPoint - point);

                        if (dCheck > d)
                        {
                            dCheck = d;
                            distance = d;
                            closestPoint = zPoint;
                        }
                    }
                }
                else if (snapMode == SnapMode.SPLINE_POINT)
                {
                    distance = Mathf.Abs(snapSettings.snapTargetPoint - point); ;
                    closestPoint = snapSettings.snapTargetPoint;
                }

                return closestPoint;
            }
        }

        private Spline TryFindSplineParent()
        {
            Transform _transform = transform;
            Spline spline = null;
            SplineObject so;

            for (int i = 0; i < 25; i++)
            {
                if (_transform.parent == null)
                    break;

                _transform = _transform.parent;
                spline = _transform.GetComponent<Spline>();
                so = _transform.GetComponent<SplineObject>();

                //Skip if parent is not spline deformation
                if (so != null && so.type != SplineObjectType.DEFORMATION)
                    break;

                if (spline != null)
                    return spline;
            }

            return spline;
        }
    }
}
