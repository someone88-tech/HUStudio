# 更新记录 / Changelog

## 0.1.2 — 2026-10-27

- 修复 Git 安装后导出的 HAP 黑屏：DevEco 模板现在包含 `libufhn.so` 所需的 `libc++_shared.so`。
- Fixed a black screen in HAPs exported from Git installs by including the C++ runtime required by `libufhn.so` in the DevEco template.
