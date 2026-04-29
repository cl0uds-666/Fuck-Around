// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: WindowExtended.cs
//
// Author: Mikael Danielsson
// Date Created: 05-08-2025
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using SplineArchitect.Libraries;
using SplineArchitect.Utility;

namespace SplineArchitect.Ui
{
    public class WindowExtended : WindowBase
    {
        //General
        private string windowTitle = "";
        private static List<Segment> segmentContainer = new List<Segment>();

        //Spline object
        const int maxSplineObjectNameLength = 25;
        private static GUIContent guiContentContainer = new GUIContent("SplineObject");
        private static string[] typeOptions = new string[] { "Deformation", "Follower", "None" };
        private static string[] normalsOption = new string[] {"Spline space (Default)", 
                                                              "Cylinder based (Good for cylinder shapes)", 
                                                              "Unity calculated",
                                                              "Unity calculated (Seamless)",
                                                              "Do not calculate" };
        private static string[] componentModeOptions = new string[] { "Remove from build", "Inactivate after scene load", "Active" };
        private static string[] optionsMeshMode = new string[] { "Save in build", "Save in scene", "Generate", "Do nothing" };

        //Control point
        private static string[] interpolationMode = new string[] { "Spline", "Line" };

        //Addons
        private static List<(string, Action<Spline, int>)> addonsDrawWindowCp = new();
        private static List<(string, GUIContent, GUIContent)> addonsButtonsCp = new();
        private static List<(string, Func<Spline, Rect>)> addonsCalcWindowSizeCp = new();

        private static List<(string, Action<SplineObject>)> addonsDrawWindowSo = new();
        private static List<(string, GUIContent, GUIContent)> addonsButtonsSo = new();
        private static List<(string, Func<SplineObject, Rect>)> addonsCalcWindowSizeSo = new();

        protected override void OnGUIExtended()
        {
            if (EGlobalSettings.GetExtendedWindowMinimized())
            {
                //Maximize
                EUiUtility.CreateButton(ButtonType.SUB_MENU, LibraryGUIContent.iconMaximize, 25, 25, () =>
                {
                    EActionToSceneGUI.Add(() =>
                    {
                        EGlobalSettings.SetExtendedWindowMinimized(false);
                    }, EActionToSceneGUI.Type.LATE, EventType.Repaint);
                    EHandleSceneView.RepaintCurrent();
                });

                return;
            }

            Spline spline = EHandleSelection.selectedSpline;
            SplineObject so = EHandleSelection.selectedSplineObject;

            if (so != null)
            {
                OnGUISplineObject(spline, so);
            }
            else if(spline != null)
            {
                int segmentIndex = SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint);

                if (segmentIndex >= 0 && spline.segments.Count > 0 && segmentIndex < spline.segments.Count)
                {
                    OnGUIControlPoint(spline, spline.segments[segmentIndex], segmentIndex, SplineUtility.GetControlHandleType(spline.selectedControlPoint));
                }
            }
        }

        private void OnGUISplineObject(Spline spline, SplineObject so)
        {
            #region top
            bool isDeformation = so.Type == SplineObjectType.DEFORMATION;
            bool isFollower = so.Type == SplineObjectType.FOLLOWER;
            bool isNone = so.Type == SplineObjectType.NONE;

            EUiUtility.ResetGetBackgroundStyleId();

            //Title
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundHeader);
            //Error, no read/write access
            if (!EHandleSplineObject.HasReadWriteAccessEnabled(so) && so.Type == SplineObjectType.DEFORMATION) 
                EUiUtility.CreateErrorWarningMessageIcon(LibraryGUIContent.warningMsgCantDoRealtimeDeformation);
            windowTitle = so.name;
            if (windowTitle.Length > maxSplineObjectNameLength) windowTitle = $"{windowTitle.Substring(0, maxSplineObjectNameLength)}..";
            if (EHandleSelection.selectedSplineObjects.Count > 0) windowTitle = $"{windowTitle} + ({EHandleSelection.selectedSplineObjects.Count})";
            EUiUtility.CreateLabelField($"<b>{windowTitle}</b>", LibraryGUIStyle.textHeaderBlack, true);

