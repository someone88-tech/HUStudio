# HUStudio HarmonyOS Unity SDK: Installation and usage

## Requirements

- Windows and Unity with the matching **WebGL Build Support** module installed through Unity Hub.
- [DevEco Studio](https://developer.huawei.com/consumer/cn/deveco-studio/resources/) with the HarmonyOS **API 26 SDK** installed. Normally you enter only the DevEco Studio installation directory; the exporter finds its JBR, Node, Hvigor, ohpm, and SDK.
- At least one enabled scene in Unity **Build Settings**.
- For a signed HAP, a certificate (`.cer`) and Profile (`.p7b`) associated with the application's Bundle Name.

## Install the package

Choose one method:

1. **Git / UPM:** In Unity, open **Window > Package Manager**, select **+ > Add package from git URL**, and enter `https://github.com/someone88-tech/HUStudio.git`. Git must be installed locally. Import **Quick Start** from the package's **Samples** tab.
2. **unitypackage:** Download `HUStudioUnitySDK-0.1.1.unitypackage` from `Releases~/0.1.1`, then use **Assets > Import Package > Custom Package**. The sample appears under `Assets/HUStudioUnitySDK/Samples/QuickStart`.
3. **Complete project:** Extract `HUStudioUnitySDK-0.1.1-SampleProject.zip` and open the extracted Unity project folder in Unity Hub. It includes the package and Quick Start.

Do not import the same version of the `.unitypackage` into a project that already has the UPM package; the assemblies would be compiled twice.

## Try the sample

Open `Scenes/HarmonyDemoScene.unity` in Quick Start. It demonstrates Unity/native messaging, networking, storage, audio, and keyboard input. For your own project, install the package and enable your game's scenes in Build Settings.

## Three-step export

Open **HarmonyOS / 环境与导出...** in Unity.

### 1. Check DevEco

Click **自动探测** (Auto-detect) to fill the paths and check the environment in one step. You can also choose the DevEco Studio installation directory manually and click **检测环境** (Check environment). The window checks the SDK, JBR, Node, Hvigor, and ohpm. If the SDK is elsewhere, expand **自定义工具路径** (Custom tool paths). Install API 26 SDK in DevEco Studio if the check asks for it.

### 2. Build WebGL

Enable the desired scenes in Unity **File > Build Settings**. The exporter reads that scene list directly. Select an output folder and click **切换到 WebGL 并构建** (Switch to WebGL and build). If the active target differs, Unity switches first and resumes the build afterward. Rebuild WebGL when scenes or related assets change.

### 3. Configure and export

Enter the application name, Bundle Name, version name, and version code. Pick PNG Texture/Sprite assets from the project's `Assets` folder for the app icon and launch icon, or drag them onto the image cards. The cards show previews. The launch icon appears centered on the system start window, with a separately configurable background color. An empty selection uses the template default; if only an app icon is selected, it is also used as the launch icon.

- **生成鸿蒙工程** (Generate DevEco project): exports an editable DevEco Studio project.
- **直接导出未签名 HAP** (Export unsigned HAP): generates the project and builds a HAP.
- **直接导出已签名 HAP** (Export signed HAP): expand **签名设置** (Signing), generate a `.p12` and `.csr` with **生成密钥与 CSR...**, or select an existing `.p12`. Obtain matching `.cer` and `.p7b` files from the Huawei developer platform, enter the alias and passwords, then build.

Regenerate the DevEco project after changing icons, background color, app settings, or WebGL output. The selected output folder keeps the project, HAP, and build logs.

## Outputs and saved settings

- Hvigor log: `HUStudio-hvigor.log` in the generated project root.
- Signing log: `HUStudio-sign.log` in the HAP output folder.
- DevEco tool paths: Unity EditorPrefs on the current computer.
- Export settings: `ProjectSettings/HUStudioExportSettings.json` in the current Unity project. It contains local paths and should not be published as sample configuration.
- Signing passwords remain in the current Unity session. The generated DevEco project does not contain private keys or passwords.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Environment check fails | Select the DevEco Studio installation directory and install API 26 SDK. Use custom paths for nonstandard installations. |
| WebGL build unavailable | Install WebGL Build Support for this Unity version and enable at least one scene in Build Settings. |
| Generated project is stale | Rebuild WebGL or regenerate the project after changing scenes, assets, app settings, or images. |
| Signing fails | Verify that the `.p12`, alias, passwords, `.cer`, `.p7b`, and Bundle Name match. |

## License

Free personal and commercial use is permitted with the watermark retained. Removing it requires a separate written commercial license. Read the [English license](../LICENSE.en.md) and [Chinese license](../LICENSE.zh-CN.md).
