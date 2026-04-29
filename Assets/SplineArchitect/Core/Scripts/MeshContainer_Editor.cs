// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: MeshContainer_Editor.cs
//
// Author: Mikael Danielsson
// Date Created: 13-01-2026
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;

using UnityEngine;

using SplineArchitect.Utility;

namespace SplineArchitect
{
    internal partial class MeshContainer
    {
#if UNITY_EDITOR
        //Runtime data
        [NonSerialized]
        private int meshData = 0;
        [NonSerialized]
        private string resourceKeyCheck = null;

        internal void UpdateInstanceMeshName()
        {
            Mesh instanceMesh = GetInstanceMesh();

            if (instanceMesh == null)
                return;

            if (instanceMesh == originMesh)
                return;

            instanceMesh.name = resourceKey;
        }

        internal bool HasReadabilityDif()
        {
            if (originMesh.isReadable != GetInstanceMesh().isReadable)
                return true;

            return false;
        }

        internal bool TryUpdateTimestamp()
        {
            if(originMesh == null)
                return false;

            string path = GeneralUtility.GetAssetPathOnlyEditor(originMesh, true);

            if (path == "")
            {
                Debug.LogError($"[Spline Architect] Can't set timestamp! {originMesh.name} does not have an asset path!");
                return false;
            }

            long check = timestamp;
            timestamp = System.IO.File.GetLastWriteTime(path).Ticks;

            if (check != 0 && timestamp != check)
                return true;

            return false;
        }

        internal void EnsureValidOriginMesh()
        {
            string path = GeneralUtility.GetAssetPathOnlyEditor(originMesh, true);

            if (path == "")
            {
                Mesh mesh = ESplineObjectUtility.GetOriginMeshFromMeshNameId(originMesh);
                originMesh = mesh;
            }
        }

        internal float GetDataUsage()
        {
            if (!string.IsNullOrEmpty(resourceKey) && resourceKeyCheck == GetResourceKey())
                return meshData;

            Mesh mesh = GetInstanceMesh();

            if (mesh == null)
                return 0;

            meshData += mesh.vertexCount * 12;
            meshData += mesh.triangles.Length * (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 ? 2 : 4);
            meshData += mesh.normals.Length * 12;
            meshData += mesh.tangents.Length * 16;
            meshData += mesh.uv.Length * 8;
            meshData += mesh.uv2.Length * 8;
            meshData += mesh.uv3.Length * 8;
            meshData += mesh.uv4.Length * 8;
            meshData += mesh.uv5.Length * 8;
            meshData += mesh.uv6.Length * 8;
            meshData += mesh.uv7.Length * 8;
            meshData += mesh.uv8.Length * 8;
            meshData += mesh.colors.Length * 16;
            meshData += mesh.name.Length * 2;
            meshData += 24;

            resourceKeyCheck = GetResourceKey();

            return meshData;
        }
#endif
    }
}