using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;

namespace ShowVersionNum;

public partial class FileLocksWindow : Window
{
    private bool _busy;

    public FileLocksWindow(string filePath)
    {
        FilePath = filePath;
        InitializeComponent();
        DataContext = this;
        ElevateButton.Visibility = IsElevated() ? Visibility.Collapsed : Visibility.Visible;
    }

    public string FilePath { get; }

    public ObservableCollection<LockingProcess> Processes { get; } = new();

    private static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ProcessGrid.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        ElevateButton.IsEnabled = !busy;
    }

    private void ShowProcesses(IReadOnlyList<LockingProcess> processes)
    {
        Processes.Clear();
        foreach (LockingProcess process in processes)
        {
            Processes.Add(process);
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        StatusTextBlock.Text = "正在检查文件占用情况……";
        try
        {
            IReadOnlyList<LockingProcess> processes = await Task.Run(() => FileLockInspector.FindProcesses(FilePath));
            ShowProcesses(processes);
            StatusTextBlock.Text = processes.Count == 0
                ? "未发现占用该文件的进程。"
                : $"找到 {processes.Count} 个占用进程。请选择要结束的进程。";
        }
        catch (Exception ex)
        {
            ShowProcesses([]);
            StatusTextBlock.Text = $"检查失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void EndTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: LockingProcess selectedProcess })
        {
            return;
        }

        SetBusy(true);
        StatusTextBlock.Text = $"正在结束进程 {selectedProcess.Name} ({selectedProcess.ProcessId})……";
        try
        {
            TerminationOutcome outcome = await Task.Run(() => FileLockInspector.EndTask(FilePath, selectedProcess));
            IReadOnlyList<LockingProcess> processes = await Task.Run(() => FileLockInspector.FindProcesses(FilePath));
            ShowProcesses(processes);
            StatusTextBlock.Text = outcome switch
            {
                TerminationOutcome.Ended => $"已结束进程 {selectedProcess.Name} ({selectedProcess.ProcessId})。",
                TerminationOutcome.AlreadyReleased => "该进程已退出或不再占用文件，列表已刷新。",
                _ => "已发出结束请求，进程仍在退出中。请稍后刷新。",
            };
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"结束失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ElevateButton_Click(object sender, RoutedEventArgs e)
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            StatusTextBlock.Text = "无法找到程序路径，不能以管理员身份重试。";
            return;
        }

        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        startInfo.ArgumentList.Add("--inspect-locks");
        startInfo.ArgumentList.Add(FilePath);

        try
        {
            using Process? elevatedProcess = Process.Start(startInfo);
            if (elevatedProcess is null)
            {
                StatusTextBlock.Text = "无法启动管理员权限窗口。";
                return;
            }

            Close();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            StatusTextBlock.Text = "已取消管理员授权，当前窗口仍可使用。";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"无法以管理员身份重试：{ex.Message}";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
