using Microsoft.Win32;
using System.IO;

namespace RightContextMenuHelper;

internal static class ShellContextMenuRegistrar
{
    private const string ZipMenuKeyPath = @"Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionFileContent";
    private const string CopyFilePathMenuKeyPath = @"Software\Classes\*\shell\CopyFilePath";
    private const string InspectLocksMenuKeyPath = @"Software\Classes\*\shell\RightContextMenuHelper.InspectLocks";
    private static readonly string[] LegacyMenuKeyPaths =
    [
        @"Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionNum",
        @"Software\Classes\SystemFileAssociations\.zip\shell\显示版本号",
        @"Software\Classes\*\shell\ShowVersionNum.InspectLocks",
    ];

    public static IReadOnlyList<string> RegisterContextMenus(bool overwriteExisting)
    {
        List<string> errors = [];
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            errors.Add("无法确定程序路径，右键菜单注册失败。");
            return errors;
        }

        TryRegisterMenu(
            ZipMenuKeyPath,
            "显示 version 文件内容",
            $"\"{executablePath}\" \"%1\"",
            "zip-version.ico",
            overwriteExisting,
            singleSelection: false,
            errors);

        TryRegisterMenu(
            CopyFilePathMenuKeyPath,
            "复制文件路径",
            $"\"{executablePath}\" --copy-file-path \"%1\"",
            "copy-file-path.ico",
            overwriteExisting,
            singleSelection: false,
            errors);

        TryRegisterMenu(
            InspectLocksMenuKeyPath,
            "查看并结束占用进程",
            $"\"{executablePath}\" --inspect-locks \"%1\"",
            "inspect-locks.ico",
            overwriteExisting,
            singleSelection: true,
            errors);

        if (errors.Count == 0)
        {
            DeleteLegacyMenuKeys(errors);
        }

        return errors;
    }

    private static void TryRegisterMenu(
        string menuKeyPath,
        string displayName,
        string command,
        string iconFileName,
        bool overwriteExisting,
        bool singleSelection,
        List<string> errors)
    {
        try
        {
            if (!overwriteExisting)
            {
                using RegistryKey? existingCommand = Registry.CurrentUser.OpenSubKey(menuKeyPath + @"\command");
                if (existingCommand?.GetValue(null) is string existingValue && !string.IsNullOrWhiteSpace(existingValue))
                {
                    return;
                }
            }

            string iconPath = Path.Combine(AppContext.BaseDirectory, "Icons", iconFileName);
            if (!File.Exists(iconPath))
            {
                throw new FileNotFoundException("找不到菜单图标文件。", iconPath);
            }

            using RegistryKey menuKey = Registry.CurrentUser.CreateSubKey(menuKeyPath)
                ?? throw new InvalidOperationException("无法创建菜单注册表项。");
            menuKey.SetValue(null, displayName);
            menuKey.SetValue("Icon", iconPath);
            if (singleSelection)
            {
                menuKey.SetValue("MultiSelectModel", "Single");
            }

            using RegistryKey commandKey = Registry.CurrentUser.CreateSubKey(menuKeyPath + @"\command")
                ?? throw new InvalidOperationException("无法创建菜单命令注册表项。");
            commandKey.SetValue(null, command);
        }
        catch (Exception ex)
        {
            errors.Add($"{displayName}：{ex.Message}");
        }
    }

    private static void DeleteLegacyMenuKeys(List<string> errors)
    {
        foreach (string legacyMenuKeyPath in LegacyMenuKeyPaths)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(legacyMenuKeyPath, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                errors.Add($"清理旧版右键菜单失败（{legacyMenuKeyPath}）：{ex.Message}");
            }
        }
    }
}
