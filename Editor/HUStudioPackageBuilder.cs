using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HUStudio.HarmonyOS.Editor
{
    public static class HUStudioPackageBuilder
    {
        public static void BuildUnityPackage()
        {
            string output = Environment.GetEnvironmentVariable("HUSTUDIO_UNITYPACKAGE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("HUSTUDIO_UNITYPACKAGE_OUTPUT is required.");
            if (!AssetDatabase.IsValidFolder("Assets/HUStudioUnitySDK"))
                throw new DirectoryNotFoundException("Assets/HUStudioUnitySDK is missing in the packaging project.");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            AssetDatabase.ExportPackage("Assets/HUStudioUnitySDK", output, ExportPackageOptions.Recurse);
            if (!File.Exists(output) || new FileInfo(output).Length < 1000000)
                throw new IOException("Unity package was not created or is unexpectedly small: " + output);
            Debug.Log("[HUStudio] Unity package: " + output);
        }
    }
}
