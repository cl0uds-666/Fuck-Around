// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EHandleDeformation.cs
//
// Author: Mikael Danielsson
// Date Created: 04-02-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System;

using UnityEngine;

using SplineArchitect.CustomTools;
using SplineArchitect.Workers;

namespace SplineArchitect
{
    internal static class EHandleDeformation
    {
        static private bool lastSplineDirty;

        internal static void ProcessJobs(Spline spline, bool updateCounter)
        {
            if (spline.jobState != JobState.EDITOR)
                return;

            if (spline.jobIntervalCounter <= 0)
            {
                spline.mainSystemWorker.Complete();
                spline.jobState = JobState.IDLE;
                spline.InvokeAfterJobs();
            }
            else if(updateCounter)
                spline.jobIntervalCounter--;
        }

        internal static void ProcessSplineObjects(Spline spline)
        {
            ProcessJobs(spline, false);

            if (spline.jobState == JobState.EDITOR)
                return;

            //Alvays priorites editor jobs.
            if (spline.jobState == JobState.RUNTIME)
            {
                spline.mainSystemWorker.Complete();
                spline.jobState = JobState.IDLE;
                spline.InvokeAfterJobs();
            }

            bool splineCacheDirty = spline.IsCacheDirty();
            bool noiseChange = spline.Monitor.NoiseChange();
            bool editorSplineCacheDirty = spline.IsEditorCacheDirty();
            bool editorTransformChange = spline.Monitor.EditorTransformChange();
            bool splineDirty = splineCacheDirty || editorSplineCacheDirty || noiseChange || editorTransformChange;

            if (splineDirty)
            {
                spline.RebuildCache();
            }

            foreach (Population population in spline.populations)
            {
                if (splineDirty || lastSplineDirty || population.IsVersionDirty())
                {
                    if (population.UpdateOverTime)
                        spline.PopulateUsingPoolJobSafe(population);
                    else
                    {
                        if (spline.jobInterval > 0 || (spline.jobStartType == JobType.LATE_UPDATE && spline.jobEndType == JobType.UPDATE))
                            continue;

                        spline.PopulateUsingPool(population);
                    }
                }
            }

            for (int i = 0; i < spline.AllSplineObjectCount; i++)
            {
                SplineObject so = spline.GetSplineObjectAtIndex(i);

                if (so == null || !so.IsEnabled() || so.Monitor == null)
                    continue;

                if (editorTransformChange && !editorSplineCacheDirty)
                {
                    //When rotation and moving the Spline we need to update oc:s monitor.
                    //Else they will be deformed but its no need for it.
                    so.Monitor.UpdatePosRotSplineSpace();
                    so.Monitor.UpdateTransform();
                    continue;
                }

                //Monitor
                bool combinedParentPosRotScaleChange = so.Monitor.CombinedParentPosRotScaleChange();
                bool posRotSplineSpaceChange = so.Monitor.PosRotSplineSpaceChange();
                bool scaleChange = so.Monitor.TransformScaleChange();
                bool versionDirty = so.IsVersionDirty();
                bool updateSo = versionDirty ||
                                combinedParentPosRotScaleChange ||
                                posRotSplineSpaceChange ||
                                scaleChange;

                if (so.editorAutoType && PositionTool.activePart == PositionTool.ActivePart.NONE)
                    EHandleSplineObject.UpdateTypeAuto(spline, so);

                if (versionDirty)
                {
                    if (so.Monitor.SplineObjectTypeChange(out SplineObjectType oldType))
                        EHandleSplineObject.Convert(spline, so, oldType);

                    if (so.Monitor.MirrorChange())
                    {
                        so.ReverseTrianglesOnAll();
                    }

                    if (so.Monitor.NormalChange())
                    {
                        if (so.NormalType == NormalType.DO_NOT_CALCULATE)
                        {
                            so.SetOriginNormalsOnAll();
                            so.SetOriginTrianglesOnAll();
                            so.SetOriginTangentsOnAll();
                        }
                        else if (so.NormalType == NormalType.UNITY_CALCULATED_SEAMLESS)
                            so.SetSeamlessTrianglesOnAll();
                        else
                            so.SetOriginTrianglesOnAll();
                    }
                }

                if (so.Type != SplineObjectType.NONE)
                {
                    if (!splineDirty && !updateSo)
                        continue;

                    if (so.Type == SplineObjectType.DEFORMATION && !EHandleSplineObject.ValidForEditorDeformation(so))
                        return;

                    spline.mainSystemWorker.Add(so);
                }
                else
                {
                    if (!splineDirty && !so.Monitor.TransformPosRotChange())
                        continue;

                    so.splinePosition = spline.WorldPositionToSplinePosition(so.transform.position, 12);
                    so.splineRotation = spline.WorldRotationToSplineRotation(so.transform.rotation, 
                                                                             so.splinePosition.z / spline.Length);
                }
            }

            int frames = spline.jobInterval;

            if (!Application.isPlaying)
            {
                float deformationPerformance = Mathf.Lerp(2f, 0f, EGlobalSettings.GetDeformationPerformance());
                frames = Math.Max(Mathf.RoundToInt(spline.GetVerticesCountInWorkers() / 
                                                   DeformationWorker.MAX_VERTICES * deformationPerformance), 2) - 2;
            }

            if (spline.mainSystemWorker.HasWork())
            {
                spline.mainSystemWorker.Start();
                spline.jobIntervalCounter = frames;
                spline.jobState = JobState.EDITOR;
            }

            lastSplineDirty = splineDirty;
        }
    }
}