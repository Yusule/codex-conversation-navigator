---
name: conversation-navigator
description: 启动本机 Codex 对话目录悬浮工具。用户说“打开对话目录”“启动目录悬浮球”“帮我定位之前的消息”或要求使用 Codex Conversation Navigator 时使用。
---

# Codex 对话目录

这个技能只负责启动已经随插件安装的本地 Windows 悬浮工具。

## 启动

从本技能目录执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "..\..\scripts\launch.ps1"
```

启动成功后，告诉用户：

- 屏幕上会出现写有“目”的圆形按钮；
- 点击按钮刷新并展开当前 Codex 对话目录；
- 点击目录中的一行会定位到对应消息；
- 拖动圆形按钮可改变位置，右键可退出。

不要声称该工具修改了 Codex 本体。它只读取当前窗口的可访问性信息并调用 Windows UI Automation 进行滚动定位，不需要联网。
