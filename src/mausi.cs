// =====================================================================
//  Mausi – das easy Mausprogramm
//  SPDX-License-Identifier: MIT (siehe LICENSE)
//
//  Zeiger-Pakete: Beim ersten Start lädt Mausi freie Pakete (GPL-3.0, github.com/ful1e5)
//           nach "cursors" neben der .exe. Dazu: Windows-eigene Zeiger, .cur/.ani im
//           Programmordner und eigene Dateien (ins Fenster ziehen → "cursors\Eigene").
//  Hinweis: SetSystemCursor gilt bis zur Abmeldung bzw. zum Neustart.
//  Bauen:   build.cmd (csc.exe aus .NET Framework 4.x, C# 5), siehe README.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("Mausi")]
[assembly: AssemblyDescription("Mausi – das easy Mausprogramm")]
[assembly: AssemblyProduct("Mausi")]
[assembly: AssemblyCopyright("MIT-Lizenz")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class Native
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadCursorFromFile(string path);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyCursor(IntPtr hcur);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hicon, int cx, int cy,
                                         int step, IntPtr brush, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint action, uint param, IntPtr pv, uint winIni);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    public const uint IMAGE_CURSOR = 2;
    public const uint LR_LOADFROMFILE = 0x10;
    public const uint DI_NORMAL = 3;
    public const uint SPI_SETCURSORS = 0x57;
    public const uint OCR_NORMAL = 32512;
    public const uint SPI_GETMOUSE = 0x0003, SPI_SETMOUSE = 0x0004;
    public const uint SPI_SETMOUSETRAILS = 0x005D, SPI_GETMOUSETRAILS = 0x005E;
    public const uint SPI_GETMOUSESPEED = 0x0070, SPI_SETMOUSESPEED = 0x0071;
    public const uint SPIF_SAVE = 0x03;   // UPDATEINIFILE | SENDCHANGE

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    public static extern bool SpiGetInt(uint action, uint param, ref int pv, uint winIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    public static extern bool SpiArray(uint action, uint param, [In, Out] int[] pv, uint winIni);

    // Touchpad (Windows 11 ab 24H2), Felder in der Reihenfolge der Microsoft-Doku zu TOUCHPAD_PARAMETERS
    public const uint SPI_GETTOUCHPADPARAMETERS = 0x00AE, SPI_SETTOUCHPADPARAMETERS = 0x00AF;

    [StructLayout(LayoutKind.Sequential)]
    public struct TouchpadParameters
    {
        public uint versionNumber, maxSupportedContacts, legacyTouchpadFeatures;
        public uint info;       // nur lesbar: Bit 0 touchpadPresent, 1 legacyTouchpadPresent, 2 externalMousePresent, 3 touchpadEnabled, 4 touchpadActive
        public uint settings;   // Nutzereinstellungen: Bit 0 allowActiveWhenMousePresent, danach feedbackEnabled, tapEnabled …
        public uint sensitivityLevel, cursorSpeed, feedbackIntensity, clickForceSensitivity, rightClickZoneWidth, rightClickZoneHeight;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    public static extern bool SpiTouchpad(uint action, uint param, ref TouchpadParameters pv, uint winIni);

    [StructLayout(LayoutKind.Sequential)]
    public struct KeybdInput { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion { [FieldOffset(0)] public MouseInput mi; [FieldOffset(0)] public KeybdInput ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input { public uint type; public InputUnion u; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(Point p);

    // Turbo: systemweiter Maus-Hook
    public delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseHookInfo { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc fn, IntPtr hMod, uint threadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string name);

    [DllImport("user32.dll")]
    public static extern bool GetPhysicalCursorPos(out POINT p);

    [DllImport("user32.dll")]
    public static extern bool SetPhysicalCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT ptScreenPos; }

    [DllImport("user32.dll")]
    public static extern bool GetCursorInfo(ref CURSORINFO info);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

    public const uint SPI_SETCURSORSIZE = 0x2029;   // Windows › Barrierefreiheit › Mauszeigergröße
}

// ---------------------------------------------------------------------
//  Touchpad: Zustand lesen und umschalten – ohne Adminrechte.
//  Umschalten: Strg+Win+F24, der in Windows eingebaute Touchpad-Schalter (Microsoft-Doku
//  "Enable, Disable Toggle Button"); so machen es auch die Touchpad-Tasten vieler Laptops.
//  Zustand: ab Windows 11 24H2 über SPI_GETTOUCHPADPARAMETERS, sonst aus der Registry.
// ---------------------------------------------------------------------
static class Touchpad
{
    const string StatusKey = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad\Status";
    static readonly int Size = Marshal.SizeOf(typeof(Native.TouchpadParameters));

    // false, wenn es die Schnittstelle nicht gibt (vor 24H2) oder die Werte unplausibel sind
    static bool Read(out Native.TouchpadParameters p)
    {
        p = new Native.TouchpadParameters();
        p.versionNumber = 1;
        try
        {
            if (!Native.SpiTouchpad(Native.SPI_GETTOUCHPADPARAMETERS, (uint)Size, ref p, 0)) return false;
        }
        catch (Exception) { return false; }
        return (p.info & 1) == 0 || p.maxSupportedContacts > 0;   // ein Precision-Touchpad meldet seine Kontakte
    }

    // true/false = an/aus; null = kein umschaltbares Touchpad gefunden
    public static bool? Enabled()
    {
        Native.TouchpadParameters p;
        if (Read(out p)) return (p.info & 1) != 0 ? (bool?)((p.info & 8) != 0) : null;
        foreach (Microsoft.Win32.RegistryKey root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = root.OpenSubKey(StatusKey))
                {
                    object v = k != null ? k.GetValue("Enabled") : null;
                    if (v is int) return (int)v != 0;
                }
            }
            catch (Exception) { }
        }
        return null;
    }

    public static void Toggle()
    {
        ushort[] keys = { 0x11, 0x5B, 0x87 };          // Strg, linke Windows-Taste, F24
        var inputs = new Native.Input[6];
        for (int i = 0; i < 3; i++)
        {
            inputs[i] = Key(keys[i], false);           // drücken in dieser Reihenfolge …
            inputs[5 - i] = Key(keys[i], true);        // … loslassen in umgekehrter
        }
        Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.Input)));
    }

    static Native.Input Key(ushort vk, bool up)
    {
        var i = new Native.Input();
        i.type = 1;                                                   // INPUT_KEYBOARD
        i.u.ki.wVk = vk;
        i.u.ki.dwFlags = (up ? 2u : 0u) | (vk == 0x5B ? 1u : 0u);     // KEYUP; Windows-Taste ist eine erweiterte Taste
        return i;
    }
}

// ---------------------------------------------------------------------
//  Turbo: schneller als das Windows-Höchsttempo (20). Ein Maus-Hook (WH_MOUSE_LL) auf eigenem
//  Thread vergrößert jede echte Mausbewegung um den Faktor und setzt den Zeiger selbst.
//  Eingespeiste Bewegungen (auch die eigenen) und Stift/Touch bleiben unverändert;
//  Spiele, die Rohdaten lesen (Raw Input), merken nichts davon. Wirkt nur, solange Mausi läuft.
// ---------------------------------------------------------------------
static class Turbo
{
    static double factor = 1, restX, restY;
    static int lastX = int.MinValue, lastY;                   // zuletzt gesehene/gesetzte Position
    static System.Threading.Thread thread;
    static uint threadId;
    static readonly Native.LowLevelMouseProc proc = Hook;   // festhalten, sonst räumt der GC den Rückruf weg

    public static double Factor { get { return factor; } }

    public static void Set(double f)
    {
        factor = Math.Max(1, Math.Min(4, f));
        if (factor > 1) Start(); else Stop();
    }

    static void Start()
    {
        if (thread != null) return;
        var ready = new System.Threading.ManualResetEvent(false);
        thread = new System.Threading.Thread(delegate()
        {
            threadId = Native.GetCurrentThreadId();
            IntPtr hook = Native.SetWindowsHookEx(14, proc, Native.GetModuleHandle(null), 0);   // WH_MOUSE_LL
            ready.Set();
            Native.MSG msg;
            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }                          // bis WM_QUIT
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
        });
        thread.IsBackground = true;
        thread.Priority = System.Threading.ThreadPriority.Highest;   // Mausbewegung darf nie warten
        thread.Start();
        ready.WaitOne(2000);
    }

    // Spiele blenden den Zeiger meist aus und lesen Rohdaten – dann bleibt alles unverändert
    static bool CursorShowing()
    {
        var ci = new Native.CURSORINFO();
        ci.cbSize = Marshal.SizeOf(typeof(Native.CURSORINFO));
        return !Native.GetCursorInfo(ref ci) || (ci.flags & 1) != 0;          // CURSOR_SHOWING
    }

    public static void Stop()
    {
        if (thread == null) return;
        Native.PostThreadMessage(threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);   // WM_QUIT
        thread.Join(1000);
        thread = null;
    }

    static IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt64() == 0x0200)                            // WM_MOUSEMOVE
        {
            var info = (Native.MouseHookInfo)Marshal.PtrToStructure(lParam, typeof(Native.MouseHookInfo));
            bool injected = (info.flags & 3) != 0;                              // LLMHF_INJECTED / LOWER_IL_INJECTED
            bool penOrTouch = (info.dwExtraInfo.ToInt64() & 0xFFFFFF00) == 0xFF515700;
            bool echo = info.pt.X == lastX && info.pt.Y == lastY;               // eigene Setzung kommt zurück
            lastX = info.pt.X;
            lastY = info.pt.Y;
            Native.POINT cur;
            if (!injected && !penOrTouch && !echo && CursorShowing() && Native.GetPhysicalCursorPos(out cur))
            {
                int dx = info.pt.X - cur.X, dy = info.pt.Y - cur.Y;
                if (Math.Abs(dx) <= 300 && Math.Abs(dy) <= 300)                 // Sprünge nie vergrößern
                {
                    // Bewegung vergrößern; Bruchteile mitnehmen, damit auch langsame Bewegungen schneller werden
                    double nx = cur.X + dx * factor + restX, ny = cur.Y + dy * factor + restY;
                    int ix = (int)Math.Round(nx), iy = (int)Math.Round(ny);
                    restX = nx - ix;
                    restY = ny - iy;
                    Native.SetPhysicalCursorPos(ix, iy);
                    lastX = ix;
                    lastY = iy;
                    return new IntPtr(1);                                       // Originalbewegung schlucken
                }
            }
        }
        return Native.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }
}

// Spiele blenden den Zeiger meist aus und lesen Rohdaten – dann lässt der Turbo alles unverändert
// Unsichtbarer Empfänger (Nachrichtenfenster): ein zweiter Start von Mausi sagt hierüber "zeig dich",
// statt eine zweite Instanz zu öffnen (zwei Instanzen = doppelter Turbo)
class ShowReceiver : NativeWindow
{
    public const string Name = "Mausi.Empfaenger";
    public const int Msg = 0x8001;                                        // WM_APP + 1
    readonly MethodInvoker onShow;

    public ShowReceiver(MethodInvoker onShow)
    {
        this.onShow = onShow;
        CreateHandle(new CreateParams { Caption = Name, Parent = new IntPtr(-3) });   // HWND_MESSAGE
    }

    public static void Notify()
    {
        IntPtr h = Native.FindWindowEx(new IntPtr(-3), IntPtr.Zero, null, Name);
        if (h != IntPtr.Zero) Native.PostMessage(h, Msg, IntPtr.Zero, IntPtr.Zero);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Msg) { onShow(); return; }
        base.WndProc(ref m);
    }
}

// Mausis eigene Einstellungen (HKCU\Software\Mausi)
static class Prefs
{
    const string Key = @"Software\Mausi";

    public static int Get(string name, int def)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key))
            {
                object v = k != null ? k.GetValue(name) : null;
                return v is int ? (int)v : def;
            }
        }
        catch (Exception) { return def; }
    }

    public static void Set(string name, int value)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Key))
                k.SetValue(name, value, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception) { }
    }
}

// ---------------------------------------------------------------------
//  Zeigertypen: Id = SetSystemCursor-Kennung (0 = nur Beschriftung),
//  Key = Name in install.inf/Registry, Aliases = typische Dateinamen.
// ---------------------------------------------------------------------
class Role
{
    public readonly uint Id;
    public readonly string Key;
    public readonly string Name;
    public readonly string[] Aliases;

    Role(uint id, string key, string name, params string[] aliases)
    {
        Id = id; Key = key; Name = name; Aliases = aliases;
    }

