# HUStudio HarmonyOS Unity SDK

将 Unity WebGL 场景导出为可编辑的 DevEco 工程，或直接构建 HarmonyOS HAP。插件提供环境检测、工程内图标与启动画面选择、HAP 构建及签名设置。

## 选择安装方式

| 方式 | 操作 | Sample |
| --- | --- | --- |
| Git / UPM | Unity **Window > Package Manager > + > Add package from git URL**，输入 `https://github.com/someone88-tech/HUStudio.git` | 在 Package Manager 导入 **Quick Start** |
| unitypackage | 下载 `Releases~/0.1.1/HUStudioUnitySDK-0.1.1.unitypackage`，在 Unity 中 **Assets > Import Package > Custom Package** | 导入时已包含 Quick Start |
| 完整 Unity 工程 | 解压 `Releases~/0.1.1/HUStudioUnitySDK-0.1.1-SampleProject.zip`，用 Unity Hub 打开 | 可直接查看和修改 |

Git 安装要求本机已安装 Git。请在同一工程中只使用一种安装方式，以免脚本重复编译。

## 导出流程

打开 **HarmonyOS / 环境与导出...**：

1. 选择 DevEco Studio 安装目录，点击 **检测环境**。如缺少 HarmonyOS API 26 SDK，请先在 DevEco Studio 中安装。
2. 在 Unity **Build Settings** 中启用要构建的场景。确认已安装 WebGL Build Support，点击 **切换到 WebGL 并构建**。
3. 填写应用名称、Bundle Name 和版本；从工程 `Assets` 选择或拖入 PNG 图标，设置启动画面与背景色。点击 **生成鸿蒙工程**，或直接导出未签名 HAP。

需要已签名 HAP 时，可在签名区生成密钥和 CSR，或选择已有密钥；还须提供与 Bundle Name 对应的证书和 Profile。

详细步骤、常见问题和输出路径见 [安装与使用教程](Documentation~/GettingStarted.zh-CN.md)。

## 授权

本 SDK 可免费用于个人和商业项目，但必须保留其正常显示的水印。去除、隐藏或替换水印须先取得单独的书面商业授权。完整条款见 [中文授权协议](LICENSE.zh-CN.md) 和 [英文版](LICENSE.en.md)。

商业授权与使用咨询：**QQ: 3152939728**。
