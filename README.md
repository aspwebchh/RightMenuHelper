# ShowVersionNum

Windows 文件右键菜单工具，包含 ZIP 压缩包中 `version` 文件查看、复制文件路径，以及查看并结束占用文件的进程。

## 安装或更新

需要 .NET SDK 和 .NET 8 Windows Desktop Runtime。在 PowerShell 中运行：

```powershell
.\Install.ps1
```

脚本将程序发布到当前用户的 `%LOCALAPPDATA%\Programs\ShowVersionNum`，并把三个右键菜单注册到该固定路径。Windows 11 中可在文件右键菜单的“显示更多选项”里找到它们。

## 文件占用功能

右击单个文件，选择“查看并结束占用进程”。窗口会列出 Windows Restart Manager 找到的占用进程；点击对应行的“强制结束”后会重新检查占用情况。遇到权限不足时，可点击“以管理员身份重试”。系统关键进程和本程序不能从该窗口结束。

此功能使用 Windows Restart Manager，因此可能找不到 PowerToys File Locksmith 通过全系统句柄和模块扫描发现的部分进程。
