using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace Aeox.App.Tools;

public sealed class RawMouse : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevRemove = 0x00000001;

    private readonly IntPtr _hwnd;
    private readonly Action<int, int> _flush;
    private readonly HwndSource _source;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly int _headerSize = Marshal.SizeOf<RawInputHeader>();
    private IntPtr _buffer = Marshal.AllocHGlobal(64);
    private int _bufferSize = 64;
    private int _dx;
    private int _dy;
    private long _lastFlush;

    public RawMouse(IntPtr hwnd, Action<int, int> flush)
    {
        _hwnd = hwnd;
        _flush = flush;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(Hook);
        var device = new RawInputDevice { UsagePage = 0x01, Usage = 0x02, Flags = RidevInputSink, Target = hwnd };
        Active = RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RawInputDevice>());
        CompositionTarget.Rendering += OnRendering;
    }

    public bool Active { get; }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmInput) return IntPtr.Zero;
        uint size = 0;
        GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, (uint)_headerSize);
        if (size == 0) return IntPtr.Zero;
        if (size > _bufferSize)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = Marshal.AllocHGlobal((int)size);
            _bufferSize = (int)size;
        }
        if (GetRawInputData(lParam, RidInput, _buffer, ref size, (uint)_headerSize) != size) return IntPtr.Zero;
        var header = Marshal.PtrToStructure<RawInputHeader>(_buffer);
        if (header.Type != 0) return IntPtr.Zero;
        var mouse = Marshal.PtrToStructure<RawMouseData>(_buffer + _headerSize);
        if ((mouse.Flags & 0x01) != 0) return IntPtr.Zero;
        _dx += mouse.LastX;
        _dy += mouse.LastY;
        if (_clock.ElapsedTicks - _lastFlush >= Stopwatch.Frequency / 1000) Flush();
        return IntPtr.Zero;
    }

    private void OnRendering(object? sender, EventArgs e) => Flush();

    private void Flush()
    {
        if (_dx == 0 && _dy == 0) return;
        var x = _dx;
        var y = _dy;
        _dx = 0;
        _dy = 0;
        _lastFlush = _clock.ElapsedTicks;
        _flush(x, y);
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        _source.RemoveHook(Hook);
        var device = new RawInputDevice { UsagePage = 0x01, Usage = 0x02, Flags = RidevRemove, Target = IntPtr.Zero };
        RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RawInputDevice>());
        Marshal.FreeHGlobal(_buffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RawMouseData
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public uint Buttons;
        [FieldOffset(8)] public uint RawButtons;
        [FieldOffset(12)] public int LastX;
        [FieldOffset(16)] public int LastY;
        [FieldOffset(20)] public uint Extra;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
}
