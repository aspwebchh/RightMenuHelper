using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShowVersionNum;

public enum LockingApplicationType
{
    Unknown = 0,
    MainWindow = 1,
    OtherWindow = 2,
    Service = 3,
    Explorer = 4,
    Console = 5,
    Critical = 1000,
}

public sealed record LockingProcess(uint ProcessId, ulong StartTime, string Name, LockingApplicationType ApplicationType)
{
    public bool CanTerminate => ProcessId != (uint)Environment.ProcessId && ApplicationType != LockingApplicationType.Critical;

    public string ApplicationTypeText => ApplicationType switch
    {
        LockingApplicationType.Service => "服务",
        LockingApplicationType.Explorer => "资源管理器",
        LockingApplicationType.Console => "控制台程序",
        LockingApplicationType.Critical => "系统关键进程",
        _ => "应用程序",
    };
}

internal enum TerminationOutcome
{
    Ended,
    AlreadyReleased,
    StillExiting,
}

internal static class FileLockInspector
{
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private const int ErrorInvalidParameter = 87;
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint Synchronize = 0x00100000;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x102;
    private const uint WaitFailed = 0xffffffff;

    public static IReadOnlyList<LockingProcess> FindProcesses(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("未提供要检查的文件路径。", nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        if ((File.GetAttributes(fullPath) & FileAttributes.Directory) != 0)
        {
            throw new ArgumentException("请选择文件，而不是文件夹。", nameof(filePath));
        }

        StringBuilder sessionKey = new(33);
        int error = RmStartSession(out uint sessionHandle, 0, sessionKey);
        if (error != ErrorSuccess)
        {
            throw new Win32Exception(error, $"无法启动文件占用扫描：{new Win32Exception(error).Message}");
        }

        try
        {
            error = RmRegisterResources(sessionHandle, 1, [fullPath], 0, IntPtr.Zero, 0, IntPtr.Zero);
            if (error != ErrorSuccess)
            {
                throw new Win32Exception(error, $"无法检查文件占用：{new Win32Exception(error).Message}");
            }

            uint needed;
            uint count = 0;
            uint rebootReasons = 0;
            error = RmGetList(sessionHandle, out needed, ref count, null, ref rebootReasons);
            if (error == ErrorSuccess)
            {
                return [];
            }

            for (int attempt = 0; attempt < 5 && error == ErrorMoreData; attempt++)
            {
                if (needed == 0 || needed > 65536)
                {
                    throw new InvalidOperationException("文件占用进程列表大小无效。");
                }

                RmProcessInfo[] processInfos = new RmProcessInfo[checked((int)needed)];
                count = needed;
                error = RmGetList(sessionHandle, out needed, ref count, processInfos, ref rebootReasons);
                if (error == ErrorSuccess)
                {
                    return processInfos
                        .Take(checked((int)count))
                        .Where(info => info.Process.ProcessId != 0)
                        .Select(info => new LockingProcess(
                            info.Process.ProcessId,
                            info.Process.ProcessStartTime.Value,
                            string.IsNullOrWhiteSpace(info.ApplicationName)
                                ? $"进程 {info.Process.ProcessId}"
                                : info.ApplicationName,
                            info.ApplicationType))
                        .OrderBy(process => process.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(process => process.ProcessId)
                        .ToArray();
                }
            }

            if (error == ErrorMoreData)
            {
                throw new InvalidOperationException("文件占用进程列表持续变化，请重试。");
            }

            throw new Win32Exception(error, $"无法获取占用进程：{new Win32Exception(error).Message}");
        }
        finally
        {
            RmEndSession(sessionHandle);
        }
    }

    public static TerminationOutcome EndTask(string filePath, LockingProcess selectedProcess)
    {
        if (!selectedProcess.CanTerminate)
        {
            throw new InvalidOperationException("不能结束本程序或系统关键进程。");
        }

        LockingProcess? currentProcess = FindProcesses(filePath).FirstOrDefault(process =>
            process.ProcessId == selectedProcess.ProcessId && process.StartTime == selectedProcess.StartTime);
        if (currentProcess is null)
        {
            return TerminationOutcome.AlreadyReleased;
        }

        if (!currentProcess.CanTerminate)
        {
            throw new InvalidOperationException("不能结束本程序或系统关键进程。");
        }

        IntPtr processHandle = OpenProcess(
            ProcessTerminate | ProcessQueryLimitedInformation | Synchronize,
            false,
            selectedProcess.ProcessId);
        if (processHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorInvalidParameter)
            {
                return TerminationOutcome.AlreadyReleased;
            }

            throw new Win32Exception(error, $"无法打开进程 {selectedProcess.ProcessId}：{new Win32Exception(error).Message}");
        }

        try
        {
            if (!GetProcessTimes(processHandle, out FileTime creationTime, out _, out _, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法核对进程创建时间。");
            }

            if (creationTime.Value != selectedProcess.StartTime)
            {
                return TerminationOutcome.AlreadyReleased;
            }

            if (!TerminateProcess(processHandle, 1))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"无法结束进程 {selectedProcess.ProcessId}：{new Win32Exception(error).Message}");
            }

            uint waitResult = WaitForSingleObject(processHandle, 5000);
            if (waitResult == WaitObject0)
            {
                return TerminationOutcome.Ended;
            }

            if (waitResult == WaitTimeout)
            {
                return TerminationOutcome.StillExiting;
            }

            if (waitResult == WaitFailed)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "已请求结束进程，但无法确认它是否退出。");
            }

            throw new InvalidOperationException($"等待进程退出时出现未知结果：{waitResult}。");
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public uint ProcessId;
        public FileTime ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ApplicationName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceName;

        public LockingApplicationType ApplicationType;
        public uint ApplicationStatus;
        public uint SessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] fileNames,
        uint applicationCount,
        IntPtr applications,
        uint serviceCount,
        IntPtr serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RmProcessInfo[]? processInfos,
        ref uint rebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        IntPtr processHandle,
        out FileTime creationTime,
        out FileTime exitTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
