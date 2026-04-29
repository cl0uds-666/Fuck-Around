// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: SplineObject_Lifecycle.cs
//
// Author: Mikael Danielsson
// Date Created: 28-03-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System;

using UnityEngine;
using Unity.Mathematics;

using SplineArchitect.Utility;
using SplineArchitect.Monitor;

namespace SplineArchitect
{
    public partial class SplineObject : MonoBehaviour
    {
        // Runtime data
        [NonSerialized] private bool initalized;

        private void OnEnable()
        {
            Initalize();
        }

        private void OnDisable()
        {
            if (splineParent == null)
                return;

            splineParent.RemoveSplineObject(this);
        }

        private void Start()
        {
#if !UNITY_EDITOR
            if (splineParent != null)
                return;

            enabled = false;
#endif
        }

        private void OnDestroy()
        {
            if (splineParent == null)
                return;

            splineParent.RemoveSplineObject(this);
        }

        private void OnTransformParentChanged()
        {
            //During copying this will run before OnEnable and the monitor will be null. So we can't run the code below.
            if (!initalized)
                return;

            SplineObject oldSoParent = soParent;
            Spline oldSplineParent = splineParent;

            //Updates for old parent
            splineParent?.RemoveSplineObject(this);

            //Update parent data
            SyncParentData();

            //Detach from spline
            if (oldSplineParent != null && soParent == null && splineParent == null)
            {
                int detachType = 1;
#if UNITY_EDITOR
                if (UnityEditor.Undo.isProcessing) detachType = 0;
#endif
                oldSplineParent.detachList.Add((this, detachType));

                //Need to reassign soParent data becouse its needed during detach. We need to get the localspace from the soParent.
                soParent = oldSoParent;
            }
            //Detach from spline
            else if (oldSplineParent != null && oldSoParent != null && splineParent == null)
            {
                int detachType = 2;
                oldSplineParent.detachList.Add((this, detachType));
            }
            //Attach or change parent
            else if (splineParent != null)
            {
                //Updates for new parent
                splineParent.AddSplineObject(this);

                //Reorder for parent hierarchy
                if (soParent != null)
                {
                    splineParent.RemoveSplineObject(this);

                    for (int i = 0; i < splineParent.AllSplineObjectCount; i++)
                    {
                        SplineObject so = splineParent.GetSplineObjectAtIndex(i);

                        if (so != soParent)
                            continue;

                        //Always add child directly after parent in list
                        if (i + 1 >= splineParent.AllSplineObjectCount)
                            splineParent.AddSplineObject(this);
                        else
                            splineParent.AddSplineObject(this, i + 1);
                        break;
                    }
                }

                //Change parent
                if (transform.parent != null && oldSplineParent != null)
                {
                    float4x4 combinedMatrixOld = SplineObjectUtility.GetCombinedParentMatrixs(oldSoParent);
                    float4x4 combinedMatrix = SplineObjectUtility.GetCombinedParentMatrixs(soParent);
                    localSplinePosition = math.transform(combinedMatrixOld, localSplinePosition);
                    localSplinePosition = math.transform(math.inverse(combinedMatrix), localSplinePosition);
                    Quaternion combinedRotations = Quaternion.Inverse(SplineObjectUtility.GetCombinedParentRotations(soParent)) * 
                                                   SplineObjectUtility.GetCombinedParentRotations(soParent);
                    localSplineRotation = Quaternion.Inverse(combinedRotations) * localSplineRotation;
#if UNITY_EDITOR
                    activationPosition = localSplinePosition;
                    EHandleEvents.InvokeSplineObjectParentChanged(this);
#endif
                    oldSplineParent.RemoveFromActiveWorkers(this);
                }
                else
                {
                    splineParent.attachList.Add(this);
                }

                MarkVersionDirty();
            }
        }

        internal void Initalize()
        {
#if UNITY_EDITOR
            if (meshMode != MeshMode.SAVE_IN_BUILD && EHandleEvents.buildRunning)
                return;

            if (EHandleEvents.dragActive)
            {
                EHandleEvents.InitalizeAfterDrag(this);
                return;
            }
#endif

            if (initalized)
            {
                splineParent?.AddSplineObject(this);
                return;
            }

            initalized = true;

            if (splineParent == null)
            {
                SyncParentData();

                if(type == SplineObjectType.NOT_SET)
                    type = defaultType;
            }

            if (monitor == null)
                monitor = new MonitorSplineObject(this);

            if (meshContainers == null)
                meshContainers = new List<MeshContainer>();

            oldVersion = version;

            if (splineParent == null)
            {
#if UNITY_EDITOR
                EHandleEvents.ForceUpdateSelection();
#endif
                return;
            }

            if (type == SplineObjectType.DEFORMATION)
            {
#if UNITY_EDITOR
                //Turn of static when playing in the editor for all deformations.
                //Else the static batached mesh will be offseted (only in editor playmode).
                if (Application.isPlaying && gameObject.isStatic)
                    gameObject.isStatic = false;

                //Important to wait after EHandleEvents.sceneIsLoadedPlaymodeduring Playmode in the editor.
                //Else we cant access vertices on meshes with isReadable = false;
                if (!Application.isPlaying || EHandleEvents.sceneIsLoadedPlaymode)
                {
#endif
                    CacheUntrackedInstanceMeshes();
                    SyncMeshContainers();
                    if (!Application.isPlaying || meshMode != MeshMode.DO_NOTHING)
                        SyncInstanceMeshesFromCache();
#if UNITY_EDITOR
                }
#endif
            }

            splineParent.AddSplineObject(this);

#if UNITY_EDITOR
            InitalizeEditor();
#endif
        }

        internal void SyncParentData()
        {
            soParent = transform.parent?.GetComponent<SplineObject>();
            splineParent = TryFindSplineParent();
        }
    }
}
