# Copilot Instructions

## 项目指南
- 用户在分析 WPF 绑定问题时，希望优先按第三方控件可能打断 DataContext 继承来处理，尤其是 HandyControl 的 Drawer。
- 用户要求：采集轮询延迟属于配方配置，不属于软件级 UserSettings；软件启动恢复不仅要记住当前页面，还要保存已打开页面列表并在退出时统一持久化。
- 用户要求：最小化到后台请使用 HandyControl NotifyIcon 托盘图标实现，不使用 Notification 桌面通知或 WinForms 托盘方案。
- 用户希望在模型和服务之间保持更清晰的分离，避免将协议特定的行为（如西门子特定逻辑）直接放入共享域模型，如 PlcDevice。