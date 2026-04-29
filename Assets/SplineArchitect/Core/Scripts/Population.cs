// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: Population.cs
//
// Author: Mikael Danielsson
// Date Created: 07-02-2026
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEngine;

using SplineArchitect.Utility;

namespace SplineArchitect
{
    public class Population
    {
        private float startPadding;
        private float endPadding;
        private float xOffset;
        private float yOffset;
        private float spacing;
        private bool snapLast;
        private int maxInstances = 0;
        private Quaternion prefabRotationOffset = Quaternion.identity;

        private bool updateOverTime;
        private GameObject prefab;
        private Bounds prefabBounds;
        private bool deform;
        private Transform parent;
        private bool worldPositionStays;

        private int version;
        private int oldVersion;
        private int warmupCounts;
        private int warmupCount;
        private bool invalid;

        internal List<SplineObject> activeSet;
        internal List<SplineObject> deformingSet;

        public float StartPadding
        {
            get => startPadding;
            set
            {
                if (GeneralUtility.Equals(value, startPadding))
                    return;

                version++;
                startPadding = value;
            }
        }
        public float EndPadding
        {
            get => endPadding;
            set
            {
                if (GeneralUtility.Equals(value, endPadding))
                    return;

                version++;
                endPadding = value;
            }
        }
        public float XOffset
        {
            get => xOffset;
            set
            {
                if (GeneralUtility.Equals(value, xOffset))
                    return;

                version++;
                xOffset = value;
            }
        }
        public float YOffset
        {
            get => yOffset;
            set
            {
                if (GeneralUtility.Equals(value, yOffset))
                    return;

                version++;
                yOffset = value;
            }
        }
        public float Spacing
        {
            get => spacing;
            set
            {
                if (GeneralUtility.Equals(value, spacing))
                    return;

                version++;
                spacing = value;
            }
        }
        public bool SnapLast
        {
            get => snapLast;
            set
            {
                if (value == snapLast)
                    return;

                version++;
                snapLast = value;
            }
        }
        public int MaxInstances
        {
            get => maxInstances;
            set
            {
                if (value == maxInstances)
                    return;

                version++;
                maxInstances = value;
            }
        }
        public Quaternion PrefabRotationOffset
        {
            get => prefabRotationOffset;
            set
            {
                if (GeneralUtility.Equals(value, prefabRotationOffset))
                    return;

                version++;
                prefabRotationOffset = value;
            }
        }
        public GameObject Prefab => prefab;
        public Bounds PrefabBounds => prefabBounds;
        public bool Deform => deform;
        public Transform Parent => parent;
        public bool UpdateOverTime => updateOverTime;
        public bool WorldPositionStays => worldPositionStays;
        public int SplineObjectCount => activeSet.Count;
        internal bool Invalid => invalid;

        public Population(GameObject prefab, bool deform, bool updateOverTime = true, Transform parent = null, bool worldPositionStays = true)
        {
            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            MeshCollider meshCollider = prefab.GetComponent<MeshCollider>();

            if(meshFilter == null && meshCollider == null)
            {
                Debug.LogError($"[Spline Architect] Population is invalid! Could not find valid bounds for {prefab.name}.");
                invalid = true;
            }

            Mesh mesh = null;

            if (meshCollider)
            {
                mesh = meshCollider.sharedMesh;
                prefabBounds = meshCollider.sharedMesh.bounds;
            }
            if (meshFilter)
            {
                mesh = meshFilter.sharedMesh;
                prefabBounds = meshFilter.sharedMesh.bounds;
            }

            prefabBounds = GeneralUtility.TransformBoundsToWorldSpace(prefabBounds, prefab.transform);

            if (mesh == null)
            {
                Debug.LogError($"[Spline Architect] Population is invalid! Could not find valid mesh for {prefab.name}.");
                invalid = true;
            }

            if (!mesh.isReadable)
            {
                Debug.LogError($"[Spline Architect] Population is invalid! The mesh used by '{prefab.name}' does not have Read/Write enabled.");
                invalid = true;
            }

            activeSet = new List<SplineObject>();
            deformingSet = new List<SplineObject>();

            this.prefab = prefab;
            this.parent = parent;
            this.deform = deform;
            this.updateOverTime = updateOverTime;
            this.worldPositionStays = worldPositionStays;

            warmupCounts = 1;
            if (updateOverTime) warmupCounts = 2;
        }

        internal bool IsVersionDirty()
        {
            bool dirty = version != oldVersion;
            oldVersion = version;

            //Warmup period
            if(warmupCount < warmupCounts) dirty = true;
            warmupCount++;

            return dirty;
        }

        internal void Clear()
        {
            activeSet.Clear();
            deformingSet.Clear();
        }

        public SplineObject GetSplineObjectAtIndex(int index)
        {
            return activeSet[index];
        }
    }
}