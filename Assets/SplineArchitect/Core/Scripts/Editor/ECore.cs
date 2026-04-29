// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: ECore.cs
//
// Author: Mikael Danielsson
// Date Created: 28-01-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;


using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEditor.SceneManagement;

using SplineArchitect.PostProcessing;
using SplineArchitect.Utility;

namespace SplineArchitect
{
    public static class ECore
    {
        public static bool firstInitialization = true;
        private static PlayModeStateChange playModeStateChange;
        private static EditorWindow lastFocusedEditorWindow;

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void AfterAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;

            EditorApplication.update += Update;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.wantsToQuit += OnEditorWantsToQuit;

            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.beforeSceneGui += BeforeSceneGUI;

            Undo.undoRedoEvent += OnRedoEvents;
            Selection.selectionChanged += OnSelectionChange;

            Tools.pivotRotationChanged += OnPivotRotationChanged;

            ShortcutManager.instance.shortcutBindingChanged += OnShortcutBindingChanged;

            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            PrefabUtility.prefabInstanceUpdated += OnPrefabUpdate;
            PrefabUtility.prefabInstanceReverted += OnPrefabRevert;
        }

        private static void OnPrefabUpdate(GameObject go)
        {
            EHandlePrefab.OnPrefabUpdate(go);
        }

        private static void OnPrefabRevert(GameObject go)
        {
            EHandlePrefab.OnPrefabRevert(go);
        }

        private static void OnPrefabStageOpened(PrefabStage prefabStage)
        {
            EHandlePrefab.OnPrefabStageOpened(prefabStage);
        }

        private static void OnPrefabStageClosing(PrefabStage prefabStage)
        {
            EHandlePrefab.OnPrefabStageClosing(prefabStage);
        }

        private static void OnShortcutBindingChanged(ShortcutBindingChangedEventArgs args)
        {
            EHandleUi.OnShortcutBindingChanged();
        }

        private static void OnWindowFocusChanged()
        {
            EHandleUi.OnWindowFocusChanged();
        }

        private static bool OnEditorWantsToQuit()
        {
            EHandleUi.OnEditorWantsToQuit();
            return true;
        }

        private static void BeforeAssemblyReload()
        {
            EHandleUi.BeforeAssemblyReload();
            HandleRegistry.DisposeNativeDataOnSplines();
        }

        private static void OnPivotRotationChanged()
        {
            EHandleTool.OnPivotRotationChanged();
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            EHandleSelection.OnHierarchyGUI(instanceID, selectionRect);
        }

        private static void OnSelectionChange()
        {
            EHandleSelection.OnSelectionChange();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            playModeStateChange = state;
            EHandleEvents.playModeStateChange = state;
            //bool domainReloadDisabled = EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload);
            foreach (Spline spline in HandleRegistry.GetSplinesUnsafe())
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    spline.DisposeCache();
            }

