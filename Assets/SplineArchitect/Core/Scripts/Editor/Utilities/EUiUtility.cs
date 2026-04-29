// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: EUiUtility.cs
//
// Author: Mikael Danielsson
// Date Created: 16-02-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;

using UnityEditor;
using UnityEngine;

using SplineArchitect.CustomTools;
using SplineArchitect.Libraries;
using SplineArchitect.Ui;

namespace SplineArchitect.Utility
{
    public class EUiUtility
    {
        private static int backgroundStyleCounter = 0;

        public static void CreateSection(string title)
        {
            GUILayout.BeginVertical();
            CreateHorizontalBlackLine();
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundSection);
            CreateLabelField($"<b>{title}</b>", LibraryGUIStyle.textSection, true);
            GUILayout.EndHorizontal();
            CreateHorizontalBlackLine();
            GUILayout.EndVertical();
        }

        public static void CreateSectionMinimized(string title, bool minimized, Action actionOnPress)
        {
            CreateHorizontalBlackLine();
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundSection);
            CreateLabelField($"<b>{title}</b>", LibraryGUIStyle.textSection, true);
            GUILayout.Space(100);
            CreateButton(ButtonType.SUB_MENU2, minimized ?
                                               LibraryGUIContent.iconMaximizeBlack :
                                               LibraryGUIContent.iconMinimizeBlack, 20, 14, () =>
            {
                actionOnPress.Invoke();
            });
            GUILayout.EndHorizontal();
            CreateHorizontalBlackLine();
        }

        public static void CreateColorField(string label, 
                                            Color color, 
                                            Action<Color> onChange, 
                                            float width = -1, 
                                            float labelWidth = -1, 
                                            bool skipGroup = false)
        {
            Color oldColor = color;

            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());

            if (labelWidth == -1) GUILayout.Label(label, LibraryGUIStyle.textDefault);
            else GUILayout.Label(label, LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));

            if (width == -1) color = EditorGUILayout.ColorField(color);
            else color = EditorGUILayout.ColorField(color, GUILayout.Width(width));

            if (!skipGroup) GUILayout.EndHorizontal();

            if (!GeneralUtility.IsEqual(oldColor, color))
            {
                onChange.Invoke(color);
            }
        }

        public static void CreateCheckbox(bool value, Action<bool> onChange, float width = 0)
        {
            bool oldValue = value;
            if(width == 0)
                value = EditorGUILayout.Toggle(value);
            else
                value = EditorGUILayout.Toggle(value, GUILayout.Width(width));

            if (oldValue != value)
                onChange.Invoke(value);
        }

        public static void CreateButtonToggle(ButtonType buttonType, 
                                              GUIContent icon, 
                                              GUIContent iconActive, 
                                              float width, 
                                              float height, 
                                              Action onPress, 
                                              bool active, 
                                              bool enable = true)
        {
            GUIStyle buttonStyle = null;
            GUIStyle buttonActiveStyle = null;

            if(buttonType == ButtonType.SUB_MENU)
            {
                buttonStyle = LibraryGUIStyle.buttonSubMenu;
                buttonActiveStyle = LibraryGUIStyle.buttonSubMenuActive;
            }
            else if (buttonType == ButtonType.DEFAULT_GREEN)
            {
                buttonStyle = LibraryGUIStyle.buttonDefaultGreen;
                buttonActiveStyle = LibraryGUIStyle.buttonDefaultActive;
            }
            else if (buttonType == ButtonType.DEFAULT_RED)
            {
                buttonStyle = LibraryGUIStyle.buttonDefaultRed;
                buttonActiveStyle = LibraryGUIStyle.buttonDefaultActive;
            }
            else if(buttonType == ButtonType.DEFAULT_MIDDLE_LEFT)
            {
                buttonStyle = LibraryGUIStyle.buttonDefaultMiddleLeft;
                buttonActiveStyle = LibraryGUIStyle.buttonDefaultMiddleLeftActive;
            }
            else
            {
                buttonStyle = LibraryGUIStyle.buttonDefault;
                buttonActiveStyle = LibraryGUIStyle.buttonDefaultActive;
            }

            GUI.enabled = enable;
            if (GUILayout.Button(active ? iconActive : icon, active ? buttonActiveStyle : buttonStyle, GUILayout.Width(width), GUILayout.Height(height)))
                onPress.Invoke();
            GUI.enabled = true;
        }

        public static void CreateButton(ButtonType buttonType, 
                                        GUIContent icon, 
                                        float width, 
                                        float height, 
                                        Action onPress, 
                                        bool enable = true)
        {
            GUIStyle buttonStyle = null;

            if (buttonType == ButtonType.SUB_MENU)
                buttonStyle = LibraryGUIStyle.buttonSubMenu;
            else if (buttonType == ButtonType.DEFAULT_RED)
                buttonStyle = LibraryGUIStyle.buttonDefaultRed;
            else if (buttonType == ButtonType.DEFAULT_WHITE)
                buttonStyle = LibraryGUIStyle.buttonDefaultWhite;
            else if (buttonType == ButtonType.SUB_MENU2)
                buttonStyle = LibraryGUIStyle.buttonSubMenu2;
            else if (buttonType == ButtonType.DEFAULT_GREEN)
                buttonStyle = LibraryGUIStyle.buttonDefaultGreen;
            else if (buttonType == ButtonType.DEFAULT_MIDDLE_LEFT)
                buttonStyle = LibraryGUIStyle.buttonDefaultMiddleLeft;
            else if (buttonType == ButtonType.DEFAULT_ACTIVE)
                buttonStyle = LibraryGUIStyle.buttonDefaultActive;
            else
                buttonStyle = LibraryGUIStyle.buttonDefault;

            GUI.enabled = enable;
            if (GUILayout.Button(icon, buttonStyle, GUILayout.Width(width), GUILayout.Height(height)))
            {
                onPress.Invoke();
            }
            GUI.enabled = true;
        }

        public static void CreateObjectField(string label, 
                                             UnityEngine.Object obj, 
                                             Type typeOf, 
                                             Action<UnityEngine.Object> actionOnChange, 
                                             float width = 0, 
                                             float labelWidth = -1, 
                                             bool skipGroup = false, 
                                             bool blackText = false)
        {
            if(!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            if (labelWidth == -1)
                GUILayout.Label(label, blackText ? LibraryGUIStyle.textDefaultBlack : LibraryGUIStyle.textDefault);
            else
                GUILayout.Label(label, blackText ? LibraryGUIStyle.textDefaultBlack : LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));
            GUI.SetNextControlName(label);
            UnityEngine.Object newO;

            if (width == 0)
                newO = EditorGUILayout.ObjectField(obj, typeOf, true);
            else
                newO = EditorGUILayout.ObjectField(obj, typeOf, true, GUILayout.Width(width));

            if (!skipGroup) GUILayout.EndHorizontal();

            if (obj != newO) actionOnChange.Invoke(newO);

            if (GUI.GetNameOfFocusedControl() == label && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Delete)
                Event.current.Use();
        }

        public static void CreateXYZInputFields(string label, 
                                                Vector3 currentValue, 
                                                Action<Vector3, Vector3> actionOnValueChange, 
                                                float labelWidth, 
                                                float xyzLabelWidth, 
                                                float inputFieldWidth, 
                                                bool disable = false, 
                                                bool skipGroup = false,
                                                bool delayed = false)
        {
            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUILayout.Label($"<b>{label}</b>", LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));
            Vector3 oldValue = currentValue;

            currentValue.x = Mathf.Round(currentValue.x * 100) / 100;
            currentValue.y = Mathf.Round(currentValue.y * 100) / 100;
            currentValue.z = Mathf.Round(currentValue.z * 100) / 100;

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            Color oldNormal = EditorStyles.label.normal.textColor;
            Color oldHover = EditorStyles.label.hover.textColor;
            Color oldFocused = EditorStyles.label.focused.textColor;
            EditorGUIUtility.labelWidth = xyzLabelWidth;
            EditorStyles.label.normal.textColor = Color.white;
            EditorStyles.label.hover.textColor = Color.white;
            EditorStyles.label.focused.textColor = Color.white;
            GUI.enabled = !disable;
            GUI.SetNextControlName("field_x");
            if(delayed) currentValue.x = EditorGUILayout.DelayedFloatField("X", currentValue.x, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(inputFieldWidth));
            else currentValue.x = EditorGUILayout.FloatField("X", currentValue.x, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(inputFieldWidth));
            GUILayout.Space(2);
            GUI.SetNextControlName("field_y");
            if (delayed) currentValue.y = EditorGUILayout.DelayedFloatField("Y", currentValue.y, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(inputFieldWidth));
            else currentValue.y = EditorGUILayout.FloatField("Y", currentValue.y, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(inputFieldWidth));
            GUILayout.Space(2);
            GUI.SetNextControlName("field_z");
            if (delayed) currentValue.z = EditorGUILayout.DelayedFloatField("Z", currentValue.z, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(inputFieldWidth));
            else currentValue.z = EditorGUILayout.FloatField("Z", currentValue.z, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(inputFieldWidth));
            GUI.enabled = true;

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorStyles.label.normal.textColor = oldNormal;
            EditorStyles.label.hover.textColor = oldHover;
            EditorStyles.label.focused.textColor = oldFocused;

            if (!skipGroup) GUILayout.EndHorizontal();

            currentValue.x = Mathf.Round(currentValue.x * 100) / 100;
            currentValue.y = Mathf.Round(currentValue.y * 100) / 100;
            currentValue.z = Mathf.Round(currentValue.z * 100) / 100;

            oldValue.x = Mathf.Round(oldValue.x * 100) / 100;
            oldValue.y = Mathf.Round(oldValue.y * 100) / 100;
            oldValue.z = Mathf.Round(oldValue.z * 100) / 100;

            if (PositionTool.activePart == PositionTool.ActivePart.NONE && (GUI.GetNameOfFocusedControl() == "field_x" ||
                                                                      GUI.GetNameOfFocusedControl() == "field_y" ||
                                                                      GUI.GetNameOfFocusedControl() == "field_z"))
            {
                if (!GeneralUtility.IsEqual(oldValue, currentValue))
                {
                    actionOnValueChange.Invoke(currentValue, oldValue - currentValue);
                }
            }
        }

        public static void CreateFromToInputField(string label, 
                                                  float currentValueFrom, 
                                                  float currentValueTo, 
                                                  Action<float, float> actionOnValueChange, 
                                                  float width = 58, 
                                                  float paddingLeft = 6, 
                                                  float labelWidth = 58, 
                                                  bool skipGroup = false, 
                                                  bool verySmall = false)
        {
            if(!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUILayout.Space(paddingLeft);
            GUILayout.Label(label, LibraryGUIStyle.textNoWdith, GUILayout.Width(labelWidth));
            float newValueFrom = EditorGUILayout.FloatField(currentValueFrom, verySmall ? LibraryGUIStyle.textFieldVerySmall : LibraryGUIStyle.textFieldSmall, GUILayout.Width(width));
            GUILayout.Label("-", LibraryGUIStyle.textNoWdith, GUILayout.Width(6));
            float newValueTo = EditorGUILayout.FloatField(currentValueTo, verySmall ? LibraryGUIStyle.textFieldVerySmall : LibraryGUIStyle.textFieldSmall, GUILayout.Width(width));
            if (!skipGroup) GUILayout.EndHorizontal();

            if (GeneralUtility.IsEqual(newValueFrom, currentValueFrom) && GeneralUtility.IsEqual(newValueTo, currentValueTo))
                return;
            
            actionOnValueChange.Invoke(newValueFrom, newValueTo);
        }

        public static void CreateSliderAndInputField(string label, 
                                                     float currentValue, 
                                                     Action<float, bool> actionOnValueChange, 
                                                     float sliderLeftValue, 
                                                     float sliderRightValue, 
                                                     float sliderWidth, 
                                                     float textFieldWidth, 
                                                     float labelWidth = 0, 
                                                     bool skipGroup = false, 
                                                     bool enable = true,
                                                     bool delayed = false)
        {
            bool changeBySlider = false;

            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUI.enabled = enable;
            if (labelWidth == 0)
                GUILayout.Label(label, LibraryGUIStyle.textDefault);
            else
                GUILayout.Label(label, LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));
            float oldValue = currentValue;
            float newValue = GUILayout.HorizontalSlider(currentValue, sliderLeftValue, sliderRightValue, GUILayout.Width(sliderWidth));

            //If slider changed value, stop focusing field
            if (!GeneralUtility.IsEqual(oldValue, newValue))
            {
                changeBySlider = true;
                GUI.FocusControl(null);
            }

            if(delayed) newValue = EditorGUILayout.DelayedFloatField(newValue, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(textFieldWidth));
            else newValue = EditorGUILayout.FloatField(newValue, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(textFieldWidth));
            GUI.enabled = true;
            if (!skipGroup) GUILayout.EndHorizontal();

            newValue = Mathf.Round(newValue * 100) / 100;
            if (GeneralUtility.IsEqual(newValue, oldValue))
                return;

            actionOnValueChange.Invoke(newValue, changeBySlider);
        }

        public static void CreateLabelField(string label, GUIStyle guiStyle, bool skipGroup = false, float labelWidth = -1)
        {
            if(!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            if(labelWidth == -1) GUILayout.Label(label, guiStyle);
            else GUILayout.Label(label, guiStyle, GUILayout.Width(labelWidth));
            if (!skipGroup) GUILayout.EndHorizontal();
        }

        public static void CreateToggleField(string label, 
                                             bool currentValue, 
                                             Action<bool> actionOnValueChange, 
                                             bool enable = true, 
                                             bool skipGroup = false, 
                                             float labelWidth = 0, 
                                             float width = 0)
        {
            bool oldValue = currentValue;
            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUI.enabled = enable;
            if(label.Length > 0)
            {
                if (labelWidth == 0) GUILayout.Label(label, LibraryGUIStyle.textDefault);
                else GUILayout.Label(label, LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));
            }
            if(width == 0) currentValue = EditorGUILayout.Toggle(currentValue);
            else currentValue = EditorGUILayout.Toggle(currentValue, GUILayout.Width(width));

            GUI.enabled = true;
            if (!skipGroup) GUILayout.EndHorizontal();

            if (currentValue == oldValue)
                return;

            actionOnValueChange.Invoke(currentValue);
        }

        public static void CreateToggleXYZField(string label, 
                                                Vector3Int currentValue, 
                                                Action<Vector3Int> actionOnValueChange, 
                                                float paddingLeft = 6, 
                                                bool skipGroup = false)
        {
            Vector3Int oldValue = currentValue;

            //Follow axels
            if(!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUILayout.Space(paddingLeft);
            GUILayout.Label(label, LibraryGUIStyle.textNoWdith);
            GUILayout.Label("X", LibraryGUIStyle.specificX);
            currentValue.x = EditorGUILayout.Toggle(currentValue.x != 0) ? 1 : 0;

            GUILayout.Label("Y", LibraryGUIStyle.specificYZ);
            currentValue.y = EditorGUILayout.Toggle(currentValue.y != 0) ? 1 : 0;

            GUILayout.Label("Z", LibraryGUIStyle.specificYZ);
            currentValue.z = EditorGUILayout.Toggle(currentValue.z != 0) ? 1 : 0;
            if (!skipGroup) GUILayout.EndHorizontal();

            if (currentValue == oldValue)
                return;

            actionOnValueChange.Invoke(currentValue);
        }

        public static void CreateFloatFieldWithLabel(string label, 
                                                     float currentValue, 
                                                     Action<float> actionOnValueChange, 
                                                     float width = -1, 
                                                     float labelWidth = -1, 
                                                     bool skipGroup = false, 
                                                     bool enable = true)
        {
            float newValue;

            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUI.enabled = enable;
            if (labelWidth == -1) EditorGUILayout.LabelField(label, LibraryGUIStyle.textDefault);
            else EditorGUILayout.LabelField(label, LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));

            if (width == -1) newValue = EditorGUILayout.FloatField(currentValue, LibraryGUIStyle.textFieldNoWidth);
            else newValue = EditorGUILayout.FloatField(currentValue, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(width));
            GUI.enabled = true;
            if (!skipGroup) GUILayout.EndHorizontal();

            if (GeneralUtility.IsEqual(newValue, currentValue))
                return;

            actionOnValueChange.Invoke(newValue);
        }

        public static void CreateDelayedFloatFieldWithLabel(string label,
                                                            float currentValue,
                                                            Action<float> actionOnValueChange,
                                                            float width = -1,
                                                            float labelWidth = -1,
                                                            bool skipGroup = false,
                                                            bool enable = true)
        {
            float newValue;

            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUI.enabled = enable;
            if (labelWidth == -1) EditorGUILayout.LabelField(label, LibraryGUIStyle.textDefault);
            else EditorGUILayout.LabelField(label, LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));

            if (width == -1) newValue = EditorGUILayout.DelayedFloatField(currentValue, LibraryGUIStyle.textFieldNoWidth);
            else newValue = EditorGUILayout.DelayedFloatField(currentValue, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(width));
            GUI.enabled = true;
            if (!skipGroup) GUILayout.EndHorizontal();

            if (GeneralUtility.IsEqual(newValue, currentValue))
                return;

            actionOnValueChange.Invoke(newValue);
        }

        public static void CreateFloatField(float currentValue, Action<float> actionOnValueChange, float width = -1, bool skipGroup = false)
        {
            float newValue;
            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            if (width == -1) newValue = EditorGUILayout.FloatField(currentValue, LibraryGUIStyle.textFieldNoWidth);
            else newValue = EditorGUILayout.FloatField(currentValue, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(width));
            if (!skipGroup) GUILayout.EndHorizontal();

            if (GeneralUtility.IsEqual(newValue, currentValue))
                return;

            actionOnValueChange.Invoke(newValue);
        }

        public static void CreateDelayedFloatField(float currentValue, Action<float> actionOnValueChange, float width = -1, bool skipGroup = false)
        {
            float newValue;
            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            if (width == -1) newValue = EditorGUILayout.DelayedFloatField(currentValue, LibraryGUIStyle.textFieldNoWidth);
            else newValue = EditorGUILayout.DelayedFloatField(currentValue, LibraryGUIStyle.textFieldNoWidth, GUILayout.Width(width));
            if (!skipGroup) GUILayout.EndHorizontal();

            if (GeneralUtility.IsEqual(newValue, currentValue))
                return;

            actionOnValueChange.Invoke(newValue);
        }

        public static void CreatePopupField(string label, 
                                            float width, 
                                            int currentType, 
                                            string[] options, 
                                            Action<int> actionOnValueChange, 
                                            float labelWidth = -1, 
                                            bool skipGroup = false, 
                                            bool enable = true, 
                                            bool skipLabel = false, 
                                            bool blackText = false)
        {
            int oldType = currentType;

            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            GUI.enabled = enable;
            if(!skipLabel)
            {
                if(labelWidth == -1)
                    GUILayout.Label(label, blackText ? LibraryGUIStyle.textDefaultBlack : LibraryGUIStyle.textDefault);
                else
                    GUILayout.Label(label, blackText ? LibraryGUIStyle.textDefaultBlack : LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));
            }
            currentType = EditorGUILayout.Popup(currentType, options, LibraryGUIStyle.popUpFieldSmallText, GUILayout.Width(width));
            GUI.enabled = true;
            if (!skipGroup) GUILayout.EndHorizontal();

            if (currentType == oldType)
                return;

            actionOnValueChange.Invoke(currentType);
        }

        public static void CreateMinMaxSlider(string label, 
                                              ref float minValue, 
                                              ref float maxValue, 
                                              float minLimit, 
                                              float maxLimit, 
                                              float labelWidth, 
                                              float sliderWidth = -1, 
                                              bool skipGroup = false)
        {
            if (!skipGroup) GUILayout.BeginHorizontal(GetBackgroundStyle());
            string startValue = Mathf.Round(minValue * 100).ToString();
            string endValue = Mathf.Round(maxValue * 100).ToString();
            GUILayout.Label($"{label} {startValue}", LibraryGUIStyle.textDefault, GUILayout.Width(labelWidth));
            if(sliderWidth == -1)
                EditorGUILayout.MinMaxSlider(ref minValue, ref maxValue, minLimit, maxLimit);
            else
                EditorGUILayout.MinMaxSlider(ref minValue, ref maxValue, minLimit, maxLimit, GUILayout.Width(sliderWidth));
            GUILayout.Label(endValue, LibraryGUIStyle.textDefault, GUILayout.Width(30));
            if (!skipGroup) GUILayout.EndHorizontal();
        }

        public static void CreateErrorWarningMessageIcon(GUIContent guiContent, bool moveBack = false)
        {
            GUILayout.Label(guiContent, LibraryGUIStyle.infoIcon);
            if (moveBack) GUILayout.Space(-6);
        }

        public static void CreateInfoMessageIcon(GUIContent guiContent, bool moveBack = false)
        {
            if(EGlobalSettings.GetInfoIconsVisibility())
            {
                GUILayout.Label(guiContent, LibraryGUIStyle.infoIcon);
                if(moveBack) GUILayout.Space(-6);
            }
        }

        public static void CreateHorizontalYellowLine()
        {
            GUILayout.Box("", LibraryGUIStyle.lineYellow, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalBlackLine()
        {
            GUILayout.Box("", LibraryGUIStyle.lineBlack, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalWhiteLine()
        {
            GUILayout.Box("", LibraryGUIStyle.lineWhite, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalGreyLine80()
        {
            GUILayout.Box("", LibraryGUIStyle.lineGrey80, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalGreyLine30()
        {
            GUILayout.Box("", LibraryGUIStyle.lineGrey30, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalGreyLine20()
        {
            GUILayout.Box("", LibraryGUIStyle.lineGrey20, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalGreyLine40()
        {
            GUILayout.Box("", LibraryGUIStyle.lineGrey40, GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        public static void CreateHorizontalSubHeader2Line()
        {
            GUILayout.BeginHorizontal(LibraryGUIStyle.backgroundSubSection);
            GUILayout.Space(10);
            GUILayout.Box("", LibraryGUIStyle.lineGrey30, GUILayout.Height(1), GUILayout.ExpandWidth(true));
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
        }

        public static void CreateVerticalYellowLine()
        {
            GUILayout.Box("", LibraryGUIStyle.lineYellow, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        }

        public static void CreateVerticalBlackLine()
        {
            GUILayout.Box("", LibraryGUIStyle.lineBlack, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        }

        public static void CreateVerticalWhiteLine()
        {
            GUILayout.Box("", LibraryGUIStyle.lineWhite, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        }

        public static void CreateVerticalGreyLine80()
        {
            GUILayout.Box("", LibraryGUIStyle.lineGrey80, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        }

        public static void CreateSeparator()
        {
            GUILayout.Box("", LibraryGUIStyle.separatorWhite);
        }

        public static void CreateSpaceWidth(float width)
        {
            GUILayout.Label(LibraryTexture.empty, GUILayout.Width(width));
        }

        public static GUIStyle GetBackgroundStyle(bool keepOld = false)
        {
            if(!keepOld)
                backgroundStyleCounter++;

            if (backgroundStyleCounter % 2 == 0)
                return LibraryGUIStyle.backgroundItem1;
            else
                return LibraryGUIStyle.backgroundItem2;
        }

        public static void ResetGetBackgroundStyleId()
        {
            backgroundStyleCounter = 1;
        }

        public static Vector2 GetWindowAnchorPosition(SceneView sceneView, Rect buttonWorldBound, bool isRow)
        {
            Rect win = sceneView.position;
            Rect wb = buttonWorldBound;
            if (isRow) return new Vector2(win.x + wb.x, win.y + wb.yMax + 5);
            else return new Vector2(win.x + wb.xMax + 5, win.y + wb.y);
        }
    }
}
