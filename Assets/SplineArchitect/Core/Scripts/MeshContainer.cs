// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: MeshContainer.cs
//
// Author: Mikael Danielsson
// Date Created: 06-04-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;

using UnityEngine;
using UnityEngine.SceneManagement;

using SplineArchitect.Utility;

namespace SplineArchitect
{
    [Serializable]
    internal partial class MeshContainer
    {
        public const int dataUsage = 24 + 
                                     8 + 8 + 8 + 8 + 64 + 64 + 4;

        // Stored data
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshCollider meshCollider;
        [SerializeField] private Mesh originMesh;
        [SerializeField] private long timestamp;

        // Runetime data
        [NonSerialized] private string resourceKey;
        [NonSerialized] private string resourceKeyShort;
        [NonSerialized] private int oldTransformsInstanceId;
        [NonSerialized] private int oldOriginMeshId;

        public MeshContainer(Component component)
        {
            MeshFilter meshFilter = component as MeshFilter;
            MeshCollider meshCollider = component as MeshCollider;

            if (meshFilter == null && meshCollider == null)
                throw new InvalidOperationException($"Both MeshFilter and MeshCollider cant be null.");
            else if (meshFilter != null && meshCollider != null)
                throw new InvalidOperationException($"Can't contain a valid MeshCollider and MeshFilter. Can only contain one of them.");

            this.meshFilter = meshFilter;
            this.meshCollider = meshCollider;

            if (meshFilter != null)
                originMesh = meshFilter.sharedMesh;

            if (meshCollider != null)
                originMesh = meshCollider.sharedMesh;

#if UNITY_EDITOR
            EnsureValidOriginMesh();
            TryUpdateTimestamp();
#endif
        }

        public void SetInstanceMesh(Mesh instanceMesh)
        {
#if UNITY_EDITOR
            if (originMesh == null)
                return;

            if (instanceMesh == originMesh)
            {
                Debug.LogError("[Spline Architect] InstanceMesh and OriginMesh is the same!");
                return;
            }

            instanceMesh.name = GetResourceKey();
#endif

            if (meshFilter != null) 
                meshFilter.sharedMesh = instanceMesh;
            else if (meshCollider != null) 
                meshCollider.sharedMesh = instanceMesh;
            else
                Debug.LogError($"[Spline Architect] Could not find MeshFilter or MeshCollider for: {instanceMesh.name}");
        }

        public void SetOriginMesh(Mesh originMesh)
        {
#if UNITY_EDITOR
            string path = GeneralUtility.GetAssetPathOnlyEditor(originMesh);
            if (path == "")
            {
                if(originMesh != null) 
                    Debug.LogError($"[Spline Architect] Can't set origin mesh! {originMesh.name} does not have an asset path!");
                else
                    Debug.LogError($"[Spline Architect] Can't set origin mesh! OriginMesh does not have an asset path!");
                return;
            }
#endif

            this.originMesh = originMesh;
        }

        public Mesh GetInstanceMesh()
        {
            if (meshCollider != null && meshCollider.sharedMesh != null)
                return meshCollider.sharedMesh;
            else if (meshFilter != null && meshFilter.sharedMesh != null)
                return meshFilter.sharedMesh;
            else 
                return null;
        }

        public Mesh GetOriginMesh()
        {
            return originMesh;
        }

        public void SetInstanceMeshToOriginMesh()
        {
            if (meshFilter != null)
                meshFilter.sharedMesh = originMesh;
            else if (meshCollider != null)
                meshCollider.sharedMesh = originMesh;
            else
                Debug.LogError($"[Spline Architect] Could not find MeshFilter or MeshCollider for: {originMesh.name}");
        }

        public Component GetMeshContainerComponent()
        {
            if (meshFilter != null) 
                return meshFilter;
            else 
                return meshCollider;
        }

        public bool IsMeshFilter()
        {
            if (meshFilter != null) return true;
            return false;
        }

        public bool Contains(Component component)
        {
            if (component == null)
                return false;

            if (meshFilter == component) return true;
            if (meshCollider == component) return true;
            return false;
        }

        public bool MeshContainerExist()
        {
            if (meshCollider != null) return true;
            if (meshFilter != null) return true;
            return false;
        }

        public string GetResourceKey()
        {
            if (string.IsNullOrEmpty(resourceKey) || oldTransformsInstanceId != GetMeshContainerComponent().transform.GetInstanceID() || oldOriginMeshId != originMesh.GetInstanceID())
                UpdateResourceKey();

            return resourceKey;
        }

        public string GetResourceKeyShort()
        {
            if (string.IsNullOrEmpty(resourceKeyShort))
                UpdateResourceKey();

            return resourceKeyShort;
        }

        public void RenderMesh(bool value)
        {
            if (meshFilter != null)
            {
                if(meshFilter.TryGetComponent(out MeshRenderer meshRender))
                    meshRender.enabled = value;
            }
            else if (meshCollider != null)
                meshCollider.enabled = value;
        }

        public bool IsMeshRendered()
        {
            if(meshFilter != null)
            {
                if(meshFilter.TryGetComponent(out MeshRenderer meshRender))
                    return meshRender.enabled;
            }
            else if (meshCollider != null)
                return meshCollider.enabled;

            return false;
        }

        internal Scene GetScene()
        {
            return GetMeshContainerComponent().gameObject.scene;
        }

        internal void UpdateResourceKey()
        {
            oldTransformsInstanceId = GetMeshContainerComponent().transform.GetInstanceID();
            oldOriginMeshId = originMesh.GetInstanceID();
#if UNITY_EDITOR
            resourceKey = $"{oldTransformsInstanceId}*{oldOriginMeshId}*{timestamp}";
            resourceKeyShort = $"{GetScene().name}*{oldOriginMeshId}*{timestamp}";
#else
            resourceKey = $"{oldTransformsInstanceId}*{oldOriginMeshId}*{0}";
            resourceKeyShort = $"{GetScene().name}*{oldOriginMeshId}*{0}";
#endif
        }
    }
}