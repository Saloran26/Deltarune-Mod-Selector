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

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    static StatusListenerDelegate _listener = OnStatus;
    static string _logFile = "";
    static bool _initialized = false;
    static IntPtr _currentHwnd = IntPtr.Zero;

    static void Log(string msg)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
        Console.WriteLine(line);
        if (!string.IsNullOrEmpty(_logFile))
        {
            try { File.AppendAllText(_logFile, line + Environment.NewLine); } catch {}
        }
    }

    static void OnStatus(int status, IntPtr hwnd, int a2, int a3, int a4, int a5, bool a6, float a7, int a8)
    {
        // status: 0=Disabled, 1=TempDisabled, 2=Enabled, 3=Countdown
        string statusStr = status switch
        {
            0 => "Disabled",
            1 => "TempDisabled",
            2 => "Enabled (Active)",
            3 => "Countdown",
            _ => status.ToString()
        };
        Log($"[STATUS] {statusStr} for HWND=0x{hwnd:X}");
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

        // Search Steam library folders via libraryfolders.vdf
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

                    // GameMaker windows typically have class YYGameMakerYY or title DELTARUNE
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

    [STAThread]
    static int Main(string[] args)
    {
        string gameDir = AppDomain.CurrentDomain.BaseDirectory;
        string customDll = "";
        string fpsArg = "120";
        string modeArg = "balanced";
        string flowArg = "100";
        bool verbose = true;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "--game-dir" || a == "-GameDir" || a == "-g") && i + 1 < args.Length) gameDir = args[++i];
            else if ((a == "--dll" || a == "-Dll") && i + 1 < args.Length) customDll = args[++i];
            else if ((a == "--fps" || a == "-Fps") && i + 1 < args.Length) fpsArg = args[++i];
            else if ((a == "--mode" || a == "-Mode") && i + 1 < args.Length) modeArg = args[++i];
            else if ((a == "--flow" || a == "-Flow") && i + 1 < args.Length) flowArg = args[++i];
            else if (a == "--verbose" || a == "-v") verbose = true;
        }

        _logFile = Path.Combine(gameDir, "lossless_bridge.log");
        Log($"=== LosslessBridge Starting ===");
        Log($"GameDir: {gameDir}");

        string dllPath = FindLosslessDll(gameDir, customDll);
        if (string.IsNullOrEmpty(dllPath))
        {
            Log("ERROR: Lossless.dll not found in Steam or local directories!");
            return 1;
        }
        Log($"Found Lossless.dll at: {dllPath}");

        // Change working directory to Lossless.dll folder for proper resource resolution
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
        int noProcessCount = 0;

        try
        {
            while (true)
            {
                // 1. Check if DELTARUNE process is still alive
                var procs = Process.GetProcessesByName("DELTARUNE");
                if (procs.Length == 0)
                {
                    // Case-insensitive check
                    var allProcs = Process.GetProcesses();
                    bool anyDeltarune = false;
                    foreach (var p in allProcs)
                    {
                        if (p.ProcessName.IndexOf("DELTARUNE", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            anyDeltarune = true;
                            break;
                        }
                    }

                    if (!anyDeltarune)
                    {
                        noProcessCount++;
                        if (noProcessCount > 8) // ~4 seconds without DELTARUNE
                        {
                            Log("DELTARUNE process exited. Shutting down bridge.");
                            break;
                        }
                    }
                    else
                    {
                        noProcessCount = 0;
                    }
                }
                else
                {
                    noProcessCount = 0;
                }

                // 2. Read dynamic settings from signal files (overrides CLI if present)
                string signalDir = Path.Combine(gameDir, "signal");
                string modeFile  = Path.Combine(signalDir, "fps_mode.txt");
                string fpsFile   = Path.Combine(signalDir, "lsfg_fps.txt");
                string perfFile  = Path.Combine(signalDir, "lsfg_mode.txt");
                string flowFile  = Path.Combine(signalDir, "lsfg_flow.txt");

                string curFpsMode = File.Exists(modeFile) ? File.ReadAllText(modeFile).Trim() : "lossless";
                string curFpsVal  = File.Exists(fpsFile)  ? File.ReadAllText(fpsFile).Trim()  : fpsArg;
                string curPerfVal = File.Exists(perfFile) ? File.ReadAllText(perfFile).Trim() : modeArg;
                string curFlowVal = File.Exists(flowFile) ? File.ReadAllText(flowFile).Trim() : flowArg;

                if (curFpsMode != "lossless")
                {
                    // User disabled lossless scaling in settings
                    Thread.Sleep(500);
                    continue;
                }

                string settingsKey = $"{curFpsVal}_{curPerfVal}_{curFlowVal}";
                if (settingsKey != lastSyncedSettings)
                {
                    float targetFps = 120.0f;
                    if (float.TryParse(curFpsVal, out float tf) && tf > 0) targetFps = tf;

                    int perfMode = (curPerfVal.Equals("performance", StringComparison.OrdinalIgnoreCase)) ? 1 : 0; // 0=Balanced, 1=Performance
                    int flowScale = 100;
                    if (int.TryParse(curFlowVal, out int fs) && fs > 0) flowScale = fs;

                    Log($"Applying settings: TargetFPS={targetFps}, Mode={(perfMode == 1 ? "Performance" : "Balanced")}, Flow={flowScale}%");

                    ApplySettings(
                        scalingMode: 0,
                        scalingFitMode: 1,
                        scalingType: 0, // Off (No spatial upscaling, pure frame generation)
                        scalingSubtype: 0,
                        scaleFactor: 1.0f,
                        resizeBeforeScale: false,
                        sharpness: 0,
                        VRS: false,
                        frameGenerationType: 1, // LSFG 3.0 / 2.3
                        frameGenerationSize: perfMode, // 0=Balanced, 1=Performance
                        frameGenerationMode: 1, // 1=Adaptive target
                        frameGenerationMultiplier: 2.0f,
                        frameGenerationTarget: targetFps,
                        frameGenerationFlowScale: flowScale,
                        clipCursor: false,
                        cursorSensitivity: false,
                        hideCursor: false,
                        scaleCursor: false,
                        syncMode: 1, // Default Sync
                        maxFrameLatency: 1,
                        gsyncSupport: false,
                        hdrSupport: false,
                        captureApi: 0, // DXGI
                        queueTarget: 0,
                        drawFps: false,
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

                // 3. Find and attach to DELTARUNE game window
                IntPtr gameHwnd = FindDeltaruneWindow();
                if (gameHwnd != IntPtr.Zero && gameHwnd != _currentHwnd)
                {
                    Log($"Found new DELTARUNE HWND: 0x{gameHwnd:X}. Activating Lossless Scaling frame generation...");
                    try
                    {
                        bool actOk = Activate(gameHwnd);
                        Log($"Activate result: {actOk}");
                        if (actOk)
                        {
                            _currentHwnd = gameHwnd;
                            Log($"*** Successfully hooked Lossless Scaling to DELTARUNE (0x{gameHwnd:X})! ***");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Activate exception: {ex.Message}");
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
