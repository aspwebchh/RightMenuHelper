$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    throw '无法确定当前用户的 LocalAppData 目录。'
}

$projectPath = Join-Path $PSScriptRoot 'RightContextMenuHelper.csproj'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\RightContextMenuHelper'
$installedExe = Join-Path $installDirectory 'RightContextMenuHelper.exe'

dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $installDirectory -v:q
if ($LASTEXITCODE -ne 0) {
    throw "发布失败，dotnet 退出码：$LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
    throw "发布后找不到程序：$installedExe"
}

$quotedExe = '"' + $installedExe + '"'
$iconDirectory = Join-Path $installDirectory 'Icons'
$menuCommands = @(
    @{
        Key = 'Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionFileContent'
        Command = "$quotedExe `"%1`""
        Icon = (Join-Path $iconDirectory 'zip-version.ico')
    },
    @{
        Key = 'Software\Classes\*\shell\CopyFilePath'
        Command = "$quotedExe --copy-file-path `"%1`""
        Icon = (Join-Path $iconDirectory 'copy-file-path.ico')
    },
    @{
        Key = 'Software\Classes\*\shell\RightContextMenuHelper.InspectLocks'
        Command = "$quotedExe --inspect-locks `"%1`""
        Icon = (Join-Path $iconDirectory 'inspect-locks.ico')
    }
)

foreach ($menu in $menuCommands) {
    if (-not (Test-Path -LiteralPath $menu.Icon -PathType Leaf)) {
        throw "发布后找不到菜单图标：$($menu.Icon)"
    }
}

$registration = Start-Process -FilePath $installedExe -ArgumentList '--register-context-menus' -Wait -PassThru -WindowStyle Hidden
if ($registration.ExitCode -ne 0) {
    throw "右键菜单注册失败，程序退出码：$($registration.ExitCode)"
}

foreach ($menu in $menuCommands) {
    $commandKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($menu.Key + '\command')
    try {
        $actualCommand = if ($null -ne $commandKey) { [string]$commandKey.GetValue('') } else { '' }
        if ($actualCommand -cne $menu.Command) {
            throw "右键菜单命令验证失败：$($menu.Key)"
        }
    }
    finally {
        if ($null -ne $commandKey) { $commandKey.Dispose() }
    }

    $menuKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($menu.Key)
    try {
        $actualIcon = if ($null -ne $menuKey) { [string]$menuKey.GetValue('Icon') } else { '' }
        if ($actualIcon -cne $menu.Icon) {
            throw "右键菜单图标验证失败：$($menu.Key)"
        }
    }
    finally {
        if ($null -ne $menuKey) { $menuKey.Dispose() }
    }
}

$lockMenuKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\*\shell\RightContextMenuHelper.InspectLocks')
try {
    if ($null -eq $lockMenuKey -or $lockMenuKey.GetValue('MultiSelectModel') -cne 'Single') {
        throw '文件占用菜单的单选设置验证失败。'
    }
}
finally {
    if ($null -ne $lockMenuKey) { $lockMenuKey.Dispose() }
}

$legacyLockMenuKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\*\shell\ShowVersionNum.InspectLocks')
try {
    if ($null -ne $legacyLockMenuKey) {
        throw '旧版文件占用菜单未清理。'
    }
}
finally {
    if ($null -ne $legacyLockMenuKey) { $legacyLockMenuKey.Dispose() }
}

Write-Output "已安装到：$installedExe"
Write-Output '右键菜单已注册。Windows 11 请在“显示更多选项”中查看。'
