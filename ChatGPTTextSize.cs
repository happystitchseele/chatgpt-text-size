using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Native {
    internal const int MouseWheelMessage = 0x20A;
    internal const int LowLevelMouseHook = 14;
    internal const uint RootWindow = 2;
    internal const uint InjectedMouse = 1;
    internal const int WheelDelta = 120;
    internal delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSE { public POINT Point; public uint Data,Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct KEY { public ushort Vk,Scan; public uint Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct UNION { [FieldOffset(0)] public KEY Key; [FieldOffset(0)] public MOUSEINPUT Mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint Type; public UNION U; }
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h,uint flag);
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)] internal static extern uint SendInput(uint n,INPUT[] inputs,int size);
    [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetWindowsHookEx(int id,HookProc cb,IntPtr module,uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr h,int c,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string s);
    internal static bool Down(int key) { return (GetAsyncKeyState(key)&0x8000)!=0; }
    internal static bool ModifiersAllowed() { return !Down(0x12)&&!Down(0x10)&&!Down(0x5B)&&!Down(0x5C); }
    internal static bool TrustedPath(string path) {
        return !String.IsNullOrEmpty(path)
            && Path.GetFileName(path).Equals("ChatGPT.exe",StringComparison.OrdinalIgnoreCase)
            && path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)+"\\WindowsApps\\OpenAI.",StringComparison.OrdinalIgnoreCase);
    }
    internal static bool Target(IntPtr window) {
        if(window==IntPtr.Zero) return false;
        uint pid; GetWindowThreadProcessId(window,out pid);
        try { using(var process=Process.GetProcessById((int)pid)) return TrustedPath(process.MainModule.FileName); }
        catch { return false; }
    }
    internal static bool ChatGPTRunning() {
        int session; using(var current=Process.GetCurrentProcess()) session=current.SessionId;
        bool running=false;
        foreach(var process in Process.GetProcessesByName("ChatGPT")) {
            using(process) {
                try { if(process.SessionId==session && TrustedPath(process.MainModule.FileName)) running=true; }
                catch { }
            }
        }
        return running;
    }
    internal static INPUT Key(ushort key,bool up) { var input=new INPUT(); input.Type=1; input.U.Key.Vk=key; input.U.Key.Flags=up?2u:0u; return input; }
    internal static bool Chord(IntPtr window,int direction) {
        if(GetForegroundWindow()!=window || !Target(window) || !ModifiersAllowed()) return false;
        ushort key=direction>0?(ushort)0xBB:(ushort)0xBD;
        bool held=Down(0x11);
        var keys=held?new[]{Key(key,false),Key(key,true)}:new[]{Key(0x11,false),Key(key,false),Key(key,true),Key(0x11,true)};
        uint sent=SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(INPUT)));
        if(sent!=keys.Length) {
            var release=held?new[]{Key(key,true)}:new[]{Key(key,true),Key(0x11,true)};
            SendInput((uint)release.Length,release,Marshal.SizeOf(typeof(INPUT)));
            return false;
        }
        return true;
    }
}

sealed class WheelHelper : ApplicationContext {
    // A message-only window defers input injection until the mouse hook returns.
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly MessageSink sink;
    readonly Native.HookProc callback;
    IntPtr hook,target,observed,pendingTarget;
    bool queued;
    int wheel,pending,ticks;
    public WheelHelper() {
        sink=new MessageSink(Drain);
        callback=MouseHook; hook=Native.SetWindowsHookEx(Native.LowLevelMouseHook,callback,Native.GetModuleHandle(null),0);
        if(hook==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"无法启动鼠标监听");
        timer.Interval=200; timer.Tick+=(s,e)=>RefreshTarget(); RefreshTarget(); timer.Start();
    }
    void RefreshTarget() {
        if(++ticks%5==0 && !Native.ChatGPTRunning()) { ExitThread(); return; }
        var foreground=Native.GetForegroundWindow();
        if(foreground!=observed) {
            observed=foreground; target=Native.Target(foreground)?foreground:IntPtr.Zero; wheel=pending=0;
        }
        if(!Native.Down(0x11)) wheel=0;
    }
    IntPtr MouseHook(int code,IntPtr w,IntPtr l) {
        if(code>=0 && w.ToInt32()==Native.MouseWheelMessage && target!=IntPtr.Zero && Native.Down(0x11) && Native.ModifiersAllowed()) {
            var mouse=(Native.MOUSE)Marshal.PtrToStructure(l,typeof(Native.MOUSE));
            if((mouse.Flags&Native.InjectedMouse)==0 && Native.GetForegroundWindow()==target && Native.GetAncestor(Native.WindowFromPoint(mouse.Point),Native.RootWindow)==target) {
                wheel+=(short)(mouse.Data>>16);
                int steps=wheel/Native.WheelDelta; wheel%=Native.WheelDelta;
                if(steps!=0) {
                    pendingTarget=target; pending=Math.Max(-12,Math.Min(12,pending+steps));
                    if(!queued) queued=Native.PostMessage(sink.Handle,MessageSink.Dispatch,IntPtr.Zero,IntPtr.Zero);
                }
                return (IntPtr)1;
            }
        }
        return Native.CallNextHookEx(hook,code,w,l);
    }
    void Drain() {
        queued=false; int steps=pending; pending=0;
        for(int i=0;i<Math.Abs(steps);i++) if(!Native.Chord(pendingTarget,Math.Sign(steps))) break;
    }
    protected override void ExitThreadCore() {
        timer.Stop(); timer.Dispose(); Native.UnhookWindowsHookEx(hook);
        sink.DestroyHandle(); base.ExitThreadCore();
    }
    sealed class MessageSink : NativeWindow {
        internal const int Dispatch=0x8001;
        readonly Action drain;
        internal MessageSink(Action action) {
            drain=action; CreateHandle(new CreateParams { Parent=(IntPtr)(-3) });
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg==Dispatch) drain(); else base.WndProc(ref message);
        }
    }
}

static class Program {
    [STAThread] static int Main() {
        // Low-level mouse positions are physical pixels; keep hit testing in the same space.
        if(!Native.SetProcessDpiAwarenessContext((IntPtr)(-4))) Native.SetProcessDPIAware();
        bool created;
        using(var mutex=new Mutex(true,"Local\\ChatGPTTextSizeHelper_v1",out created)) {
            if(!created) return 0;
            if(!Native.ChatGPTRunning()) return 0;
            Application.EnableVisualStyles();
            try { Application.Run(new WheelHelper()); }
            catch(Exception error) { MessageBox.Show(error.Message,"ChatGPT 字号助手启动失败"); return 1; }
        }
        return 0;
    }
}
