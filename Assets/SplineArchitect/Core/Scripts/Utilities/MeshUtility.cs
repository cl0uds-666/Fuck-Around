// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: MeshUtility.cs
//
// Author: Mikael Danielsson
// Date Created: 29-10-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

using SplineArchitect.Jobs;

namespace SplineArchitect.Utility
{
    internal class MeshUtility
    {
        private static List<int> triangleContainer = new List<int>();

        internal static void ReverseTriangles(Mesh mesh)
        {
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                triangleContainer.Clear();

                mesh.GetTriangles(triangleContainer, subMeshIndex);

                for (int i = 0; i < triangleContainer.Count; i += 3)
                {
                    int temp = triangleContainer[i];
                    triangleContainer[i] = triangleContainer[i + 1];
                    triangleContainer[i + 1] = temp;
                }

                mesh.SetTriangles(triangleContainer, subMeshIndex);
            }
        }

        internal static void SetSeamlessTriangles(MeshContainer mc)
        {
            Mesh mesh = mc.GetInstanceMesh();

            NativeHashMap<int, int> vertexMap = new NativeHashMap<int, int>(mesh.vertexCount, Allocator.TempJob);
            NativeArray<Vector3> vertecies = new NativeArray<Vector3>(HandleCachedResources.FetchOriginVertices(mc), Allocator.TempJob);
            JobHandle jobHandle;

            //Iterate through all submeshes
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                int[] subMeshTriangles = mesh.GetTriangles(i);
                if (subMeshTriangles == null || subMeshTriangles.Length == 0) continue;

                triangleContainer.Clear();

                //Create and schedule a TriangleLinkingJob for the current submesh
                TriangleLinkingJob triangleLinkingJob = new TriangleLinkingJob()
                {
                    triangles = new NativeArray<int>(subMeshTriangles, Allocator.TempJob),
                    vertices = vertecies,
                    vertextMap = vertexMap
                };

                jobHandle = triangleLinkingJob.Schedule();
                jobHandle.Complete();

                foreach(int i2 in triangleLinkingJob.triangles)
                    triangleContainer.Add(i2);

                //Set the linked triangles for the current submesh
                mesh.SetTriangles(triangleContainer, i);

                //Dispose of the nativeArray.
                triangleLinkingJob.triangles.Dispose();
            }

            vertexMap.Dispose();
            vertecies.Dispose();
        }
    }
}
