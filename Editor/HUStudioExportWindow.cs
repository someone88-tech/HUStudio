using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HUStudio.HarmonyOS.Editor
{
    [Serializable]
    internal sealed class HUStudioExportSettings
    {
        private static HUStudioExportSettings loaded;
        private static string SettingsPath
        {
            get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "ProjectSettings", "HUStudioExportSettings.json"); }
        }
        public static HUStudioExportSettings instance
        {
            get
            {
                if (loaded != null) return loaded;
                string path = SettingsPath;
                loaded = File.Exists(path)
                    ? JsonUtility.FromJson<HUStudioExportSettings>(File.ReadAllText(path))
                    : new HUStudioExportSettings();
                if (loaded == null) loaded = new HUStudioExportSettings();
                if (string.IsNullOrEmpty(loaded.splashBackgroundColor))
                    loaded.splashBackgroundColor = "#FFFFFF";
                return loaded;
            }
        }
        public string outputDirectory = "";
        public string applicationName = "Unity for HarmonyOS";
        public string bundleName = "com.example.unityharmony";
        public string versionName = "1.0.0";
        public int versionCode = 1;
        public string iconGuid = "";
        public string splashGuid = "";
        public string splashBackgroundColor = "#FFFFFF";
        public string[] scenePaths = new string[0];
        public string lastWebGLDirectory = "";
        public string lastProjectDirectory = "";

        public void Persist()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(this, true), new UTF8Encoding(false));
        }
    }

    [Serializable]
    internal sealed class HUStudioWebGLManifest
    {
        public string unityVersion;
        public string inputFingerprint;
        public string[] scenes;
        public string builtUtc;
    }

    [Serializable]
    internal sealed class HUStudioProjectManifest
    {
        public string webglDirectory;
        public string applicationName;
        public string bundleName;
        public string versionName;
        public int versionCode;
        public string iconHash;
        public string splashHash;
        public string iconGuid;
        public string splashGuid;
        public string splashBackgroundColor;
    }

    public sealed class HUStudioExportWindow : EditorWindow
    {
        private const string PendingWebGLKey = "HUStudio.PendingWebGLBuild";
        private Vector2 scroll;
        private string status = "";
        private bool showAdvancedPaths;
        private bool showSigning;

        [MenuItem("HarmonyOS/环境与导出...")]
        public static void Open()
        {
            GetWindow<HUStudioExportWindow>("HarmonyOS 导出").minSize = new Vector2(560, 480);
        }

        [InitializeOnLoadMethod]
        private static void RegisterPendingBuild()
        {
            EditorApplication.update -= ResumePendingWebGLBuild;
            EditorApplication.update += ResumePendingWebGLBuild;
        }

        private static void ResumePendingWebGLBuild()
        {
            if (!SessionState.GetBool(PendingWebGLKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                SessionState.SetBool(PendingWebGLKey, false);
                Debug.LogError("[HUStudio] Failed to switch Unity to WebGL.");
                return;
            }
            SessionState.SetBool(PendingWebGLKey, false);
            try { HUStudioExportPipeline.BuildWebGL(HUStudioExportSettings.instance); }
            catch (Exception error) { Debug.LogException(error); }
            var window = Resources.FindObjectsOfTypeAll<HUStudioExportWindow>().FirstOrDefault();
            if (window != null) window.Repaint();
        }

        private void OnGUI()
        {
            var settings = HUStudioExportSettings.instance;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("HarmonyOS 环境与导出", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("1. 检测 DevEco 环境 → 2. 构建 WebGL → 3. 导出鸿蒙工程或 HAP", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("步骤 1：DevEco 环境", EditorStyles.boldLabel);
            DrawPath("DevEco Studio", () => HUStudioToolchain.StudioRoot, HUStudioToolchain.SetFromStudio, true);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("自动探测")) ProbeEnvironment(true);
            if (GUILayout.Button("检测环境")) ProbeEnvironment(false);
            if (GUILayout.Button("打开官方下载页")) Application.OpenURL(HUStudioToolchain.DownloadUrl);
            EditorGUILayout.EndHorizontal();
            string toolchainReason;
            bool pathsValid = HUStudioToolchain.Validate(out toolchainReason);
            bool toolchainReady = HUStudioToolchain.IsVerified;
            EditorGUILayout.HelpBox(toolchainReady ? "DevEco 环境已就绪。" :
                    pathsValid ? "点击“自动探测”或“检测环境”继续。" : "环境未就绪：" + toolchainReason,
                toolchainReady ? MessageType.Info : MessageType.Warning);
            showAdvancedPaths = EditorGUILayout.Foldout(showAdvancedPaths, "自定义工具路径（自动识别失败时使用）", true);
            if (showAdvancedPaths)
            {
                DrawPath("HarmonyOS SDK", () => HUStudioToolchain.SdkRoot, value => HUStudioToolchain.SdkRoot = value, true);
                DrawPath("JDK / JBR", () => HUStudioToolchain.JavaHome, value => HUStudioToolchain.JavaHome = value, true);
                DrawPath("Node.exe", () => HUStudioToolchain.NodeExe, value => HUStudioToolchain.NodeExe = value, false);
                DrawPath("hvigorw.js", () => HUStudioToolchain.HvigorJs, value => HUStudioToolchain.HvigorJs = value, false);
                DrawPath("ohpm.bat", () => HUStudioToolchain.OhpmBat, value => HUStudioToolchain.OhpmBat = value, false);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("步骤 2：检测并构建 WebGL", EditorStyles.boldLabel);
            bool webglModule = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            EditorGUILayout.LabelField(webglModule ? "WebGL Build Support 已安装" : "缺少 Unity WebGL Build Support");
            if (!webglModule && GUILayout.Button("打开 Unity Hub 下载页")) Application.OpenURL("https://unity.com/download");
            EditorGUILayout.HelpBox("使用 Unity Build Settings 中已启用的场景。若当前平台不是 WebGL，切换完成后自动继续构建。", MessageType.None);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            settings.outputDirectory = EditorGUILayout.TextField("输出目录", settings.outputDirectory);
            if (GUILayout.Button("选择", GUILayout.Width(54)))
            {
                string selected = EditorUtility.OpenFolderPanel("选择 HarmonyOS 输出目录", settings.outputDirectory, "");
                if (!string.IsNullOrEmpty(selected)) settings.outputDirectory = selected;
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) settings.Persist();
            using (new EditorGUI.DisabledScope(!toolchainReady || !webglModule))
            {
                if (GUILayout.Button("切换到 WebGL 并构建", GUILayout.Height(30)))
                {
                    try
                    {
                        HUStudioExportPipeline.ValidateWebGLInputs(settings);
                        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                        {
                            SessionState.SetBool(PendingWebGLKey, true);
                            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                            {
                                SessionState.SetBool(PendingWebGLKey, false);
                                throw new InvalidOperationException("Unity WebGL platform switch failed. Install WebGL Build Support in Unity Hub.");
                            }
                            status = "正在切换到 WebGL；Unity 重编脚本后会自动继续。";
                        }
                        else HUStudioExportPipeline.BuildWebGL(settings);
                    }
                    catch (Exception error) { Report(error); }
                }
            }
            string webglReason;
            bool webglReady = HUStudioExportPipeline.IsWebGLReady(settings, out webglReason);
            EditorGUILayout.LabelField(webglReady ? "WebGL 已就绪" : "WebGL 未就绪：" + webglReason, EditorStyles.wordWrappedLabel);
            if (webglReady) EditorGUILayout.SelectableLabel(settings.lastWebGLDirectory, GUILayout.Height(18));

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("步骤 3：导出鸿蒙工程或 HAP", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            settings.applicationName = EditorGUILayout.TextField("应用名称", settings.applicationName);
            settings.bundleName = EditorGUILayout.TextField("Bundle Name", settings.bundleName);
            settings.versionName = EditorGUILayout.TextField("版本名称", settings.versionName);
            settings.versionCode = EditorGUILayout.IntField("版本代码", settings.versionCode);
            if (EditorGUI.EndChangeCheck()) settings.Persist();
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("图标与启动画面", EditorStyles.boldLabel);
            DrawProjectImage("应用图标", settings.iconGuid, value => {
                settings.iconGuid = value; settings.Persist();
            }, "显示在桌面和应用列表中");
            EditorGUILayout.Space(4);
            DrawProjectImage("闪屏图标", settings.splashGuid, value => {
                settings.splashGuid = value; settings.Persist();
            }, "在系统启动页居中显示；留空时使用应用图标");
            Color splashColor;
            if (!ColorUtility.TryParseHtmlString(settings.splashBackgroundColor, out splashColor))
                splashColor = Color.white;
            EditorGUI.BeginChangeCheck();
            splashColor = EditorGUILayout.ColorField("闪屏背景色", splashColor);
            if (EditorGUI.EndChangeCheck())
            {
                settings.splashBackgroundColor = "#" + ColorUtility.ToHtmlStringRGB(splashColor);
                settings.Persist();
            }
            EditorGUILayout.EndVertical();
            using (new EditorGUI.DisabledScope(!toolchainReady || !webglReady))
            {
                if (GUILayout.Button("生成鸿蒙工程", GUILayout.Height(30)))
                {
                    try
                    {
                        string path = HUStudioExportPipeline.GenerateProject(settings);
                        status = "工程已生成：" + path;
                    }
                    catch (Exception error) { Report(error); }
                }
            }
            if (!string.IsNullOrEmpty(settings.lastProjectDirectory))
                EditorGUILayout.SelectableLabel(settings.lastProjectDirectory, GUILayout.Height(18));

            bool projectReady = HUStudioExportPipeline.IsProjectReady(settings);
            if (!projectReady && !string.IsNullOrEmpty(settings.lastProjectDirectory))
                EditorGUILayout.HelpBox("应用参数或 WebGL 产物已变化，请重新生成鸿蒙工程。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!toolchainReady || !webglReady))
            {
                if (GUILayout.Button("直接导出未签名 HAP", GUILayout.Height(30)))
                {
                    try
                    {
                        EditorUtility.DisplayProgressBar("HUStudio", "正在构建 HAP...", 0.5f);
                        if (!projectReady) HUStudioExportPipeline.GenerateProject(settings);
                        string hap = HUStudioToolchain.BuildUnsignedHap(settings.lastProjectDirectory, settings.outputDirectory);
                        status = "未签名 HAP 已生成：" + hap;
                    }
                    catch (Exception error) { Report(error); }
                    finally { EditorUtility.ClearProgressBar(); }
                }
            }
            showSigning = EditorGUILayout.Foldout(showSigning, "签名设置", true);
            if (showSigning)
            {
                if (GUILayout.Button("生成密钥与 CSR...")) HUStudioSigningWizard.Open();
                DrawPath("密钥库 (.p12)", () => HUStudioSigning.KeyStorePath,
                    value => HUStudioSigning.KeyStorePath = value, false);
                HUStudioSigning.KeyAlias = EditorGUILayout.TextField("密钥别名", HUStudioSigning.KeyAlias);
                HUStudioSigning.StorePassword = EditorGUILayout.PasswordField("密钥库密码", HUStudioSigning.StorePassword);
                HUStudioSigning.KeyPassword = EditorGUILayout.PasswordField("密钥密码（留空则相同）", HUStudioSigning.KeyPassword);
                DrawPath("证书 (.cer)", () => HUStudioSigning.CertificatePath,
                    value => HUStudioSigning.CertificatePath = value, false);
                DrawPath("Profile (.p7b)", () => HUStudioSigning.ProfilePath,
                    value => HUStudioSigning.ProfilePath = value, false);
                string signingReason;
                bool signingReady = HUStudioSigning.Validate(out signingReason);
                EditorGUILayout.HelpBox(signingReady ? "签名资料已就绪。" : signingReason,
                    signingReady ? MessageType.Info : MessageType.None);
                using (new EditorGUI.DisabledScope(!toolchainReady || !webglReady || !signingReady))
                {
                    if (GUILayout.Button("直接导出已签名 HAP", GUILayout.Height(30)))
                    {
                        try
                        {
                            EditorUtility.DisplayProgressBar("HUStudio", "正在构建并签名 HAP...", 0.5f);
                            if (!HUStudioExportPipeline.IsProjectReady(settings))
                                HUStudioExportPipeline.GenerateProject(settings);
                            string unsignedHap = HUStudioToolchain.BuildUnsignedHap(
                                settings.lastProjectDirectory, settings.outputDirectory);
                            status = "已签名 HAP：" + HUStudioSigning.SignHap(unsignedHap);
                        }
                        catch (Exception error) { Report(error); }
                        finally { EditorUtility.ClearProgressBar(); }
                    }
                }
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawPath(string label, Func<string> get, Action<string> set, bool folder)
        {
            EditorGUILayout.BeginHorizontal();
            string current = get();
            string edited = EditorGUILayout.TextField(label, current);
            if (edited != current) set(edited);
            if (GUILayout.Button("选择", GUILayout.Width(54)))
            {
                string selected = folder
                    ? EditorUtility.OpenFolderPanel("选择 " + label, current, "")
                    : EditorUtility.OpenFilePanel("选择 " + label,
                        string.IsNullOrEmpty(current) ? "" : Path.GetDirectoryName(current), "");
                if (!string.IsNullOrEmpty(selected)) set(selected);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProjectImage(string label, string guid, Action<string> setGuid, string description)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid ?? "");
            Sprite sprite = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Texture2D texture = sprite == null && !string.IsNullOrEmpty(path)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
            Rect area = GUILayoutUtility.GetRect(0, 112, GUILayout.ExpandWidth(true));
            GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
            Rect preview = new Rect(area.x + 8, area.y + 8, 96, 96);
            GUI.Box(preview, GUIContent.none, EditorStyles.helpBox);
            if (sprite != null)
            {
                Rect region = sprite.textureRect;
                Rect uv = new Rect(region.x / sprite.texture.width, region.y / sprite.texture.height,
                    region.width / sprite.texture.width, region.height / sprite.texture.height);
                float aspect = region.width / region.height;
                Rect image = aspect >= 1
                    ? new Rect(preview.x + 4, preview.center.y - 44 / aspect, 88, 88 / aspect)
                    : new Rect(preview.center.x - 44 * aspect, preview.y + 4, 88 * aspect, 88);
                GUI.DrawTextureWithTexCoords(image, sprite.texture, uv, true);
            }
            else if (texture != null)
                GUI.DrawTexture(new Rect(preview.x + 4, preview.y + 4, 88, 88),
                    texture, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(preview, "PNG", EditorStyles.centeredGreyMiniLabel);

            float rightX = area.x + 116;
            float rightWidth = area.width - 124;
            EditorGUI.LabelField(new Rect(rightX, area.y + 8, rightWidth, 18), label, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object selected = EditorGUI.ObjectField(
                new Rect(rightX, area.y + 34, rightWidth - 74, 20),
                (UnityEngine.Object)texture ?? sprite, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck()) SelectProjectImage(selected, setGuid);
            if (GUI.Button(new Rect(rightX + rightWidth - 68, area.y + 34, 68, 20), "恢复默认"))
                SelectProjectImage(null, setGuid);
            string caption = string.IsNullOrEmpty(path) ? "从 Assets 选择或拖入 PNG Texture / Sprite" : path;
            EditorGUI.LabelField(new Rect(rightX, area.y + 61, rightWidth, 18),
                new GUIContent(caption, path), EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rightX, area.y + 82, rightWidth, 18),
                description, EditorStyles.wordWrappedMiniLabel);

            Event current = Event.current;
            if (!area.Contains(current.mousePosition) ||
                (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)) return;
            UnityEngine.Object dragged = DragAndDrop.objectReferences.FirstOrDefault(candidate =>
                candidate is Texture2D || candidate is Sprite);
            string draggedPath = dragged == null ? "" : AssetDatabase.GetAssetPath(dragged);
            bool valid = IsProjectPng(draggedPath);
            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (valid) SelectProjectImage(dragged, setGuid);
                else status = "请选择 Assets 目录中的 PNG 图片。";
            }
            current.Use();
        }

        private void SelectProjectImage(UnityEngine.Object asset, Action<string> setGuid)
        {
            if (asset == null) { setGuid(""); status = ""; return; }
            if (!(asset is Texture2D) && !(asset is Sprite))
            { status = "请选择 PNG Texture 或 Sprite。"; return; }
            string path = AssetDatabase.GetAssetPath(asset);
            if (!IsProjectPng(path)) { status = "请选择 Assets 目录中的 PNG 图片。"; return; }
            setGuid(AssetDatabase.AssetPathToGUID(path));
            status = "";
        }

        private static bool IsProjectPng(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        private void ProbeEnvironment(bool autoDetect)
        {
            try
            {
                if (autoDetect) HUStudioToolchain.AutoDetect();
                HUStudioToolchain.Probe();
                status = "";
            }
            catch (Exception error) { Report(error); }
        }

        private void Report(Exception error)
        {
            status = error.Message;
            Debug.LogException(error);
        }
    }

    internal static class HUStudioExportPipeline
    {
        private const string ManifestName = "HUStudioBuild.json";
        private static readonly string[] RequiredBuildFiles =
            { "out.data", "out.framework.js", "out.loader.js", "out.wasm" };

        private static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
        }

        internal static void ValidateWebGLInputs(HUStudioExportSettings settings)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException("请先在 Unity Build Settings 中启用至少一个场景。");
            foreach (string scene in scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new InvalidOperationException("Scene is missing: " + scene);
            if (string.IsNullOrWhiteSpace(settings.outputDirectory))
                throw new InvalidOperationException("Select an output directory.");
        }

        private static string Fingerprint(HUStudioExportSettings settings)
        {
            var input = new StringBuilder(Application.unityVersion);
            foreach (string path in EnabledScenes())
                input.Append('|').Append(path).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
            input.Append('|').Append(PlayerSettings.companyName)
                 .Append('|').Append(PlayerSettings.productName)
                 .Append('|').Append(PlayerSettings.WebGL.template);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()))).Replace("-", "");
        }

        private static string ProjectImagePath(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return "";
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("图片资源已移除或不是 Assets 中的 PNG：" + guid);
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }

        private static string ImageHash(string guid)
        {
            string path = ProjectImagePath(guid);
            if (string.IsNullOrEmpty(path)) return "";
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static void ValidateBranding(HUStudioExportSettings settings)
        {
            foreach (string guid in new[] { settings.iconGuid, settings.splashGuid })
            {
                string path = ProjectImagePath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path))
                    throw new InvalidOperationException("请选择有效的 PNG 图片：" + path);
                using (var stream = File.OpenRead(path))
                {
                    byte[] signature = new byte[8];
                    if (stream.Read(signature, 0, signature.Length) != signature.Length ||
                        !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                        throw new InvalidDataException("图片不是 PNG 格式：" + path);
                }
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(settings.splashBackgroundColor ?? "", @"^#[0-9a-fA-F]{6}$"))
                throw new InvalidOperationException("闪屏背景色无效。");
        }

        private static void CopyBranding(string project, HUStudioExportSettings settings)
        {
            string appIcon = Path.Combine(project, "AppScope", "resources", "base", "media", "app_icon.png");
            string startIcon = Path.Combine(project, "entry", "src", "main", "resources", "base", "media", "start_icon.png");
            string iconSource = ProjectImagePath(settings.iconGuid);
            if (!string.IsNullOrEmpty(iconSource)) File.Copy(iconSource, appIcon, true);
            string startSource = ProjectImagePath(!string.IsNullOrWhiteSpace(settings.splashGuid)
                ? settings.splashGuid : settings.iconGuid);
            if (!string.IsNullOrWhiteSpace(startSource)) File.Copy(startSource, startIcon, true);
            string colorFile = Path.Combine(project, "entry", "src", "main", "resources", "base", "element", "color.json");
            File.WriteAllText(colorFile, "{\"color\":[{\"name\":\"start_window_background\",\"value\":\"" +
                settings.splashBackgroundColor + "\"}]}", new UTF8Encoding(false));
        }

        internal static bool IsWebGLReady(HUStudioExportSettings settings, out string reason)
        {
            reason = "尚未构建";
            if (string.IsNullOrEmpty(settings.lastWebGLDirectory)) return false;
            string manifestPath = Path.Combine(settings.lastWebGLDirectory, ManifestName);
            if (!File.Exists(manifestPath)) return false;
            try
            {
                var manifest = JsonUtility.FromJson<HUStudioWebGLManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.inputFingerprint != Fingerprint(settings))
                {
                    reason = "场景或构建设置已变化";
                    return false;
                }
                if (!File.Exists(Path.Combine(settings.lastWebGLDirectory, "main.js")))
                {
                    reason = "main.js 缺失";
                    return false;
                }
                foreach (string name in RequiredBuildFiles)
                    if (!File.Exists(Path.Combine(settings.lastWebGLDirectory, "Build", name)))
                    {
                        reason = name + " 缺失";
                        return false;
                    }
                reason = "";
                return true;
            }
            catch (Exception) { reason = "构建清单无效"; return false; }
        }

        internal static void BuildWebGL(HUStudioExportSettings settings)
        {
            ValidateWebGLInputs(settings);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Unity active build target must be WebGL.");
            string outputRoot = Path.GetFullPath(settings.outputDirectory);
            Directory.CreateDirectory(outputRoot);
            string stage = Path.Combine(outputRoot, ".webgl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            WebGLCompressionFormat oldCompression = PlayerSettings.WebGL.compressionFormat;
            bool oldThreads = PlayerSettings.WebGL.threadsSupport;
            bool oldHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            string oldProduct = PlayerSettings.productName;
            string fingerprint = Fingerprint(settings);
            string[] scenes = EnabledScenes();
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.threadsSupport = false;
                PlayerSettings.WebGL.nameFilesAsHashes = false;
                PlayerSettings.productName = "out";
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = stage,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Unity WebGL build failed: " + report.summary.result);
                NormalizeBuildFiles(stage);
                File.WriteAllBytes(Path.Combine(stage, "main.js"), LoadResource("MainTemplate").bytes);
                var manifest = new HUStudioWebGLManifest
                {
                    unityVersion = Application.unityVersion,
                    inputFingerprint = fingerprint,
                    scenes = scenes,
                    builtUtc = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(Path.Combine(stage, ManifestName), JsonUtility.ToJson(manifest, true));
                string finalPath = Path.Combine(outputRoot, "WebGL-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                if (Directory.Exists(finalPath)) finalPath += "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                Directory.Move(stage, finalPath);
                settings.scenePaths = scenes;
                settings.lastWebGLDirectory = finalPath;
                settings.lastProjectDirectory = "";
                settings.Persist();
                Debug.Log("[HUStudio] WebGL output: " + finalPath);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = oldCompression;
                PlayerSettings.WebGL.threadsSupport = oldThreads;
                PlayerSettings.WebGL.nameFilesAsHashes = oldHashes;
                PlayerSettings.productName = oldProduct;
            }
        }

        private static void NormalizeBuildFiles(string webgl)
        {
            string build = Path.Combine(webgl, "Build");
            string loader = Directory.GetFiles(build, "*.loader.js").FirstOrDefault();
            if (loader == null) throw new FileNotFoundException("Unity WebGL loader is missing.");
            string stem = Path.GetFileName(loader).Replace(".loader.js", "");
            foreach (string suffix in new[] { ".loader.js", ".framework.js", ".data", ".wasm" })
            {
                string source = Path.Combine(build, stem + suffix);
                if (!File.Exists(source)) throw new FileNotFoundException("WebGL output is missing: " + source);
                string destination = Path.Combine(build, "out" + suffix);
                if (source != destination)
                {
                    File.Copy(source, destination, true);
                    File.Delete(source);
                }
            }
        }

        internal static bool IsProjectReady(HUStudioExportSettings settings)
        {
            if (string.IsNullOrEmpty(settings.lastProjectDirectory) ||
                !Directory.Exists(settings.lastProjectDirectory)) return false;
            string path = Path.Combine(settings.lastProjectDirectory, "HUStudio-project.json");
            if (!File.Exists(path)) return false;
            try
            {
                var manifest = JsonUtility.FromJson<HUStudioProjectManifest>(File.ReadAllText(path));
                return manifest != null && manifest.webglDirectory == settings.lastWebGLDirectory &&
                    manifest.applicationName == settings.applicationName &&
                    manifest.bundleName == settings.bundleName &&
                    manifest.versionName == settings.versionName &&
                    manifest.versionCode == settings.versionCode &&
                    manifest.iconGuid == settings.iconGuid &&
                    manifest.splashGuid == settings.splashGuid &&
                    manifest.iconHash == ImageHash(settings.iconGuid) &&
                    manifest.splashHash == ImageHash(settings.splashGuid) &&
                    manifest.splashBackgroundColor == settings.splashBackgroundColor;
            }
            catch (Exception) { return false; }
        }

        internal static string GenerateProject(HUStudioExportSettings settings)
        {
            string reason;
            if (!IsWebGLReady(settings, out reason))
                throw new InvalidOperationException("Build WebGL first: " + reason);
            if (string.IsNullOrWhiteSpace(settings.applicationName) ||
                !System.Text.RegularExpressions.Regex.IsMatch(settings.bundleName ?? "", @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$") ||
                settings.versionCode < 1)
                throw new InvalidOperationException("Check application name, Bundle Name, and version code.");
            ValidateBranding(settings);
            string outputRoot = Path.GetFullPath(settings.outputDirectory);
            string stage = Path.Combine(outputRoot, ".deveco-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                ExtractTemplate(LoadResource("DevEcoTemplate").bytes, stage);
                string appScope = Path.Combine(stage, "AppScope");
                string entry = Path.Combine(stage, "entry");
                ReplaceTokens(Path.Combine(appScope, "app.json5"), settings);
                ReplaceTokens(Path.Combine(appScope, "resources", "base", "element", "string.json"), settings);
                ReplaceTokens(Path.Combine(entry, "src", "main", "resources", "base", "element", "string.json"), settings);
                CopyBranding(stage, settings);
                if (!File.Exists(Path.Combine(entry, "libs", "arm64-v8a", "libufhn.so")))
                    throw new InvalidDataException("DevEco 模板缺少 libufhn.so。");
                string rawfile = Path.Combine(entry, "src", "main", "resources", "rawfile", "unity_webgl");
                CopyWebGL(settings.lastWebGLDirectory, rawfile);
                string mainPath = Path.Combine(rawfile, "main.js");
                File.WriteAllText(mainPath, File.ReadAllText(mainPath).Replace("__APP_NAME__",
                    settings.applicationName.Replace("\\", "\\\\").Replace("'", "\\'")), new UTF8Encoding(false));
                PatchFramework(Path.Combine(rawfile, "Build", "out.framework.js"));
                WriteUnityFiles(stage, rawfile);
                File.WriteAllText(Path.Combine(stage, "HUStudio-project.json"), JsonUtility.ToJson(
                    new HUStudioProjectManifest {
                        webglDirectory = settings.lastWebGLDirectory,
                        applicationName = settings.applicationName,
                        bundleName = settings.bundleName,
                        versionName = settings.versionName,
                        versionCode = settings.versionCode,
                        iconGuid = settings.iconGuid,
                        splashGuid = settings.splashGuid,
                        iconHash = ImageHash(settings.iconGuid),
                        splashHash = ImageHash(settings.splashGuid),
                        splashBackgroundColor = settings.splashBackgroundColor
                    }, true), new UTF8Encoding(false));
                string finalPath = Path.Combine(outputRoot, "DevEcoProject-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                if (Directory.Exists(finalPath)) finalPath += "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                Directory.Move(stage, finalPath);
                settings.lastProjectDirectory = finalPath;
                settings.Persist();
                Debug.Log("[HUStudio] DevEco project: " + finalPath);
                return finalPath;
            }
            catch { throw; }
        }

        private static TextAsset LoadResource(string name)
        {
            var asset = Resources.Load<TextAsset>("HUStudio/" + name);
            if (asset == null) throw new FileNotFoundException("Package resource is missing: " + name);
            return asset;
        }

        internal static void SmokeResources()
        {
            byte[] template = LoadResource("DevEcoTemplate").bytes;
            byte[] main = LoadResource("MainTemplate").bytes;
            if (template.Length < 1000000 || main.Length < 100)
                throw new InvalidDataException("Bundled exporter resources are unexpectedly small.");
            string output = Path.Combine(Path.GetTempPath(), "HUStudioSmoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            ExtractTemplate(template, output);
            if (!File.Exists(Path.Combine(output, "entry", "src", "main", "ets", "pages", "Index.ets")))
                throw new InvalidDataException("DevEco template did not extract correctly.");
            string library = Path.Combine(output, "entry", "libs", "arm64-v8a", "libufhn.so");
            if (!File.Exists(library) || new FileInfo(library).Length < 1000000)
                throw new InvalidDataException("DevEco template is missing libufhn.so.");
            Debug.Log("[HUStudio] Bundled resources verified. Template=" + template.Length +
                      ", libufhn.so=" + new FileInfo(library).Length + ", main.js=" + main.Length);
        }

        internal static void SmokeProject()
        {
            var settings = HUStudioExportSettings.instance;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sampleWebGL = Path.Combine(projectRoot, "WebGL");
            string outputRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "build", "unity-export-smoke"));
            Directory.CreateDirectory(outputRoot);
            settings.outputDirectory = outputRoot;
            settings.scenePaths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (settings.scenePaths.Length == 0)
                throw new InvalidOperationException("Sample project has no enabled scenes.");
            string icon = Environment.GetEnvironmentVariable("HUSTUDIO_TEST_ICON");
            string splash = Environment.GetEnvironmentVariable("HUSTUDIO_TEST_SPLASH");
            if (!string.IsNullOrEmpty(icon)) settings.iconGuid = AssetDatabase.AssetPathToGUID(icon);
            if (!string.IsNullOrEmpty(splash)) settings.splashGuid = AssetDatabase.AssetPathToGUID(splash);
            if (!string.IsNullOrEmpty(icon) || !string.IsNullOrEmpty(splash))
                settings.splashBackgroundColor = "#123456";
            settings.lastWebGLDirectory = sampleWebGL;
            File.WriteAllText(Path.Combine(sampleWebGL, ManifestName),
                JsonUtility.ToJson(new HUStudioWebGLManifest
                {
                    unityVersion = Application.unityVersion,
                    inputFingerprint = Fingerprint(settings),
                    scenes = settings.scenePaths,
                    builtUtc = DateTime.UtcNow.ToString("o")
                }, true));
            GenerateProject(settings);
        }

        internal static void SmokeWebGL()
        {
            var settings = HUStudioExportSettings.instance;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            settings.outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "build", "unity-export-smoke"));
            settings.scenePaths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            BuildWebGL(settings);
        }

        private static void ExtractTemplate(byte[] archive, string destination)
        {
            using (var memory = new MemoryStream(archive))
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Read))
            {
                string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var entry in zip.Entries)
                {
                    string path = Path.GetFullPath(Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Invalid template entry: " + entry.FullName);
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open()) using (var output = File.Create(path)) input.CopyTo(output);
                }
            }
        }

        private static void ReplaceTokens(string file, HUStudioExportSettings settings)
        {
            string contents = File.ReadAllText(file);
            contents = contents.Replace("__BUNDLE_NAME__", settings.bundleName)
                .Replace("__VERSION_NAME__", settings.versionName.Replace("'", ""))
                .Replace("__VERSION_CODE__", settings.versionCode.ToString())
                .Replace("__APP_NAME__", settings.applicationName.Replace("\\", "\\\\").Replace("\"", "\\\""));
            File.WriteAllText(file, contents, new UTF8Encoding(false));
        }

        private static void CopyWebGL(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                if (relative == ManifestName || relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void PatchFramework(string file)
        {
            const string oldCall = "mobile_input.input.setSelectionRange(start,start+length)";
            const string fallback = "(typeof mobile_input.input.setSelectionRange===\"function\"?mobile_input.input.setSelectionRange(start,start+length):(mobile_input.input.selectionStart=start,mobile_input.input.selectionEnd=start+length))";
            string contents = File.ReadAllText(file);
            if (contents.Contains(oldCall)) File.WriteAllText(file, contents.Replace(oldCall, fallback), new UTF8Encoding(false));
        }

        private static void WriteUnityFiles(string project, string rawfile)
        {
            var names = Directory.GetFiles(rawfile, "*", SearchOption.AllDirectories)
                .Select(file => file.Substring(rawfile.Length + 1).Replace('\\', '/'))
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var lines = new List<string> { "// Generated by HUStudio exporter.", "export const unityFiles: string[] = [" };
            lines.AddRange(names.Select(name => "  '" + name.Replace("'", "\\'") + "',"));
            lines.Add("];");
            File.WriteAllLines(Path.Combine(project, "entry", "src", "main", "ets", "UnityFiles.ets"), lines, new UTF8Encoding(false));
        }
    }

    public static class HUStudioBatchSmoke
    {
        public static void Run() { HUStudioExportPipeline.SmokeResources(); }
        public static void BuildProject() { HUStudioExportPipeline.SmokeProject(); }
        public static void BuildWebGL() { HUStudioExportPipeline.SmokeWebGL(); }
        public static void CheckEnvironment()
        {
            string studio = Environment.GetEnvironmentVariable("HUSTUDIO_TEST_STUDIO");
            if (string.IsNullOrEmpty(studio)) throw new InvalidOperationException("HUSTUDIO_TEST_STUDIO is required.");
            HUStudioToolchain.SetFromStudio(studio);
            Debug.Log("[HUStudio] Environment: " + HUStudioToolchain.Probe());
        }
        public static void BuildHap()
        {
            var settings = HUStudioExportSettings.instance;
            if (!HUStudioExportPipeline.IsProjectReady(settings))
                HUStudioExportPipeline.GenerateProject(settings);
            string path = HUStudioToolchain.BuildUnsignedHap(settings.lastProjectDirectory, settings.outputDirectory);
            Debug.Log("[HUStudio] HAP smoke: " + path);
        }
    }
}