    public static readonly Role[] All =
    {
        new Role(32512, "Arrow",       "Normal",        "pointer", "default", "arrow", "normal", "normal select"),
        new Role(32651, "Help",        "Hilfe",         "help", "help select", "helpsel"),
        new Role(32650, "AppStarting", "Hintergrund",   "work", "working", "appstarting", "progress", "working in background"),
        new Role(32514, "Wait",        "Beschäftigt",   "busy", "wait"),
        new Role(32515, "Crosshair",   "Präzision",     "cross", "crosshair", "precision", "precision select"),
        new Role(32513, "IBeam",       "Text",          "text", "ibeam", "beam", "text select"),
        new Role(32648, "No",          "Nicht erlaubt", "unavailable", "unavailiable", "unavail", "no", "not-allowed"),
        new Role(32645, "SizeNS",      "Größe N-S",     "vert", "vertical", "sizens", "vertical resize", "ns"),
        new Role(32644, "SizeWE",      "Größe W-O",     "horz", "horizontal", "sizewe", "horizontal resize", "ew"),
        new Role(32642, "SizeNWSE",    "Diagonal 1",    "dgn1", "dng1", "diagonal_1", "diagonal1", "sizenwse", "diagonal resize 1", "nwse"),
        new Role(32643, "SizeNESW",    "Diagonal 2",    "dgn2", "dng2", "diagonal_2", "diagonal2", "sizenesw", "diagonal resize 2", "nesw"),
        new Role(32646, "SizeAll",     "Verschieben",   "move", "sizeall"),
        new Role(32516, "UpArrow",     "Alternativ",    "alternate", "uparrow", "up", "alternate select"),
        new Role(32649, "Hand",        "Link",          "link", "hand", "link select"),
        new Role(0,     "NWPen",       "Handschrift",   "handwriting", "pen", "nwpen"),
        new Role(0,     "Pin",         "Ort wählen",    "pin"),
        new Role(0,     "Person",      "Person wählen", "person"),
        new Role(0,     "Pan",         "Schwenken",     "pan"),
        new Role(0,     "Grabbing",    "Greifen",       "grabbing", "grab"),
        new Role(0,     "Zoom-in",     "Zoom +",        "zoom-in", "zoomin"),
        new Role(0,     "Zoom-out",    "Zoom -",        "zoom-out", "zoomout"),
    };

    public static Role ByKey(string key)
    {
        foreach (Role r in All)
            if (string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase)) return r;
        return null;
    }

    public static Role ByAlias(string fileName)
    {
        string n = fileName.Trim().ToLowerInvariant();
        if (n.StartsWith("aero_")) n = n.Substring(5);   // Windows-Zeiger: aero_arrow, aero_busy …
        foreach (Role r in All)
            foreach (string a in r.Aliases)
                if (a == n) return r;
        return null;
    }

    public static Role ById(uint id)
    {
        foreach (Role r in All) if (r.Id == id) return r;
        return null;
    }

    public static uint[] SettableIds()
    {
        var ids = new List<uint>();
        foreach (Role r in All) if (r.Id != 0) ids.Add(r.Id);
        return ids.ToArray();
    }
}

// ---------------------------------------------------------------------
//  Eine Zeigerdatei (.cur/.ani) mit Vorschau-Handles und Animationstakt
// ---------------------------------------------------------------------
class CursorItem
{
    public readonly string FilePath;
    public readonly string FileName;      // ohne Endung
    public Role Role;                     // Rolle im Set (Beschriftung + Sortierung)
    public bool Animated;

    int[] stepEnds;                       // Ende jedes Animationsschritts in ms
    int period;
    CursorArt art;                        // selbst gelesenes Bild
    IntPtr small;                         // Ersatz, falls die Datei nur Windows lesen kann
    bool smallTried;

    public CursorItem(string path)
    {
        FilePath = path;
        FileName = Path.GetFileNameWithoutExtension(path);
        if (path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase)) ReadAniTiming();
    }

    public string Label { get { return Role != null ? Role.Name : FileName; } }
    public int Steps { get { return stepEnds == null ? 1 : stepEnds.Length; } }

    public int StepAt(long ms)
    {
        if (stepEnds == null || period <= 0) return 0;
        int t = (int)(ms % period);
        for (int i = 0; i < stepEnds.Length; i++)
            if (t < stepEnds[i]) return i;
        return 0;
    }

    // Zeichnet den Zeiger mittig in box; size = Kantenlänge des ganzen Zeigerbilds. false = Datei unlesbar
    public bool Draw(Graphics g, Rectangle box, int size, int step)
    {
        if (!smallTried)
        {
            smallTried = true;
            art = CursorArt.Load(FilePath, size);
            if (art == null) small = Load(size);
        }
        if (art != null) { art.Draw(g, box, size, step); return true; }
        if (small == IntPtr.Zero) return false;
        int s = Math.Min(size, Math.Min(box.Width, box.Height));
        Ui.DrawCursor(g, small, box.X + (box.Width - s) / 2, box.Y + (box.Height - s) / 2, s, step);
        return true;
    }

    public void ReleaseAll()
    {
        if (small != IntPtr.Zero) Native.DestroyCursor(small);
        small = IntPtr.Zero;
        if (art != null) art.Dispose();
        art = null;
        smallTried = false;
    }

    IntPtr Load(int size)
    {
        // LoadImage wählt bei Mehrfach-Größen die passende aus und skaliert sauber
        IntPtr h = Native.LoadImage(IntPtr.Zero, FilePath, Native.IMAGE_CURSOR, size, size, Native.LR_LOADFROMFILE);
        if (h == IntPtr.Zero) h = Native.LoadCursorFromFile(FilePath);
        return h;
    }

    // RIFF/ACON lesen: Anzahl Schritte + Takt (anih, rate), damit die Vorschau animiert
    void ReadAniTiming()
    {
        try
        {
            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 12 || Tag(br) != "RIFF") return;
                br.ReadUInt32();
                if (Tag(br) != "ACON") return;

                int steps = 0, jiffies = 6;
                int[] rates = null;
                while (fs.Position + 8 <= fs.Length)
                {
                    string id = Tag(br);
                    long size = br.ReadUInt32();
                    long next = fs.Position + size + (size & 1);
                    if (id == "anih" && size >= 36)
                    {
                        br.ReadUInt32();                 // cbSize
                        int frames = br.ReadInt32();
                        steps = br.ReadInt32();
                        br.ReadBytes(16);                // Breite, Höhe, Bits, Ebenen
                        jiffies = br.ReadInt32();        // 1 Jiffy = 1/60 s
                        if (steps <= 0) steps = frames;
                    }
                    else if (id == "rate")
                    {
                        rates = new int[(int)Math.Min(size / 4, 4096)];
                        for (int i = 0; i < rates.Length; i++) rates[i] = br.ReadInt32();
                    }
                    if (next > fs.Length) break;
                    fs.Position = next;
                }

                if (steps < 2) return;
                steps = Math.Min(steps, 4096);
                stepEnds = new int[steps];
                int t = 0;
                for (int i = 0; i < steps; i++)
                {
                    int j = rates != null && i < rates.Length ? rates[i] : jiffies;
                    t += Math.Max(16, j * 1000 / 60);
                    stepEnds[i] = t;
                }
                period = t;
                Animated = true;
            }
        }
        catch (Exception)
        {
            stepEnds = null;
            Animated = false;
        }
    }

    static string Tag(BinaryReader br)
    {
        return Encoding.ASCII.GetString(br.ReadBytes(4));
    }
}

// ---------------------------------------------------------------------
//  Zeigerbild selbst lesen (.cur/.ani mit PNG- oder Bitmap-Bildern):
//  passende Auflösung wählen, weich skalieren, nach sichtbarem Inhalt zentrieren.
//  (Windows' LoadImage/DrawIconEx skaliert pixelig und zentriert nach der Bildfläche.)
// ---------------------------------------------------------------------
class CursorArt : IDisposable
{
    // Bilder werden erst gelesen, wenn sie gebraucht werden, und fertig skaliert aufgehoben:
    // der Start liest nur das erste Bild je Zeiger, eine Animation kopiert danach nur noch.
    byte[] d;
    int[] at, len;            // je Animationsbild: Lage der gewählten Auflösung in der Datei
    int[] seq;                // Animationsschritt -> Bild (null = gleiche Nummer)
    RectangleF visible;       // sichtbarer Bereich (aus dem ersten Bild), anteilig 0..1
    Size native;              // Pixelmaß der gewählten Auflösung
    Bitmap[] cache;           // fertig skalierte Bilder
    Size cached;

    public static CursorArt Load(string path, int target)
    {
        try
        {
            var a = new CursorArt { d = File.ReadAllBytes(path) };
            var at = new List<int>();
            var len = new List<int>();
            if (a.d.Length >= 12 && Tag(a.d, 0) == "RIFF" && Tag(a.d, 8) == "ACON")
                a.ReadAni(12, a.d.Length, target, at, len);
            else
                Pick(a.d, 0, a.d.Length, target, at, len);
            if (at.Count == 0) return null;
            a.at = at.ToArray();
            a.len = len.ToArray();
            a.cache = new Bitmap[a.at.Length];
            using (Bitmap first = a.Decode(0))
            {
                a.native = first.Size;
                a.visible = Bounds(first);
            }
            return a;
        }
        catch (Exception) { return null; }
    }

    public void Dispose()
    {
        Flush(Size.Empty);
        d = null;
    }

    void Flush(Size size)
    {
        for (int i = 0; i < cache.Length; i++)
            if (cache[i] != null) { cache[i].Dispose(); cache[i] = null; }
        cached = size;
    }

    // Das ganze Zeigerbild wäre size Pixel groß (höchstens 3× vergrößert); der sichtbare Teil sitzt mittig in box
    public void Draw(Graphics g, Rectangle box, int size, int step)
    {
        int i = seq != null && step >= 0 && step < seq.Length ? seq[step] : step;
        i = Math.Max(0, Math.Min(at.Length - 1, i));
        float kw = Math.Min(size, native.Width * 3f), kh = kw * native.Height / native.Width;
        float fit = Math.Min(1f, Math.Min(box.Width / (visible.Width * kw), box.Height / (visible.Height * kh)));
        var sz = new Size(Math.Max(1, (int)Math.Round(kw * fit)), Math.Max(1, (int)Math.Round(kh * fit)));
        if (sz != cached) Flush(sz);
        if (cache[i] == null)
            using (Bitmap raw = Decode(i)) cache[i] = Scale(raw, sz);
        int x = (int)Math.Round(box.X + (box.Width - visible.Width * sz.Width) / 2f - visible.X * sz.Width);
        int y = (int)Math.Round(box.Y + (box.Height - visible.Height * sz.Height) / 2f - visible.Y * sz.Height);
        g.DrawImage(cache[i], new Rectangle(x, y, sz.Width, sz.Height));
    }

