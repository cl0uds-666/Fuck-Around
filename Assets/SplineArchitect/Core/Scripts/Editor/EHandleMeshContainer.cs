// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleMeshContainer.cs
//
// Author: Mikael Danielsson
// Date Created: 18-02-2024
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

using SplineArchitect.Utility;

namespace SplineArchitect
{
    internal static class EHandleMeshContainer
    {
        private static bool refresh;
        private static List<Component> componentContainer = new List<Component>();
        private static HashSet<MeshContainer> hasRunOriginMeshWarning = new HashSet<MeshContainer>();

        internal static void Refresh()
        {
            refresh = true;
        }

        internal static void RefreshAfterAssetImport(HashSet<Spline> splines)
        {
            if (!refresh)
                return;

            refresh = false;
            foreach (Spline spline in splines)
            {
                for (int i2 = 0; i2 < spline.AllSplineObjectCount; i2++)
                {
                    SplineObject so = spline.GetSplineObjectAtIndex(i2);

                    if (so == null || so.transform == null)
                        continue;

                    if (so.Type != SplineObjectType.DEFORMATION)
                        continue;

                    bool foundModification = false;

                    for (int i = 0; i < so.MeshContainerCount; i++)
                    {
                        MeshContainer mc = so.GetMeshContainerAtIndex(i);
                        if (mc.TryUpdateTimestamp())
                        {
                            foundModification = true;
                            if (!TryUpdateOriginMesh(so, mc))
                                Debug.LogError($"[Spline Architect] Failed to update the origin mesh after the asset modification refresh on SplineObject {so.name}. " +
                                               $"Has the asset been deleted? If so, add the asset back and reload the scene.");
                        }
                        else if (mc.HasReadabilityDif())
                        {
                            foundModification = true;
                            RefreshInstanceMesh(so, mc);
                        }
                    }

                    if (foundModification)
                    {
                        so.MarkVersionDirty();
                        EHandleSpline.MarkForInfoUpdate(spline);
                    }
                }
                
                EHandleDeformation.ProcessSplineObjects(spline);
            }
        }

        internal static void Initialize(SplineObject so)
        {
            for (int i = 0; i < so.gameObject.GetComponentCount(); i++)
            {
                if (so.Type != SplineObjectType.DEFORMATION)
                    continue;

                Component component = so.gameObject.GetComponentAtIndex(i);
                MeshFilter meshFilter = component as MeshFilter;
                MeshCollider meshCollider = component as MeshCollider;

                Mesh sharedMesh = null;

                if (meshFilter != null)
                {
                    sharedMesh = meshFilter.sharedMesh;
                }
                else if (meshCollider != null)
                {
                    sharedMesh = meshCollider.sharedMesh;
                }

                if (sharedMesh == null)
                    continue;

                bool allreadyExists = false;
                for (int i2 = 0; i2 < so.MeshContainerCount; i2++)
                {
                    MeshContainer mc2 = so.GetMeshContainerAtIndex(i2);
                    if (mc2 != null && mc2.Contains(component))
                    {
                        allreadyExists = true;
                        break;
                    }
                }
                if (allreadyExists) continue;

                Mesh originMesh = ESplineObjectUtility.GetOriginMeshFromMeshNameId(sharedMesh);
                if (originMesh != null && meshFilter != null)
                {
                    meshFilter.sharedMesh = originMesh;
                }
                else if (originMesh != null && meshCollider != null)
                {
                    meshCollider.sharedMesh = originMesh;
                }

                MeshContainer mc = new MeshContainer(component);
                so.AddMeshContainer(mc);
                mc.SetInstanceMesh(HandleCachedResources.FetchInstanceMesh(mc));
            }
        }

