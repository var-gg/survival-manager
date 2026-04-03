<#
.SYNOPSIS
Unity 에디터 창을 Windows 포그라운드로 강제 전환한다.
Unity가 백그라운드 상태일 때 CLI/MCP 명령 실행 전에 호출하면
에디터 freeze를 예방할 수 있다.

.DESCRIPTION
Windows에서 Unity 에디터가 비활성 창이면 OS가 메시지 펌프 우선순위를 낮추고,
도메인 리로드/테스트 실행 등 UI 스레드 작업이 지연되어 에디터가 멈출 수 있다.
이 스크립트는 Win32 API로 Unity 창을 강제 활성화한다.

SetForegroundWindow는 호출 프로세스가 포그라운드를 소유하지 않으면 실패하므로,
ALT 키 시뮬레이션 + minimize → restore 트릭을 결합하여 우회한다.
#>

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class UnityFocusHelper {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    public const int SW_RESTORE  = 9;
    public const int SW_SHOW     = 5;
    public const int SW_MINIMIZE = 6;

    /// <summary>
    /// Attempts to forcefully bring a window to the foreground.
    /// Uses thread input attachment trick to bypass Windows restrictions.
    /// </summary>
    public static bool ForceForeground(IntPtr targetHwnd) {
        IntPtr foreHwnd = GetForegroundWindow();
        uint foreThread = GetWindowThreadProcessId(foreHwnd, out _);
        uint curThread  = GetCurrentThreadId();

        bool attached = false;
        if (foreThread != curThread) {
            attached = AttachThreadInput(curThread, foreThread, true);
        }

        // Restore if minimized
        if (IsIconic(targetHwnd)) {
            ShowWindow(targetHwnd, SW_RESTORE);
        }

        BringWindowToTop(targetHwnd);
        bool result = SetForegroundWindow(targetHwnd);

        if (!result) {
            // Fallback: minimize then restore forces Windows to reactivate
            ShowWindow(targetHwnd, SW_MINIMIZE);
            ShowWindow(targetHwnd, SW_RESTORE);
            result = SetForegroundWindow(targetHwnd);
        }

        if (attached) {
            AttachThreadInput(curThread, foreThread, false);
        }

        return result;
    }
}
"@

$unityProc = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
    Select-Object -First 1

if ($null -eq $unityProc) {
    Write-Host "No Unity process with a visible window found."
    exit 1
}

$hwnd = $unityProc.MainWindowHandle
$result = [UnityFocusHelper]::ForceForeground($hwnd)

if ($result) {
    Write-Host "Unity editor focused (PID $($unityProc.Id))"
} else {
    Write-Host "Unity editor focus attempted (PID $($unityProc.Id), best-effort)"
}