    // einmal hochwertig skalieren (vorvervielfachtes Alpha = schnellstes Zeichnen danach)
    static Bitmap Scale(Bitmap raw, Size sz)
    {
        var b = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        using (var ia = new ImageAttributes())
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = sz == raw.Size ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            ia.SetWrapMode(WrapMode.TileFlipXY);   // keine dunklen Säume am Bildrand
            g.DrawImage(raw, new Rectangle(0, 0, sz.Width, sz.Height), 0, 0, raw.Width, raw.Height, GraphicsUnit.Pixel, ia);
        }
        return b;
    }

    static string Tag(byte[] d, int pos)
    {
        return Encoding.ASCII.GetString(d, pos, 4);
    }

    // RIFF/ACON: LIST "fram" enthält je Schritt eine komplette .cur/.ico, "seq " die Reihenfolge
    void ReadAni(int pos, int end, int target, List<int> at, List<int> len)
    {
        while (pos + 8 <= end)
        {
            string id = Tag(d, pos);
            int size = BitConverter.ToInt32(d, pos + 4), body = pos + 8;
            if (size < 0 || body + size > end) break;
            if (id == "LIST" && size >= 4) ReadAni(body + 4, body + size, target, at, len);
            else if (id == "icon") Pick(d, body, size, target, at, len);
            else if (id == "seq ")
            {
                seq = new int[size / 4];
                for (int k = 0; k < seq.Length; k++) seq[k] = BitConverter.ToInt32(d, body + k * 4);
            }
            pos = body + size + (size & 1);
        }
    }

    // .cur/.ico: kleinste Auflösung >= Ziel nehmen, sonst die größte; merkt sich nur die Lage
    static void Pick(byte[] d, int off, int len, int target, List<int> ats, List<int> lens)
    {
        int count = BitConverter.ToUInt16(d, off + 4), best = -1, bestW = 0;
        for (int k = 0; k < count && off + 6 + k * 16 + 16 <= off + len; k++)
        {
            int w = d[off + 6 + k * 16] == 0 ? 256 : d[off + 6 + k * 16];
            bool better = best < 0
                || (w >= target && (bestW < target || w < bestW))
                || (w < target && bestW < target && w > bestW);
            if (better) { best = k; bestW = w; }
        }
        if (best < 0) throw new InvalidDataException();
        int e = off + 6 + best * 16;
        int size = BitConverter.ToInt32(d, e + 8), at = off + BitConverter.ToInt32(d, e + 12);
        if (size <= 8 || at < off || at + size > off + len) throw new InvalidDataException();
        ats.Add(at);
        lens.Add(size);
    }

    Bitmap Decode(int i)
    {
        int a = at[i], n = len[i];
        if (d[a] == 0x89 && d[a + 1] == 'P' && d[a + 2] == 'N' && d[a + 3] == 'G')
        {
            using (var ms = new MemoryStream(d, a, n))
            using (Image img = Image.FromStream(ms))
            {
                var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
                }
                return bmp;
            }
        }
        return ReadDib(d, a, n);
    }

    // Klassisches Bitmap-Bild (1/4/8/24/32 bpp) samt UND-Maske
    static Bitmap ReadDib(byte[] d, int at, int size)
    {
        int hdr = BitConverter.ToInt32(d, at), w = BitConverter.ToInt32(d, at + 4);
        int h = BitConverter.ToInt32(d, at + 8) / 2, bpp = BitConverter.ToUInt16(d, at + 14);
        int used = BitConverter.ToInt32(d, at + 32);
        if (w <= 0 || h <= 0 || w > 1024 || h > 1024) throw new InvalidDataException();
        if (bpp != 1 && bpp != 4 && bpp != 8 && bpp != 24 && bpp != 32) throw new InvalidDataException();
        int colors = bpp <= 8 ? (used > 0 ? used : 1 << bpp) : 0;
        int pal = at + hdr, xor = pal + colors * 4;
        int xs = ((w * bpp + 31) / 32) * 4, ms = ((w + 31) / 32) * 4, and = xor + xs * h;
        if (and > at + size) throw new InvalidDataException();
        bool mask = and + ms * h <= at + size, alpha = false;
        const int Opaque = unchecked((int)0xFF000000);

        var px = new int[w * h];
        for (int y = 0; y < h; y++)
        {
            int row = xor + (h - 1 - y) * xs;
            for (int x = 0; x < w; x++)
            {
                int c;
                if (bpp == 32)
                {
                    c = BitConverter.ToInt32(d, row + x * 4);
                    if ((c & Opaque) != 0) alpha = true;
                }
                else if (bpp == 24)
                {
                    int p = row + x * 3;
                    c = Opaque | (d[p + 2] << 16) | (d[p + 1] << 8) | d[p];
                }
                else
                {
                    int bit = x * bpp;
                    int p = pal + ((d[row + bit / 8] >> (8 - bpp - bit % 8)) & ((1 << bpp) - 1)) * 4;
                    c = Opaque | (d[p + 2] << 16) | (d[p + 1] << 8) | d[p];
                }
                px[y * w + x] = c;
            }
        }
        if (!alpha)   // ohne Alphakanal entscheidet die Maske; "Invertieren"-Pixel werden dunkelgrau
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int c = px[y * w + x] | Opaque;
                    if (mask && ((d[and + (h - 1 - y) * ms + x / 8] >> (7 - x % 8)) & 1) != 0)
                        c = (c & 0xFFFFFF) == 0 ? 0 : unchecked((int)0xFF303030);
                    px[y * w + x] = c;
                }
        }

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            Marshal.Copy(px, y * w, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), w);
        bmp.UnlockBits(bd);
        return bmp;
    }

    // Sichtbarer Bereich (Alpha > 16), damit Pfeil, Sanduhr und Hand gleich mittig sitzen
    static RectangleF Bounds(Bitmap f)
    {
        int w = f.Width, h = f.Height;
        var px = new int[w * h];
        BitmapData bd = f.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), px, y * w, w);
        f.UnlockBits(bd);
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (((px[y * w + x] >> 24) & 0xFF) <= 16) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        return x1 < 0 ? new RectangleF(0, 0, 1, 1)
                      : RectangleF.FromLTRB(x0 / (float)w, y0 / (float)h, (x1 + 1) / (float)w, (y1 + 1) / (float)h);
    }
}

class CursorSet
{
    public string Name;
    public string Dir;
    public bool Own, IsSystem;
    public readonly List<CursorItem> Items = new List<CursorItem>();
    public readonly Dictionary<uint, CursorItem> Roles = new Dictionary<uint, CursorItem>();

    public bool CanApply
    {
        get { return Roles.ContainsKey(Native.OCR_NORMAL); }
    }

    public override string ToString() { return Name; }
}

// ---------------------------------------------------------------------
//  Bibliothek: cursors\<Set>\*.cur|*.ani (+ install.inf), cursors\Eigene
// ---------------------------------------------------------------------
static class Library
{
    public const string WindowsName = "Windows (Original)";

    static readonly Regex RoleLine = new Regex(
        @"^HKCU\s*,\s*""Control Panel\\Cursors""\s*,\s*""?([A-Za-z\-]+)""?\s*,[^,]*,\s*""?([^""]*)",
        RegexOptions.IgnoreCase);
    static readonly Regex Var = new Regex("%([^%]+)%");

    public static bool IsCursorFile(string path)
    {
        string e = Path.GetExtension(path).ToLowerInvariant();
        return e == ".cur" || e == ".ani";
    }

    // Eigene Einzeldateien: Typen über die Dateinamen; ohne Pfeil wird die erste freie Datei der normale Pfeil
    public static void MapRoles(CursorSet set)
    {
        set.Roles.Clear();
        CursorItem free = null;
        foreach (CursorItem it in set.Items)
        {
            Role r = Role.ByAlias(it.FileName);
            if (r != null && r.Id != 0 && !set.Roles.ContainsKey(r.Id)) set.Roles[r.Id] = it;
            else if (free == null && (r == null || r.Id == 0)) free = it;
        }
        if (!set.Roles.ContainsKey(Native.OCR_NORMAL) && set.Items.Count > 0)
            set.Roles[Native.OCR_NORMAL] = free ?? set.Items[0];
    }

    static List<string> Files(string dir, bool deep)
    {
        var list = new List<string>();
        foreach (string f in Directory.GetFiles(dir, "*.*", deep ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            if (IsCursorFile(f)) list.Add(f);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public static List<CursorSet> Load(string appDir, string root)
    {
        var sets = new List<CursorSet>();
        var own = new CursorSet { Name = "Eigene Zeiger", Dir = Path.Combine(root, "Eigene"), Own = true };
        try
        {
            foreach (string f in Files(appDir, false)) own.Items.Add(new CursorItem(f));   // .cur/.ani direkt neben der exe
            if (Directory.Exists(root))
            {
                foreach (string f in Files(root, false)) own.Items.Add(new CursorItem(f));
                string[] dirs = Directory.GetDirectories(root);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                foreach (string d in dirs)
                {
                    if (string.Equals(Path.GetFileName(d), "Eigene", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string f in Files(d, true)) own.Items.Add(new CursorItem(f));
                        continue;
                    }
                    CursorSet s = LoadSet(d);
                    if (s.Items.Count > 0) sets.Add(s);
                }
            }
        }
        catch (Exception) { }
        // "Windows (Original)" immer zuerst – auch ohne lesbare Windows-Zeiger, die Karte stellt das Original her
        sets.Insert(0, LoadWindows() ?? new CursorSet { Name = WindowsName, Dir = "", IsSystem = true });
        MapRoles(own);
        sets.Add(own);   // "Eigene Zeiger" immer als letzter Eintrag
        return sets;
    }

    // Windows-eigene Zeiger (C:\Windows\Cursors, nur die normale Größe)
    static CursorSet LoadWindows()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Cursors");
            if (!Directory.Exists(dir)) return null;
            var files = new List<string>();
            foreach (string f in Files(dir, false))
            {
                string n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (n.StartsWith("aero_") && !n.EndsWith("_l") && !n.EndsWith("_xl")) files.Add(f);
            }
            if (files.Count == 0) return null;
            CursorSet set = BuildSet(WindowsName, dir, files, false);
            set.IsSystem = true;
            return set;
        }
        catch (Exception) { return null; }
    }

    public static CursorSet LoadSet(string dir)
    {
        return BuildSet(Path.GetFileName(dir.TrimEnd('\\', '/')), dir, Files(dir, true), true);
    }

    static CursorSet BuildSet(string name, string dir, List<string> files, bool useInf)
    {
        var set = new CursorSet { Name = name, Dir = dir };
        var byName = new Dictionary<string, CursorItem>(StringComparer.OrdinalIgnoreCase);
        foreach (string f in files)
        {
            var item = new CursorItem(f);
            set.Items.Add(item);
            string n = Path.GetFileName(f);
            if (!byName.ContainsKey(n)) byName[n] = item;
        }

        // 1) install.inf: eindeutige Zeilen wie  HKCU,"Control Panel\Cursors",Arrow,0x00020000,"...\%pointer%"
        try
        {
            string inf = useInf ? FindInf(dir) : null;
            if (inf != null) ReadInf(inf, set, byName);
        }
        catch (Exception) { }

        // 2) Lücken über Dateinamen füllen (Pointer, Busy, Link, Dgn1 …)
        foreach (CursorItem it in set.Items)
        {
            Role r = Role.ByAlias(it.FileName);
            if (r != null && r.Id != 0 && !set.Roles.ContainsKey(r.Id)) set.Roles[r.Id] = it;
        }

        // Beschriftung: gesetzte Rollen, danach Extras (Pin, Zoom …)
        foreach (KeyValuePair<uint, CursorItem> kv in set.Roles)
            if (kv.Value.Role == null) kv.Value.Role = Role.ById(kv.Key);
        foreach (CursorItem it in set.Items)
        {
            if (it.Role != null) continue;
            Role r = Role.ByAlias(it.FileName);
            if (r != null && r.Id == 0) it.Role = r;
        }

        set.Items.Sort(Compare);
        return set;
    }

    static string FindInf(string dir)
    {
        string[] infs = Directory.GetFiles(dir, "*.inf", SearchOption.AllDirectories);
        foreach (string f in infs)
            if (string.Equals(Path.GetFileName(f), "install.inf", StringComparison.OrdinalIgnoreCase)) return f;
        return infs.Length > 0 ? infs[0] : null;
    }

    static void ReadInf(string inf, CursorSet set, Dictionary<string, CursorItem> byName)
    {
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<KeyValuePair<string, string>>();
        string section = "";
        foreach (string raw in File.ReadAllLines(inf))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == ';') continue;
            if (line[0] == '[') { section = line.Trim('[', ']', ' '); continue; }
            if (string.Equals(section, "Strings", StringComparison.OrdinalIgnoreCase))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) strings[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Trim('"');
                continue;
            }
            Match m = RoleLine.Match(line);
            if (m.Success) lines.Add(new KeyValuePair<string, string>(m.Groups[1].Value, m.Groups[2].Value));
        }

        foreach (KeyValuePair<string, string> l in lines)
        {
            Role r = Role.ByKey(l.Key);
            if (r == null || r.Id == 0 || set.Roles.ContainsKey(r.Id)) continue;
            string value = Var.Replace(l.Value, delegate(Match v)
            {
                string s;
                return strings.TryGetValue(v.Groups[1].Value, out s) ? s : "";
            });
            string file = value.Replace('/', '\\');
            int cut = file.LastIndexOf('\\');
            if (cut >= 0) file = file.Substring(cut + 1);
            CursorItem it;
            if (file.Length > 0 && byName.TryGetValue(file, out it)) set.Roles[r.Id] = it;
        }
    }

    static int Compare(CursorItem a, CursorItem b)
    {
        int ia = a.Role != null ? Array.IndexOf(Role.All, a.Role) : int.MaxValue;
        int ib = b.Role != null ? Array.IndexOf(Role.All, b.Role) : int.MaxValue;
        if (ia != ib) return ia.CompareTo(ib);
        return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
    }
}

// ---------------------------------------------------------------------
//  Optik: Farben, Schriften, Helfer
// ---------------------------------------------------------------------
static class Ui
{
    public static float Scale = 1f;
    public static int S(int v) { return (int)Math.Round(v * Scale); }
    public static readonly Stopwatch Clock = Stopwatch.StartNew();

