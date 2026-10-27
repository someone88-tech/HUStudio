# HUStudio HarmonyOS Unity SDK：安装与使用

## 准备

- Windows、Unity，且在 Unity Hub 中安装与该版本对应的 **WebGL Build Support**。
- 安装 [DevEco Studio](https://developer.huawei.com/consumer/cn/deveco-studio/resources/)。在 DevEco Studio 中安装 HarmonyOS **API 26 SDK**。通常只需在导出窗口填写 DevEco Studio 安装目录，插件会查找随 DevEco 安装的 JBR、Node、Hvigor、ohpm 和 SDK。
- Unity 工程的 **Build Settings** 中至少有一个已启用的场景。
- 如需已签名 HAP，还需要与应用 Bundle Name 匹配的证书（`.cer`）和 Profile（`.p7b`）。

## 安装插件

任选其一：

1. **Git / UPM**：Unity 菜单 **Window > Package Manager**，点击 **+ > Add package from git URL**，输入 `https://github.com/someone88-tech/HUStudio.git`。本机需要安装 Git。安装后在该包的 **Samples** 标签导入 **Quick Start**。
2. **unitypackage**：从仓库 `Releases~/0.1.2` 下载 `HUStudioUnitySDK-0.1.2.unitypackage`，Unity 菜单 **Assets > Import Package > Custom Package** 导入。示例位于 `Assets/HUStudioUnitySDK/Samples/QuickStart`。
3. **完整工程**：解压 `HUStudioUnitySDK-0.1.2-SampleProject.zip`，用 Unity Hub 打开解压后的工程目录。该工程已包含插件和 Quick Start 示例。

已有 UPM 包的工程不要再导入同一版本的 `.unitypackage`，否则会产生重复程序集。

## 快速体验

打开 Quick Start 的 `Scenes/HarmonyDemoScene.unity`。示例包含 Unity 与原生消息、网络、存储、音频及输入法交互。自己的游戏工程只需安装插件，并在 Build Settings 中启用要导出的场景。

## 三步导出

打开 Unity 菜单 **HarmonyOS / 环境与导出...**。

### 1. 检测 DevEco 环境

点击 **自动探测** 即可自动填写路径并完成环境检测；也可手动选择 **DevEco Studio** 安装目录，再点击 **检测环境**。窗口会检查 SDK、JBR、Node、Hvigor 和 ohpm。若 SDK 位于其他目录，可展开 **自定义工具路径** 修改。若提示缺少 API 26 SDK，先在 DevEco Studio 中安装再检测。

### 2. 构建 WebGL

在 Unity **File > Build Settings** 中勾选要构建的场景。导出窗口直接读取这些场景，不需要再次选择。选择输出目录，点击 **切换到 WebGL 并构建**；若 Unity 当前平台不是 WebGL，切换完成后会继续构建。场景或相关资源变化后需重新构建。

### 3. 设置应用并导出

填写应用名称、Bundle Name、版本名称和版本代码。应用图标及闪屏图标从工程 `Assets` 中选择 PNG Texture/Sprite，也可拖入图像卡片；卡片会显示缩略图。闪屏图标在系统启动页居中显示，背景色可单独设置；不选择图标时使用模板默认图，单独设置应用图标时启动图使用该图标。

- **生成鸿蒙工程**：导出可在 DevEco Studio 打开和编辑的工程。
- **直接导出未签名 HAP**：先生成工程，再构建 HAP。
- **直接导出已签名 HAP**：展开 **签名设置**，点击 **生成密钥与 CSR...** 创建 `.p12` 和 `.csr`，或选择现有 `.p12`。从华为开发者平台取得与密钥和 Bundle Name 匹配的 `.cer` 与 `.p7b`，填写别名和密码后构建。

更换图标、背景色、应用参数或 WebGL 内容后，重新生成鸿蒙工程。输出目录中保留工程、HAP 和构建日志。

### 真机测试

在导出窗口的 **测试包：调试构建与真机部署** 区域点击 **刷新设备**。手机开启 USB 调试并授权后，可选择目标设备。首次使用时点击 **首次设置：在 DevEco 中打开工程**，在 DevEco 中完成自动调试签名，再回到 Unity 点击 **自动检测测试签名**。也可使用下方手动填写的签名材料。最后点击 **构建并安装测试 HAP**。

如果重新生成了鸿蒙工程，DevEco 的自动签名需要在新工程中重新配置并检测。未签名 HAP 不会被当作已签名测试包安装。

## 输出与配置

- Hvigor 日志：生成工程根目录的 `HUStudio-hvigor.log`。
- 签名日志：HAP 输出目录的 `HUStudio-sign.log`。
- DevEco 工具路径：当前电脑的 Unity EditorPrefs。
- 导出选项：当前 Unity 工程的 `ProjectSettings/HUStudioExportSettings.json`。此文件含本机路径，不应作为公共示例配置发布。
- 签名密码只保留在当前 Unity 会话；导出的 DevEco 工程不包含私钥或密码。

## 常见问题

| 现象 | 检查 |
| --- | --- |
| 检测环境失败 | 确认选择的是 DevEco Studio 安装目录；确认 API 26 SDK 已安装。自定义路径在高级区域填写。 |
| WebGL 按钮不可用 | 在 Unity Hub 为当前 Unity 版本安装 WebGL Build Support，并在 Build Settings 启用场景。 |
| 工程提示过期 | 场景、WebGL、应用参数或图片有变化；重新构建 WebGL 或重新生成工程。 |
| 无法签名 | 检查 `.p12`、别名、密码、`.cer`、`.p7b` 与 Bundle Name 是否互相匹配。 |
| 安装后黑屏，日志提示 `libc++_shared.so` 缺失 | 更新到 0.1.2 或更新 Git 包，重新生成鸿蒙工程与 HAP。旧工程不会自动补入该运行库。 |

## 授权

可免费用于个人及商业项目，但必须保留水印。去水印需要单独的书面商业授权。详见 [中文授权协议](../LICENSE.zh-CN.md) 和 [英文版](../LICENSE.en.md)。