            EHandleTool.OnPlayModeStateChanged(state);
        }

        private static void BeforeSceneGUI(SceneView sceneView)
        {
            if (!EHandleSceneView.IsValid(sceneView))
                return;

            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode || playModeStateChange == PlayModeStateChange.ExitingEditMode)
                return;

            EHandleTool.BeforeSceneGUIGlobal(sceneView, Event.current);
            EHandleSpline.BeforeSceneGUIGlobal(sceneView, Event.current);
            EHandleSceneView.BeforeSceneGUIGlobal(sceneView, Event.current);
            EHandleSelection.BeforeSceneGUIGlobal(sceneView, Event.current);
            EHandleUi.BeforeSceneGUIGlobal(sceneView, Event.current);
            EHandleEvents.InitAfterDrag(sceneView);
            EActionToSceneGUI.BeforeOnSceneGUI(Event.current);
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!EHandleSceneView.IsValid(sceneView))
                return;

            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode || playModeStateChange == PlayModeStateChange.ExitingEditMode)
                return;

            //Needs to be first
            EActionToSceneGUI.EArlyOnSceneGUI(Event.current);

            foreach (Spline spline in HandleRegistry.GetSplinesUnsafe())
            {
                if (spline == null)
                {
                    EHandleUndo.MarkSplineForDestroy(spline);
                    continue;
                }

                if (spline.IsEnabled() == false) continue;
                if (!spline.editorInitialized) continue;

                if (!EHandleSelection.IsPrimiarySelection(spline)) continue;

                EHandleSpline.OnSceneGUI(spline, Event.current);
                EHandleTool.OnSceneGUI(spline, Event.current, sceneView);
                EHandleSplineObject.OnSceneGUI(spline, Event.current);
            }

            EHandleUi.OnSceneGUIGlobal(sceneView);
            EHandleUndo.DestroyMarkedSplines();
            EHandleDrawing.OnSceneGUIGlobal(HandleRegistry.GetSplinesUnsafe(), Event.current);

            //Needs to be last
            EActionToSceneGUI.LateOnSceneGUI(Event.current);
        }

        private static void Update()
        {
            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode || playModeStateChange == PlayModeStateChange.ExitingEditMode)
              return;

            if (firstInitialization)
                EHandleEvents.InvokeFirstUpdate();

            if(lastFocusedEditorWindow != EditorWindow.focusedWindow)
            {
                lastFocusedEditorWindow = EditorWindow.focusedWindow;
                OnWindowFocusChanged();
            }

            EHandleEvents.InvokeUpdateEarly();
            EActionDelayed.UpdateGlobalEarly();

            //Needs to be before all splines update. Spline connectors might change segment positions and if its after "EHandleDeformation.ProcessSplineObjects(spline)"
            //changes will not be registred at all.
            EHandleSplineConnector.UpdateGlobal();

            foreach (Spline spline in HandleRegistry.GetSplinesUnsafe())
            {
                if (spline == null) continue;

                EHandleSpline.InitalizeEditor(spline, firstInitialization);

                if (spline.IsEnabled() == false) continue;

                EHandleSegment.HandleLinking(spline);

                bool isPrimiarySelection = EHandleSelection.IsPrimiarySelection(spline);
                bool IsSecondarySelection = EHandleSelection.IsSecondarySelection(spline);
                bool forceUpdate = spline.IsEditorCacheDirty() || EHandleSelection.IsConnectedToSelection(spline) || EHandleSelection.IsChildOfSelected(spline);

                if (isPrimiarySelection || IsSecondarySelection || forceUpdate)
                {
                    EHandleSpline.UpdateLinksOnTransformChange(spline);
                    EHandleDeformation.ProcessSplineObjects(spline);
                }

                if (isPrimiarySelection || IsSecondarySelection || forceUpdate || spline.jobState == JobState.EDITOR)
                {
                    EHandleDeformation.ProcessJobs(spline, true);
                }

                if (isPrimiarySelection)
                {
                    EHandleSplineObject.Update(spline);
                }
            }

            EHandlePrefab.UpdateGlobal();
            EHandleMeshContainer.RefreshAfterAssetImport(HandleRegistry.GetSplinesUnsafe());
            BuildProcessReport.UpdateGlobal();
            EHandleSpline.ProcessMarkedForInfoUpdates();
            EHandleUi.UpdateGlobal();
            HandleRegistry.UpdateGlobal();
            EHandleWarningWindow.UpdateGlobal();
            AssetModificationDetection.UpdateGlobal();
            EHandleUi.Init();

            //Second last
            EActionDelayed.UpdateGlobalLate();
            
            //Last
            foreach (Spline spline in HandleRegistry.GetSplinesUnsafe())
            {
                if (spline == null) continue;
                if (spline.IsEnabled() == false) continue;
                spline.Monitor.EditorUpdateTransform();
            }

            firstInitialization = false;
            EHandleEvents.undoActive = false;
        }

        private static void OnRedoEvents(in UndoRedoInfo undo)
        {
            EHandleEvents.undoActive = true;
            EHandleEvents.undoWasRedo = undo.isRedo;

            EHandleSelection.OnUndo();

            foreach (Spline spline in HandleRegistry.GetSplinesUnsafe())
            {
                if (spline == null) continue;

                spline.Monitor.EditorUpdateChildCount();

                for (int i = 0; i > spline.segments.Count; i++)
                {
                    Segment s = spline.segments[i];
                    s.indexInSpline = i;
                    s.splineParent = spline;
                    s.localSpace = spline.transform;
                }

                if (spline.IsEnabled() == false) continue;
                if (spline.segments.Count < 2) continue;

                bool isPrimiarySelection = EHandleSelection.IsPrimiarySelection(spline);
                bool IsSecondarySelection = EHandleSelection.IsSecondarySelection(spline);

                if (!IsSecondarySelection && !isPrimiarySelection) continue;

                EHandleEvents.InvokeUndoSelection(spline);
                EHandleSpline.MarkForInfoUpdate(spline);
            }

            EHandleTool.OnUndoGlobal();

            //Needs to be last
            EHandleUndo.UpdateUndoTriggerTime();
        }

        public static PlayModeStateChange GetLastPlayMode()
        {
            return playModeStateChange;
        }
    }
}