    // Farben: Graphit mit Signalgelb. Gelb heißt nur "aktiv/an", Rotorange ist der Turbo.
    public static readonly Color Bg = Color.FromArgb(30, 30, 30);
    public static readonly Color Surface = Color.FromArgb(40, 40, 40);
    public static readonly Color SurfaceHover = Color.FromArgb(50, 50, 50);
    public static readonly Color Border = Color.FromArgb(56, 56, 56);
    public static readonly Color BorderStrong = Color.FromArgb(74, 74, 74);
    public static readonly Color Ink = Color.FromArgb(242, 242, 242);
    public static readonly Color Muted = Color.FromArgb(150, 150, 150);
    public static readonly Color Accent = Color.FromArgb(255, 204, 51);         // Signalgelb
    public static readonly Color AccentDark = Color.FromArgb(230, 182, 40);
    public static readonly Color AccentSoft = Color.FromArgb(56, 50, 36);         // Hintergrund des aktiven Pakets
    public static readonly Color AccentDisabled = Color.FromArgb(110, 96, 50);
    public static readonly Color AccentText = Color.FromArgb(255, 204, 51);
    public static readonly Color OnAccent = Color.FromArgb(30, 30, 30);           // Schrift/Knopf auf Gelb
    public static readonly Color Good = Color.FromArgb(120, 210, 120);
    public static readonly Color Bad = Color.FromArgb(255, 125, 110);
    public static readonly Color Rail = Color.FromArgb(70, 70, 70);               // Reglerschiene, Schalter aus
    public static readonly Color Turbo = Color.FromArgb(255, 99, 56);             // Rotorange
    public static readonly Color TurboRail = Color.FromArgb(84, 46, 36);

    // Schrift: Bahnschrift (DIN-Stil, seit Windows 10 dabei); fehlt sie, nimmt Mausi Segoe UI
    static Font F(float size, bool bold)
    {
        try
        {
            using (new FontFamily("Bahnschrift"))
                return new Font("Bahnschrift", size, bold ? FontStyle.Bold : FontStyle.Regular);
        }
        catch (ArgumentException) { return new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size); }
    }

    public static readonly Font Body = F(10.5f, false);
    public static readonly Font Small = F(9f, false);
    public static readonly Font Semi = F(10.5f, true);
    public static readonly Font Heading = F(13f, true);
    public static readonly Font Title = F(19f, true);
    public static readonly Font Tagline = F(13f, false);
    public static readonly Font Caption = F(8.5f, true);

    // Oberkante bis Grundlinie in Pixeln – damit verschieden große Texte auf einer Linie stehen
    public static int Ascent(Graphics g, Font f)
    {
        FontFamily ff = f.FontFamily;
        return (int)Math.Round(f.SizeInPoints * g.DpiY / 72f * ff.GetCellAscent(f.Style) / ff.GetEmHeight(f.Style));
    }

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static GraphicsPath RoundF(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
    {
        TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix);
    }

    // Zeiger direkt per GDI ins (gepufferte) Steuerelement zeichnen – mit Alpha und Animationsschritt
    public static void DrawCursor(Graphics g, IntPtr h, int x, int y, int size, int step)
    {
        IntPtr hdc = g.GetHdc();
        try
        {
            if (!Native.DrawIconEx(hdc, x, y, h, size, size, step, IntPtr.Zero, Native.DI_NORMAL) && step != 0)
                Native.DrawIconEx(hdc, x, y, h, size, size, 0, IntPtr.Zero, Native.DI_NORMAL);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }
    }

    public static void DrawLogo(Graphics g, Rectangle r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath p = Round(r, (int)(r.Width * 0.2f)))
        using (var b = new SolidBrush(Accent))
            g.FillPath(b, p);
        float k = r.Width / 40f, ox = r.X + 14 * k, oy = r.Y + 9 * k;
        PointF[] arrow =
        {
            new PointF(ox, oy), new PointF(ox, oy + 19 * k), new PointF(ox + 4.5f * k, oy + 14.5f * k),
            new PointF(ox + 8 * k, oy + 21.5f * k), new PointF(ox + 11 * k, oy + 20 * k),
            new PointF(ox + 7.5f * k, oy + 13 * k), new PointF(ox + 13.5f * k, oy + 13 * k)
        };
        using (var b = new SolidBrush(OnAccent)) g.FillPolygon(b, arrow);
    }

    public static Icon MakeIcon()
    {
        using (var bmp = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(bmp)) DrawLogo(g, new Rectangle(0, 0, 31, 31));
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    public static Button Btn(string text, bool primary)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Font = primary ? Semi : Body,
            TextAlign = ContentAlignment.MiddleCenter
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.BorderColor = BorderStrong;
        if (primary)
        {
            b.BackColor = Accent;
            b.ForeColor = OnAccent;
            b.FlatAppearance.MouseOverBackColor = AccentDark;
            b.FlatAppearance.MouseDownBackColor = AccentDark;
            b.EnabledChanged += delegate { b.BackColor = b.Enabled ? Accent : AccentDisabled; };
        }
        else
        {
            b.BackColor = SurfaceHover;
            b.ForeColor = Ink;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
            b.FlatAppearance.MouseDownBackColor = Rail;
        }
        return b;
    }

    // Häkchen im Kreis (aktives Paket)
    public static void Check(Graphics g, Rectangle c, Color fill)
    {
        using (var b = new SolidBrush(fill)) g.FillEllipse(b, c);
        using (var pen = new Pen(fill == Accent ? OnAccent : Color.White, Math.Max(1.5f, 1.8f * Scale)))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, new[] { new PointF(c.X + c.Width * 0.28f, c.Y + c.Height * 0.52f),
                                     new PointF(c.X + c.Width * 0.44f, c.Y + c.Height * 0.68f),
                                     new PointF(c.X + c.Width * 0.73f, c.Y + c.Height * 0.36f) });
        }
    }

    // Dunkle Titelleiste (Windows 10 ab 1809, Windows 11); auf Windows 11 zusätzlich in Fensterfarbe
    public static void DarkTitle(IntPtr hwnd)
    {
        try
        {
            int on = 1;
            if (Native.DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0)
                Native.DwmSetWindowAttribute(hwnd, 19, ref on, 4);
            int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16);
            Native.DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
        }
        catch (Exception) { }
    }

    // Dunkle Bildlaufleisten
    public static void DarkScroll(Control c)
    {
        c.HandleCreated += delegate
        {
            try { Native.SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch (Exception) { }
        };
    }

    public static void DoubleBuffer(Control c)
    {
        PropertyInfo pi = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance);
        if (pi != null) pi.SetValue(c, true, null);
    }
}

// ---------------------------------------------------------------------
//  Schieberegler im Stil von Windows 11: Ziehen (der Griff folgt der Maus stufenlos und rastet
//  beim Loslassen ein), auf die Schiene klicken, Pfeiltasten, Mausrad schon beim Drüberfahren.
//  Unter der Schiene stehen die Enden in Worten; der Punkt markiert den Windows-Standard.
// ---------------------------------------------------------------------
class SliderBar : Control
{
    int min, max = 10, val, def = int.MinValue, wheelAcc;
    float dragX = -1;                 // beim Ziehen: Position des Griffs, sonst -1
    bool hover;
    public int WheelStep = 1;         // Werte je Mausrad-Raste
    public int HotFrom = int.MaxValue; // Werte darüber = Turbo-Bereich (orange)
    public string MinText = "", MaxText = "";
    public event EventHandler ValueChanged;

    public SliderBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
    }

    public int Minimum { get { return min; } set { min = value; max = Math.Max(max, min); Value = val; Invalidate(); } }
    public int Maximum { get { return max; } set { max = Math.Max(value, min); Value = val; Invalidate(); } }
    public int Default { get { return def; } set { def = value; Invalidate(); } }

    public int Value
    {
        get { return val; }
        set
        {
            int v = Math.Max(min, Math.Min(max, value));
            if (v == val) return;
            val = v;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    // Griff 22 px + Luft für den Fokusring: die Schiene beginnt 15 px nach dem linken Rand
    public static int Inset { get { return Ui.S(15); } }
    float Start { get { return Inset; } }
    float End { get { return Width - Inset - 1; } }
    float TrackY { get { return Ui.S(16); } }

    float XOf(int v)
    {
        return max == min ? Start : Start + (v - min) * (End - Start) / (max - min);
    }

    int ValueAt(float x)
    {
        return End <= Start ? min : min + (int)Math.Round((x - Start) * (max - min) / (End - Start));
    }

    // Mausrad: Rasten aufsummieren (Touchpads liefern viele kleine Schritte), je Raste WheelStep Werte
    public void Wheel(int delta)
    {
        wheelAcc += delta;
        int notches = wheelAcc / 120;
        if (notches == 0) return;
        wheelAcc -= notches * 120;
        Value += notches * WheelStep;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            dragX = Math.Max(Start, Math.Min(End, e.X));
            Value = ValueAt(dragX);
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragX >= 0)
        {
            dragX = Math.Max(Start, Math.Min(End, e.X));
            Value = ValueAt(dragX);
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragX = -1;                   // Griff rastet auf dem Wert ein
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; wheelAcc = 0; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Wheel(e.Delta); base.OnMouseWheel(e); }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
            case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown:
                return true;
        }
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int page = Math.Max(1, (max - min) / 5);
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Down: Value -= 1; break;
            case Keys.Right: case Keys.Up: Value += 1; break;
            case Keys.PageDown: Value -= page; break;
            case Keys.PageUp: Value += page; break;
            case Keys.Home: Value = min; break;
            case Keys.End: Value = max; break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cy = TrackY, a = Start, b = End, t = Ui.S(4);
        float x = dragX >= 0 ? dragX : XOf(val);

        float hx = HotFrom < max ? XOf(HotFrom) : b;      // ab hier Turbo-Bereich
        using (GraphicsPath p = Ui.RoundF(new RectangleF(a, cy - t / 2, b - a, t), t / 2))
        using (var br = new SolidBrush(Ui.Rail))
            g.FillPath(br, p);
        if (hx < b)
            using (GraphicsPath p = Ui.RoundF(new RectangleF(hx, cy - t / 2, b - hx, t), t / 2))
            using (var br = new SolidBrush(Ui.TurboRail))
                g.FillPath(br, p);
        if (x - a >= 1)
            using (GraphicsPath p = Ui.RoundF(new RectangleF(a, cy - t / 2, Math.Min(x, hx) - a, t), t / 2))
            using (var br = new SolidBrush(Ui.Accent))
                g.FillPath(br, p);
        if (x > hx)
            using (GraphicsPath p = Ui.RoundF(new RectangleF(hx, cy - t / 2, x - hx, t), t / 2))
            using (var br = new SolidBrush(Ui.Turbo))
                g.FillPath(br, p);

        if (def > min && def < max)       // Standard in der Mitte markieren; am Rand sagen es die Endtexte
        {
            float dx = XOf(def), r = Math.Max(1.5f, Ui.S(2));
            using (var br = new SolidBrush(Ui.Muted))
                g.FillEllipse(br, dx - r, cy + Ui.S(13), 2 * r, 2 * r);
        }

        var ends = new Rectangle((int)a - Ui.S(2), (int)cy + Ui.S(12), (int)(b - a) + Ui.S(4), Ui.S(18));
        Ui.Text(g, MinText, Ui.Small, ends, Ui.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        Ui.Text(g, MaxText, Ui.Small, ends, Ui.Muted, TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        float d = dragX >= 0 ? Ui.S(20) : hover ? Ui.S(18) : Ui.S(16);    // volle Scheibe, wächst beim Anfassen
        using (var br = new SolidBrush(val > HotFrom ? Ui.Turbo : Ui.Accent))
            g.FillEllipse(br, x - d / 2, cy - d / 2, d, d);

        if (Focused && ShowFocusCues)
            using (var pen = new Pen(Ui.Ink, Math.Max(1, Ui.S(2))))
                g.DrawEllipse(pen, x - Ui.S(13), cy - Ui.S(13), Ui.S(26), Ui.S(26));
    }
}

// Mausrad über einem Regler geht an den Regler – auch wenn gerade etwas anderes den Fokus hat
class WheelToSlider : IMessageFilter
{
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != 0x020A) return false;                                        // WM_MOUSEWHEEL
        var s = Control.FromChildHandle(Native.WindowFromPoint(Cursor.Position)) as SliderBar;
        if (s == null) return false;
        s.Wheel((short)((m.WParam.ToInt64() >> 16) & 0xFFFF));
        return true;
    }
}

