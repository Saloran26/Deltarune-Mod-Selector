using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LosslessBridge;

class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void StatusListenerDelegate(
        int status,
        IntPtr hwnd,
        int arg2,
        int arg3,
        int arg4,
        int arg5,
        bool arg6,
        float arg7,
        int arg8
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string path);

    [DllImport("Lossless.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Init(StatusListenerDelegate callback);

    [DllImport("Lossless.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UnInit();

    [DllImport("Lossless.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Activate(IntPtr hwnd);

    [DllImport("Lossless.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetDwmRefreshRate();

    [DllImport("Lossless.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ApplySettings(
        int scalingMode,
        int scalingFitMode,
        int scalingType,
        int scalingSubtype,
        float scaleFactor,
        bool resizeBeforeScale,
        int sharpness,
        bool VRS,
        int frameGenerationType,
        int frameGenerationSize,
        int frameGenerationMode,
        float frameGenerationMultiplier,
        float frameGenerationTarget,
        int frameGenerationFlowScale,
        bool clipCursor,
        bool cursorSensitivity,
        bool hideCursor,
        bool scaleCursor,
        int syncMode,
        int maxFrameLatency,
        bool gsyncSupport,
        bool hdrSupport,
        int captureApi,
        int queueTarget,
        bool drawFps,
        int gpuId,
        int displayId,
        int captureOffsetLeft,
        int captureOffsetTop,
        int captureOffsetRight,
        int captureOffsetBottom,
        bool multiDisplayMode,
        int setupPhase
    );

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    static StatusListenerDelegate _listener = OnStatus;
    static string _logFile = "";
    static bool _initialized = false;
    static IntPtr _currentHwnd = IntPtr.Zero;
    static int _currentStatus = 0; // 0=Disabled, 1=TempDisabled, 2=Enabled, 3=Countdown
    static DateTime _lastActivateTime = DateTime.MinValue;

    static void Log(string msg)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
        Console.WriteLine(line);
        if (!string.IsNullOrEmpty(_logFile))
        {
            try { File.AppendAllText(_logFile, line + Environment.NewLine); } catch {}
        }
    }

    static void OnStatus(int status, IntPtr hwnd, int arg2, int arg3, int arg4, int arg5, bool arg6, float arg7, int arg8)
    {
        _currentStatus = status;

        string statusStr = status switch
        {
            0 => "Disabled",
            1 => "TempDisabled",
            2 => "Enabled (Active)",
            3 => "Countdown",
            _ => status.ToString()
        };
        Log($"[STATUS] {statusStr} (HWND=0x{hwnd:X}, Target={arg2}x{arg3}, Src={arg4}x{arg5}, Foreground={arg6})");
    }

    static string ReadSignal(string filename, string gameDir, string defaultValue)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates = new string[]
        {
            Path.Combine(localAppData, "DELTARUNE", filename),
            Path.Combine(gameDir, "signal", filename),
            Path.Combine(gameDir, filename)
        };

        foreach (var p in candidates)
        {
            try
            {
                if (File.Exists(p))
                {
                    string content = File.ReadAllText(p).Trim();
                    if (!string.IsNullOrEmpty(content))
                        return content;
                }
            }
            catch {}
        }
        return defaultValue;
    }

    static string FindLosslessDll(string gameDir, string customDllPath)
    {
        if (!string.IsNullOrEmpty(customDllPath) && File.Exists(customDllPath))
            return customDllPath;

        string[] candidates = new string[]
        {
            Path.Combine(gameDir, "_tools", "LosslessScaling", "Lossless.dll"),
            Path.Combine(gameDir, "Lossless.dll"),
            Path.Combine(AppContext.BaseDirectory, "Lossless.dll"),
            @"C:\Program Files (x86)\Steam\steamapps\common\Lossless Scaling\Lossless.dll",
            @"D:\SteamLibrary\steamapps\common\Lossless Scaling\Lossless.dll",
            @"E:\SteamLibrary\steamapps\common\Lossless Scaling\Lossless.dll",
            @"F:\SteamLibrary\steamapps\common\Lossless Scaling\Lossless.dll"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        string steamConfig = @"C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf";
        if (File.Exists(steamConfig))
        {
            try
            {
                foreach (var line in File.ReadAllLines(steamConfig))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        string p = Path.Combine(match.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common", "Lossless Scaling", "Lossless.dll");
                        if (File.Exists(p)) return p;
                    }
                }
            }
            catch {}
        }

        return "";
    }

    static IntPtr FindDeltaruneWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd)) return true;

            if (GetClientRect(hwnd, out RECT rc))
            {
                int w = rc.Right - rc.Left;
                int h = rc.Bottom - rc.Top;
                if (w < 200 || h < 150) return true;
            }

            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                var proc = Process.GetProcessById((int)pid);
                if (proc.ProcessName.IndexOf("DELTARUNE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var sbTitle = new StringBuilder(256);
                    GetWindowText(hwnd, sbTitle, 256);
                    string title = sbTitle.ToString();

                    var sbClass = new StringBuilder(256);
                    GetClassName(hwnd, sbClass, 256);
                    string cls = sbClass.ToString();

                    if (title.IndexOf("DELTARUNE", StringComparison.OrdinalIgnoreCase) >= 0 || cls == "YYGameMakerYY")
                    {
                        found = hwnd;
                        return false; // Stop enumeration
                    }
                }
            }
            catch {}
            return true;
        }, IntPtr.Zero);

        return found;
    }

    static bool IsAnyDeltaruneProcessRunning()
    {
        try
        {
            var procs = Process.GetProcesses();
            foreach (var p in procs)
            {
                if (p.ProcessName.IndexOf("DELTARUNE", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        catch {}
        return false;
    }

    static bool TryActivate(IntPtr hwnd)
    {
        try
        {
            _lastActivateTime = DateTime.UtcNow;
            IntPtr fg = GetForegroundWindow();
            if (fg != hwnd)
            {
                SetForegroundWindow(hwnd);
                Thread.Sleep(100);
            }
            bool ok = Activate(hwnd);
            Log($"Activate(0x{hwnd:X}) called -> result: {ok}");
            return ok;
        }
        catch (Exception ex)
        {
            Log($"Activate exception: {ex.Message}");
            return false;
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        string gameDir = AppDomain.CurrentDomain.BaseDirectory;
        string customDll = "";
        string fpsArg = "240";
        string modeArg = "balanced";
        string flowArg = "100";

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "--game-dir" || a == "-GameDir" || a == "-g") && i + 1 < args.Length) gameDir = args[++i];
            else if ((a == "--dll" || a == "-Dll") && i + 1 < args.Length) customDll = args[++i];
            else if ((a == "--fps" || a == "-Fps") && i + 1 < args.Length) fpsArg = args[++i];
            else if ((a == "--mode" || a == "-Mode") && i + 1 < args.Length) modeArg = args[++i];
            else if ((a == "--flow" || a == "-Flow") && i + 1 < args.Length) flowArg = args[++i];
        }

        _logFile = Path.Combine(gameDir, "lossless_bridge.log");
        Log($"=== LosslessBridge Starting (Chapter Mode) ===");
        Log($"GameDir: {gameDir}");

        string dllPath = FindLosslessDll(gameDir, customDll);
        if (string.IsNullOrEmpty(dllPath))
        {
            Log("ERROR: Lossless.dll not found in Steam or local directories!");
            return 1;
        }
        Log($"Found Lossless.dll at: {dllPath}");

        string lsDir = Path.GetDirectoryName(dllPath)!;
        Directory.SetCurrentDirectory(lsDir);

        IntPtr hMod = LoadLibrary(dllPath);
        if (hMod == IntPtr.Zero)
        {
            Log($"ERROR: Failed to load {dllPath} (Win32 Error: {Marshal.GetLastWin32Error()})");
            return 2;
        }
        Log($"Loaded Lossless.dll module at 0x{hMod:X}");

        try
        {
            float hz = GetDwmRefreshRate();
            Log($"DWM Screen Refresh Rate: {hz:F1} Hz");
        }
        catch (Exception ex)
        {
            Log($"Could not read refresh rate: {ex.Message}");
        }

        Log("Calling Core.Init...");
        try
        {
            _initialized = Init(_listener);
            if (!_initialized)
            {
                Log("ERROR: Core.Init returned false!");
                return 3;
            }
            Log("Core.Init succeeded!");
        }
        catch (Exception ex)
        {
            Log($"FATAL: Exception in Core.Init: {ex}");
            return 4;
        }

        string lastSyncedSettings = "";
        int noProcessTicks = 0;
        int maxNoProcessTicks = 30; // 30 * 500ms = 15s after DELTARUNE exits

        try
        {
            while (true)
            {
                // 1. Check if DELTARUNE process is alive
                bool deltaruneRunning = IsAnyDeltaruneProcessRunning();
                if (deltaruneRunning)
                {
                    noProcessTicks = 0;
                }
                else
                {
                    noProcessTicks++;
                    if (noProcessTicks > maxNoProcessTicks)
                    {
                        Log("DELTARUNE process exited. Shutting down bridge.");
                        break;
                    }
                }

                // 2. Read dynamic settings from signal files
                string curFpsMode = ReadSignal("fps_mode.txt", gameDir, "lossless");
                string curFpsVal  = ReadSignal("lsfg_fps.txt", gameDir, fpsArg);
                string curPerfVal = ReadSignal("lsfg_mode.txt", gameDir, modeArg);
                string curFlowVal = ReadSignal("lsfg_flow.txt", gameDir, flowArg);
                string curDrawFps = ReadSignal("lsfg_drawfps.txt", gameDir, "1");

                if (!curFpsMode.Equals("lossless", StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(500);
                    continue;
                }

                string settingsKey = $"{curFpsVal}_{curPerfVal}_{curFlowVal}_{curDrawFps}";
                if (settingsKey != lastSyncedSettings)
                {
                    float targetFps = 240.0f;
                    if (float.TryParse(curFpsVal, out float tf) && tf > 0) targetFps = tf;

                    int perfMode = curPerfVal.Equals("performance", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    int flowScale = 100;
                    if (int.TryParse(curFlowVal, out int fs) && fs > 0) flowScale = fs;

                    bool showFps = !curDrawFps.Equals("0", StringComparison.OrdinalIgnoreCase) && !curDrawFps.Equals("false", StringComparison.OrdinalIgnoreCase);

                    float multiplier = 4.0f;
                    int fgMode = 1; // 1 = ADAPTIVE
                    if (targetFps <= 60.0f)
                    {
                        multiplier = 2.0f;
                        fgMode = 0; // FIXED x2
                    }
                    else if (targetFps <= 90.0f)
                    {
                        multiplier = 3.0f;
                        fgMode = 0; // FIXED x3
                    }
                    else if (targetFps <= 120.0f)
                    {
                        multiplier = 4.0f;
                        fgMode = 0; // FIXED x4
                    }
                    else
                    {
                        multiplier = 4.0f;
                        fgMode = 1; // ADAPTIVE
                    }

                    Log($"Applying settings: TargetFPS={targetFps}, Mode={(perfMode == 1 ? "Performance" : "Balanced")}, Flow={flowScale}%, Mult={multiplier}x, FGMode={(fgMode == 1 ? "ADAPTIVE" : "FIXED")}, DrawFps={showFps}, GSync=true, MaxLatency=3, QueueTarget=1");

                    ApplySettings(
                        scalingMode: 0,
                        scalingFitMode: 0,             // 0 = AspectRatio (official DELTARUNE profile)
                        scalingType: 0,                // 0 = Off
                        scalingSubtype: 0,
                        scaleFactor: 1.0f,
                        resizeBeforeScale: false,
                        sharpness: 5,
                        VRS: false,
                        frameGenerationType: 1,        // LSFG 3.0
                        frameGenerationSize: perfMode, // 0 = Balanced, 1 = Performance
                        frameGenerationMode: fgMode,
                        frameGenerationMultiplier: multiplier,
                        frameGenerationTarget: targetFps,
                        frameGenerationFlowScale: flowScale,
                        clipCursor: true,
                        cursorSensitivity: false,
                        hideCursor: false,
                        scaleCursor: false,
                        syncMode: 1,
                        maxFrameLatency: 3,
                        gsyncSupport: true,
                        hdrSupport: false,
                        captureApi: 0,
                        queueTarget: 1,
                        drawFps: showFps,
                        gpuId: 0,
                        displayId: 0,
                        captureOffsetLeft: 0,
                        captureOffsetTop: 0,
                        captureOffsetRight: 0,
                        captureOffsetBottom: 0,
                        multiDisplayMode: false,
                        setupPhase: 0
                    );

                    lastSyncedSettings = settingsKey;
                    Log("Settings successfully applied to Lossless.dll!");
                }

                // 3. Window tracking & Activation
                if (_currentHwnd != IntPtr.Zero && !IsWindow(_currentHwnd))
                {
                    Log($"Attached HWND 0x{_currentHwnd:X} was closed. Resetting window handle.");
                    _currentHwnd = IntPtr.Zero;
                    _currentStatus = 0;
                }

                IntPtr gameHwnd = FindDeltaruneWindow();
                if (gameHwnd != IntPtr.Zero)
                {
                    if (gameHwnd != _currentHwnd)
                    {
                        Log($"Found Chapter window HWND: 0x{gameHwnd:X}. Activating Lossless Scaling frame generation...");
                        if (TryActivate(gameHwnd))
                        {
                            _currentHwnd = gameHwnd;
                            Log($"*** Successfully hooked Lossless Scaling to Chapter (0x{gameHwnd:X})! ***");
                        }
                    }
                    else if (_currentStatus != 2 && (DateTime.UtcNow - _lastActivateTime).TotalSeconds >= 2.0)
                    {
                        // Window is attached but status is not Active (e.g. TempDisabled or focus was lost)
                        IntPtr fg = GetForegroundWindow();
                        if (fg == _currentHwnd)
                        {
                            Log($"Window 0x{_currentHwnd:X} is in foreground but status is {_currentStatus}. Re-activating...");
                            TryActivate(_currentHwnd);
                        }
                    }
                }

                Thread.Sleep(500);
            }
        }
        finally
        {
            if (_initialized)
            {
                Log("Calling Core.UnInit...");
                try { UnInit(); } catch {}
                _initialized = false;
                Log("LosslessBridge clean shutdown completed.");
            }
        }

        return 0;
    }
}
