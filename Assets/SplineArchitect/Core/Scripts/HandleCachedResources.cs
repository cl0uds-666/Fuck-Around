// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: HandleCachedResources.cs
//
// Author: Mikael Danielsson
// Date Created: 14-05-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SplineArchitect
{
    public class HandleCachedResources
    {
        private static Dictionary<string, (Mesh, string)> instanceMeshesRuntime = new Dictionary<string, (Mesh, string)>();
        private static Dictionary<string, (Vector3[], string)> originMeshVertices = new Dictionary<string, (Vector3[], string)>();
        private static Dictionary<string, (Vector3[], string)> originMeshNormals = new Dictionary<string, (Vector3[], string)>();
        private static Dictionary<string, (Vector4[], string)> originMeshTangents = new Dictionary<string, (Vector4[], string)>();
        private static Dictionary<string, (Vector3[], string)> verticeNormalContainer = new Dictionary<string, (Vector3[], string)>();
        private static Dictionary<string, (Vector4[], string)> tangentContainer = new Dictionary<string, (Vector4[], string)>();

        private static List<string> clearContainer = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            ClearScene(scene.name);
        }

        internal static bool IsInstanceMeshCached(Mesh instanceMesh)
        {
            return instanceMeshesRuntime.ContainsKey(instanceMesh.name);
        }

        internal static Mesh FetchInstanceMesh(MeshContainer mc)
        {
            Mesh originMesh = mc.GetOriginMesh();

            if (originMesh == null)
                return null;

            string key = mc.GetResourceKey();

            if (instanceMeshesRuntime.ContainsKey(key))
            {
                if (instanceMeshesRuntime[key].Item1 != null)
                    return instanceMeshesRuntime[key].Item1;

                instanceMeshesRuntime.Remove(key);
            }

            Mesh instanceMesh = Object.Instantiate(originMesh);
            instanceMeshesRuntime.Add(key, (instanceMesh, mc.GetScene().name));

            return instanceMesh;
        }

        internal static Vector3[] FetchOriginVertices(MeshContainer mc)
        {
            Mesh originMesh = mc.GetOriginMesh();
            string key = mc.GetResourceKeyShort();

            if (originMeshVertices.ContainsKey(key))
                return originMeshVertices[key].Item1;

            Vector3[] vertices = originMesh.vertices;
            originMeshVertices.Add(key, (vertices, mc.GetScene().name));

            return vertices;
        }

        internal static Vector3[] FetchOriginNormals(MeshContainer mc)
        {
            Mesh originMesh = mc.GetOriginMesh();
            string key = mc.GetResourceKeyShort();

            if (originMeshNormals.ContainsKey(key))
                return originMeshNormals[key].Item1;

            Vector3[] normals = originMesh.normals;
            originMeshNormals.Add(key, (normals, mc.GetScene().name));

            return normals;
        }

        internal static Vector4[] FetchOriginTangents(MeshContainer mc)
        {
            Mesh originMesh = mc.GetOriginMesh();
            string key = mc.GetResourceKeyShort();

            if (originMeshTangents.ContainsKey(key))
                return originMeshTangents[key].Item1;

            Vector4[] normals = originMesh.tangents;
            originMeshTangents.Add(key, (normals, mc.GetScene().name));

            return normals;
        }

        internal static Vector3[] FetchVerticeNormalContainer(MeshContainer mc)
        {
            Mesh originMesh = mc.GetOriginMesh();
            string key = mc.GetResourceKeyShort();

            if (verticeNormalContainer.ContainsKey(key))
                return verticeNormalContainer[key].Item1;

            Vector3[] vertices = originMesh.vertices;
            verticeNormalContainer.Add(key, (vertices, mc.GetScene().name));

            return vertices;
        }

        internal static Vector4[] FetchOriginTangentsContainer(MeshContainer mc)
        {
            Mesh originMesh = mc.GetOriginMesh();
            string key = mc.GetResourceKeyShort();

            if (tangentContainer.ContainsKey(key))
                return tangentContainer[key].Item1;

            Vector4[] normals = originMesh.tangents;
            tangentContainer.Add(key, (normals, mc.GetScene().name));

            return normals;
        }

        internal static void AddOrUpdateInstanceMesh(MeshContainer mc)
        {
            Mesh instanceMesh = mc.GetInstanceMesh();
            string resourceKey = mc.GetResourceKey();

            if(instanceMeshesRuntime.ContainsKey(resourceKey))
                instanceMeshesRuntime.Remove(resourceKey);
            instanceMeshesRuntime.Add(resourceKey, (instanceMesh, mc.GetScene().name));

            Mesh originMesh = mc.GetOriginMesh();
            string resourceKeyShort = mc.GetResourceKeyShort();

            if (originMeshVertices.ContainsKey(resourceKeyShort))
                originMeshVertices.Remove(resourceKeyShort);
            Vector3[] vertices = originMesh.vertices;
            originMeshVertices.Add(resourceKeyShort, (vertices, mc.GetScene().name));

            if (originMeshNormals.ContainsKey(resourceKeyShort))
                originMeshNormals.Remove(resourceKeyShort);
            Vector3[] normals = originMesh.normals;
            originMeshNormals.Add(resourceKeyShort, (normals, mc.GetScene().name));

            if (originMeshTangents.ContainsKey(resourceKeyShort))
                originMeshTangents.Remove(resourceKeyShort);
            Vector4[] tangents = originMesh.tangents;
            originMeshTangents.Add(resourceKeyShort, (tangents, mc.GetScene().name));

            if (verticeNormalContainer.ContainsKey(resourceKeyShort))
                verticeNormalContainer.Remove(resourceKeyShort);
            Vector3[] verticesContainer = originMesh.vertices;
            verticeNormalContainer.Add(resourceKeyShort, (verticesContainer, mc.GetScene().name));

            if (tangentContainer.ContainsKey(resourceKeyShort))
                tangentContainer.Remove(resourceKeyShort);
            Vector4[] tangentsContainer = originMesh.tangents;
            tangentContainer.Add(resourceKeyShort, (tangentsContainer, mc.GetScene().name));
        }

        /// <summary>
        /// Gets the total number of cached instance meshes. 
        /// An instance mesh is a deformed copy
        /// created from a source mesh in your asset library.
        /// </summary>
        public static int GetInstanceMeshCount()
        {
            return instanceMeshesRuntime.Count;
        }

        /// <summary>
        /// Gets the number of Vector3 arrays that store the original mesh vertices,
        /// which are used by instance meshes during the deformation process.
        /// </summary>
        public static int GetOriginMeshVerticesCount()
        {
            return originMeshVertices.Count;
        }

        /// <summary>
        /// Gets the number of Vector3 arrays that store the original mesh normals,
        /// which are used by instance meshes during the deformation process.
        /// </summary>
        public static int GetOriginMeshNormalsCount()
        {
            return originMeshNormals.Count;
        }

        /// <summary>
        /// Gets the number of Vector4 arrays that store the original mesh tangents,
        /// which are used by instance meshes during the deformation process.
        /// </summary>
        public static int GetOriginMeshTangentsCount()
        {
            return originMeshTangents.Count;
        }

        /// <summary>
        /// Gets the number of Vector3 array containers currently in use.
        /// They are used by instance meshes during the deformation process.
        /// </summary>
        public static int GetNewVerticesContainerCount()
        {
            return verticeNormalContainer.Count;
        }

        internal static void ClearScene(string name)
        {
            clearContainer.Clear();

            foreach (KeyValuePair<string, (Vector3[], string)> item in originMeshVertices)
            {
                if (item.Value.Item2 == name)
                    clearContainer.Add(item.Key);
            }

            foreach (string s in clearContainer)
            {
                originMeshVertices.Remove(s);
                originMeshNormals.Remove(s);
                originMeshTangents.Remove(s);
                verticeNormalContainer.Remove(s);
                tangentContainer.Remove(s);
            }

            clearContainer.Clear();

            foreach (KeyValuePair<string, (Mesh, string)> item in instanceMeshesRuntime)
            {
                if (item.Value.Item2 == name)
                    clearContainer.Add(item.Key);
            }

            foreach (string s in clearContainer)
            {
                instanceMeshesRuntime.Remove(s);
            }
        }
    }
}