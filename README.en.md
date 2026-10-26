# HUStudio HarmonyOS Unity SDK

Export a Unity WebGL scene as an editable DevEco project or build a HarmonyOS HAP directly. The package includes environment checks, project asset pickers for icons and the launch screen, HAP building, and signing controls.

## Installation options

| Method | Action | Sample |
| --- | --- | --- |
| Git / UPM | In Unity, open **Window > Package Manager > + > Add package from git URL** and enter `https://github.com/someone88-tech/HUStudio.git` | Import **Quick Start** in Package Manager |
| unitypackage | Download `Releases~/0.1.1/HUStudioUnitySDK-0.1.1.unitypackage`, then use **Assets > Import Package > Custom Package** | Quick Start is included on import |
| Complete Unity project | Extract `Releases~/0.1.1/HUStudioUnitySDK-0.1.1-SampleProject.zip` and open it with Unity Hub | Ready to inspect and modify |

Git installation requires a local Git client. Use only one installation method per Unity project to avoid duplicate script assemblies.

## Export workflow

Open **HarmonyOS / 环境与导出...**:

1. Select the DevEco Studio installation directory and click **检测环境** (Check environment). Install HarmonyOS API 26 SDK in DevEco Studio if prompted.
2. Enable the desired scenes in Unity **Build Settings**. Install WebGL Build Support, then click **切换到 WebGL 并构建** (Switch to WebGL and build).
3. Enter the application name, Bundle Name, and version. Pick or drag PNG images from the project's `Assets` folder for the app icon and launch screen, and set the background color. Click **生成鸿蒙工程** (Generate DevEco project), or export an unsigned HAP directly.

For a signed HAP, generate or select a keystore and provide the certificate and Profile associated with the Bundle Name.

See the [installation and usage guide](Documentation~/GettingStarted.en.md) for detailed steps, troubleshooting, and output locations.

## License

The SDK is free for personal and commercial projects if its normal watermark remains visible. Removing, hiding, or replacing the watermark requires a separate written commercial license. Read the [English license](LICENSE.en.md) and [Chinese license](LICENSE.zh-CN.md).

Commercial licensing and inquiries: **QQ: 3152939728**.
