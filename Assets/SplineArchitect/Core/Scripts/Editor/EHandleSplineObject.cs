// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleSplineObject.cs
//
// Author: Mikael Danielsson
// Date Created: 18-02-2024
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using SplineArchitect.Utility;
using SplineArchitect.Ui;
using SplineArchitect.CustomTools;
using SplineArchitect.Workers;

namespace SplineArchitect
{
    public static class EHandleSplineObject
    {
        private static List<Segment> segmentContainer = new List<Segment>();
        private static HashSet<SplineObject> noOriginMeshWarningContainer = new HashSet<SplineObject>();

        internal static void OnSceneGUI(Spline spline, Event e)
        {
            if(PositionTool.activePart == PositionTool.ActivePart.NONE)
            {
                noOriginMeshWarningContainer.Clear();
            }

            if (e.type == EventType.Layout)
            {
                for (int i = spline.AllSplineObjectCount - 1; i >= 0; i--)
                {
                    //Many spline objects can be deleted or added during the same frame so we need this check
                    if (i > spline.AllSplineObjectCount - 1)
                        continue;

                    SplineObject so = spline.GetSplineObjectAtIndex(i);

                    if (so == null)
                    {
                        spline.RemoveSplineObject(so);
                        continue;
                    }

                    EHandleMeshContainer.CheckForOriginMeshChange(spline, so);
                    EHandleEvents.InvokeSplineObjectSceneGUI(spline, so);
                }
            }
        }

        internal static void Update(Spline spline)
        {
            for (int i = 0; i < spline.AllSplineObjectCount; i++)
            {
                SplineObject so = spline.GetSplineObjectAtIndex(i);

                if (so == null)
                    continue;

                if (so.Monitor.EditorComponentCountChange())
                {
                    EHandleMeshContainer.Initialize(so);
                    EHandleMeshContainer.DeleteUnvalidMeshContainers(so);
                }

                if (so.Monitor.EditorStaticChange())
                {
                    WindowExtended.RepaintAll();
                }
            }
        }

        public static bool HasReadWriteAccessEnabled(SplineObject so)
        {
            for(int i = 0; i < so.MeshContainerCount; i++)
            {
                MeshContainer mc = so.GetMeshContainerAtIndex(i);
                Mesh instanceMesh = mc.GetInstanceMesh();

                if(instanceMesh != null)

                if(!instanceMesh.isReadable)
                    return false;
            }

            return true;
        }

        public static void ToSplineCenter(SplineObject so)
        {
            so.localSplinePosition = new Vector3(0, 0, GeneralUtility.RoundToClosest(so.localSplinePosition.z, EditorSnapSettings.move.z));
            so.activationPosition = so.localSplinePosition;
            so.localSplineRotation.eulerAngles = new Vector3(0, 0, 0);
        }

