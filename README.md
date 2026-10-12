# ChatGPT Text Size

Windows 商店版 ChatGPT 的 **Ctrl＋滚轮缩放助手**。随 ChatGPT 启动，退出 ChatGPT 后自动结束，没有悬浮按钮、托盘图标或任务栏窗口。

不修改 ChatGPT 文件、不注入代码、不读取聊天内容，无网络请求。非 OpenAI 官方插件。

## 安装与使用

1. 从 [Releases](https://github.com/happystitchseele/chatgpt-text-size/releases/latest) 下载 ZIP，解压到固定目录。
2. 在该目录运行 `powershell -ExecutionPolicy Bypass -File .\setup.ps1`（仅此次运行使用该执行策略，不修改系统策略）。
3. 完全退出并重新打开 ChatGPT。鼠标放在聊天区域内，**Ctrl＋向上滚轮放大，Ctrl＋向下滚轮缩小**。
4. **Ctrl＋0** 恢复默认（ChatGPT 自带快捷键）。

无需管理员权限，不设开机或登录自启。需要 Windows 10/11 x64、.NET Framework 4.8 和商店版 ChatGPT；已在 Windows 11、200% 显示缩放环境验证，用户确认启动联动正常。

## 停用

在原目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\setup.ps1 -Disable
```

这会移除本工具的任务并停止助手，随后可删除目录。移动目录后需重新运行安装命令更新路径。

## 原理与边界

Windows 任务计划监听 ChatGPT 的 AppModel-Runtime 启动事件；安装脚本自动检测应用 ID。助手将目标窗口内的 Ctrl＋滚轮转换为应用已有的 Ctrl＋加减快捷键。ChatGPT 进程全部退出后，助手约一秒内退出；关闭窗口但程序仍在后台时继续待命。

控制的是整个界面缩放。焦点在内置浏览器等面板时，应用可能缩放该面板。直接调用 EXE 的特殊启动方式如果不产生商店启动事件，无法触发助手。其他应用版本的进程名或启动事件可能不同，尚未验证。

## 从源码构建

运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1`，使用 Windows 内置 C# 编译器，无需下载构建依赖。源码集中在 `ChatGPTTextSize.cs`，安装与卸载在 `setup.ps1`。

MIT License。
