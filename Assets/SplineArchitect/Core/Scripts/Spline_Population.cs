// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: Spline_Event.cs
//
// Author: Mikael Danielsson
// Date Created: 15-01-2026
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;

using SplineArchitect.Utility;

namespace SplineArchitect
{
    public partial class Spline : MonoBehaviour
    {
        [NonSerialized] internal bool finalizePopulateUsingPoolJobSafe;
        [NonSerialized] internal HashSet<Population> populations = new HashSet<Population>();
        [NonSerialized] private Dictionary<GameObject, List<SplineObject>> populationPool = new Dictionary<GameObject, List<SplineObject>>();

        public void AddPopulation(Population population)
        {
            if (populations.Contains(population))
            {
                Debug.LogError("[Spline Architect] Population is already added to this spline.");
                return;
            }

            if(population.Invalid)
            {
                Debug.LogError("[Spline Architect] Population is invalid, could not add it to the spline.");
                return;
            }

            population.Clear();
            populations.Add(population);
        }

        public void RemovePopulation(Population population)
        {
            populations.Remove(population);
        }

        public bool ContainsPopulation(Population population)
        {
            return populations.Contains(population);
        }

        public void ClearPopulations()
        {
            populations.Clear();
        }

        /// <summary>
        /// Bakes (instantiates) the given Population into SplineObjects. 
        /// Baked objects are not managed by the Population system
        /// and will not update when the spline or Population settings change.
        /// </summary>
        public void BakePopulation(Population population, 
                                   ICollection<SplineObject> resultSplineObjects = null)
        {
            GameObject prefab = population.Prefab;
            Transform parent = population.Parent;
            Quaternion prefabRotationOffset = population.PrefabRotationOffset;
            Bounds prefabMeshBounds = population.PrefabBounds;
            float spacing = population.Spacing;
            float startPadding = population.StartPadding;
            float endPadding = population.EndPadding;
            float xOffset = population.XOffset;
            float yOffset = population.YOffset;
            int maxInstances = population.MaxInstances;
            bool deform = population.Deform;
            bool snapLast = population.SnapLast;

            float soLength = prefabMeshBounds.size.z + spacing;
            float alignOffset = -prefabMeshBounds.center.z + prefabMeshBounds.extents.z;

            int amount = Mathf.FloorToInt((length - startPadding + endPadding + (soLength / 2) + (spacing / 2)) / soLength);
            amount = Math.Max(amount, 1);

            if (maxInstances > 0)
                amount = Mathf.Min(amount, maxInstances);

            SplineObject last = null;

            for (int i = 0; i < amount; i++)
            {
                SplineObject soClone = null;
                GameObject goClone = Instantiate(prefab);
                if (deform) soClone = CreateDeformation(goClone, Vector3.zero, prefabRotationOffset, true, parent, false, population.WorldPositionStays);
                else soClone = CreateFollower(goClone, Vector3.zero, prefabRotationOffset, true, parent, false, population.WorldPositionStays);

#if UNITY_EDITOR
                if (soClone == null)
                    continue;
#endif

                soClone.localSplinePosition = new Vector3(xOffset,
                                                          yOffset,
                                                          startPadding + alignOffset + (soLength * i));

                if (i == amount - 1) last = soClone;
                if (resultSplineObjects != null) resultSplineObjects.Add(soClone);
                directSystemWorker.Add(soClone);
            }

            //End snapping
            if (snapLast && last != null)
            {
                last.snapSettings.endSnapDistance = soLength;
                last.snapSettings.startSnapDistance = 0;
                last.snapSettings.snapMode = SnapMode.SPLINE_POINT;
                last.snapSettings.snapTargetPoint = length + endPadding;
            }

            //First deformation snapping
            if (amount == 1 && last.Type == SplineObjectType.DEFORMATION && snapLast && length < prefabMeshBounds.size.z)
            {
                last.snapSettings.startSnapDistance = 0;
                if (GeneralUtility.IsZero(startPadding))
                {
                    last.snapSettings.endSnapDistance = soLength;
                    last.localSplinePosition.z = alignOffset - (prefabMeshBounds.size.z / 2) + (length / 2);
                }
                last.snapSettings.endSnapDistance = soLength;
                last.snapSettings.snapMode = SnapMode.CONTROL_POINTS;
            }

            directSystemWorker.Complete();
        }

