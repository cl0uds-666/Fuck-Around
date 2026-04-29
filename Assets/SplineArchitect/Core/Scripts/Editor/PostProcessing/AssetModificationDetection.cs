// -----------------------------------------------------------------------------
// SplineArchitect
// Filename: AssetModificationDetection.cs
//
// Author: Mikael Danielsson
// Date Created: 23-03-2023
// (C) 2023 Mikael Danielsson. All rights reserved.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SplineArchitect.PostProcessing
{
    public class AssetModificationDetection : AssetPostprocessor
    {
        private static bool assembliesReloaded = true;
        private static HashSet<Object> objects = new HashSet<Object>();
        private static string[] modelFileTypes = { ".fbx", ".dae", ".dxf", ".obj" };

        public static void UpdateGlobal()
        {
            assembliesReloaded = false;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (assembliesReloaded)
            {
                assembliesReloaded = false;
                return;
            }

            objects.Clear();
            foreach (string assetPath in importedAssets)
            {
                Object obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (objects.Contains(obj)) continue;

                foreach(string fileType in modelFileTypes)
                {
                    if (assetPath.EndsWith(fileType, StringComparison.OrdinalIgnoreCase))
                    {
                        EHandleMeshContainer.Refresh();
                        break;
                    }
                }

                EHandleEvents.InvokeAssetImported(obj);
                objects.Add(obj);
            }
        }
    }
}
