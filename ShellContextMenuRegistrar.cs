using Microsoft.Win32;

namespace ShowVersionNum;

internal static class ShellContextMenuRegistrar
{
    private const string ZipMenuKeyPath = @"Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionFileContent";
    private const string CopyFilePathMenuKeyPath = @"Software\Classes\*\shell\CopyFilePath";
    private static readonly string[] LegacyMenuKeyPaths =
    [
        @"Software\Classes\SystemFileAssociations\.zip\shell\ShowVersionNum",
        @"Software\Classes\SystemFileAssociations\.zip\shell\显示版本号",
    ];

    public static void RegisterContextMenus()
    {
        DeleteLegacyMenuKeys();

        string? executablePath;
        try
        {
            executablePath = Environment.ProcessPath;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        TryRegisterMenu(
            ZipMenuKeyPath,
            "显示 version 文件内容",
            $"\"{executablePath}\" \"%1\"");

        TryRegisterMenu(
            CopyFilePathMenuKeyPath,
            "复制文件路径",
            $"\"{executablePath}\" --copy-file-path \"%1\"");
    }

    private static void TryRegisterMenu(string menuKeyPath, string displayName, string command)
    {
        try
        {
            using RegistryKey? menuKey = Registry.CurrentUser.CreateSubKey(menuKeyPath);
            menuKey?.SetValue(null, displayName);

            using RegistryKey? commandKey = Registry.CurrentUser.CreateSubKey(menuKeyPath + @"\command");
            commandKey?.SetValue(null, command);
        }
        catch
        {
            // Each context menu is best-effort so one failure does not block the other.
        }
    }

    private static void DeleteLegacyMenuKeys()
    {
        foreach (string legacyMenuKeyPath in LegacyMenuKeyPaths)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(legacyMenuKeyPath, throwOnMissingSubKey: false);
            }
            catch
            {
                // Leave cleanup best-effort so the new menu can still be registered.
            }
        }
    }
}
