using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HUStudio.HarmonyOS.Editor
{
    internal static class HUStudioSigning
    {
        private const string Prefix = "HUStudio.Signing.";
        private const string PasswordEnvironmentName = "HUSTUDIO_KEYSTORE_PASSWORD";
        private static string storePassword = "";
        private static string keyPassword = "";
        private static string autoSigningProject = "";
        internal static bool AutoSigningReady { get; private set; }

        internal static bool IsAutoSigningReadyFor(string projectDirectory)
        {
            return AutoSigningReady && !string.IsNullOrWhiteSpace(projectDirectory) &&
                string.Equals(autoSigningProject, Path.GetFullPath(projectDirectory), StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(projectDirectory, "build-profile.json5"));
        }

        internal static string KeyStorePath
        {
            get { return EditorPrefs.GetString(Prefix + "KeyStorePath", ""); }
            set { EditorPrefs.SetString(Prefix + "KeyStorePath", value ?? ""); }
        }
        internal static string KeyAlias
        {
            get { return EditorPrefs.GetString(Prefix + "KeyAlias", ""); }
            set { EditorPrefs.SetString(Prefix + "KeyAlias", value ?? ""); }
        }
        internal static string CertificatePath
        {
            get { return EditorPrefs.GetString(Prefix + "CertificatePath", ""); }
            set { EditorPrefs.SetString(Prefix + "CertificatePath", value ?? ""); }
        }
        internal static string ProfilePath
        {
            get { return EditorPrefs.GetString(Prefix + "ProfilePath", ""); }
            set { EditorPrefs.SetString(Prefix + "ProfilePath", value ?? ""); }
        }
        internal static string StorePassword { get { return storePassword; } set { storePassword = value ?? ""; } }
        internal static string KeyPassword { get { return keyPassword; } set { keyPassword = value ?? ""; } }

        internal static bool Validate(out string reason)
        {
            if (!File.Exists(KeyStorePath) || !KeyStorePath.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            { reason = "选择 .p12 密钥库，或先生成密钥与 CSR。"; return false; }
            if (!Regex.IsMatch(KeyAlias ?? "", @"^[A-Za-z0-9._-]{1,64}$"))
            { reason = "填写密钥别名。"; return false; }
            if (string.IsNullOrEmpty(StorePassword))
            { reason = "填写密钥库密码。密码仅在本次 Unity 会话中保留。"; return false; }
            if (!File.Exists(CertificatePath) || !CertificatePath.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))
            { reason = "选择与密钥对应的华为证书 (.cer)。"; return false; }
            if (!File.Exists(ProfilePath) || !ProfilePath.EndsWith(".p7b", StringComparison.OrdinalIgnoreCase))
            { reason = "选择与 Bundle Name 对应的 Profile (.p7b)。"; return false; }
            reason = "";
            return true;
        }

        internal static bool TryLoadAutoSigningFromProject(string projectDirectory, out string reason)
        {
            AutoSigningReady = false;
            autoSigningProject = "";
            string buildProfile = Path.Combine(projectDirectory ?? "", "build-profile.json5");
            if (!File.Exists(buildProfile))
            { reason = "当前鸿蒙工程中没有 build-profile.json5，请先生成鸿蒙工程。"; return false; }
            string source = File.ReadAllText(buildProfile);
            Match section = Regex.Match(source, @"['""]?signingConfigs['""]?\s*:\s*\[(?<items>[\s\S]*?)\]",
                RegexOptions.CultureInvariant);
            if (!section.Success)
            { reason = "工程尚未配置自动签名。请在 DevEco 中进入“文件 > 项目结构 > 项目 > 签名配置”，启用自动生成签名并保存。"; return false; }

            string items = section.Groups["items"].Value;
            string config = items;
            string selectedName = "";
            Match selected = Regex.Match(source, @"['""]?signingConfig['""]?\s*:\s*['""](?<name>[^'""]+)['""]");
            if (selected.Success) selectedName = selected.Groups["name"].Value;
            if (!string.IsNullOrEmpty(selectedName))
            {
                foreach (string candidate in ExtractTopLevelObjects(items))
                {
                    string name = ReadJson5String(candidate, "name");
                    if (string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase))
                    { config = candidate; break; }
                }
            }

            string store = ReadJson5String(config, "storeFile");
            string cert = ReadJson5String(config, "certpath");
            string profile = ReadJson5String(config, "profile");
            string alias = ReadJson5String(config, "keyAlias");
            if (string.IsNullOrEmpty(store) || string.IsNullOrEmpty(cert) || string.IsNullOrEmpty(profile))
            { reason = "已找到签名配置，但缺少 .p12、.cer 或 .p7b 路径。请在 DevEco 中勾选自动生成签名并点击 OK，等待签名完成后重试。"; return false; }

            store = ResolveSigningPath(projectDirectory, store);
            cert = ResolveSigningPath(projectDirectory, cert);
            profile = ResolveSigningPath(projectDirectory, profile);
            if (!File.Exists(store) || !File.Exists(cert) || !File.Exists(profile))
            { reason = "build-profile.json5 中有签名配置，但对应的 .p12/.cer/.p7b 文件未全部找到。请在 DevEco 等待自动签名完成，再重试检测。"; return false; }

            string configName = ReadJson5String(config, "name");
            if (string.IsNullOrEmpty(configName)) configName = selectedName;
            if (string.IsNullOrEmpty(configName)) configName = "default";
            string updatedSource;
            EnsureProductSigningConfig(source, configName, out updatedSource);
            source = updatedSource;
            File.WriteAllText(buildProfile, source, new UTF8Encoding(false));

            KeyStorePath = store; CertificatePath = cert; ProfilePath = profile;
            KeyAlias = alias;
            autoSigningProject = Path.GetFullPath(projectDirectory);
            AutoSigningReady = true;
            reason = "已读取 DevEco 自动调试签名配置并关联到当前构建产品。";
            return true;
        }

        private static void EnsureProductSigningConfig(string source, string configName, out string updated)
        {
            updated = source;
            Match products = Regex.Match(source, @"\bproducts\s*:\s*\[(?<items>[\s\S]*?)\]", RegexOptions.CultureInvariant);
            if (!products.Success) return;
            string firstProduct = ExtractTopLevelObjects(products.Groups["items"].Value).FirstOrDefault();
            if (string.IsNullOrEmpty(firstProduct) || Regex.IsMatch(firstProduct, @"['""]?signingConfig['""]?\s*:")) return;
            int insertAt = source.IndexOf(firstProduct, products.Index, StringComparison.Ordinal);
            if (insertAt < 0) return;
            updated = source.Insert(insertAt + 1, "\n        signingConfig: '" + configName.Replace("'", "") + "',");
        }

        private static string ResolveSigningPath(string projectDirectory, string path)
        {
            path = path.Replace("\\\\", "\\").Replace("\\\"", "\"");
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(projectDirectory, path));
        }

        private static string ReadJson5String(string source, string key)
        {
            Match match = Regex.Match(source ?? "", @"(?:^|[,{\s])['""]?" + Regex.Escape(key) + @"['""]?" +
                @"\s*:\s*(['""])(?<value>(?:\\.|(?!\1)[^\\])*)\1", RegexOptions.Singleline);
            if (!match.Success) return "";
            return match.Groups["value"].Value.Replace("\\\\", "\\").Replace("\\\"", "\"");
        }

        private static IEnumerable<string> ExtractTopLevelObjects(string source)
        {
            int depth = 0, start = -1;
            char quote = '\0'; bool escaped = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (quote != '\0')
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '\'' || c == '"') { quote = c; continue; }
                if (c == '{') { if (depth++ == 0) start = i; }
                else if (c == '}' && depth > 0 && --depth == 0 && start >= 0)
                    yield return source.Substring(start, i - start + 1);
            }
        }

        internal static string[] GenerateKeyAndCsr(string folder, string alias, string commonName, string password)
        {
            if (!HUStudioToolchain.IsVerified)
                throw new InvalidOperationException("请先完成 DevEco 环境检测。");
            if (!Regex.IsMatch(alias ?? "", @"^[A-Za-z0-9._-]{1,64}$"))
                throw new ArgumentException("密钥别名只能使用英文字母、数字、点、下划线和横线，最长 64 位。");
            if (string.IsNullOrWhiteSpace(commonName) || commonName.IndexOfAny(new[] { ',', '=', '\n', '\r', '"' }) >= 0)
                throw new ArgumentException("证书名称不能为空，且不能包含逗号、等号或引号。");
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                throw new ArgumentException("密钥密码至少 8 位。");
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("请选择保存目录。");
            folder = Path.GetFullPath(folder);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.TrimEnd(Path.DirectorySeparatorChar);
            if (folder.Equals(projectRoot, StringComparison.OrdinalIgnoreCase) ||
                folder.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请将签名文件保存在 Unity 工程目录之外。");
            Directory.CreateDirectory(folder);
            string keyStore = Path.Combine(folder, alias + ".p12");
            string csr = Path.Combine(folder, alias + ".csr");
            if (File.Exists(keyStore) || File.Exists(csr))
                throw new IOException("目标目录中已存在同名 .p12 或 .csr，请更换别名或目录。");

            string keytool = Path.Combine(HUStudioToolchain.JavaHome, "bin", "keytool.exe");
            if (!File.Exists(keytool)) throw new FileNotFoundException("DevEco JDK 缺少 keytool.exe。", keytool);
            try
            {
                RunKeytool(keytool, new[] {
                    "-genkeypair", "-alias", alias, "-keyalg", "EC", "-groupname", "secp256r1",
                    "-sigalg", "SHA256withECDSA", "-keystore", keyStore, "-storetype", "PKCS12",
                    "-dname", "CN=" + commonName.Trim(), "-validity", "3650",
                    "-storepass:env", PasswordEnvironmentName, "-keypass:env", PasswordEnvironmentName
                }, password);
                RunKeytool(keytool, new[] {
                    "-certreq", "-alias", alias, "-file", csr, "-keystore", keyStore,
                    "-storetype", "PKCS12", "-sigalg", "SHA256withECDSA",
                    "-storepass:env", PasswordEnvironmentName
                }, password);
                if (!File.Exists(keyStore) || !File.Exists(csr))
                    throw new IOException("密钥或 CSR 未生成。");
            }
            catch
            {
                if (File.Exists(csr)) File.Delete(csr);
                if (File.Exists(keyStore)) File.Delete(keyStore);
                throw;
            }
            KeyStorePath = keyStore;
            KeyAlias = alias;
            StorePassword = password;
            KeyPassword = "";
            Debug.Log("[HUStudio] 签名密钥与 CSR 已生成：" + folder);
            return new[] { keyStore, csr };
        }

        internal static string SignHap(string unsignedHap)
        {
            string reason;
            if (!Validate(out reason)) throw new InvalidOperationException(reason);
            if (!File.Exists(unsignedHap)) throw new FileNotFoundException("待签名 HAP 不存在。", unsignedHap);
            string java = Path.Combine(HUStudioToolchain.JavaHome, "bin", "java.exe");
            string jar = Path.Combine(HUStudioToolchain.SdkRoot, "default", "openharmony",
                "toolchains", "lib", "hap-sign-tool.jar");
            if (!File.Exists(jar)) throw new FileNotFoundException("HarmonyOS SDK 缺少签名工具。", jar);
            string output = Path.Combine(Path.GetDirectoryName(unsignedHap),
                Path.GetFileNameWithoutExtension(unsignedHap).Replace("-unsigned", "-signed") + ".hap");
            string password = string.IsNullOrEmpty(KeyPassword) ? StorePassword : KeyPassword;
            var args = new List<string> {
                "-jar", jar.Replace('\\', '/'), "sign-app", "-mode", "localSign",
                "-keyAlias", KeyAlias, "-appCertFile", CertificatePath.Replace('\\', '/'),
                "-profileFile", ProfilePath.Replace('\\', '/'), "-inFile", unsignedHap.Replace('\\', '/'),
                "-signAlg", "SHA256withECDSA", "-keystoreFile", KeyStorePath.Replace('\\', '/'),
                "-keystorePwd", StorePassword, "-keyPwd", password,
                "-outFile", output.Replace('\\', '/'), "-compatibleVersion", "24", "-signCode", "1"
            };
            string log = Path.Combine(Path.GetDirectoryName(unsignedHap), "HUStudio-sign.log");
            RunJavaArgFile(java, args, log, new[] { StorePassword, password });
            if (!File.Exists(output) || new FileInfo(output).Length < 1000)
                throw new IOException("签名工具未生成 HAP；日志：" + log);
            Debug.Log("[HUStudio] 已签名 HAP：" + output);
            return output;
        }

        private static void RunKeytool(string tool, IEnumerable<string> arguments, string password)
        {
            var start = new ProcessStartInfo {
                FileName = tool,
                Arguments = string.Join(" ", arguments.Select(QuoteCommandArgument).ToArray()),
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };
            start.EnvironmentVariables[PasswordEnvironmentName] = password;
            int exitCode;
            string output = Run(start, 30000, out exitCode);
            if (exitCode != 0) throw new InvalidOperationException("keytool 失败：" + output.Replace(password, "[redacted]"));
            if (!string.IsNullOrWhiteSpace(output)) Debug.Log("[HUStudio] keytool: " + output.Trim());
        }

        private static void RunJavaArgFile(string java, IEnumerable<string> arguments, string log, string[] secrets)
        {
            string argFile = Path.Combine(Path.GetTempPath(), "HUStudio-sign-" + Guid.NewGuid().ToString("N") + ".args");
            try
            {
                File.WriteAllLines(argFile, arguments.Select(QuoteJavaArg).ToArray(), new UTF8Encoding(false));
                var start = new ProcessStartInfo {
                    FileName = java,
                    Arguments = "@" + QuoteCommandArgument(argFile.Replace('\\', '/')),
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                int exitCode;
                string output = Run(start, 600000, out exitCode);
                foreach (string secret in secrets.Where(value => !string.IsNullOrEmpty(value)).Distinct())
                    output = output.Replace(secret, "[redacted]");
                File.WriteAllText(log, output, new UTF8Encoding(false));
                if (exitCode != 0)
                    throw new InvalidOperationException("HAP 签名失败；请检查证书、Profile、密钥和密码。日志：" + log);
                if (!output.Contains("Sign app success") && !output.Contains("SIGN_APP_SUCCESS"))
                    Debug.Log("[HUStudio] 签名日志：" + log);
            }
            finally { if (File.Exists(argFile)) File.Delete(argFile); }
        }

        private static string Run(ProcessStartInfo start, int timeoutMs, out int exitCode)
        {
            var output = new StringBuilder();
            using (var process = new Process { StartInfo = start })
            {
                process.OutputDataReceived += (sender, args) => { if (args.Data != null) lock (output) output.AppendLine(args.Data); };
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) lock (output) output.AppendLine(args.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    throw new TimeoutException("签名工具运行超时。");
                }
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            return output.ToString();
        }

        private static string QuoteCommandArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteJavaArg(string value)
        {
            if (value.Contains("\n") || value.Contains("\r")) throw new ArgumentException("签名参数包含换行符。");
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class HUStudioSigningWizard : EditorWindow
    {
        private string folder = "";
        private string alias = "HUStudioKey";
        private string commonName = "HUStudio";
        private string password = "";
        private string confirm = "";

        internal static void Open()
        {
            GetWindow<HUStudioSigningWizard>(true, "生成签名密钥", true).minSize = new Vector2(480, 220);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("生成密钥库与证书请求文件", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            folder = EditorGUILayout.TextField("保存目录", folder);
            if (GUILayout.Button("选择", GUILayout.Width(54)))
            {
                string selected = EditorUtility.OpenFolderPanel("保存签名文件", folder, "");
                if (!string.IsNullOrEmpty(selected)) folder = selected;
            }
            EditorGUILayout.EndHorizontal();
            alias = EditorGUILayout.TextField("密钥别名", alias);
            commonName = EditorGUILayout.TextField("证书名称", commonName);
            password = EditorGUILayout.PasswordField("密码", password);
            confirm = EditorGUILayout.PasswordField("确认密码", confirm);
            EditorGUILayout.HelpBox("生成 .p12 和 .csr 后，使用 CSR 在华为开发者平台申请 .cer 和 .p7b，再回到导出窗口选择这两个文件。", MessageType.Info);
            if (GUILayout.Button("生成并设为当前密钥", GUILayout.Height(28)))
            {
                try
                {
                    if (password != confirm) throw new ArgumentException("两次密码不一致。");
                    string[] files = HUStudioSigning.GenerateKeyAndCsr(folder, alias, commonName, password);
                    EditorUtility.DisplayDialog("生成完成", "密钥库：" + files[0] + "\nCSR：" + files[1], "确定");
                    Close();
                }
                catch (Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("生成失败", error.Message, "确定"); }
            }
        }
    }

    public static class HUStudioSigningBatchSmoke
    {
        public static void Generate()
        {
            string studio = Environment.GetEnvironmentVariable("HUSTUDIO_TEST_STUDIO");
            string folder = Environment.GetEnvironmentVariable("HUSTUDIO_SIGN_TEST_DIR");
            string password = Environment.GetEnvironmentVariable("HUSTUDIO_SIGN_TEST_PASSWORD");
            if (string.IsNullOrEmpty(studio) || string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Signing smoke environment is incomplete.");
            HUStudioToolchain.SetFromStudio(studio);
            HUStudioToolchain.Probe();
            string[] files = HUStudioSigning.GenerateKeyAndCsr(folder, "HUStudioSmoke", "HUStudio Smoke", password);
            Debug.Log("[HUStudio] Signing smoke: " + string.Join(", ", files));
        }

        public static void RejectInvalidMaterials()
        {
            string studio = Environment.GetEnvironmentVariable("HUSTUDIO_TEST_STUDIO");
            string folder = Environment.GetEnvironmentVariable("HUSTUDIO_SIGN_TEST_DIR");
            string password = Environment.GetEnvironmentVariable("HUSTUDIO_SIGN_TEST_PASSWORD");
            string unsignedHap = Environment.GetEnvironmentVariable("HUSTUDIO_UNSIGNED_HAP");
            if (string.IsNullOrEmpty(studio) || string.IsNullOrEmpty(folder) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(unsignedHap))
                throw new InvalidOperationException("Signing smoke environment is incomplete.");
            HUStudioToolchain.SetFromStudio(studio);
            HUStudioToolchain.Probe();
            HUStudioSigning.KeyStorePath = Path.Combine(folder, "HUStudioSmoke.p12");
            HUStudioSigning.KeyAlias = "HUStudioSmoke";
            HUStudioSigning.StorePassword = password;
            string cert = Path.Combine(folder, "invalid.cer");
            string profile = Path.Combine(folder, "invalid.p7b");
            File.WriteAllText(cert, "invalid test certificate");
            File.WriteAllText(profile, "invalid test profile");
            HUStudioSigning.CertificatePath = cert;
            HUStudioSigning.ProfilePath = profile;
            try { HUStudioSigning.SignHap(unsignedHap); }
            catch (InvalidOperationException)
            {
                string log = Path.Combine(Path.GetDirectoryName(unsignedHap), "HUStudio-sign.log");
                if (!File.Exists(log) || File.ReadAllText(log).Contains(password))
                    throw new InvalidOperationException("Signing failure log is missing or contains the password.");
                Debug.Log("[HUStudio] Invalid signing materials were rejected without logging the password.");
                return;
            }
            throw new InvalidOperationException("Invalid signing materials unexpectedly succeeded.");
        }
    }
}
