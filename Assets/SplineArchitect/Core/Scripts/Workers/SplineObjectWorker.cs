// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: SplineObjectWorker.cs
//
// Author: Mikael Danielsson
// Date Created: 16-01-2026
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

using UnityEngine;

namespace SplineArchitect.Workers
{
    public class SplineObjectWorker
    {
        private Spline spline;
        private FollowerWorker followerWorker;
        private List<DeformationWorker> deformationWorkers;
        private List<SplineObject> waitingList;
        private List<SplineObject> workingList;

        public SplineObjectWorker(Spline spline = null)
        {
            this.spline = spline;

            followerWorker = new FollowerWorker(spline);
            deformationWorkers = new List<DeformationWorker>();
            workingList = new List<SplineObject>();
            waitingList = new List<SplineObject>();
        }

        public void Add(SplineObject so)
        {
            if (IsWorking())
            {
                waitingList.Add(so);
                return;
            }

            if (so.Type == SplineObjectType.DEFORMATION)
            {
                DeformationWorker deformationWorker = GetDeformationWorkerFromPool();
                deformationWorker.Add(so);
            }
            else
            {
                followerWorker.Add(so);
            }

            workingList.Add(so);
        }

        public void Start()
        {
            if (spline.isInvalidShape)
            {
                waitingList.Clear();
                foreach (DeformationWorker dw in deformationWorkers)
                    dw.CompleteWithoutAssignData();
                followerWorker.CompleteWithoutAssignData();
                return;
            }

            foreach (SplineObject so in waitingList) Add(so);
            waitingList.Clear();

            foreach (DeformationWorker dw in deformationWorkers)
                dw.Start();

            followerWorker.Start();
        }

        public void Complete()
        {
            foreach (DeformationWorker dw in deformationWorkers)
                dw.Complete();

            followerWorker.Complete();

            foreach(SplineObject so in workingList)
            { if(so.Type == SplineObjectType.DEFORMATION && so != null) so.UpdateExternalComponents(); }
            workingList.Clear();
        }

        public void CompleteWithoutAssignData()
        {
            foreach (DeformationWorker dw in deformationWorkers)
                dw.CompleteWithoutAssignData();

            followerWorker.CompleteWithoutAssignData();
        }

        public void Remove(SplineObject so)
        {
            if (so.Type == SplineObjectType.DEFORMATION)
            {
                foreach (DeformationWorker dw in deformationWorkers)
                {
                    if (dw.Contains(so))
                    {
                        dw.Remove(so);
                        break;
                    }
                }
            }
            else
            {
                if (followerWorker.Contains(so))
                    followerWorker.Remove(so);
            }
        }

        public bool Contains(SplineObject so)
        {
            if (so.Type == SplineObjectType.DEFORMATION)
            {
                foreach (DeformationWorker dw in deformationWorkers)
                {
                    if (dw.Contains(so))
                        return true;
                }
            }
            else
            {
                if (followerWorker.Contains(so))
                    return true;
            }

            return false;
        }

        public void Deform(SplineObject so, bool updateExternalComponents = false)
        {
            if (so.Type == SplineObjectType.DEFORMATION)
            {
                DeformationWorker deformationWorker = GetDeformationWorkerFromPool();
                deformationWorker.Deform(so);

                if(updateExternalComponents)
                    so.UpdateExternalComponents();
            }
            else
            {
                followerWorker.Deform(so);

                if (updateExternalComponents)
                    so.UpdateExternalComponents();
            }
        }

        public void SetSpline(Spline spline)
        {
            this.spline = spline;
            followerWorker.SetSpline(spline);

            foreach (DeformationWorker dw in deformationWorkers)
                dw.SetSpline(spline);
        }

        public bool IsWorking()
        {
            if (followerWorker.workerState == WorkerState.WORKING)
                return true;

            foreach (DeformationWorker dw in deformationWorkers)
            {
                if (dw.workerState == WorkerState.WORKING)
                    return true;
            }

            return false;
        }

        public bool HasWork()
        {
            if (followerWorker.GetWorkCount() > 0)
                return true;

            foreach(DeformationWorker dw in deformationWorkers)
            {
                if (dw.GetWorkCount() > 0)
                    return true;
            }

            return false;
        }

        public int GetVerticesCount()
        {
            int count = 0;

            count += followerWorker.GetWorkCount();

            foreach (DeformationWorker dw in deformationWorkers)
                count += dw.totalVertices;

            return count;
        }

        private DeformationWorker GetDeformationWorkerFromPool()
        {
            foreach (DeformationWorker dw in deformationWorkers)
            {
                if (dw.workerState != WorkerState.FULL && dw.workerState != WorkerState.WORKING)
                {
                    return dw;
                }
            }

            DeformationWorker newDw = new DeformationWorker(spline);
            deformationWorkers.Add(newDw);

#if UNITY_EDITOR
            if (deformationWorkers.Count > 500)
                Debug.LogWarning($"[Spline Architect] Currently: {deformationWorkers.Count} deformation workers exists!");
#endif

            return newDw;
        }
    }
}
