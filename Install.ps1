$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    throw '无法确定当前用户的 LocalAppData 目录。'
}

$projectPath = Join-Path $PSScriptRoot 'ShowVersionNum.csproj'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\ShowVersionNum'
$installedExe = Join-Path $installDirectory 'ShowVersionNum.exe'

dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $installDirectory -v:q
if ($LASTEXITCODE -ne 0) {
    throw "发布失败，dotnet 退出码：$LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
    throw "发布后找不到程序：$installedExe"
}

$registration = Start-Process -FilePath $installedExe -ArgumentList '--register-context-menus' -Wait -PassThru -WindowStyle Hidden
if ($registration.ExitCode -ne 0) {
    throw "右键菜单注册失败，程序退出码：$($registration.ExitCode)"
}

$quotedExe = '"' + $installedExe + '"'
$menuCommands = @(
    @{
        Key = 'Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionFileContent'
        Command = "$quotedExe `"%1`""
    },
    @{
        Key = 'Software\Classes\*\shell\CopyFilePath'
        Command = "$quotedExe --copy-file-path `"%1`""
    },
    @{
        Key = 'Software\Classes\*\shell\ShowVersionNum.InspectLocks'
        Command = "$quotedExe --inspect-locks `"%1`""
    }
)

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
}

$lockMenuKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Classes\*\shell\ShowVersionNum.InspectLocks')
try {
    if ($null -eq $lockMenuKey -or $lockMenuKey.GetValue('MultiSelectModel') -cne 'Single') {
        throw '文件占用菜单的单选设置验证失败。'
    }
}
finally {
    if ($null -ne $lockMenuKey) { $lockMenuKey.Dispose() }
}

Write-Output "已安装到：$installedExe"
Write-Output '右键菜单已注册。Windows 11 请在“显示更多选项”中查看。'
