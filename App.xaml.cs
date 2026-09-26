using System.IO;
using System.Windows;

namespace ShowVersionNum
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string CopyFilePathArgument = "--copy-file-path";
        private const string InspectLocksArgument = "--inspect-locks";
        private const string RegisterContextMenusArgument = "--register-context-menus";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (IsCommand(e.Args, RegisterContextMenusArgument))
            {
                IReadOnlyList<string> errors = ShellContextMenuRegistrar.RegisterContextMenus(overwriteExisting: true);
                Shutdown(errors.Count == 0 ? 0 : 1);
                return;
            }

            if (e.Args.Length == 0)
            {
                ShellContextMenuRegistrar.RegisterContextMenus(overwriteExisting: false);
            }

            if (IsCommand(e.Args, CopyFilePathArgument))
            {
                int exitCode = CopyFilePath(e.Args.ElementAtOrDefault(1));
                Shutdown(exitCode);
                return;
            }

            if (IsCommand(e.Args, InspectLocksArgument))
            {
                FileLocksWindow locksWindow = new(e.Args.ElementAtOrDefault(1) ?? string.Empty);
                locksWindow.Show();
                return;
            }

            string message = GetDisplayMessage(e.Args);
            MainWindow window = new(message);
            window.Show();
        }

        private static bool IsCommand(string[] args, string command)
        {
            return args.Length > 0
                && args[0].Equals(command, StringComparison.OrdinalIgnoreCase);
        }

        private static int CopyFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowCopyError("未提供要复制路径的文件。");
                return 1;
            }

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath))
                {
                    ShowCopyError($"找不到文件：{fullPath}");
                    return 1;
                }

                Clipboard.SetText(fullPath);
                return 0;
            }
            catch (Exception ex)
            {
                ShowCopyError($"无法复制文件路径：{ex.Message}");
                return 1;
            }
        }

        private static void ShowCopyError(string message)
        {
            MessageBox.Show(
                message,
                "复制文件路径",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private static string GetDisplayMessage(string[] args)
        {
            string? zipPath = args.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                return "请通过右键菜单选择一个zip压缩包查看版本号。";
            }

            if (!File.Exists(zipPath))
            {
                return $"找不到压缩包文件：{zipPath}";
            }

            return ZipVersionReader.ReadVersionText(zipPath);
        }
    }
}