        public static Vector3 SnapPosition(Vector3 position)
        {
            float snapValue = EditorSnapSettings.move.x + EditorSnapSettings.move.y + EditorSnapSettings.move.z;

#if UNITY_6000_0_OR_NEWER
            if (EditorSnapSettings.snapEnabled && snapValue > 0)
            {
#else
            if (EditorSnapSettings.gridSnapEnabled && snapValue > 0)
            {
#endif
                position.x = GeneralUtility.RoundToClosest(position.x, EditorSnapSettings.move.x);
                position.y = GeneralUtility.RoundToClosest(position.y, EditorSnapSettings.move.y);
                position.z = GeneralUtility.RoundToClosest(position.z, EditorSnapSettings.move.z);
            }

            return position;
        }

        public static bool ValidForEditorDeformation(SplineObject so)
        {
            for(int i = 0; i < so.MeshContainerCount; i++)
            {
                MeshContainer mc = so.GetMeshContainerAtIndex(i);
                Mesh instanceMesh = mc.GetInstanceMesh();
                Mesh originMesh = mc.GetOriginMesh();

                if (originMesh == null)
                {
                    if (!noOriginMeshWarningContainer.Contains(so) && PositionTool.activePart != PositionTool.ActivePart.NONE)
                    {
                        noOriginMeshWarningContainer.Add(so);
                        Debug.LogWarning($"[Spline Architect] Could not find the origin mesh for {so.name}, deformation aborted. Has the asset been deleted?");
                    }

                    return false;
                }
                if (instanceMesh == null) return false;
                if (instanceMesh.vertexCount != originMesh.vertexCount) return false;

                if (instanceMesh == originMesh)
                {
                    return false;
                }
            }

            return true;
        }

        public static void UpdateTypeAuto(Spline spline, SplineObject so)
        {
            if (so.Type == SplineObjectType.NONE)
                return;

            Mesh originMesh = null;

            if (so.MeshContainerCount == 0)
            {
                MeshFilter meshFilter = so.gameObject.GetComponent<MeshFilter>();

                if(meshFilter == null)
                {
                    MeshCollider meshCollider = so.gameObject.GetComponent<MeshCollider>();

                    if(meshCollider != null)
                        originMesh = meshCollider.sharedMesh;
                }
                else
                    originMesh = meshFilter.sharedMesh;
            }
            else
            {
                originMesh = so.GetMeshContainerAtIndex(0).GetOriginMesh();
            }

            if (originMesh == null)
                return;

            float zExtend = originMesh.bounds.extents.z;

            if(so.splinePosition.z < -zExtend || so.splinePosition.z > zExtend + spline.Length)
            {
                so.Type = SplineObjectType.FOLLOWER;
                return;
            }

            segmentContainer.Clear();

            for (int i = 1; i < spline.segments.Count; i++)
            {
                Segment s1 = spline.segments[i - 1];
                Segment s2 = spline.segments[i];

                float bak = so.splinePosition.z - zExtend;
                float front = so.splinePosition.z + zExtend;

                if (front > s1.zPosition && bak < s2.zPosition)
                {
                    if(!segmentContainer.Contains(s1))
                        segmentContainer.Add(s1);

                    if (!segmentContainer.Contains(s2))
                        segmentContainer.Add(s2);
                }
            }

            if (segmentContainer.Count == 0)
                return;

            bool sameDirection = true;
            Vector3 originDirection = segmentContainer[0].GetPosition(ControlHandle.ANCHOR) - segmentContainer[0].GetPosition(ControlHandle.TANGENT_A);
            originDirection = originDirection.normalized;

            for (int i = 1; i < segmentContainer.Count; i++)
            {
                if (segmentContainer[i].GetInterpolationType() == InterpolationType.LINE)
                    continue;

                Vector3 d = segmentContainer[i].GetPosition(ControlHandle.ANCHOR) - segmentContainer[i].GetPosition(ControlHandle.TANGENT_A);
                d = d.normalized;

                if(!GeneralUtility.IsEqual(d, originDirection))
                {
                    sameDirection = false;
                    break;
                }
            }

            if (sameDirection)
                so.Type = SplineObjectType.FOLLOWER;
            else
                so.Type = SplineObjectType.DEFORMATION;
        }

        public static void Convert(Spline spline, SplineObject so, SplineObjectType oldType)
        {
            if (so.Type == SplineObjectType.DEFORMATION)
                ConvertToDeformation(spline, so, oldType);
            else if (so.Type == SplineObjectType.FOLLOWER)
                ConvertToFollower(spline, so, oldType);
            else if (so.Type == SplineObjectType.NONE)
                ConvertToNone(spline, so, oldType);

            EHandleSpline.MarkForInfoUpdate(spline);
        }

        public static void ConvertToFollower(Spline spline, SplineObject so, SplineObjectType oldType)
        {
            for (int i = 0; i < so.gameObject.GetComponentCount(); i++)
            {
                Component c = so.gameObject.GetComponentAtIndex(i);

                if (c == null)
                    continue;

                MeshFilter meshFilter = c as MeshFilter;
                MeshCollider meshCollider = c as MeshCollider;

                if(meshFilter != null)
                {
                    Mesh originMesh = ESplineObjectUtility.GetOriginMeshFromMeshNameId(meshFilter.sharedMesh);

                    if(originMesh == null)
                        continue;

                    meshFilter.sharedMesh = originMesh;
                }
                else if (meshCollider != null)
                {
                    Mesh originMesh = ESplineObjectUtility.GetOriginMeshFromMeshNameId(meshCollider.sharedMesh);

                    if (originMesh == null)
                        continue;

                    meshCollider.sharedMesh = originMesh;
                }
            }

            if (oldType == SplineObjectType.DEFORMATION)
            {
                SplineObject[] cildSos = so.transform.GetComponentsInChildren<SplineObject>();

                for (int i = 0; i < cildSos.Length; i++)
                {
                    SplineObject childSo = cildSos[i];

                    if (childSo == null)
                        continue;

                    if (childSo == so)
                        continue;

                    childSo.SetInstanceMeshesToOriginMesh();
                    childSo.transform.localPosition = childSo.localSplinePosition;
                    childSo.transform.localRotation = childSo.localSplineRotation;
                    childSo.UpdateExternalComponents(true);
                    Object.DestroyImmediate(childSo);
                }
            }

            so.ClearMeshContainers();
            so.Type = SplineObjectType.FOLLOWER;
            spline.directSystemWorker.Deform(so, true);
            EHandleTool.UpdateOrientationForPositionTool(EHandleSceneView.GetCurrent(), spline);
        }

        public static void ConvertToDeformation(Spline spline, SplineObject so, SplineObjectType oldType)
        {
            so.Type = SplineObjectType.DEFORMATION;

            so.SyncMeshContainers();
            Transform[] childs = so.transform.GetComponentsInChildren<Transform>();

            for (int i = 0; i < childs.Length; i++)
            {
                Transform child = childs[i];

                if (child == null)
                    continue;

                if (child == so.transform)
                    continue;

                ESplineObjectUtility.TryAttacheOnTransformEditor(spline, child, true, true);
            }

            so.transform.localPosition = so.localSplinePosition;
            so.transform.localRotation = so.localSplineRotation;
            EHandleTool.UpdateOrientationForPositionTool(EHandleSceneView.GetCurrent(), spline);
            so.SyncInstanceMeshesFromCache();
        }

        public static void ConvertToNone(Spline spline, SplineObject so, SplineObjectType oldType)
        {
            ConvertToFollower(spline, so, oldType);
            so.Type = SplineObjectType.NONE;
        }

        public static void ExportMeshes(SplineObject so, string filePath)
        {
            if (so.MeshContainerCount == 0)
                return;

            Mesh meshFilterMesh = so.GetMeshContainerAtIndex(0).GetInstanceMesh();
            int count = 0;

            for(int i2 = 0; i2 < so.MeshContainerCount; i2++)
            {
                MeshContainer mc = so.GetMeshContainerAtIndex(i2);
                if (count > 0 && mc.GetInstanceMesh() == meshFilterMesh)
                    continue;

                Mesh instanceMesh = Object.Instantiate(mc.GetInstanceMesh());
                Mesh originMesh = mc.GetOriginMesh();

                Vector3[] originVertecies = HandleCachedResources.FetchOriginVertices(mc);
                Vector3[] deformedVertecies = instanceMesh.vertices;

                //Align pivot
                for (int i = 0; i < originVertecies.Length; i++)
                    deformedVertecies[i] += originMesh.bounds.center - instanceMesh.bounds.center;

                instanceMesh.SetVertices(deformedVertecies);
                instanceMesh.RecalculateBounds();
                instanceMesh.RecalculateNormals();
                instanceMesh.RecalculateTangents();

                string p = $"{filePath}";
                if (count > 0) p = $"{filePath}-{count + 1}";

                if(mc.IsMeshFilter()) p = $"{p}.asset";
                else p = $"{p}(collider).asset";

                AssetDatabase.CreateAsset(instanceMesh, p);

                count++;
            }

            AssetDatabase.SaveAssets();
        }
    }
}