        internal List<SplineObject> GetPopulationPool(GameObject prefab)
        {
            if(populationPool.ContainsKey(prefab))
                return populationPool[prefab];

            List<SplineObject> newPool = new List<SplineObject>();
            populationPool.Add(prefab, newPool);

            return newPool;
        }

        internal void PopulateUsingPoolJobSafe(Population population)
        {
            GameObject prefab = population.Prefab;
            Transform parent = population.Parent;
            Quaternion prefabRotationOffset = population.PrefabRotationOffset;
            Bounds prefabMeshBounds = population.PrefabBounds;
            float spacing = population.Spacing;
            float startPadding = population.StartPadding;
            float endPadding = population.EndPadding;
            float xOffset = population.XOffset;
            float yOffset = population.YOffset;
            int maxInstances = population.MaxInstances;
            bool deform = population.Deform;
            bool snapLast = population.SnapLast;
            List<SplineObject> pool = GetPopulationPool(prefab);

            bool lastSegmentsIsSame = false;

            if (segments.Count > 1)
            {
                Vector3 lastAnchor = segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
                Vector3 secondLastAnchor = segments[segments.Count - 2].GetPosition(ControlHandle.ANCHOR);

                if (GeneralUtility.Equals(lastAnchor, secondLastAnchor))
                    lastSegmentsIsSame = true;
            }

            float soLength = prefabMeshBounds.size.z + spacing;
            float alignOffset = -prefabMeshBounds.center.z + prefabMeshBounds.extents.z;
            float splineLength = length;
            if (lastSegmentsIsSame && segments.Count > 1)
            {
                splineLength -= segments[segments.Count - 2].length;
            }

            int newAmount = Mathf.FloorToInt((splineLength - startPadding + endPadding + (soLength / 2) + (spacing / 2)) / soLength);
            newAmount = Math.Max(newAmount, 1);
            if (maxInstances > 0) newAmount = Mathf.Min(newAmount, maxInstances);

            if (isInvalidShape)
                newAmount = 0;

            if (lastSegmentsIsSame && segments.Count == 2)
                newAmount = 0;

            for (int i = population.activeSet.Count - 1; i >= 0; i--)
            {
                SplineObject so = population.activeSet[i];
                pool.Add(so);
                so.RenderMeshes(false);
                population.activeSet.RemoveAt(i);
            }

            for (int i = population.deformingSet.Count - 1; i >= 0; i--)
            {
                SplineObject so = population.deformingSet[i];

                if (finalizePopulateUsingPoolJobSafe)
                {
                    pool.Add(so);
                    so.RenderMeshes(false);
                }
                else
                {
                    population.activeSet.Add(so);
                    so.RenderMeshes(true);
                }

                RemoveSplineObject(so);
                population.deformingSet.RemoveAt(i);
            }

            for (int i = 0; i < newAmount; i++)
            {
                SplineObject soClone = null;

                if (pool.Count > 0)
                {
                    soClone = pool[0];
                    pool.RemoveAt(0);
                    soClone.transform.parent = parent == null ? transform : parent;
                }

                if (soClone == null)
                {
                    GameObject goClone = Instantiate(prefab);
                    if (deform) soClone = CreateDeformation(goClone, Vector3.zero, prefabRotationOffset, false, parent, false, population.WorldPositionStays);
                    else soClone = CreateFollower(goClone, Vector3.zero, prefabRotationOffset, false, parent, false, population.WorldPositionStays);
                }

                soClone.localSplinePosition = new Vector3(xOffset,
                                                          yOffset,
                                                          startPadding + alignOffset + (soLength * i));

                soClone.RenderMeshes(finalizePopulateUsingPoolJobSafe ? true : false);
                population.deformingSet.Add(soClone);
                AddSplineObject(soClone);
                soClone.MarkVersionDirty();
            }

            UpdateSnapping(population.deformingSet);

            void UpdateSnapping(List<SplineObject> splineObjects)
            {
                int lastId = -1;
                int firstId = -1;
                float zMax = -99999;
                float zMin = 99999;

                //End snapping
                for (int i = splineObjects.Count - 1; i >= 0; i--)
                {
                    SplineObject so = splineObjects[i];

                    if (so.localSplinePosition.z > zMax)
                    {
                        zMax = so.localSplinePosition.z;
                        lastId = i;
                    }
                    if (zMin > so.localSplinePosition.z)
                    {
                        zMin = so.localSplinePosition.z;
                        firstId = i;
                    }

                    so.snapSettings.endSnapDistance = 0;
                    so.snapSettings.startSnapDistance = 0;
                    so.snapSettings.snapMode = SnapMode.NONE;
                }

                if (lastId != -1)
                {
                    SplineObject last = splineObjects[lastId];
                    last.snapSettings.endSnapDistance = soLength;
                    last.snapSettings.startSnapDistance = 0;
                    last.snapSettings.snapTargetPoint = splineLength + endPadding;
                    last.snapSettings.snapMode = SnapMode.SPLINE_POINT;
                }

                if (firstId != -1)
                {
                    SplineObject first = splineObjects[firstId];

                    //First deformation snapping
                    if (snapLast &&
                        splineObjects.Count == 1 && segments.Count == 2 &&
                        first.Type == SplineObjectType.DEFORMATION &&
                        splineLength < prefabMeshBounds.size.z)
                    {
                        first.snapSettings.startSnapDistance = 0;

                        if (GeneralUtility.IsZero(startPadding))
                        {
                            first.localSplinePosition.z = alignOffset - (prefabMeshBounds.size.z / 2) + (splineLength / 2);
                            first.snapSettings.startSnapDistance = soLength;
                        }

                        first.snapSettings.endSnapDistance = soLength;
                        first.snapSettings.snapMode = SnapMode.CONTROL_POINTS;
                    }
                    else if (splineObjects.Count > 0 && GeneralUtility.IsZero(startPadding))
                    {
                        first.localSplinePosition.z = startPadding + alignOffset;
                    }
                }
            }
        }