        internal static void DeleteUnvalidMeshContainers(SplineObject so)
        {
            for (int i = so.MeshContainerCount - 1; i >= 0; i--)
            {
                MeshContainer mc = so.GetMeshContainerAtIndex(i);

                if (mc == null)
                {
                    so.RemoveMeshContainer(mc);
                    continue;
                }

                if (mc.GetMeshContainerComponent() == null)
                {
                    so.RemoveMeshContainer(mc);
                    continue;
                }

                Mesh instanceMesh = mc.GetInstanceMesh();

                if (instanceMesh == null)
                {
                    so.RemoveMeshContainer(mc);
                }
            }
        }

        internal static void DeleteDuplicates(Spline spline)
        {
            for (int i2 = 0; i2 < spline.AllSplineObjectCount; i2++)
            {
                SplineObject so = spline.GetSplineObjectAtIndex(i2);

                if (so.MeshContainerCount < 2)
                    continue;

                componentContainer.Clear();
                for (int i = so.MeshContainerCount - 1; i >= 0; i--)
                {
                    MeshContainer mc = so.GetMeshContainerAtIndex(i);
                    Component c = mc.GetMeshContainerComponent();

                    if (componentContainer.Contains(c))
                    {
                        so.RemoveMeshContainerAt(i);
                        Debug.Log("[Spline Architect] Found MeshContainer duplicate! It's now removed. " +
                                  "this is for fixing a bug that can happen in older Spline Architect versions (1.2.5 or less).");
                    }
                    else
                    {
                        componentContainer.Add(c);
                    }
                }
            }
        }

        internal static void CheckForOriginMeshChange(Spline spline, SplineObject so)
        {
            if (so.Type != SplineObjectType.DEFORMATION)
                return;

            bool changed = false;

            //Update meshContainers
            for (int i = so.MeshContainerCount - 1; i >= 0; i--)
            {
                MeshContainer mc = so.GetMeshContainerAtIndex(i);

                if (mc.MeshContainerExist() == false)
                    continue;

                Mesh instanceMesh = mc.GetInstanceMesh();

                if (instanceMesh == null)
                    continue;

                if (instanceMesh.name != mc.GetResourceKey())
                {
                    changed = true;
                    if(!TryUpdateOriginMesh(so, mc) && !hasRunOriginMeshWarning.Contains(mc))
                    {
                        hasRunOriginMeshWarning.Add(mc);
                        Debug.LogError($"[Spline Architect] Failed to update the origin mesh on SplineObject {so.name} at index {i}. " +
                                       $"Has the asset been deleted? If so, add the asset back and reload the scene.");
                    }
                }
            }

            if (changed)
            {
                EHandleSpline.MarkForInfoUpdate(spline);
            }
        }

        private static bool TryUpdateOriginMesh(SplineObject so, MeshContainer mc)
        {
            Mesh instanceMesh = mc.GetInstanceMesh();

            // First try get mesh from name.
            Mesh originMesh = ESplineObjectUtility.GetOriginMeshFromMeshNameId(instanceMesh);

            // Second try get mesh from guid and localId. If a mesh is find this way this mesh should always be retrived and used.
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceMesh, out string guid, out long localId);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid));
            foreach (Object a in assets)
            {
                if (a == null) 
                    continue;

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out var g, out long id))
                {
                    if (g == guid && id == localId)
                    {
                        originMesh = (Mesh)a;
                        break;
                    }
                }
            }

            if (originMesh != null)
            {
                mc.SetOriginMesh(originMesh);
                mc.UpdateResourceKey();
                mc.SetInstanceMesh(HandleCachedResources.FetchInstanceMesh(mc));
                so.MarkVersionDirty();

                return true;
            }

            return false;
        }

        private static void RefreshInstanceMesh(SplineObject so, MeshContainer mc)
        {
            Mesh orginMesh = mc.GetOriginMesh();
            Mesh newInstanceMesh = Object.Instantiate(orginMesh);
            mc.SetInstanceMesh(newInstanceMesh);
            HandleCachedResources.AddOrUpdateInstanceMesh(mc);
            so.MarkVersionDirty();

            if (orginMesh.isReadable != newInstanceMesh.isReadable)
                Debug.LogError("[Spline Architect] Redable status dif error!");
        }
    }
}
