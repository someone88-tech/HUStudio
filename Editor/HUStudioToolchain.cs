using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HUStudio.HarmonyOS.Editor
{
    // Paths are machine-specific and deliberately live in EditorPrefs, not in
    // the Unity project or any exported DevEco project.
    internal static class HUStudioToolchain
    {
        private const string Prefix = "HUStudio.Environment.";
        internal const string DownloadUrl = "https://developer.huawei.com/consumer/cn/deveco-studio/resources/";

        internal static string StudioRoot
        {
            get { return EditorPrefs.GetString(Prefix + "StudioRoot", ""); }
            set { EditorPrefs.SetString(Prefix + "StudioRoot", value ?? ""); }
        }
        internal static string SdkRoot
        {
            get { return EditorPrefs.GetString(Prefix + "SdkRoot", ""); }
            set { EditorPrefs.SetString(Prefix + "SdkRoot", value ?? ""); }
        }
        internal static string JavaHome
        {
            get { return EditorPrefs.GetString(Prefix + "JavaHome", ""); }
            set { EditorPrefs.SetString(Prefix + "JavaHome", value ?? ""); }
        }
        internal static string NodeExe
        {
            get { return EditorPrefs.GetString(Prefix + "NodeExe", ""); }
            set { EditorPrefs.SetString(Prefix + "NodeExe", value ?? ""); }
        }
        internal static string HvigorJs
        {
            get { return EditorPrefs.GetString(Prefix + "HvigorJs", ""); }
            set { EditorPrefs.SetString(Prefix + "HvigorJs", value ?? ""); }
        }
        internal static string OhpmBat
        {
            get { return EditorPrefs.GetString(Prefix + "OhpmBat", ""); }
            set { EditorPrefs.SetString(Prefix + "OhpmBat", value ?? ""); }
        }

        private static string PathSignature
        {
            get { return string.Join("|", StudioRoot, SdkRoot, JavaHome, NodeExe, HvigorJs, OhpmBat); }
        }

        internal static bool IsVerified
        {
            get
            {
                string reason;
                return Validate(out reason) && EditorPrefs.GetString(Prefix + "VerifiedPaths", "") == PathSignature;
            }
        }

        internal static void SetFromStudio(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) { StudioRoot = ""; return; }
            root = Path.GetFullPath(root);
            StudioRoot = root;
            string bundledSdk = Path.Combine(root, "sdk");
            string configuredSdk = Environment.GetEnvironmentVariable("DEVECO_SDK_HOME");
            SdkRoot = File.Exists(Path.Combine(bundledSdk, "default", "sdk-pkg.json")) ? bundledSdk :
                !string.IsNullOrEmpty(configuredSdk) ? configuredSdk : bundledSdk;
            JavaHome = Path.Combine(root, "jbr");
            NodeExe = Path.Combine(root, "tools", "node", "node.exe");
            HvigorJs = Path.Combine(root, "tools", "hvigor", "bin", "hvigorw.js");
            OhpmBat = Path.Combine(root, "tools", "ohpm", "bin", "ohpm.bat");
        }

        internal static void AutoDetect()
        {
            string[] candidates = {
                StudioRoot,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Huawei", "DevEco Studio"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Huawei", "DevEco Studio")
            };
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                string hvigor = Path.Combine(candidate, "tools", "hvigor", "bin", "hvigorw.js");
                if (File.Exists(hvigor)) { SetFromStudio(candidate); return; }
            }
        }

        internal static bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(StudioRoot) || !Directory.Exists(StudioRoot))
            { reason = "请选择 DevEco Studio 安装目录"; return false; }
            if (string.IsNullOrWhiteSpace(SdkRoot) ||
                !Directory.Exists(Path.Combine(SdkRoot, "default", "openharmony", "ets")) ||
                !Directory.Exists(Path.Combine(SdkRoot, "default", "openharmony", "toolchains")))
            { reason = "HarmonyOS SDK 未安装或路径不同；请在 DevEco Studio 中安装 SDK，或展开下方自定义路径"; return false; }
            if (string.IsNullOrWhiteSpace(JavaHome) || !File.Exists(Path.Combine(JavaHome, "bin", "java.exe")))
            { reason = "DevEco Studio 的 JDK/JBR 未找到；可展开下方自定义路径"; return false; }
            if (string.IsNullOrWhiteSpace(NodeExe) || !File.Exists(NodeExe))
            { reason = "DevEco Studio 的 Node.exe 未找到；可展开下方自定义路径"; return false; }
            if (string.IsNullOrWhiteSpace(HvigorJs) || !File.Exists(HvigorJs))
            { reason = "DevEco Studio 的 Hvigor 未找到；可展开下方自定义路径"; return false; }
            if (string.IsNullOrWhiteSpace(OhpmBat) || !File.Exists(OhpmBat))
            { reason = "DevEco Studio 的 ohpm 未找到；可展开下方自定义路径"; return false; }
            reason = "";
            return true;
        }

        internal static string Probe()
        {
            EditorPrefs.DeleteKey(Prefix + "VerifiedPaths");
            string reason;
            if (!Validate(out reason)) throw new InvalidOperationException(reason);
            string sdkPackage = Path.Combine(SdkRoot, "default", "sdk-pkg.json");
            if (!File.Exists(sdkPackage) || !System.Text.RegularExpressions.Regex.IsMatch(
                    File.ReadAllText(sdkPackage), "\\\"apiVersion\\\"\\s*:\\s*\\\"26\\\""))
                throw new InvalidDataException("请在 DevEco Studio 中安装 HarmonyOS API 26 SDK。");
            string nodeVersion = RunVersion(NodeExe, "--version");
            string javaVersion = RunVersion(Path.Combine(JavaHome, "bin", "java.exe"), "-version");
            string hvigorVersion = RunVersion(NodeExe, "\"" + HvigorJs + "\" --version");
            EditorPrefs.SetString(Prefix + "VerifiedPaths", PathSignature);
            Debug.Log("[HUStudio] DevEco detected: API 26; Node " + nodeVersion +
                      "; Java " + javaVersion + "; Hvigor " + hvigorVersion);
            return "DevEco 环境检测通过。";
        }

        private static string RunVersion(string program, string arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = program,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(start))
            {
                if (!process.WaitForExit(12000))
                {
                    process.Kill();
                    throw new TimeoutException(program + " version check timed out.");
                }
                string combined = (process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim();
                if (process.ExitCode != 0) throw new InvalidOperationException(program + " version check failed: " + combined);
                return combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "unknown";
            }
        }

        internal static string BuildUnsignedHap(string project, string outputRoot)
        {
            return BuildHap(project, outputRoot, false);
        }

        internal static string BuildAutoSignedHap(string project, string outputRoot)
        {
            return BuildHap(project, outputRoot, true);
        }

        private static string BuildHap(string project, string outputRoot, bool signed)
        {
            string reason;
            if (!Validate(out reason)) throw new InvalidOperationException("环境检查未通过: " + reason);
            if (!IsVerified) throw new InvalidOperationException("请先在导出窗口点击“检测环境”。");
            if (!Directory.Exists(project)) throw new DirectoryNotFoundException("DevEco 工程不存在: " + project);
            var start = new ProcessStartInfo
            {
                FileName = NodeExe,
                Arguments = "\"" + HvigorJs + "\" assembleHap --mode module -p module=entry@default -p product=default --no-daemon",
                WorkingDirectory = project,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.EnvironmentVariables["DEVECO_SDK_HOME"] = SdkRoot;
            start.EnvironmentVariables["OHOS_BASE_SDK_HOME"] = Path.Combine(SdkRoot, "default", "openharmony");
            start.EnvironmentVariables["JAVA_HOME"] = JavaHome;
            string prepend = string.Join(Path.PathSeparator.ToString(), new[] {
                Path.GetDirectoryName(NodeExe), Path.GetDirectoryName(HvigorJs),
                Path.GetDirectoryName(OhpmBat), Path.Combine(JavaHome, "bin"),
                Environment.SystemDirectory
            });
            start.EnvironmentVariables["PATH"] = prepend + Path.PathSeparator + start.EnvironmentVariables["PATH"];
            start.EnvironmentVariables["ComSpec"] = Path.Combine(Environment.SystemDirectory, "cmd.exe");

            var output = new StringBuilder();
            int exitCode;
            using (var process = new Process { StartInfo = start })
            {
                process.OutputDataReceived += (sender, args) => { if (args.Data != null) lock (output) output.AppendLine(args.Data); };
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) lock (output) output.AppendLine(args.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(600000))
                {
                    process.Kill();
                    throw new TimeoutException("HAP 构建超过 10 分钟，请检查 Hvigor 和 SDK 环境。");
                }
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            string log = Path.Combine(project, "HUStudio-hvigor.log");
            File.WriteAllText(log, output.ToString(), new UTF8Encoding(false));
            if (exitCode != 0) throw new InvalidOperationException("HAP 构建失败。日志: " + log);
            string outputs = Path.Combine(project, "entry", "build", "default", "outputs");
            string suffix = signed ? "-signed.hap" : "-unsigned.hap";
            string hap = Directory.GetFiles(outputs, "*.hap", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (hap == null) throw new FileNotFoundException("Hvigor 未生成" + (signed ? "已" : "未") + "签名 HAP: " + outputs);
            string destination = Path.Combine(outputRoot, "HAP");
            Directory.CreateDirectory(destination);
            string target = Path.Combine(destination, "HUStudio-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + suffix);
            File.Copy(hap, target, false);
            Debug.Log("[HUStudio] HAP: " + target);
            return target;
        }

        internal static string HdcExe
        {
            get { return Path.Combine(SdkRoot, "default", "openharmony", "toolchains", "hdc.exe"); }
        }

        internal static string[] ListDevices()
        {
            string reason;
            if (!Validate(out reason) || !IsVerified)
                throw new InvalidOperationException("请先完成 DevEco 环境检测。" + (string.IsNullOrEmpty(reason) ? "" : " " + reason));
            if (!File.Exists(HdcExe)) throw new FileNotFoundException("SDK 中未找到 hdc 设备工具。", HdcExe);
            string output = RunHdc(new[] { "list", "targets" }, 15000);
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line) && line != "[Empty]")
                .Where(line => !line.StartsWith("[", StringComparison.Ordinal))
                .ToArray();
        }

        internal static string InstallHap(string device, string hap)
        {
            if (string.IsNullOrWhiteSpace(device)) throw new ArgumentException("请选择已连接的设备。", "device");
            if (!File.Exists(hap)) throw new FileNotFoundException("HAP 文件不存在。", hap);
            // Normalize mixed separators from Unity's persisted output path. hdc
            // treats a Windows drive path containing forward slashes as relative.
            string localHap = Path.GetFullPath(hap).Replace('/', '\\');
            string output = RunHdc(new[] { "-t", device, "install", "-r", localHap }, 120000);
            if (output.IndexOf("install successfully", StringComparison.OrdinalIgnoreCase) < 0 &&
                output.IndexOf("success", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("设备未确认安装成功。hdc 输出：" + output);
            return output.Trim();
        }

        internal static string LaunchApp(string device, string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName)) throw new ArgumentException("Bundle Name 不能为空。", "bundleName");
            return RunHdc(new[] { "-t", device, "shell", "aa", "start", "-b", bundleName, "-a", "EntryAbility" }, 30000).Trim();
        }

        internal static void OpenProjectInDevEco(string projectDirectory)
        {
            string executable = Path.Combine(StudioRoot, "bin", "devecostudio64.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("未找到 DevEco Studio 启动程序。", executable);
            if (!Directory.Exists(projectDirectory)) throw new DirectoryNotFoundException("鸿蒙工程不存在：" + projectDirectory);
            var start = new ProcessStartInfo { FileName = executable, Arguments = QuoteArgument(projectDirectory),
                UseShellExecute = false, CreateNoWindow = true };
            Process.Start(start);
        }

        private static string RunHdc(IEnumerable<string> arguments, int timeoutMs)
        {
            var start = new ProcessStartInfo { FileName = HdcExe, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.Arguments = string.Join(" ", arguments.Select(QuoteArgument));
            using (var process = Process.Start(start))
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                process.OutputDataReceived += (sender, args) => { if (args.Data != null) lock (stdout) stdout.AppendLine(args.Data); };
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) lock (stderr) stderr.AppendLine(args.Data); };
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(timeoutMs)) { process.Kill(); throw new TimeoutException("hdc 操作超时。"); }
                process.WaitForExit();
                string output = stdout.ToString() + stderr.ToString();
                if (process.ExitCode != 0) throw new InvalidOperationException("hdc 操作失败：" + output.Trim());
                return output;
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
