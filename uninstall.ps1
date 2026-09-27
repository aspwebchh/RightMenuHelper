[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param()

$ErrorActionPreference = 'Stop'

$executableNames = @('RightContextMenuHelper.exe', 'ShowVersionNum.exe')
$menus = @(
    @{
        Key = 'Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionFileContent'
        Arguments = ' "%1"'
    },
    @{
        Key = 'Software\Classes\*\shell\CopyFilePath'
        Arguments = ' --copy-file-path "%1"'
    },
    @{
        Key = 'Software\Classes\*\shell\RightContextMenuHelper.InspectLocks'
        Arguments = ' --inspect-locks "%1"'
    },
    @{
        Key = 'Software\Classes\*\shell\ShowVersionNum.InspectLocks'
        Arguments = ' --inspect-locks "%1"'
    },
    @{
        Key = 'Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionNum'
        Arguments = ' "%1"'
    },
    @{
        Key = 'Software\Classes\SystemFileAssociations\.zip\shell\显示版本号'
        Arguments = ' "%1"'
    }
)

$matchedCount = 0
$removedCount = 0

foreach ($menu in $menus) {
    $commandKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($menu.Key + '\command')
    try {
        if ($null -eq $commandKey) {
            continue
        }

        $command = [string]$commandKey.GetValue('')
    }
    finally {
        if ($null -ne $commandKey) {
            $commandKey.Dispose()
        }
    }

    $pattern = '^"(?<Executable>[^"]+)"' + [regex]::Escape($menu.Arguments) + '$'
    $match = [regex]::Match($command, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        Write-Output "跳过命令不匹配的菜单：$($menu.Key)"
        continue
    }

    $executableName = [System.IO.Path]::GetFileName($match.Groups['Executable'].Value)
    if ($executableName -notin $executableNames) {
        Write-Output "跳过非本项目菜单：$($menu.Key)"
        continue
    }

    $matchedCount++
    if ($PSCmdlet.ShouldProcess($menu.Key, '移除右键菜单')) {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($menu.Key, $false)

        $remainingKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($menu.Key)
        try {
            if ($null -ne $remainingKey) {
                throw "右键菜单删除后仍存在：$($menu.Key)"
            }
        }
        finally {
            if ($null -ne $remainingKey) {
                $remainingKey.Dispose()
            }
        }

        $removedCount++
        Write-Output "已移除：$($menu.Key)"
    }
}

Write-Output "找到 $matchedCount 个本项目菜单项，已移除 $removedCount 个。"