        internal void PopulateUsingPool(Population population)
        {
            GameObject prefab = population.Prefab;
            Transform parent = population.Parent;
            Quaternion prefabRotationOffset = population.PrefabRotationOffset;
            Bounds prefabMeshBounds = population.PrefabBounds;
            float spacing = population.Spacing;
            float startPadding = population.StartPadding;
            float endPadding = population.EndPadding;
            float xOffset = population.XOffset;
            float yOffset = population.YOffset;
            int maxInstances = population.MaxInstances;
            bool deform = population.Deform;
            bool snapLast = population.SnapLast;
            List<SplineObject> pool = GetPopulationPool(prefab);

            bool lastSegmentsIsSame = false;

            if (segments.Count > 1)
            {
                Vector3 lastAnchor = segments[segments.Count - 1].GetPosition(ControlHandle.ANCHOR);
                Vector3 secondLastAnchor = segments[segments.Count - 2].GetPosition(ControlHandle.ANCHOR);

                if (GeneralUtility.Equals(lastAnchor, secondLastAnchor))
                    lastSegmentsIsSame = true;
            }

            float soLength = prefabMeshBounds.size.z + spacing;
            float alignOffset = -prefabMeshBounds.center.z + prefabMeshBounds.extents.z;
            float splineLength = length;
            if (lastSegmentsIsSame && segments.Count > 1)
            {
                splineLength -= segments[segments.Count - 2].length;
            }

            int amount = Mathf.FloorToInt((splineLength - startPadding + endPadding + (soLength / 2) + (spacing / 2)) / soLength);
            amount = Math.Max(amount, 1);
            if (maxInstances > 0) amount = Mathf.Min(amount, maxInstances);

            if (isInvalidShape)
                amount = 0;

            if (lastSegmentsIsSame && segments.Count == 2)
                amount = 0;

            int dif = amount - population.activeSet.Count;

            bool check1 = population.activeSet.Count > 0 && (!GeneralUtility.IsEqual(population.activeSet[0].localSplinePosition.z, alignOffset + startPadding) ||
                                                            !GeneralUtility.IsEqual(population.activeSet[0].localSplinePosition.x, xOffset) ||
                                                            !GeneralUtility.IsEqual(population.activeSet[0].localSplinePosition.y, yOffset));
            bool check2 = population.activeSet.Count > 1 && !GeneralUtility.IsEqual(population.activeSet[0].localSplinePosition.z - population.activeSet[1].localSplinePosition.z, soLength);

            if (check1 || check2)
            {
                for (int i = 0; i < population.activeSet.Count; i++)
                {
                    SplineObject so = population.activeSet[i];
                    so.localSplinePosition = new Vector3(xOffset,
                                                         yOffset,
                                                         startPadding + alignOffset + (soLength * i));
                }
            }

            //Add
            if (dif > 0)
            {
                for (int i = population.activeSet.Count; i < amount; i++)
                {
                    SplineObject soClone = null;

                    if (pool.Count > 0)
                    {
                        soClone = pool[0];
                        pool.RemoveAt(0);
                        soClone.gameObject.SetActive(true);
                        soClone.transform.parent = parent == null ? transform : parent;
                    }

                    if (soClone == null)
                    {
                        GameObject goClone = Instantiate(prefab);
                        if (deform) soClone = CreateDeformation(goClone, Vector3.zero, prefabRotationOffset, false, parent, true, population.WorldPositionStays);
                        else soClone = CreateFollower(goClone, Vector3.zero, prefabRotationOffset, false, parent, true, population.WorldPositionStays);
                    }

#if UNITY_EDITOR
                    if (soClone == null)
                        continue;
#endif

                    soClone.localSplinePosition = new Vector3(xOffset,
                                                              yOffset,
                                                              startPadding + alignOffset + (soLength * i));

                    population.activeSet.Add(soClone);
                }
            }
            //Remove
            else if (dif < 0)
            {
                for (int i = population.activeSet.Count - 1; i >= amount; i--)
                {   
                    SplineObject so = population.activeSet[i];
                    population.activeSet.RemoveAt(i);
                    pool.Add(so);
                    so.gameObject.SetActive(false);
                }
            }

            //End snapping
            for (int i = 0; i < population.activeSet.Count; i++)
            {
                SplineObject so = population.activeSet[i];

                if (snapLast && i == population.activeSet.Count - 1)
                {
                    so.snapSettings.endSnapDistance = soLength;
                    so.snapSettings.startSnapDistance = 0;
                    so.snapSettings.snapTargetPoint = splineLength + endPadding;
                    so.snapSettings.snapMode = SnapMode.SPLINE_POINT;
                }
                else
                {
                    so.snapSettings.endSnapDistance = 0;
                    so.snapSettings.startSnapDistance = 0;
                    so.snapSettings.snapMode = SnapMode.NONE;
                }
            }

            //First deformation snapping
            if (snapLast &&
                population.activeSet.Count == 1 && segments.Count == 2 &&
                population.activeSet[0].Type == SplineObjectType.DEFORMATION &&
                splineLength < prefabMeshBounds.size.z)
            {
                SplineObject so = population.activeSet[0];

                so.snapSettings.startSnapDistance = 0;

                if (GeneralUtility.IsZero(startPadding))
                {
                    so.localSplinePosition.z = alignOffset - (prefabMeshBounds.size.z / 2) + (splineLength / 2);
                    so.snapSettings.startSnapDistance = soLength;
                }

                so.snapSettings.endSnapDistance = soLength;
                so.snapSettings.snapMode = SnapMode.CONTROL_POINTS;
            }
            else if (population.activeSet.Count > 0 && GeneralUtility.IsZero(startPadding))
            {
                SplineObject so = population.activeSet[0];
                so.localSplinePosition.z = startPadding + alignOffset;
            }
        }
    }
}