// ---------------------------------------------------------------------
//  Ein/Aus-Schalter im Stil von Windows 11 (statt CheckBox) – für Einstellungen,
//  die sofort wirken. Text steht rechts, Größe ergibt sich aus dem Text.
// ---------------------------------------------------------------------
class ToggleSwitch : Control
{
    bool on, hover;
    public event EventHandler CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        ForeColor = Ui.Ink;
    }

    public bool Checked
    {
        get { return on; }
        set
        {
            if (on == value) return;
            on = value;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }
    }

    // 4 px Luft links für den Fokusring: der Schalter selbst beginnt bei Inset
    public static int Inset { get { return Ui.S(4); } }

    void FitSize()
    {
        Size t = TextRenderer.MeasureText(Text, Font);
        Size = new Size(Inset + Ui.S(40) + Ui.S(10) + t.Width, Math.Max(Ui.S(28), t.Height));
    }

    protected override void OnTextChanged(EventArgs e) { FitSize(); Invalidate(); base.OnTextChanged(e); }
    protected override void OnFontChanged(EventArgs e) { FitSize(); base.OnFontChanged(e); }
    protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); FitSize(); }
    protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) Checked = !Checked;
        base.OnKeyUp(e);
    }

    // Schalter zeichnen (auch für die Touchpad-Karte): 40 × 20 px, x0/y = linke obere Ecke
    public static void DrawSwitch(Graphics g, int x0, int y, bool on, bool hover)
    {
        int w = Ui.S(40), h = Ui.S(20);
        var track = new RectangleF(x0 + 0.5f, y + 0.5f, w - 1, h - 1);
        using (GraphicsPath p = Ui.RoundF(track, (h - 1) / 2f))
        using (var b = new SolidBrush(on ? (hover ? Ui.AccentDark : Ui.Accent) : (hover ? Ui.BorderStrong : Ui.Rail)))
            g.FillPath(b, p);
        float d = hover ? Ui.S(14) : Ui.S(12), cx = on ? x0 + w - h / 2f : x0 + h / 2f, cy = y + h / 2f;
        using (var b = new SolidBrush(on ? Ui.OnAccent : Color.FromArgb(190, 190, 190)))
            g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int w = Ui.S(40), h = Ui.S(20), x0 = Inset, y = (Height - h) / 2;
        DrawSwitch(g, x0, y, on, hover);

        if (Focused && ShowFocusCues)
            using (var pen = new Pen(Ui.Ink, Math.Max(1, Ui.S(2))))
            using (GraphicsPath p = Ui.RoundF(new RectangleF(x0 - Ui.S(3), y - Ui.S(3), w + Ui.S(6), h + Ui.S(6)), (h + Ui.S(6)) / 2f))
                g.DrawPath(pen, p);

        TextRenderer.DrawText(g, Text, Font, new Rectangle(x0 + w + Ui.S(10), 0, Width - x0 - w - Ui.S(10), Height), ForeColor,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

// ---------------------------------------------------------------------
//  Touchpad-Karte oben rechts: Symbol, Zustand in Worten und wofür man sie braucht.
//  Die ganze Fläche ist klickbar; aus = Symbol rot durchgestrichen.
// ---------------------------------------------------------------------
class TouchpadCard : Control
{
    bool on = true, hover;
    public event EventHandler CheckedChanged;

    public TouchpadCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Size = new Size(Ui.S(380), Ui.S(58));
    }

    public bool Checked
    {
        get { return on; }
        set
        {
            if (on == value) return;
            on = value;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) Checked = !Checked;
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Ui.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(6)))
        using (var b = new SolidBrush(hover ? Ui.SurfaceHover : Ui.Surface))
            g.FillPath(b, p);
        if (Focused && ShowFocusCues)
            using (GraphicsPath p = Ui.Round(new Rectangle(Ui.S(3), Ui.S(3), Width - Ui.S(7), Height - Ui.S(7)), Ui.S(4)))
            using (var pen = new Pen(Ui.Ink, Math.Max(1, Ui.S(2))))
                g.DrawPath(pen, p);

        // Symbol: Touchpad-Fläche mit zwei Tasten unten
        int pad = Ui.S(14);
        var ic = new Rectangle(pad, (Height - Ui.S(26)) / 2, Ui.S(34), Ui.S(26));
        using (var pen = new Pen(on ? Ui.Ink : Ui.Muted, Math.Max(1.5f, 1.6f * Ui.Scale)))
        using (GraphicsPath p = Ui.Round(ic, Ui.S(5)))
        {
            g.DrawPath(pen, p);
            int ly = ic.Bottom - Ui.S(8);
            g.DrawLine(pen, ic.X, ly, ic.Right, ly);
            g.DrawLine(pen, ic.X + ic.Width / 2, ly, ic.X + ic.Width / 2, ic.Bottom);
        }
        if (!on)
            using (var pen = new Pen(Ui.Bad, Math.Max(2f, 2.2f * Ui.Scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, ic.X - Ui.S(3), ic.Bottom + Ui.S(3), ic.Right + Ui.S(3), ic.Y - Ui.S(3));

        // Zustand in Worten, darunter wofür man das braucht
        int tx = ic.Right + Ui.S(12), sx = Width - pad - Ui.S(40), tw = sx - tx - Ui.S(10);
        Ui.Text(g, on ? "Touchpad ist an" : "Touchpad ist aus", Ui.Semi, new Rectangle(tx, Ui.S(9), tw, Ui.S(22)), Ui.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        Ui.Text(g, on ? "Zum Tippen oder mit Maus ausschalten" : "Klick schaltet es wieder ein", Ui.Small,
                new Rectangle(tx, Ui.S(30), tw, Ui.S(20)), Ui.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        ToggleSwitch.DrawSwitch(g, sx, (Height - Ui.S(20)) / 2, on, hover);
    }
}

// ---------------------------------------------------------------------
//  Paket-Karte: zeigt die wichtigsten Zeiger eines Pakets. Ein Klick übernimmt das ganze Paket.
// ---------------------------------------------------------------------
class SetCard : Control
{
    public readonly CursorSet Set;
    public bool Active;
    readonly List<CursorItem> strip = new List<CursorItem>();
    readonly int[] steps;
    readonly string info;
    bool hover;

    // Normal, Link, Text, Beschäftigt, Hintergrund, Hilfe, Verschieben, Präzision
    static readonly uint[] StripRoles = { 32512, 32649, 32513, 32514, 32650, 32651, 32646, 32515 };

    public SetCard(CursorSet set)
    {
        Set = set;
        foreach (uint id in StripRoles)
        {
            CursorItem it;
            if (set.Roles.TryGetValue(id, out it) && !strip.Contains(it)) strip.Add(it);
        }
        foreach (CursorItem it in set.Items)            // ohne erkannte Typen: einfach die ersten Dateien
        {
            if (strip.Count >= StripRoles.Length) break;
            if (!strip.Contains(it)) strip.Add(it);
        }
        steps = new int[strip.Count];

        int ani = 0, all = Role.SettableIds().Length;
        foreach (CursorItem it in set.Items) if (it.Animated) ani++;
        if (set.IsSystem)
            info = "Zurück zu den normalen Windows-Zeigern";
        else
            info = set.Items.Count + (set.Own ? " eigene Dateien" : " Zeiger") + (ani > 0 ? ", " + ani + " animiert" : "") +
                   (!set.Own && set.Roles.Count < all ? ", " + set.Roles.Count + " von " + all + " Typen" : "");

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Size = new Size(Ui.S(320), Ui.S(150));
        Margin = new Padding(Ui.S(8));
        Cursor = Cursors.Hand;
    }

    public Rectangle StripRect { get { return new Rectangle(Ui.S(14), Ui.S(14), Width - Ui.S(28), Ui.S(68)); } }

    int ShownCount { get { return Math.Min(strip.Count, Math.Max(1, StripRect.Width / Ui.S(44))); } }

    // Nur die Zeiger neu zeichnen, deren Animationsschritt sich geändert hat
    public void Tick(long ms)
    {
        int n = ShownCount;
        for (int i = 0; i < n; i++)
            if (strip[i].Animated && strip[i].StepAt(ms) != steps[i]) Invalidate(Box(i, n));
    }

    Rectangle Box(int i, int n)
    {
        Rectangle sr = StripRect;
        var box = new Rectangle(sr.X + i * sr.Width / n, sr.Y, sr.Width / n, sr.Height);
        box.Inflate(-Ui.S(3), -Ui.S(4));
        return box;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Color fill = Active ? Ui.AccentSoft : hover ? Ui.SurfaceHover : Ui.Surface;
        if (Rectangle.Inflate(ClientRectangle, -Ui.S(8), -Ui.S(8)).Contains(e.ClipRectangle))
        {
            using (var b = new SolidBrush(fill)) g.FillRectangle(b, e.ClipRectangle);   // nur ein Zeiger: schlicht füllen
        }
        else
        {
            g.Clear(Parent != null ? Parent.BackColor : Ui.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(6)))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, p);
            if (Active)                                                // aktives Paket: gelbe Kante links
                using (var b = new SolidBrush(Ui.Accent))
                    g.FillRectangle(b, 0, Ui.S(12), Ui.S(4), Height - Ui.S(24));
            if (Focused && ShowFocusCues)
                using (GraphicsPath p = Ui.Round(new Rectangle(Ui.S(3), Ui.S(3), Width - Ui.S(7), Height - Ui.S(7)), Ui.S(4)))
                using (var pen = new Pen(Ui.Ink, Math.Max(1, Ui.S(2))))
                    g.DrawPath(pen, p);
        }

        // Zeigerreihe: gleich breite Felder, jeder Zeiger nach seinem sichtbaren Inhalt mittig
        Rectangle sr = StripRect;
        int n = ShownCount;
        long ms = Ui.Clock.ElapsedMilliseconds;
        for (int i = 0; i < n; i++)
        {
            Rectangle box = Box(i, n);
            if (!e.ClipRectangle.IntersectsWith(box)) continue;     // nur zeichnen, was neu muss
            steps[i] = strip[i].StepAt(ms);
            strip[i].Draw(g, box, Ui.S(48), steps[i]);
        }
        if (n == 0)
            Ui.Text(g, "Keine Vorschau", Ui.Body, sr, Ui.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        // Name, darunter Anzahl; rechts der Haken, wenn das Paket gerade aktiv ist
        int pad = Ui.S(18), ty = Height - Ui.S(60), right = Width - pad;
        if (e.ClipRectangle.Bottom <= ty) return;                     // nur Zeiger neu: Text bleibt stehen
        if (Active)
        {
            Size ts = TextRenderer.MeasureText("Aktiv", Ui.Semi);
            int d = Ui.S(18), bw = d + Ui.S(6) + ts.Width;
            Ui.Check(g, new Rectangle(right - bw, ty + (Ui.S(28) - d) / 2, d, d), Ui.Accent);
            Ui.Text(g, "Aktiv", Ui.Semi, new Rectangle(right - ts.Width, ty, ts.Width, Ui.S(28)), Ui.AccentText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            right -= bw + Ui.S(12);
        }
        Ui.Text(g, Set.Name, Ui.Heading, new Rectangle(pad, ty, right - pad, Ui.S(28)), Ui.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        Ui.Text(g, info, Ui.Small, new Rectangle(pad, ty + Ui.S(28), Width - 2 * pad, Ui.S(20)), Ui.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

// ---------------------------------------------------------------------
//  Letzte Karte: eigene Zeiger hinzufügen (Klick = Dateien wählen; Ordner/Dateien hineinziehen geht auch)
// ---------------------------------------------------------------------
class DropCard : Control
{
    bool hover;

    public DropCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Size = new Size(Ui.S(320), Ui.S(150));
        Margin = new Padding(Ui.S(8));
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Ui.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(6)))
        using (var b = new SolidBrush(hover ? Ui.SurfaceHover : Color.FromArgb(35, 35, 35)))
            g.FillPath(b, p);

        int cx = Width / 2, cy = Height / 2 - Ui.S(20), arm = Ui.S(11);
        using (var pen = new Pen(hover ? Ui.AccentText : Ui.Muted, Math.Max(1.5f, 2f * Ui.Scale)))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
            g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
        }
        Ui.Text(g, "Eigene Zeiger hinzufügen", Ui.Semi, new Rectangle(0, cy + Ui.S(20), Width, Ui.S(24)),
                hover ? Ui.AccentText : Ui.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        Ui.Text(g, "Ordner oder .cur/.ani hierher ziehen", Ui.Small, new Rectangle(0, cy + Ui.S(44), Width, Ui.S(20)),
                Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

// ---------------------------------------------------------------------
//  Hauptfenster
// ---------------------------------------------------------------------
public class MausChangerForm : Form
{
    readonly string appDir = AppDomain.CurrentDomain.BaseDirectory;
    readonly string libDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cursors");
    List<CursorSet> sets = new List<CursorSet>();
    CursorSet activeSet;
    readonly List<SetCard> cards = new List<SetCard>();
    readonly ToolTip tip = new ToolTip();
    readonly Timer anim = new Timer();

    FlowLayoutPanel flow;
    DropCard dropCard;
    Label status;

    TouchpadCard padSwitch;
    bool padSync, padShown;   // padSync: Schalter wird gerade aus dem echten Zustand gesetzt
    readonly Timer padTimer = new Timer();
    readonly WheelToSlider wheelFilter = new WheelToSlider();
    NotifyIcon tray;
    bool startHidden, exiting, trayHint;
    ShowReceiver receiver;

    SliderBar speedBar, sizeBar, trailBar;
    Label speedVal, sizeVal, trailVal, mouseCaption;
    ToggleSwitch precision;
    Button btnMouseDefault;
    readonly List<Control[]> sliderRows = new List<Control[]>();
    readonly Timer mouseTimer = new Timer();
    bool loadingUi, pendSpeed, pendSize, pendTrail, pendPrecision;
    int cursorBase = 32;      // Zeigergröße bei 100 % Skalierung (32–256, wie Windows)

    static int S(int v) { return Ui.S(v); }
    const string RepoUrl = "https://github.com/ulliiiiiiiii/mausi";

    public MausChangerForm(bool hidden)
    {
        startHidden = hidden;
        Text = "Mausi";
        Font = Ui.Body;
        BackColor = Ui.Bg;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        try { Icon = Ui.MakeIcon(); } catch (Exception) { }
        tray = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Text = "Mausi – Turbo läuft" };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Mausi öffnen", null, delegate { ShowFromTray(); });
        trayMenu.Items.Add("Beenden", null, delegate { exiting = true; Close(); });
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += delegate { ShowFromTray(); };
        receiver = new ShowReceiver(delegate { ShowFromTray(); });

        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        ClientSize = new Size(Math.Min(S(1180), wa.Width - 40), Math.Min(S(760), wa.Height - 60));
        MinimumSize = new Size(Math.Min(S(780), wa.Width), Math.Min(S(600), wa.Height));

        SuspendLayout();
        Controls.Add(BuildGrid());       // Fill zuerst, damit es den Rest bekommt
        Controls.Add(BuildMouseBar());   // unten über der Statuszeile
        Controls.Add(BuildHeader());     // oben
        Controls.Add(BuildStatus());     // ganz unten, volle Breite
        ResumeLayout(true);

        HookDrop(this);
        Application.AddMessageFilter(wheelFilter);
        KeyDown += OnKey;
        anim.Interval = 33;
        anim.Tick += delegate { Animate(); };
        anim.Start();
        mouseTimer.Interval = 150;
        mouseTimer.Tick += delegate { ApplyMouse(); };

        padTimer.Interval = 800;
        padTimer.Tick += delegate { CheckPad(); };

        LoadMouseValues();
        ReloadLibrary();
        RefreshTouchpad();
    }

    // ---------------- Aufbau ----------------

    Control BuildHeader()
    {
        var head = new Panel { Dock = DockStyle.Top, Height = S(88), BackColor = Ui.Bg };
        Ui.DoubleBuffer(head);
        head.Paint += delegate(object s, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.DrawLogo(g, new Rectangle(S(24), S(24), S(40), S(40)));
            var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            Size t = TextRenderer.MeasureText(g, "Mausi", Ui.Title, Size.Empty, flags);
            int x = S(76), y = S(44) - t.Height / 2;                       // mittig zum Logo
            Ui.Text(g, "Mausi", Ui.Title, new Rectangle(x, y, t.Width + 2, t.Height), Ui.Ink, flags);
            int x2 = x + t.Width + S(10), y2 = y + Ui.Ascent(g, Ui.Title) - Ui.Ascent(g, Ui.Tagline);   // gleiche Grundlinie
            int right = padShown ? padSwitch.Left - S(24) : head.Width - S(24);
            Ui.Text(g, "– das easy Mausprogramm", Ui.Tagline, new Rectangle(x2, y2, Math.Max(0, right - x2), S(30)), Ui.Muted,
                    flags | TextFormatFlags.EndEllipsis);
        };

        padSwitch = new TouchpadCard();
        tip.SetToolTip(padSwitch, "Schaltet das Touchpad des Laptops aus und wieder ein (wie Strg+Win+F24)");
        padSwitch.CheckedChanged += delegate { if (!padSync) TogglePad(); };
        head.Controls.Add(padSwitch);
        head.Resize += delegate { LayoutHeader(); };
        return head;
    }

    // Touchpad-Schalter rechts, bündig mit dem rechten Rand der Karten
    void LayoutHeader()
    {
        Control head = padSwitch.Parent;
        int right = head.Width - S(24) - SystemInformation.VerticalScrollBarWidth - 2, mid = S(44);
        padSwitch.Location = new Point(right - padSwitch.Width, mid - padSwitch.Height / 2);
        head.Invalidate();
    }

    // Schalter an den echten Zustand anpassen: beim Start, beim Aktivieren des Fensters, nach dem Umschalten
    void RefreshTouchpad()
    {
        bool? on = Touchpad.Enabled();
        padSync = true;
        padShown = on.HasValue;
        padSwitch.Visible = padShown;
        if (on.HasValue) padSwitch.Checked = on.Value;
        padSync = false;
        LayoutHeader();
    }

    void TogglePad()
    {
        Touchpad.Toggle();
        padTimer.Tag = padSwitch.Checked;
        padTimer.Stop();
        padTimer.Start();     // Windows braucht einen Moment, danach den echten Zustand prüfen
    }

    void CheckPad()
    {
        padTimer.Stop();
        bool want = (bool)padTimer.Tag;
        bool? on = Touchpad.Enabled();
        RefreshTouchpad();
        if (on.HasValue && on.Value == want)
            Status(want ? "Touchpad ist an." : "Touchpad ist aus. Wieder an: hier oder mit Strg+Win+F24.", Ui.Good);
        else
            Status("Touchpad ließ sich nicht umschalten – der Treiber reagiert nicht auf Strg+Win+F24.", Ui.Bad);
    }

    Control BuildGrid()
    {
        flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.Bg,
            Padding = new Padding(S(16), 0, S(16), S(8))
        };
        Ui.DoubleBuffer(flow);
        Ui.DarkScroll(flow);
        flow.Resize += delegate { FitCards(); };

        dropCard = new DropCard();
        dropCard.Click += delegate { OpenFiles(); };
        tip.SetToolTip(dropCard, "Dateien auswählen (Strg+O) – ganze Ordner einfach ins Fenster ziehen");
        HookDrop(dropCard);
        return flow;
    }

    // ---------------- Maus: Tempo, Größe, Spur ----------------

    Control BuildMouseBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = S(150), BackColor = Ui.Bg };
        Ui.DoubleBuffer(bar);
        bar.Paint += delegate(object s, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Ui.Round(CardRect(bar), S(6)))
            using (var b = new SolidBrush(Ui.Surface))
                e.Graphics.FillPath(b, p);
        };

        mouseCaption = new Label { Text = "Maus", Font = Ui.Semi, ForeColor = Ui.Ink, BackColor = Ui.Surface, AutoSize = true };
        precision = new ToggleSwitch { Text = "Präzision verbessern", BackColor = Ui.Surface };
        precision.CheckedChanged += delegate { pendPrecision = true; MouseChanged(); };
        tip.SetToolTip(precision, "Zeigerbeschleunigung: schnelle Bewegungen tragen weiter");
        btnMouseDefault = Ui.Btn("Standardwerte", false);
        btnMouseDefault.Size = new Size(TextRenderer.MeasureText(btnMouseDefault.Text, btnMouseDefault.Font).Width + S(30), S(32));
        btnMouseDefault.Click += delegate { MouseDefaults(); };

        speedBar = MakeSlider(bar, "Tempo", 1, 30, 10, "langsam", "Turbo", 1, out speedVal);
        speedBar.HotFrom = 20;                                    // 1–20 = Windows, 21–30 = Turbo ×1,2 … ×3,0
        tip.SetToolTip(speedBar, "Ziehen, klicken oder Mausrad\nRechts von 20: Turbo, schneller als Windows erlaubt\nNotbremse: Strg+Alt+T");
        sizeBar = MakeSlider(bar, "Größe", 0, 48, 0, "normal", "groß", 2, out sizeVal);
        trailBar = MakeSlider(bar, "Spur", 0, 6, 0, "aus", "lang", 1, out trailVal);
        speedBar.ValueChanged += delegate { pendSpeed = true; MouseChanged(); };
        sizeBar.ValueChanged += delegate { pendSize = true; MouseChanged(); };
        trailBar.ValueChanged += delegate { pendTrail = true; MouseChanged(); };

        bar.Controls.Add(mouseCaption);
        bar.Controls.Add(precision);
        bar.Controls.Add(btnMouseDefault);
        bar.Resize += delegate { LayoutMouseBar(bar); };
        return bar;
    }

    SliderBar MakeSlider(Control bar, string name, int min, int max, int standard, string minText, string maxText,
                         int wheelStep, out Label value)
    {
        var label = new Label
        {
            Text = name, Font = Ui.Semi, ForeColor = Ui.Ink, BackColor = Ui.Surface,
            AutoSize = false, TextAlign = ContentAlignment.MiddleLeft
        };
        value = new Label
        {
            Font = Ui.Semi, ForeColor = Ui.AccentText, BackColor = Ui.Surface,
            AutoSize = false, TextAlign = ContentAlignment.MiddleRight
        };
        var tb = new SliderBar
        {
            Minimum = min, Maximum = max, Default = standard, BackColor = Ui.Surface,
            MinText = minText, MaxText = maxText, WheelStep = wheelStep
        };
        tip.SetToolTip(tb, "Ziehen, klicken oder Mausrad" +
                           (standard > min && standard < max ? "\nDer Punkt markiert den Windows-Standard" : ""));
        bar.Controls.Add(label);
        bar.Controls.Add(value);
        bar.Controls.Add(tb);
        sliderRows.Add(new Control[] { label, value, tb });
        return tb;
    }

    Rectangle CardRect(Control bar)
    {
        // bündig mit den Karten: gleicher Rand links, rechts endet sie vor der Bildlaufleiste
        return new Rectangle(S(24), S(8), bar.Width - S(48) - SystemInformation.VerticalScrollBarWidth - 2, bar.Height - S(24));
    }

    void LayoutMouseBar(Control bar)
    {
        Rectangle c = CardRect(bar);
        int pad = S(16), x0 = c.X + pad, top = c.Y + S(12);
        mouseCaption.Location = new Point(x0, top + S(6));
        btnMouseDefault.Location = new Point(c.Right - pad - btnMouseDefault.Width, top - S(2));
        precision.Location = new Point(btnMouseDefault.Left - S(20) - precision.Width,
                                       btnMouseDefault.Top + (btnMouseDefault.Height - precision.Height) / 2);
        int gap = S(28), colW = Math.Max(S(60), (c.Width - 2 * pad - 2 * gap) / 3), y = top + S(36);
        for (int i = 0; i < sliderRows.Count; i++)
        {
            int x = x0 + i * (colW + gap);
            Control[] row = sliderRows[i];
            row[0].SetBounds(x, y, colW / 2, S(22));
            row[1].SetBounds(x + colW / 2, y, colW - colW / 2, S(22));
            row[2].SetBounds(x - SliderBar.Inset, y + S(24), colW + 2 * SliderBar.Inset, S(48));
        }
        bar.Invalidate();
    }

    void LoadMouseValues()
    {
        loadingUi = true;
        int speed = 10, trails = 0;
        Native.SpiGetInt(Native.SPI_GETMOUSESPEED, 0, ref speed, 0);
        Native.SpiGetInt(Native.SPI_GETMOUSETRAILS, 0, ref trails, 0);
        var acc = new int[3];
        bool accOk = Native.SpiArray(Native.SPI_GETMOUSE, 0, acc, 0);
        cursorBase = RegistryBase();
        int turbo = Prefs.Get("Turbo", 100);                     // Prozent, 100 = aus
        speedBar.Value = turbo > 100 ? 20 + (int)Math.Round((turbo / 100.0 - 1) / 0.2) : Math.Max(1, Math.Min(20, speed));
        Turbo.Set(TurboFactor(speedBar.Value));
        trailBar.Value = trails <= 1 ? 0 : Math.Min(6, trails - 1);
        sizeBar.Value = PxToSize(cursorBase);
        precision.Checked = accOk && acc[2] != 0;
        loadingUi = false;
        UpdateMouseLabels();
    }

    void UpdateMouseLabels()
    {
        speedVal.Text = speedBar.Value <= 20 ? speedBar.Value.ToString() : "Turbo ×" + TurboFactor(speedBar.Value).ToString("0.0");
        speedVal.ForeColor = speedBar.Value > 20 ? Ui.Turbo : Ui.AccentText;
        sizeVal.Text = SizeToPx(sizeBar.Value) + " px";
        trailVal.Text = trailBar.Value == 0 ? "aus" : (trailBar.Value + 1).ToString();   // Länge der Spur
    }

    void MouseChanged()
    {
        UpdateMouseLabels();
        if (loadingUi)
        {
            pendSpeed = pendSize = pendTrail = pendPrecision = false;
            return;
        }
        mouseTimer.Stop();     // kurz warten, damit beim Ziehen nicht jeder Zwischenwert gesetzt wird
        mouseTimer.Start();
    }

    void ApplyMouse()
    {
        mouseTimer.Stop();
        bool turboNow = false;
        if (pendSpeed)
        {
            double f = TurboFactor(speedBar.Value);
            Native.SystemParametersInfo(Native.SPI_SETMOUSESPEED, 0, new IntPtr(Math.Min(20, speedBar.Value)), Native.SPIF_SAVE);
            Turbo.Set(f);
            Prefs.Set("Turbo", (int)Math.Round(f * 100));
            SetAutostart(f > 1);                                  // Turbo braucht Mausi auch nach dem Neustart
            turboNow = f > 1;
        }
        if (pendTrail)
            Native.SystemParametersInfo(Native.SPI_SETMOUSETRAILS, (uint)(trailBar.Value == 0 ? 0 : trailBar.Value + 1),
                                        IntPtr.Zero, Native.SPIF_SAVE);
        if (pendPrecision)
            Native.SpiArray(Native.SPI_SETMOUSE, 0, precision.Checked ? new[] { 6, 10, 1 } : new[] { 0, 0, 0 }, Native.SPIF_SAVE);
        if (pendSize)
        {
            cursorBase = SizeToPx(sizeBar.Value);
            ApplyAllAtSize();
        }
        pendSpeed = pendSize = pendTrail = pendPrecision = false;
        if (turboNow)
            Status(speedVal.Text + " an – Mausi bleibt dafür im Hintergrund (Symbol neben der Uhr) und startet mit Windows.", Ui.Good);
        else
            Status(string.Format("Maus übernommen: Tempo {0}, Größe {1}, Spur {2}, Präzision {3}",
                                 speedVal.Text, sizeVal.Text, trailVal.Text, precision.Checked ? "an" : "aus"), Ui.Good);
    }

    void MouseDefaults()
    {
        speedBar.Value = 10;
        sizeBar.Value = 0;
        trailBar.Value = 0;
        precision.Checked = true;
        pendSpeed = pendSize = pendTrail = pendPrecision = true;
        ApplyMouse();
    }

    static double TurboFactor(int v) { return v > 20 ? 1 + (v - 20) * 0.2 : 1; }

    static void SetAutostart(bool on)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (on) k.SetValue("Mausi", "\"" + Application.ExecutablePath + "\" /tray");
                else if (k.GetValue("Mausi") != null) k.DeleteValue("Mausi", false);
            }
        }
        catch (Exception) { }
    }

    // Größe: Regler 0–48 = 32–128 px in 2-px-Schritten (Windows selbst: 16-px-Stufen bis 256)
    static int SizeToPx(int v) { return 32 + v * 2; }
    static int PxToSize(int px) { return Math.Max(0, Math.Min(48, (px - 32 + 1) / 2)); }

    int SizePx()
    {
        return Math.Max(16, (int)Math.Round(cursorBase * Ui.Scale));
    }

    static int RegistryBase()
    {
        try
        {
            object o = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors", "CursorBaseSize", null);
            if (o is int) return Math.Max(32, Math.Min(256, (int)o));
        }
        catch (Exception) { }
        return 32;
    }

    static IntPtr LoadSized(string path, int px)
    {
        IntPtr h = Native.LoadImage(IntPtr.Zero, path, Native.IMAGE_CURSOR, px, px, Native.LR_LOADFROMFILE);
        return h != IntPtr.Zero ? h : Native.LoadCursorFromFile(path);
    }

    // Windows-eigene Zeigergröße setzen (wie Einstellungen › Barrierefreiheit › Mauszeiger)
    void SetWindowsCursorSize()
    {
        try
        {
            Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors", "CursorBaseSize",
                                              cursorBase, Microsoft.Win32.RegistryValueKind.DWord);
            Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Accessibility", "CursorSize",
                                              (cursorBase - 32 + 8) / 16 + 1, Microsoft.Win32.RegistryValueKind.DWord);   // nächste Stufe für die Windows-Einstellungen
        }
        catch (Exception) { }
        Native.SystemParametersInfo(Native.SPI_SETCURSORSIZE, 0, new IntPtr(cursorBase), Native.SPIF_SAVE);
        Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, Native.SPIF_SAVE);
    }

    // Größe geändert: Windows-Größe setzen, danach das aktive Paket in der neuen Größe neu anwenden
    void ApplyAllAtSize()
    {
        SetWindowsCursorSize();
        if (activeSet != null && !activeSet.IsSystem)
            foreach (KeyValuePair<uint, CursorItem> kv in activeSet.Roles)
                SetCursorFile(kv.Value.FilePath, new uint[] { kv.Key });
    }

    Control BuildStatus()
    {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = S(34), BackColor = Ui.Bg };
        status = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
            Padding = new Padding(S(24), 0, 0, 0), ForeColor = Ui.Muted
        };
        panel.Controls.Add(status);

        // unten rechts: Link zum Projekt
        var link = new LinkLabel
        {
            Text = "made by ulliiiiiiii", Dock = DockStyle.Right, AutoSize = false, Font = Ui.Small,
            TextAlign = ContentAlignment.MiddleRight, BackColor = Ui.Bg, Cursor = Cursors.Hand,
            Padding = new Padding(S(12), 0, S(24) + SystemInformation.VerticalScrollBarWidth + 2, 0),
            LinkColor = Ui.Muted, ActiveLinkColor = Ui.Accent, VisitedLinkColor = Ui.Muted,
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        link.Width = TextRenderer.MeasureText(link.Text, link.Font).Width + link.Padding.Horizontal + S(4);   // volle Zeilenhöhe: mittig zum Status
        link.MouseEnter += delegate { link.LinkColor = Ui.Accent; };
        link.MouseLeave += delegate { link.LinkColor = Ui.Muted; };
        link.LinkClicked += delegate
        {
            try { Process.Start(RepoUrl); }
            catch (Exception) { Status("Browser ließ sich nicht öffnen: " + RepoUrl, Ui.Bad); }
        };
        tip.SetToolTip(link, RepoUrl);
        panel.Controls.Add(link);
        return panel;
    }

    // ---------------- Pakete & Anzeige ----------------

    void ReloadLibrary()
    {
        string active = activeSet != null ? activeSet.Name : null;
        foreach (CursorSet s in sets)
            foreach (CursorItem it in s.Items) it.ReleaseAll();   // alte Handles freigeben (F5)
        sets = Library.Load(appDir, libDir);
        activeSet = active == null ? null : sets.Find(x => x.Name == active);
        RefreshCards();
        int packs = 0;
        foreach (CursorSet s in sets) if (!s.IsSystem && s.Items.Count > 0) packs++;
        Status(packs == 0
            ? "Noch keine Zeiger-Pakete – Ordner oder .cur/.ani-Dateien ins Fenster ziehen."
            : string.Format("{0} Zeiger-Pakete bereit.", packs), Ui.Muted);
    }

    void RefreshCards()
    {
        flow.SuspendLayout();
        foreach (SetCard old in cards)
        {
            tip.SetToolTip(old, null);
            old.Dispose();
        }
        cards.Clear();
        flow.Controls.Clear();
        foreach (CursorSet s in sets)
        {
            if (s.Own && s.Items.Count == 0) continue;     // leere "Eigene Zeiger" nicht als Karte
            var c = new SetCard(s) { Active = s == activeSet };
            c.Click += delegate { ApplySet(c.Set); };
            tip.SetToolTip(c, s.IsSystem ? "Klick stellt die Windows-Zeiger wieder her" : "Klick übernimmt alle Zeiger dieses Pakets");
            HookDrop(c);
            cards.Add(c);
            flow.Controls.Add(c);
        }
        flow.Controls.Add(dropCard);
        FitCards();
        flow.ResumeLayout();
    }

    // Karten füllen die Breite: so viele Spalten wie passen, der Rest wird verteilt.
    // Die Bildlaufleiste ist immer eingerechnet, damit ihr Erscheinen nichts verschiebt.
    void FitCards()
    {
        int m = S(16);
        int avail = flow.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2;
        int cols = Math.Max(1, avail / (S(300) + m));
        int w = Math.Max(S(220), avail / cols - m);
        flow.SuspendLayout();
        foreach (Control c in flow.Controls)
            if (c.Width != w) c.Width = w;
        flow.ResumeLayout();
    }

    void Animate()
    {
        if (!Visible || WindowState == FormWindowState.Minimized || ActiveForm != this) return;   // nur im Vordergrund animieren
        long ms = Ui.Clock.ElapsedMilliseconds;
        foreach (SetCard c in cards) c.Tick(ms);
    }

    void Status(string text, Color color)
    {
        status.ForeColor = color;
        status.Text = text;
    }

    void MarkActive()
    {
        foreach (SetCard c in cards)
        {
            bool a = c.Set == activeSet;
            if (c.Active != a) { c.Active = a; c.Invalidate(); }
        }
    }

    // ---------------- Anwenden ----------------

    int SetCursorFile(string path, uint[] ids)
    {
        int ok = 0;
        foreach (uint id in ids)
        {
            // SetSystemCursor übernimmt (und zerstört) das Handle – daher pro Typ neu laden, in der gewählten Größe
            IntPtr h = LoadSized(path, SizePx());
            if (h == IntPtr.Zero) continue;
            if (Native.SetSystemCursor(h, id)) ok++;
            else Native.DestroyCursor(h);
        }
        return ok;
    }

    // Ein Paket übernehmen: jeder Zeigertyp bekommt seine Datei aus dem Paket (fehlende bleiben Windows-Standard)
    void ApplySet(CursorSet set)
    {
        if (set == null) return;
        if (set.IsSystem) { ResetCursors(); return; }
        if (!set.CanApply)
        {
            Status("„" + set.Name + "“ hat keinen normalen Pfeil und lässt sich nicht übernehmen.", Ui.Bad);
            return;
        }
        Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);   // sauberer Ausgangszustand
        int ok = 0;
        foreach (KeyValuePair<uint, CursorItem> kv in set.Roles)
            ok += SetCursorFile(kv.Value.FilePath, new uint[] { kv.Key });
        activeSet = ok > 0 ? set : null;
        MarkActive();
        if (ok > 0)
            Status(string.Format("„{0}“ ist aktiv – {1} Zeigertypen ersetzt. Gilt bis zur Abmeldung.", set.Name, ok), Ui.Good);
        else
            Status("„" + set.Name + "“ ließ sich nicht übernehmen.", Ui.Bad);
    }

    void ResetCursors()
    {
        bool ok = Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        activeSet = ok ? sets.Find(x => x.IsSystem) : null;
        MarkActive();
        loadingUi = true;
        cursorBase = RegistryBase();
        sizeBar.Value = PxToSize(cursorBase);
        loadingUi = false;
        UpdateMouseLabels();
        if (ok) Status("Windows-Zeiger wiederhergestellt.", Ui.Good);
        else Status("Wiederherstellen fehlgeschlagen.", Ui.Bad);
    }

    // ---------------- Dateien & Ordner ----------------

    void OpenFiles()
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "Mauszeiger hinzufügen";
            dlg.Filter = "Mauszeiger (*.cur;*.ani)|*.cur;*.ani|Alle Dateien (*.*)|*.*";
            dlg.Multiselect = true;
            if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(dlg.FileNames);
        }
    }

    void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(libDir);
            Process.Start("explorer.exe", "\"" + libDir + "\"");
        }
        catch (Exception ex)
        {
            Status("Ordner lässt sich nicht öffnen: " + ex.Message, Ui.Bad);
        }
    }

    void AddPaths(string[] paths)
    {
        CursorSet own = sets[sets.Count - 1];
        CursorSet showSet = null;
        int added = 0;

        foreach (string p in paths)
        {
            try
            {
                if (Directory.Exists(p))
                {
                    string dir = p;
                    if (!IsInside(p, libDir))
                    {
                        try
                        {
                            dir = UniqueDir(Path.Combine(libDir, Path.GetFileName(p.TrimEnd('\\', '/'))));
                            CopyCursorFolder(p, dir);
                        }
                        catch (Exception) { dir = p; }   // kein Schreibrecht: direkt aus dem Ordner nutzen
                    }
                    CursorSet s = Library.LoadSet(dir);
                    if (s.Items.Count == 0) continue;
                    sets.Insert(sets.Count - 1, s);
                    showSet = s;
                    added += s.Items.Count;
                }
                else if (File.Exists(p) && Library.IsCursorFile(p))
                {
                    string file = p;
                    if (!IsInside(p, libDir))
                    {
                        try { file = CopyIntoOwn(p, own.Dir); }
                        catch (Exception) { file = p; }
                    }
                    CursorItem it = own.Items.Find(x => string.Equals(x.FilePath, file, StringComparison.OrdinalIgnoreCase));
                    if (it == null)
                    {
                        it = new CursorItem(file);
                        own.Items.Add(it);
                        added++;
                    }
                    showSet = own;
                }
            }
            catch (Exception ex)
            {
                Status("Fehler beim Hinzufügen: " + ex.Message, Ui.Bad);
            }
        }

        if (showSet == null)
        {
            Status("Keine .cur- oder .ani-Dateien gefunden.", Ui.Bad);
            return;
        }
        Library.MapRoles(own);
        RefreshCards();
        Status(added > 0
            ? string.Format("{0} Zeiger hinzugefügt – „{1}“ anklicken, um sie zu übernehmen.", added, showSet.Name)
            : "Schon vorhanden.", Ui.Good);
    }

    static bool IsInside(string path, string dir)
    {
        string full = Path.GetFullPath(path).TrimEnd('\\') + "\\";
        string root = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    static string UniqueDir(string dir)
    {
        string d = dir;
        int n = 2;
        while (Directory.Exists(d) || File.Exists(d)) d = dir + " (" + (n++) + ")";
        return d;
    }

    static string UniqueFile(string dir, string name)
    {
        string stem = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name);
        string f = Path.Combine(dir, name);
        int n = 2;
        while (File.Exists(f)) f = Path.Combine(dir, stem + " (" + (n++) + ")" + ext);
        return f;
    }

    static string CopyIntoOwn(string src, string ownDir)
    {
        Directory.CreateDirectory(ownDir);
        string target = Path.Combine(ownDir, Path.GetFileName(src));
        if (File.Exists(target))
        {
            if (new FileInfo(target).Length == new FileInfo(src).Length) return target;   // schon da
            target = UniqueFile(ownDir, Path.GetFileName(src));
        }
        File.Copy(src, target);
        return target;
    }

    static void CopyCursorFolder(string src, string dst)
    {
        string root = Path.GetFullPath(src).TrimEnd('\\');
        foreach (string f in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext != ".cur" && ext != ".ani" && ext != ".inf") continue;
            string to = Path.Combine(dst, f.Substring(root.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(f, to, true);
        }
    }

    // ---------------- Drag & Drop, Tasten ----------------

    void HookDrop(Control c)
    {
        c.AllowDrop = true;
        c.DragEnter += OnDragEnter;
        c.DragDrop += OnDragDrop;
        foreach (Control child in c.Controls) HookDrop(child);
    }

    void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void OnDragDrop(object sender, DragEventArgs e)
    {
        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths != null) BeginInvoke(new MethodInvoker(delegate { AddPaths(paths); }));
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.O) { OpenFiles(); e.Handled = true; }
        else if (e.KeyCode == Keys.F5) { ReloadLibrary(); e.Handled = true; }
    }

    // ---------------- Gratis-Zeiger: beim ersten Start selbst laden ----------------

    // Name, Pfad im GitHub-Release, SHA-256 der Zip-Datei. Release-Dateien lassen sich nachträglich
    // austauschen; eine Datei mit anderer Prüfsumme wird deshalb verworfen, nicht entpackt.
    static readonly string[,] FreePacks =
    {
        { "GoogleDot Blau", "ful1e5/Google_Cursor/releases/download/v2.0.0/GoogleDot-Blue-Windows.zip",
          "0fbecb1659e2c01ff6f5028aff29b0696e2b01b39a81f084f7e713b92443baf3" },
        { "Bibata Classic", "ful1e5/Bibata_Cursor/releases/download/v2.0.7/Bibata-Modern-Classic-Windows.zip",
          "b98eca4077d6e7796ff7e710e4d220ca13439f1118160d1e7b3db9e91fdec2be" },
        { "Bibata Ice", "ful1e5/Bibata_Cursor/releases/download/v2.0.7/Bibata-Modern-Ice-Windows.zip",
          "0045e40324da5b540b3bee260f53d0792df62be9cdef91655a024ae9f151bd04" },
        { "Banana", "ful1e5/banana-cursor/releases/download/v2.0.0/Banana-Windows.zip",
          "854c66d43ae6783ebad9c240f1dee187791d05a5920248acdd642c864159f2f9" },
        { "Fuchsia Pop", "ful1e5/fuchsia-cursor/releases/download/v2.0.1/Fuchsia-Pop-Windows.zip",
          "7b1064c600f1fa7a843ef1348f8f0b9eb60ce4bd4926d9a604812a54a7b3a052" },
        { "Bibata Amber", "ful1e5/Bibata_Cursor/releases/download/v2.0.7/Bibata-Modern-Amber-Windows.zip",
          "5151e0f8648d00fb734fea501def12b5cc1fbc65ba12a6e28db2e1295ebf8961" },
        { "Pokémon", "ful1e5/pokemon-cursor/releases/download/v2.0.0/Pokemon-Windows.zip",
          "5767fe9c4f48eb436aeb5b65b1086bc3c2427c4ef1c6f27ee136bea537613013" },
        { "macOS", "ful1e5/apple_cursor/releases/download/v2.0.1/macOS-Windows.zip",
          "64a2c74780908954a2ec497f67919709280ebb8ebdf3e3b9336bac071589c9de" }
    };

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ActiveControl = null;      // keine Karte sieht beim Start "ausgewählt" aus
        StartFreeDownload();
    }

    void Post(MethodInvoker m)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke(m); }
        catch (Exception) { }
    }

    // ".gratis-sets" merkt sich, welche Pakete schon einmal da waren: neue Pakete aus einem Update
    // werden nachgeladen, absichtlich gelöschte kommen nicht wieder.
    void StartFreeDownload()
    {
        string marker = Path.Combine(libDir, ".gratis-sets");
        var done = new List<string>();
        try { if (File.Exists(marker)) done.AddRange(File.ReadAllLines(marker)); } catch (Exception) { }
        var todo = new List<int>();
        for (int k = 0; k < FreePacks.GetLength(0); k++)
        {
            string name = FreePacks[k, 0];
            if (Directory.Exists(Path.Combine(libDir, name))) { if (!done.Contains(name)) done.Add(name); }
            else if (!done.Contains(name)) todo.Add(k);
        }
        try { Directory.CreateDirectory(libDir); File.WriteAllLines(marker, done.ToArray()); } catch (Exception) { }
        if (todo.Count == 0) return;

        var worker = new System.Threading.Thread(delegate()
        {
            try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; }
            catch (Exception) { }
            int n = 0, ok = 0;
            foreach (int i in todo)
            {
                string name = FreePacks[i, 0];
                n++;
                string msg = string.Format("Lade Gratis-Zeiger {0}/{1}: {2} …", n, todo.Count, name);
                Post(delegate { Status(msg, Ui.Muted); });
                string dir = DownloadPack(name, "https://github.com/" + FreePacks[i, 1], FreePacks[i, 2]);
                if (dir == null) continue;
                ok++;
                done.Add(name);
                try { File.WriteAllLines(marker, done.ToArray()); } catch (Exception) { }
                Post(delegate { AddDownloadedSet(dir); });
            }
            string end = ok == todo.Count
                ? ok + " Gratis-Sets geladen."
                : string.Format("{0} von {1} Gratis-Sets geladen – der Rest kommt beim nächsten Start.", ok, todo.Count);
            Color c = ok > 0 ? Ui.Good : Ui.Bad;
            Post(delegate { Status(end, c); });
        });
        worker.IsBackground = true;
        worker.Start();
    }

    static string Sha256Hex(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (FileStream fs = File.OpenRead(path))
        {
            var sb = new StringBuilder(64);
            foreach (byte b in sha.ComputeHash(fs)) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // Zip laden, Prüfsumme vergleichen, mit Windows-tar entpacken, Ordner "Regular" nach cursors\<Name> kopieren
    string DownloadPack(string name, string url, string sha256)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "mausi-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            string zip = Path.Combine(tmp, "pack.zip");
            using (var wc = new System.Net.WebClient())
            {
                wc.Headers.Add("User-Agent", "Mausi");
                wc.DownloadFile(url, zip);
            }
            if (!string.Equals(Sha256Hex(zip), sha256, StringComparison.OrdinalIgnoreCase)) return null;
            string x = Path.Combine(tmp, "x");
            Directory.CreateDirectory(x);
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "tar.exe"),
                                           "-xf \"" + zip + "\" -C \"" + x + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (Process p = Process.Start(psi)) p.WaitForExit(120000);

            string src = null;
            var dirs = new List<string> { x };
            dirs.AddRange(Directory.GetDirectories(x, "*", SearchOption.AllDirectories));
            foreach (string d in dirs)
            {
                if (Directory.GetFiles(d, "*.cur").Length == 0 && Directory.GetFiles(d, "*.ani").Length == 0) continue;
                bool regular = d.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0;
                if (src == null || regular) src = d;
                if (regular) break;
            }
            if (src == null) return null;

            string dst = Path.Combine(libDir, name);
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".cur" || ext == ".ani" || ext == ".inf")
                    File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            }
            return dst;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch (Exception) { }
        }
    }

    void AddDownloadedSet(string dir)
    {
        if (sets.Exists(x => string.Equals(x.Dir, dir, StringComparison.OrdinalIgnoreCase))) return;
        CursorSet s = Library.LoadSet(dir);
        if (s.Items.Count == 0) return;
        int at = sets.FindIndex(x => x.Own);
        sets.Insert(at < 0 ? sets.Count : at, s);
        RefreshCards();
    }

    // Start mit "/tray" (Autostart für den Turbo): Fenster bleibt verborgen, nur das Symbol erscheint
    protected override void SetVisibleCore(bool value)
    {
        if (startHidden)
        {
            startHidden = false;
            if (!IsHandleCreated) CreateHandle();
            value = false;
            tray.Visible = true;
        }
        base.SetVisibleCore(value);
    }

    void ShowFromTray()
    {
        tray.Visible = false;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    // Notbremse für den Turbo
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt64() == 1 && speedBar.Value > 20)       // WM_HOTKEY: Turbo aus
        {
            speedBar.Value = 20;
            ApplyMouse();
            Status("Turbo aus (Strg+Alt+T). Tempo steht auf 20, dem Windows-Höchstwert.", Ui.Good);
            return;
        }
        base.WndProc(ref m);
    }

    // Schließen bei aktivem Turbo: in den Infobereich statt beenden
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting && e.CloseReason == CloseReason.UserClosing && Turbo.Factor > 1)
        {
            e.Cancel = true;
            Hide();
            tray.Visible = true;
            if (!trayHint)
            {
                trayHint = true;
                tray.ShowBalloonTip(5000, "Mausi läuft weiter", "Der Turbo braucht Mausi im Hintergrund. Beenden: Rechtsklick auf das Symbol.", ToolTipIcon.Info);
            }
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (padSwitch != null && !padTimer.Enabled) RefreshTouchpad();   // z. B. per Fn-Taste umgeschaltet
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Ui.DarkTitle(Handle);
        Native.RegisterHotKey(Handle, 1, 0x0002 | 0x0001 | 0x4000, (uint)Keys.T);   // Strg+Alt+T = Turbo-Notbremse
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        anim.Stop();
        Application.RemoveMessageFilter(wheelFilter);
        Turbo.Stop();
        Native.UnregisterHotKey(Handle, 1);
        receiver.DestroyHandle();
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }

    [STAThread]
    public static void Main(string[] args)
    {
        bool hidden = args.Length > 0 && string.Equals(args[0], "/tray", StringComparison.OrdinalIgnoreCase);
        if (hidden && Prefs.Get("Turbo", 100) <= 100) { SetAutostart(false); return; }   // Autostart nur für den Turbo

        bool first;
        using (var once = new System.Threading.Mutex(true, "Mausi-nur-einmal", out first))
        {
            if (!first)
            {
                if (!hidden) ShowReceiver.Notify();               // laufende Mausi zeigt sich
                return;
            }
            try { Native.SetProcessDPIAware(); } catch (Exception) { }
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Ui.Scale = Math.Max(1f, g.DpiX / 96f);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MausChangerForm(hidden));
        }
    }
}