            if (!EGlobalSettings.GetIsWindowsFloating())
            {
                GUILayout.FlexibleSpace();
                //Minimize
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconMinimize, 19, 14, () =>
                {
                    EActionToSceneGUI.Add(() =>
                    {
                        EGlobalSettings.SetExtendedWindowMinimized(true);
                    }, EActionToSceneGUI.Type.LATE, EventType.Repaint);
                    EHandleSceneView.RepaintCurrent();
                });
            }
            GUILayout.EndHorizontal();

            EUiUtility.CreateHorizontalBlackLine();
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundToolbar);

            //Snap
            bool enableSnap = isDeformation && so.MeshContainerCount > 0;
            EUiUtility.CreateButtonToggle(ButtonType.DEFAULT, LibraryGUIContent.iconMagnet,
            so.SnapSettings.snapMode == SnapMode.SPLINE_POINT ? LibraryGUIContent.iconMagnetActive2 : LibraryGUIContent.iconMagnetActive, 35, 19, () =>
            {
                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                {
                    SnapSettings snapSettings = selected.SnapSettings;
                    if (selected.SnapSettings.snapMode == SnapMode.NONE) snapSettings.snapMode = SnapMode.CONTROL_POINTS;
                    else if (selected.SnapSettings.snapMode == SnapMode.CONTROL_POINTS) snapSettings.snapMode = SnapMode.SPLINE_POINT;
                    else if (selected.SnapSettings.snapMode == SnapMode.SPLINE_POINT) snapSettings.snapMode = SnapMode.NONE;
                    selected.SnapSettings = snapSettings;
                }, "Toggle snap deformation");
            }, so.SnapSettings.snapMode > 0 && enableSnap, isDeformation && so.MeshContainerCount > 0);

            //Mirror
            EUiUtility.CreateButtonToggle(ButtonType.DEFAULT, LibraryGUIContent.iconMirrorDeformation, LibraryGUIContent.iconMirrorDeformationActive, 35, 19, () =>
            {
                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                {
                    selected.MirrorDeformation = !selected.MirrorDeformation;
                }, "Toggle mirror deformation");
            }, so.MirrorDeformation, isDeformation && so.MeshContainerCount > 0);

            //Auto type
            EUiUtility.CreateButtonToggle(ButtonType.DEFAULT, LibraryGUIContent.iconAuto, LibraryGUIContent.iconAutoActive, 35, 19, () =>
            {
                bool value = !so.editorAutoType;
                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                {
                    selected.editorAutoType = value;
                }, "Toggled auto type");
            }, so.editorAutoType, so.Type != SplineObjectType.NONE);

            EUiUtility.CreateSeparator();

            //Objects to spline center
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconToCenter, 35, 19, () =>
            {
                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                {
                    EHandleSplineObject.ToSplineCenter(selected);
                }, "Object to spline center");

                EActionToSceneGUI.Add(() =>
                {
                    EHandleTool.UpdateOrientationForPositionTool(EHandleSceneView.GetCurrent(), spline);
                }, EActionToSceneGUI.Type.LATE, EventType.Layout);
            }, isFollower || isDeformation);

            //Export mesh
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconExport, 35, 19, () =>
            {
                string path = EditorUtility.SaveFilePanelInProject("Export mesh", $"{so.name}", "asset", "Assets/");
                int count = 0;

                if (!string.IsNullOrEmpty(path))
                {
                    //For some very odd reason I need to do this. Likely becouse of EditorUtility.SaveFilePanelInProject.
                    EHandleSelection.stopUpdateSelection = true;
                    EHandleSelection.UpdatedSelectedSplineObjects((selected) =>
                    {
                        string[] data = path.Split('/');

                        path = "";
                        for (int i = 0; i < data.Length - 1; i++)
                            path += $"{data[i]}/";

                        if (count == 0) EHandleSplineObject.ExportMeshes(selected, $"{path}{so.name}");
                        else EHandleSplineObject.ExportMeshes(selected, $"{path}{so.name}{count}");
                        count++;
                    });
                    EHandleSelection.stopUpdateSelection = false;
                }
            });

            GUILayout.EndHorizontal();

            if (EGlobalSettings.GetSubmenusOnTop()) DrawBottomSplineObject(so);
            #endregion

            #region general
            if (so.selectedMenu == "general")
            {
                //Sub title
                EUiUtility.CreateSection("GENERAL");

                //Position
                EUiUtility.CreateXYZInputFields("Position", so.localSplinePosition, (position, dif) =>
                {
                    EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                    {
                        selected.localSplinePosition -= dif;
                        selected.activationPosition = so.localSplinePosition;
                        EHandleEvents.InvokeAfterSplineObjectSetPositionInUi(selected);
                    }, "Updated position");

                    EActionToSceneGUI.Add(() =>
                    {
                        EHandleTool.UpdateOrientationForPositionTool(EHandleSceneView.GetCurrent(), spline);
                    }, EActionToSceneGUI.Type.LATE, EventType.Layout);
                }, 92, 10, 62, isNone);

                //Rotation
                bool rotFieldDisabled = so.FollowAxels.x == 0 || so.FollowAxels.y == 0 || so.FollowAxels.z == 0 || isNone;
                Vector3 localSplineRotation = so.localSplineRotation.eulerAngles;
                if (rotFieldDisabled)
                {
                    if (spline == null)
                        localSplineRotation = so.transform.localRotation.eulerAngles;
                    else
                        localSplineRotation = spline.WorldRotationToSplineRotation(so.transform.rotation, so.localSplinePosition.z / spline.Length).eulerAngles;
                }
                EUiUtility.CreateXYZInputFields("Rotation", localSplineRotation, (euler, dif) =>
                {
                    EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                    {
                        selected.localSplineRotation.eulerAngles = euler;
                    }, "Updated rotation");
                }, 92, 10, 62, rotFieldDisabled);

                if (isFollower)
                {
                    //Follow rotation
                    EUiUtility.CreateToggleXYZField("Follow rotation:", so.FollowAxels, (Vector3Int newValue) => {

                        EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                        {
                            selected.FollowAxels = newValue;

                            if (selected.FollowAxels == Vector3.one)
                            {
                                if (spline == null)
                                    selected.localSplineRotation = selected.transform.localRotation;
                                else
                                    selected.localSplineRotation = spline.WorldRotationToSplineRotation(selected.transform.rotation, selected.localSplinePosition.z / spline.Length);
                            }
                        }, "Updated follower axels");
                    });
                }

                else if (isDeformation)
                {
                    GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                    //Normals
                    EUiUtility.CreatePopupField("Normals:", 80, (int)so.NormalType, normalsOption, (int newValue) =>
                    {
                        EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                        {
                            selected.NormalType = (NormalType)newValue;
                        }, "Updated normal type");
                    }, 60, true);

                    //Can't generate meshe during runtime
                    GUILayout.FlexibleSpace();
                    if (so.meshMode == MeshMode.GENERATE && (so.componentMode == ComponentMode.REMOVE_FROM_BUILD || (spline != null && spline.componentMode == ComponentMode.REMOVE_FROM_BUILD)))
                        EUiUtility.CreateErrorWarningMessageIcon(LibraryGUIContent.errorMsgGenerateMeshRuntime, true);
                    else if (EGlobalSettings.GetInfoIconsVisibility())
                        EUiUtility.CreateInfoMessageIcon(LibraryGUIContent.infoMsgMeshMode, true);
                    else
                        GUILayout.Space(15);
                    EUiUtility.CreatePopupField("Mesh:", 80, (int)so.meshMode, optionsMeshMode, (int newValue) =>
                    {
                        EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                        {
                            selected.meshMode = (MeshMode)newValue;

                            EditorUtility.SetDirty(selected);
                        }, "Change mesh mode");
                    }, -1, true);
                    GUILayout.EndHorizontal();

                }
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                if (isFollower)
                {
                    //World position mode
                    EUiUtility.CreateLabelField("Lock position: ", LibraryGUIStyle.textDefault, true, 88);
                    EUiUtility.CreateCheckbox(so.LockPosition, (bool newValue) => {

                        EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                        {
                            selected.LockPosition = newValue;
                        }, "Toggled lock position");
                    });
                }
                GUILayout.FlexibleSpace();
                //Should not be active or inactive when spline is removed
                if (spline != null && spline.componentMode == ComponentMode.REMOVE_FROM_BUILD && so.componentMode != ComponentMode.REMOVE_FROM_BUILD)
                    EUiUtility.CreateErrorWarningMessageIcon(LibraryGUIContent.warningMsgComponentOptionIsRedundant, true);
                //Remove from build unsupported for prefabs
                else if (!EHandlePrefab.IsPrefabRoot(so.gameObject) && (EHandlePrefab.IsPartOfAnyPrefab(so.gameObject) || EHandlePrefab.prefabStageOpen))
                    EUiUtility.CreateErrorWarningMessageIcon(LibraryGUIContent.warningMsgComponentOptionPrefab, true);
                //Can't generate mesh on static game objects.
                else if (so.gameObject.isStatic && (so.meshMode == MeshMode.GENERATE || so.meshMode == MeshMode.DO_NOTHING))
                    EUiUtility.CreateErrorWarningMessageIcon(LibraryGUIContent.errorMsgStaticSO, true);
                else 
                    EUiUtility.CreateInfoMessageIcon(LibraryGUIContent.infoMsgSOComponentMode, true);
                EUiUtility.CreatePopupField("Component:", 80, (int)so.componentMode, componentModeOptions, (int newValue) =>
                {
                    EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                    {
                        selected.componentMode = (ComponentMode)newValue;

                        //Set component mode for whole hierarchy
                        SplineObject firstSo = selected;
                        for (int i = 0; i < 25; i++)
                        {
                            if (firstSo.SoParent == null)
                                break;

                            firstSo = firstSo.SoParent;
                        }

                        EHandleUndo.RecordNow(firstSo);
                        firstSo.componentMode = (ComponentMode)newValue;

                        if(so.SplineParent != null)
                        {
                            for (int i = 0; i < so.SplineParent.AllSplineObjectCount; i++)
                            {
                                SplineObject so2 = so.SplineParent.GetSplineObjectAtIndex(i);
                                if (firstSo.IsAncestorOf(so2))
                                {
                                    EHandleUndo.RecordNow(so2);
                                    so2.componentMode = (ComponentMode)newValue;
                                }
                            }
                        }
                    }, "Change component mode");

                    EHandleSpline.MarkForInfoUpdate(spline);
                }, 80, true);

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());

                GUILayout.Space(7);
                //Align to end
                EUiUtility.CreateToggleField("Align to end:", so.AlignToEnd, (value) =>
                {
                    EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                    {
                        //Set component mode for whole hierarchy
                        SplineObject firstSo = selected;
                        for (int i = 0; i < 25; i++)
                        {
                            if (firstSo.SoParent == null)
                                break;

                            firstSo = firstSo.SoParent;
                        }

                        EHandleUndo.RecordNow(firstSo);
                        bool oldAlignToEnd = firstSo.AlignToEnd;
                        firstSo.AlignToEnd = value;

                        if(so.SplineParent != null)
                        {
                            firstSo.MarkVersionDirty();
                            for (int i = 0; i < so.SplineParent.AllSplineObjectCount; i++)
                            {
                                SplineObject so2 = so.SplineParent.GetSplineObjectAtIndex(i);

                                if (firstSo.IsAncestorOf(so2))
                                {
                                    EHandleUndo.RecordNow(so2);
                                    oldAlignToEnd = so2.AlignToEnd;
                                    so2.AlignToEnd = value;
                                    so2.MarkVersionDirty();
                                }
                            }
                        }
                    }, "Toggle align to end");

                    EHandleTool.ActivatePositionToolForSplineObject(spline, so);
                    EHandleSceneView.RepaintCurrent();
                }, true, true, 81);

                //Type
                EUiUtility.CreatePopupField("Type:", 80, (int)so.Type, typeOptions, (int newValue) =>
                {
                    SplineObjectType state = (SplineObjectType)newValue;

                    EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                    {
                        selected.Type = state;
                        EditorUtility.SetDirty(selected);
                    }, "Update type");

                    EHandleSpline.MarkForInfoUpdate(spline);
                }, -1, true, !so.editorAutoType);
                GUILayout.EndHorizontal();

                if (so.Type == SplineObjectType.DEFORMATION && so.snapSettings.snapMode != SnapMode.NONE)
                {
                    //Sub title
                    GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundSection);
                    EUiUtility.CreateLabelField("<b>SNAP SETTINGS</b>", LibraryGUIStyle.textSection, true);
                    GUILayout.Space(100);
                    EUiUtility.CreateButton(ButtonType.SUB_MENU2,
                    so.snapSettingsMinimized ?
                    LibraryGUIContent.iconMaximizeBlack :
                    LibraryGUIContent.iconMinimizeBlack, 18, 12, () =>
                    {
                        EHandleUndo.RecordNow(so, "Toggle snap settings");
                        so.snapSettingsMinimized = !so.snapSettingsMinimized;
                    });
                    GUILayout.EndHorizontal();

                    if(!so.snapSettingsMinimized)
                    {
                        GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                        EUiUtility.CreateLabelField("Snap length:", LibraryGUIStyle.textDefault, true, 99);

                        //Snap length start
                        EUiUtility.CreateFloatFieldWithLabel("Start", so.SnapSettings.startSnapDistance, (newValue) => {
                            //Update selected an record undo
                            EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                            {
                                SnapSettings snapSettings = selected.SnapSettings;
                                snapSettings.startSnapDistance = newValue;
                                selected.SnapSettings = snapSettings;
                            }, "Set snap length start");
                        }, 50, 38, true);

                        EUiUtility.CreateSpaceWidth(0);

                        //Snap length end
                        EUiUtility.CreateFloatFieldWithLabel("End", so.SnapSettings.endSnapDistance, (newValue) => {
                            //Update selected an record undo
                            EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                            {
                                SnapSettings snapSettings = selected.SnapSettings;
                                snapSettings.endSnapDistance = newValue;
                                selected.SnapSettings = snapSettings;
                            }, "Set snap length end");
                        }, 50, 34, true);
                        GUILayout.EndHorizontal();

                        if (so.SnapSettings.snapMode == SnapMode.CONTROL_POINTS)
                        {
                            GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                            GUILayout.Space(2);
                            EUiUtility.CreateLabelField("Snap offset:", LibraryGUIStyle.textDefault, true, 97);

                            //Snap length start
                            EUiUtility.CreateFloatFieldWithLabel("Start", so.SnapSettings.startSnapOffset, (newValue) => {
                                //Update selected an record undo
                                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                                {
                                    SnapSettings snapSettings = selected.SnapSettings;
                                    snapSettings.startSnapOffset = newValue;
                                    selected.SnapSettings = snapSettings;
                                }, "Set snap offset start");
                            }, 50, 38, true);

                            EUiUtility.CreateSpaceWidth(0);

                            //Snap length end
                            EUiUtility.CreateFloatFieldWithLabel("End", so.SnapSettings.endSnapOffset, (newValue) => {
                                //Update selected an record undo
                                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                                {
                                    SnapSettings snapSettings = selected.SnapSettings;
                                    snapSettings.endSnapOffset = newValue;
                                    selected.SnapSettings = snapSettings;
                                }, "Set snap offset end");
                            }, 50, 34, true);
                            GUILayout.EndHorizontal();
                        }
                        else if(so.SnapSettings.snapMode == SnapMode.SPLINE_POINT)
                        {
                            EUiUtility.CreateFloatFieldWithLabel("Spline point:", so.SnapSettings.snapTargetPoint, (newValue) => {
                                //Update selected an record undo
                                EHandleSelection.UpdatedSelectedSplineObjectsRecordUndo((selected) =>
                                {
                                    SnapSettings snapSettings = selected.SnapSettings;
                                    snapSettings.snapTargetPoint = newValue;
                                    selected.SnapSettings = snapSettings;
                                }, "Set snap length end");
                            }, 50);
                        }
                    }
                }
            }
            #endregion

            #region info
            else if (so.selectedMenu == "info")
            {
                EUiUtility.CreateSection("INFO");

                //Vertecies label
                int vertecies = so.deformedVertecies;
                int deformations = so.deformations;
                List<SplineObject> selection = EHandleSelection.selectedSplineObjects;
                if (selection.Count > 0)
                {
                    foreach (SplineObject oc2 in selection)
                    {
                        vertecies += oc2.deformedVertecies;
                        deformations += oc2.deformations;
                    }
                }
                EUiUtility.CreateLabelField("Vertecies: " + vertecies, LibraryGUIStyle.textDefault);

                //Deformations label
                EUiUtility.CreateLabelField("Deformations: " + deformations, LibraryGUIStyle.textDefault);
            }
            #endregion

            #region addons
            else
            {
                bool foundAddon = false;

                for (int i = 0; i < addonsDrawWindowSo.Count; i++)
                {
                    if (addonsDrawWindowSo[i].Item1 == so.selectedMenu)
                    {
                        foundAddon = true;
                        addonsDrawWindowSo[i].Item2.Invoke(so);
                    }
                }

                if (foundAddon == false)
                    so.selectedMenu = "general";
            }
            #endregion

            if (!EGlobalSettings.GetSubmenusOnTop()) DrawBottomSplineObject(so);
        }

        private void OnGUIControlPoint(Spline spline, Segment segment, int segmentIndex, ControlHandle handle)
        {
            bool leftMouseUp = false;
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0) leftMouseUp = true;

            EUiUtility.ResetGetBackgroundStyleId();

            #region Top
            //Header
            string selectionCountText = spline.selectedAnchors.Count > 0 ? $" + ({spline.selectedAnchors.Count})" : "";
            if (handle == ControlHandle.TANGENT_A) windowTitle = $"Tangent A {segmentIndex + 1}";
            else if (handle == ControlHandle.TANGENT_B) windowTitle = $"Tangent B {segmentIndex + 1}";
            else windowTitle = $"Anchor {segmentIndex + 1} {selectionCountText}";
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundHeader);
            EUiUtility.CreateLabelField($"<b>{windowTitle}</b>", LibraryGUIStyle.textHeaderBlack, true);

            if (!EGlobalSettings.GetIsWindowsFloating())
            {
                GUILayout.FlexibleSpace();
                //Minimize
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconMinimize, 19, 14, () =>
                {
                    EActionToSceneGUI.Add(() =>
                    {
                        EGlobalSettings.SetExtendedWindowMinimized(true);
                    }, EActionToSceneGUI.Type.LATE, EventType.Repaint);
                    EHandleSceneView.RepaintCurrent();
                });
            }
            GUILayout.EndHorizontal();

            EUiUtility.CreateHorizontalBlackLine();
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundToolbar);
            //Unlink
            if (segment.linkTarget != LinkTarget.NONE)
            {
                EUiUtility.CreateButton(ButtonType.DEFAULT_ACTIVE, LibraryGUIContent.iconUnlink, 35, 19, (Action)(() =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (Action<Segment>)((s) =>
                    {
                        s.linkTarget = LinkTarget.NONE;

                        if (s.LinkCount == 1 || s.LinkCount == 2)
                        {
                            for (int i = 0; i < s.LinkCount; i++)
                            {
                                Segment link = s.GetLinkAtIndex(i);
                                EHandleUndo.RecordNow(link.splineParent, "Unlinked anchor");
                                link.linkTarget = LinkTarget.NONE;
                            }
                        }

                        if (s.SplineConnector != null)
                            s.SplineConnector.RemoveConnection(s);

                        s.Unlink();
                    }), "Unlinked anchor");

                    EHandleTool.ActivatePositionToolForControlPoint(spline);
                    EHandleSceneView.RepaintCurrent();
                }));
            }
            //Link
            else
            {
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconLink, 35, 19, () =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (s) =>
                    {
                        //Get anchor point
                        Vector3 anchorPoint = s.GetPosition(ControlHandle.ANCHOR);

                        //Closest segment
                        Segment closestSegment = EHandleSpline.GetClosestSegment(HandleRegistry.GetSplinesUnsafe(), anchorPoint, out _, out _, s);
                        Vector3 linkPointAnchor = closestSegment.GetPosition(ControlHandle.ANCHOR);
                        float anchorDistance = Vector3.Distance(anchorPoint, linkPointAnchor);

                        //Closest connector
                        SplineConnector closestConnector = SplineConnectorUtility.GetClosest(anchorPoint, HandleRegistry.GetSplineConnectorsUnsafe());
                        Vector3 linkPointConnector = new Vector3(99999, 99999, 99999);
                        if (closestConnector != null) linkPointConnector = closestConnector.transform.position;
                        float connectorDistance = Vector3.Distance(anchorPoint, linkPointConnector) - 0.01f;

                        if (connectorDistance > anchorDistance)
                        {
                            s.SetAnchorPosition(linkPointAnchor);
                            s.linkTarget = LinkTarget.ANCHOR;

                            if (spline.Loop && spline.segments[0] == s)
                                spline.segments[spline.segments.Count - 1].SetPosition(ControlHandle.ANCHOR, linkPointAnchor);

                            SplineUtility.GetSegmentsAtPointNoAlloc(segmentContainer, HandleRegistry.GetSplinesUnsafe(), linkPointAnchor);
                            foreach (Segment s2 in segmentContainer)
                            {
                                EHandleUndo.RecordNow(s2.splineParent, "Linked anchor");
                                s2.linkTarget = LinkTarget.ANCHOR;
                            }
                        }
                        else
                        {
                            closestConnector.AlignSegment(segment);
                            s.linkTarget = LinkTarget.SPLINE_CONNECTOR;

                            if (spline.Loop && spline.segments[0] == s)
                                closestConnector.AlignSegment(spline.segments[spline.segments.Count - 1]);
                        }

                        EHandleEvents.InvokeSegmentLinked(segment);
                    }, "Linked anchor");

                    EHandleTool.ActivatePositionToolForControlPoint(spline);
                    EHandleSceneView.RepaintCurrent();
                });
            }

            EUiUtility.CreateSeparator();

            //Align tangents
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconAlignTangents, 35, 19, (Action)(() =>
            {
                EHandleSelection.UpdateSelectedAnchors(spline, (Action<Segment>)((seg) =>
                {
                    if (seg.LinkCount == 0)
                        return;

                    segmentContainer.Clear();
                    segmentContainer.Add(seg);

                    for (int i = 0; i < seg.LinkCount; i++)
                    {
                        Segment seg2 = seg.GetLinkAtIndex(i);

                        EHandleUndo.RecordNow(seg2.splineParent, "Aligned tangents");

                        if (seg2 == seg)
                            continue;

                        segmentContainer.Add(seg2);
                    }

                    SplineUtility.AlignTangents(segmentContainer);
                }));

                EHandleSceneView.RepaintCurrent();
            }), segment.linkTarget == LinkTarget.ANCHOR);

            //Flatten
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconFlatten, 35, 19, () =>
            {
                EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                {
                    EHandleSpline.FlattenControlPoints(spline, selected);
                }, "Flatten control points");

                EHandleTool.ActivatePositionToolForControlPoint(spline);
                EHandleSceneView.RepaintCurrent();
                EHandleEvents.InvokeSegmentFlatten(segment);
            });

            //Split
            int selectedSegmentId = SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint);
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconSplit, 35, 19, () =>
            {
                Spline newSpline = EHandleSpline.Split(spline, selectedSegmentId);
                EHandleEvents.InvokeSplineSplit(spline, newSpline);
                EHandleSceneView.RepaintCurrent();
            }, !spline.Loop && selectedSegmentId > 0 && selectedSegmentId < (spline.segments.Count - 1));

            //Prev control point
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconPrevControlPoint, 35, 19, () =>
            {
                EHandleUndo.RecordNow(spline, "Prev control point");
                EHandleSelection.SelectPrimaryControlPoint(spline, EHandleSpline.GetNextControlPoint(spline, true));

                EHandleTool.ActivatePositionToolForControlPoint(spline);
                EHandleSceneView.RepaintCurrent();
            });

            //Next control point
            EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconNextControlPoint, 35, 19, () =>
            {
                EHandleUndo.RecordNow(spline, "Next control point");
                EHandleSelection.SelectPrimaryControlPoint(spline, EHandleSpline.GetNextControlPoint(spline));

                EHandleTool.ActivatePositionToolForControlPoint(spline);
                EHandleSceneView.RepaintCurrent();
            });
            GUILayout.EndHorizontal();

            if (EGlobalSettings.GetSubmenusOnTop()) DrawBottomControlPoint(spline);
            #endregion

            #region general
            if (spline.selectedAnchorMenu == "general")
            {
                EUiUtility.CreateSection("GENERAL");

                if (segment.SplineConnector != null && segment.linkTarget == LinkTarget.SPLINE_CONNECTOR)
                {
                    EUiUtility.CreateXYZInputFields("Position offset", segment.connectorPosOffset, (position, dif) =>
                    {
                        EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                        {
                            selected.connectorPosOffset = position;
                            EHandleSceneView.RepaintCurrent();
                        }, "Changed connector offset position: " + spline.selectedControlPoint);
                    }, 93, 10, 54);

                    EUiUtility.CreateXYZInputFields("Rotation offset", segment.connectorRotOffset.eulerAngles, (rotation, dif) =>
                    {
                        EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                        {
                            selected.connectorRotOffset = Quaternion.Euler(rotation);
                            EHandleSceneView.RepaintCurrent();
                        }, "Changed connector offset roation: " + spline.selectedControlPoint);
                    }, 93, 10, 54);
                }
                else
                {
                    EUiUtility.CreateXYZInputFields("Position", segment.GetPosition(handle), (position, dif) =>
                    {
                        EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                        {
                            if (handle == ControlHandle.TANGENT_A || handle == ControlHandle.TANGENT_B)
                            {
                                if (EGlobalSettings.GetHandleType() == ControlHandleType.CONTINUOUS)
                                {
                                    segment.SetContinuousPosition(handle, position);
                                }
                                else if (EGlobalSettings.GetHandleType() == ControlHandleType.MIRRORED)
                                {
                                    segment.SetMirroredPosition(handle, position);
                                }
                            }
                            else
                            {
                                selected.TranslateAnchor(dif);
                                EHandleSegment.LinkMovement(selected);
                            }

                        }, "Moved control handle: " + spline.selectedControlPoint);

                        EHandleTool.ActivatePositionToolForControlPoint(spline);
                        EHandleSceneView.RepaintCurrent();
                    }, 69, 10, 62, segment.SplineConnector != null && segment.linkTarget == LinkTarget.SPLINE_CONNECTOR);
                }

                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateLabelField($"Z position: {Mathf.Round(segment.zPosition * 100) / 100}", LibraryGUIStyle.textDefault, true, 153);
                EUiUtility.CreateLabelField($"Length: {Mathf.Round(segment.length * 100) / 100}", LibraryGUIStyle.textDefault, true);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());

                //Ignore snapping
                EUiUtility.CreateToggleField("Ignore snapping:", segment.IgnoreSnapping, (newValue) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.IgnoreSnapping = newValue;
                    }, "Updated ignore snapping");
                }, true, true, 102, 16);;;

                //Type
                GUILayout.FlexibleSpace();
                EUiUtility.CreatePopupField("Type:", 60, (int)segment.GetInterpolationType(), interpolationMode, (int newValue) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.SetInterpolationType((InterpolationType)newValue);
                        selected.UpdateLineOrientation();
                    }, "Updated interpolation type");

                    EHandleUndo.RecordNow(spline);
                    spline.selectedControlPoint = SplineUtility.SegmentIndexToControlPointId(segmentIndex, ControlHandle.ANCHOR);
                    EHandleSelection.ForceUpdate();
                    EHandleTool.ActivatePositionToolForControlPoint(spline);
                }, 40, true);
                GUILayout.EndHorizontal();

                if(segment.LinkCount > 0)
                {
                    EUiUtility.CreateSectionMinimized("LINKS", segment.linksMinimized, () =>
                    {
                        EHandleUndo.RecordNow(spline, "Toggle sub section links");
                        segment.linksMinimized = !segment.linksMinimized;
                    });

                    if(!segment.linksMinimized)
                    {
                        for (int i = 0; i < segment.LinkCount; i++)
                        {
                            Segment link = segment.GetLinkAtIndex(i);
                            Spline splineParent = link.splineParent;

                            if (splineParent == null)
                                continue;

                            string color = "#FFFFFF";
                            if (link.ignoreLink) color = "#4688FF";
                            GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                            if(splineParent.name.Length > 15) EUiUtility.CreateLabelField($"<color={color}>{splineParent.name.Substring(0, 15)}..</color>", LibraryGUIStyle.textDefault, true, 150);
                            else EUiUtility.CreateLabelField($"<color={color}>{splineParent.name}</color>", LibraryGUIStyle.textDefault, true, 150);
                            EUiUtility.CreateLabelField($"<color={color}>anchor {link.IndexInSpline}</color>", LibraryGUIStyle.textDefault, true, 60);
                            if (link == segment) EUiUtility.CreateLabelField($"<color={color}>self</color>", LibraryGUIStyle.textDefault, true, 38);
                            else GUILayout.Space(38);

                            EUiUtility.CreateToggleField("", !link.ignoreLink, (newValue) => 
                            { 
                                EHandleUndo.RecordNow(splineParent, "Toggled ignore link");
                                link.ignoreLink = !newValue;
                            }, true, true);

                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
            #endregion

            #region deformation
            else if (spline.selectedAnchorMenu == "deformation")
            {
                EUiUtility.CreateSection("DEFORMATION");

                //Scale X
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Scale X: ", segment.Scale.x, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.Scale = new Vector2(newValue, selected.Scale.y);
                    }, "Changed scale x");
                }, 0, 10, 90, 50, 0, true, true, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => 
                    {
                        selected.Scale = new Vector2(Segment.defaultScale, selected.Scale.y);
                    }, "Assigned default value");
                }, segment.Scale.x != Segment.defaultScale);
                GUILayout.EndHorizontal();

                //Scale Y
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Scale Y: ", segment.Scale.y, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.Scale = new Vector2(selected.Scale.x, newValue);
                    }, "Changed scale y");
                }, 0, 10, 90, 50, 0, true, true, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => 
                    { 
                        selected.Scale = new Vector2(selected.Scale.x, Segment.defaultScale); 
                    }, "Assigned default value");
                }, segment.Scale.y != Segment.defaultScale);
                GUILayout.EndHorizontal();

                //Z rotation
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Rotation: ", segment.ZRotation, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.ZRotation = newValue;
                    }, "Changed rotation");

                }, -180, 180, 90, 50, 0, true, segment.linkTarget != LinkTarget.SPLINE_CONNECTOR, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => { selected.ZRotation = Segment.defaultZRotation; }, "Assigned default value");
                }, segment.ZRotation != Segment.defaultZRotation && segment.linkTarget != LinkTarget.SPLINE_CONNECTOR);
                GUILayout.EndHorizontal();

                if (spline.Loop && spline.SplineType == SplineType.DYNAMIC && spline.selectedControlPoint == 1000)
                {
                    //Z rotation loop alignment
                    GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                    EUiUtility.CreateSliderAndInputField("Rotation alignment: ", spline.segments[spline.segments.Count - 1].ZRotation, (newValue, changeBySlider) =>
                    {
                        EHandleUndo.RecordNow(spline, "Changed rotation alignment");
                        spline.segments[spline.segments.Count - 1].ZRotation = newValue;
                    }, -180, 180, 90, 50, 0, true, true, true);
                    //Default
                    EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                    {
                        GUI.FocusControl(null);
                        EHandleUndo.RecordNow(spline, "Assigned default value");
                        spline.segments[spline.segments.Count - 1].ZRotation = Segment.defaultZRotation;
                    }, spline.segments[spline.segments.Count - 1].ZRotation != Segment.defaultZRotation);
                    GUILayout.EndHorizontal();
                }

                //Saddle skew X
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Saddle skew X: ", segment.SaddleSkew.x, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.SaddleSkew = new Vector2(newValue, selected.SaddleSkew.y);
                    }, "Changed saddle skew x");
                }, -5, 5, 90, 50, 0, true, true, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => 
                    {
                        selected.SaddleSkew = new Vector2(Segment.defaultSaddleSkewX, selected.SaddleSkew.y);
                    }, "Assigned default value");
                }, segment.SaddleSkew.x != Segment.defaultSaddleSkewX);
                GUILayout.EndHorizontal();

                //Saddle skew Y
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Saddle skew Y: ", segment.SaddleSkew.y, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.SaddleSkew = new Vector2(selected.SaddleSkew.x, newValue);
                    }, "Changed saddle skew y");
                }, -5, 5, 90, 50, 0, true, true, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => 
                    { 
                        selected.Scale = new Vector2(selected.Scale.x, Segment.defaultSaddleSkewY); 
                    }, "Assigned default value");
                }, segment.SaddleSkew.y != Segment.defaultSaddleSkewY);
                GUILayout.EndHorizontal();

                //NoiseLayer
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Noise: ", segment.Noise, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.Noise = newValue;
                    }, "Changed noise");
                }, 0, 1, 90, 50, 0, true, true, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => 
                    {
                        selected.Noise = Segment.defaultNoise; 
                    }, "Assigned default value");
                }, segment.Noise != Segment.defaultNoise);
                GUILayout.EndHorizontal();

                //Contrast
                GUILayout.BeginHorizontal(EUiUtility.GetBackgroundStyle());
                EUiUtility.CreateSliderAndInputField("Contrast: ", segment.Contrast, (newValue, changeBySlider) =>
                {
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) =>
                    {
                        selected.Contrast = newValue;
                    }, "Changed contrast");
                }, -20, 20, 90, 50, 0, true, true, true);
                //Default
                EUiUtility.CreateButton(ButtonType.DEFAULT, LibraryGUIContent.iconDefault, 20, 18, () =>
                {
                    GUI.FocusControl(null);
                    EHandleSelection.UpdateSelectedAnchorsRecordUndo(spline, (selected) => 
                    {
                        selected.Contrast = Segment.defaultContrast;
                    }, "Assigned default value");
                }, segment.Contrast != Segment.defaultContrast);
                GUILayout.EndHorizontal();
            }
            #endregion

            #region addons
            else
            {
                bool foundAddon = false;

                for (int i = 0; i < addonsDrawWindowCp.Count; i++)
                {
                    if (addonsDrawWindowCp[i].Item1 == spline.selectedAnchorMenu)
                    {
                        foundAddon = true;
                        addonsDrawWindowCp[i].Item2.Invoke(spline, segmentIndex);
                    }
                }

                if (foundAddon == false)
                    spline.selectedAnchorMenu = "general";
            }
            #endregion

            if (!EGlobalSettings.GetSubmenusOnTop()) DrawBottomControlPoint(spline);
            EHandleEvents.InvokeWindowControlPointGUI(Event.current, leftMouseUp);
        }

        private void DrawBottomSplineObject(SplineObject so)
        {
            EUiUtility.CreateHorizontalBlackLine();

            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundBottomMenu);

            //General
            EUiUtility.CreateButtonToggle(ButtonType.SUB_MENU, LibraryGUIContent.iconGeneral, LibraryGUIContent.iconGeneralActive, 35, 18, () =>
            {
                EHandleUndo.RecordNow(so, "Changed objectmenu");
                so.selectedMenu = "general";
            }, so.selectedMenu == "general");

            //Addons
            for (int i = 0; i < addonsButtonsSo.Count; i++)
            {
                EUiUtility.CreateButtonToggle(ButtonType.SUB_MENU, addonsButtonsSo[i].Item2, addonsButtonsSo[i].Item3, 35, 18, () =>
                {
                    EHandleUndo.RecordNow(so, "Changed submenu");
                    so.selectedMenu = addonsButtonsSo[i].Item1;
                }, so.selectedMenu == addonsButtonsSo[i].Item1);
            }

            //Info
            EUiUtility.CreateButtonToggle(ButtonType.SUB_MENU, LibraryGUIContent.iconInfo, LibraryGUIContent.iconInfoActive, 35, 18, () =>
            {
                EHandleUndo.RecordNow(so, "Changed submenu");
                so.selectedMenu = "info";
            }, so.selectedMenu == "info");

            GUILayout.EndHorizontal();
        }

        private void DrawBottomControlPoint(Spline spline)
        {
            //Bottom
            EUiUtility.CreateHorizontalBlackLine();
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundBottomMenu);

            //General
            EUiUtility.CreateButtonToggle(ButtonType.SUB_MENU, LibraryGUIContent.iconGeneral, LibraryGUIContent.iconGeneralActive, 35, 18, () =>
            {
                EHandleUndo.RecordNow(spline, "Changed anchor menu");
                spline.selectedAnchorMenu = "general";
                GUI.FocusControl(null);
            }, spline.selectedAnchorMenu == "general");

            //Deformation
            EUiUtility.CreateButtonToggle(ButtonType.SUB_MENU, LibraryGUIContent.iconCurve, LibraryGUIContent.iconCurveActive, 35, 18, () =>
            {
                EHandleUndo.RecordNow(spline, "Changed anchor menu");
                spline.selectedAnchorMenu = "deformation";
                GUI.FocusControl(null);
            }, spline.selectedAnchorMenu == "deformation");

            //Addon buttons
            for (int i = 0; i < addonsButtonsCp.Count; i++)
            {
                EUiUtility.CreateButtonToggle(ButtonType.SUB_MENU, addonsButtonsCp[i].Item2, addonsButtonsCp[i].Item3, 35, 18, () =>
                {
                    EHandleUndo.RecordNow(spline, "Changed sub menu");
                    spline.selectedAnchorMenu = addonsButtonsCp[i].Item1;
                    GUI.FocusControl(null);
                }, spline.selectedAnchorMenu == addonsButtonsCp[i].Item1);
            }

            GUILayout.EndHorizontal();
        }

        protected override void UpdateWindowSize()
        {
            Spline spline = EHandleSelection.selectedSpline;
            SplineObject so = EHandleSelection.selectedSplineObject;

            if (EGlobalSettings.GetExtendedWindowMinimized())
            {
                cachedRect.width = 27;
                cachedRect.height = 27;
            }
            else if(so != null)
            {
                if (so.selectedMenu == "general")
                {
                    cachedRect.height = headerHeight + toolbarHeight + bottomHeight;
                    cachedRect.height += sectionHeight;

                    if (so.Type == SplineObjectType.NONE)
                        cachedRect.height += itemHeight * 4;
                    else if (so.Type == SplineObjectType.DEFORMATION)
                    {
                        cachedRect.height += itemHeight * 5;

                        if(so.SnapSettings.snapMode != SnapMode.NONE)
                        {
                            cachedRect.height += 14;

                            if(!so.snapSettingsMinimized)
                            {
                                cachedRect.height += itemHeight * 2;
                            }
                        }
                    }
                    else if (so.Type == SplineObjectType.FOLLOWER)
                    {
                        cachedRect.height += itemHeight * 5;
                    }

                    cachedRect.width = 292;
                }
                else if (so.selectedMenu == "info")
                {
                    cachedRect.height = headerHeight + toolbarHeight + bottomHeight;
                    cachedRect.height += sectionHeight;
                    cachedRect.height += itemHeight * 2;
                    cachedRect.width = 195;
                }
                //Addons
                else
                {
                    for (int i = 0; i < addonsCalcWindowSizeSo.Count; i++)
                    {
                        if (so.selectedMenu == addonsCalcWindowSizeSo[i].Item1)
                        {
                            Rect rect = addonsCalcWindowSizeSo[i].Item2.Invoke(so);
                            cachedRect.height = rect.height;
                            cachedRect.width = rect.width;
                            break;
                        }
                    }
                }

                //Expand window for title.
                guiContentContainer.text = windowTitle;
                Vector2 labelSize = LibraryGUIStyle.textHeader.CalcSize(guiContentContainer);
                labelSize.x += LibraryGUIStyle.textHeader.padding.left + LibraryGUIStyle.textHeader.padding.right + 70;
                if (labelSize.x > cachedRect.width) cachedRect.width = labelSize.x;
            }
            else if(spline != null && spline.selectedControlPoint != 0)
            {
                Segment segment = spline.segments[SplineUtility.ControlPointIdToSegmentIndex(spline.selectedControlPoint)];

                if (spline.selectedAnchorMenu == "general")
                {
                    cachedRect.width = 270;
                    cachedRect.height = headerHeight + toolbarHeight + bottomHeight;
                    cachedRect.height += sectionHeight;
                    cachedRect.height += itemHeight * 3;

                    if (segment.SplineConnector != null && segment.linkTarget == LinkTarget.SPLINE_CONNECTOR)
                        cachedRect.height += itemHeight * 1;

                    if(segment.LinkCount > 0)
                    {
                        cachedRect.height += sectionHeight;
                        if(!segment.linksMinimized) cachedRect.height += itemHeight * segment.LinkCount;
                    }

                }
                else if (spline.selectedAnchorMenu == "deformation")
                {
                    cachedRect.height = headerHeight + toolbarHeight + bottomHeight;
                    cachedRect.height += sectionHeight;
                    cachedRect.height += itemHeight * 7;

                    if (spline.Loop && spline.SplineType == SplineType.DYNAMIC && spline.selectedControlPoint == 1000)
                        cachedRect.height += itemHeight;

                    cachedRect.width = 272;
                }
                else
                {
                    //Addons
                    for (int i = 0; i < addonsCalcWindowSizeCp.Count; i++)
                    {
                        if (spline.selectedAnchorMenu == addonsCalcWindowSizeCp[i].Item1)
                        {
                            Rect rect = addonsCalcWindowSizeCp[i].Item2.Invoke(spline);
                            cachedRect.height = rect.height;
                            cachedRect.width = rect.width;
                            break;
                        }
                    }
                }
            }
        }

        public static void AddSubMenuControlPoint(string id, GUIContent button, GUIContent buttonActive, Action<Spline, int> DrawWindow, Func<Spline, Rect> calcWindowSize)
        {
            for (int i = 0; i < addonsDrawWindowCp.Count; i++)
            {
                if (addonsDrawWindowCp[i].Item1 == id)
                {
                    addonsDrawWindowCp.RemoveAt(i);
                    addonsButtonsCp.RemoveAt(i);
                    addonsCalcWindowSizeCp.RemoveAt(i);
                }
            }

            addonsDrawWindowCp.Add((id, DrawWindow));
            addonsButtonsCp.Add((id, button, buttonActive));
            addonsCalcWindowSizeCp.Add((id, calcWindowSize));
        }

        public static void AddSubMenuSplineObject(string id, GUIContent button, GUIContent buttonActive, Action<SplineObject> DrawWindow, Func<SplineObject, Rect> calcWindowSize)
        {
            for (int i = 0; i < addonsDrawWindowSo.Count; i++)
            {
                if (addonsDrawWindowSo[i].Item1 == id)
                {
                    addonsDrawWindowSo.RemoveAt(i);
                    addonsButtonsSo.RemoveAt(i);
                    addonsCalcWindowSizeSo.RemoveAt(i);
                }
            }

            addonsDrawWindowSo.Add((id, DrawWindow));
            addonsButtonsSo.Add((id, button, buttonActive));
            addonsCalcWindowSizeSo.Add((id, calcWindowSize));
        }
    }
}
