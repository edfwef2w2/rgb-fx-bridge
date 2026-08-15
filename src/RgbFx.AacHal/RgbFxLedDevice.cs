using System.Runtime.InteropServices;

namespace RgbFx.AacHal;

/// <summary>Virtual AddressableStrip-like device (IAacLedDevice + Opt + VariedLedCount).</summary>
[ComVisible(true)]
[Guid("C3D91E40-8A2B-4F7E-9D11-6E4B2A0C8F55")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class RgbFxLedDevice :
    IAacLedDevice,
    IAacLedDeviceOpt,
    IAacLedDeviceVariedLedCount,
    IAacLedDevice2,
    IAacLedDeviceOpt2,
    ICustomQueryInterface
{
    private readonly MsiFrameSink _sink;
    private readonly string[] _zones;
    private readonly string _title;
    private readonly int _argbId;
    private readonly bool _keyboard;
    private int _ledCount;
    private readonly object _gate = new();
    private uint[] _lastColors;
    private uint _lastEffect = 1;

    public RgbFxLedDevice(
        MsiFrameSink sink,
        string zone,
        string title,
        int argbId,
        int ledCount = CapabilityBuilder.DefaultLedCount)
        : this(sink, new[] { zone }, title, argbId, ledCount, keyboard: false)
    {
    }

    public RgbFxLedDevice(
        MsiFrameSink sink,
        IReadOnlyList<string> zones,
        string title,
        int argbId,
        int ledCount = CapabilityBuilder.DefaultLedCount,
        bool keyboard = false)
    {
        _sink = sink;
        _zones = zones.Where(z => !string.IsNullOrWhiteSpace(z)).ToArray();
        _title = title;
        _argbId = argbId;
        _keyboard = keyboard;
        _ledCount = Math.Clamp(ledCount, 1, AuraLedCap());
        _lastColors = new uint[_ledCount];
        for (int i = 0; i < _ledCount; i++)
            _lastColors[i] = 0x00FFFFFF;
    }

    public string Zone => _zones.Length > 0 ? _zones[0] : "";
    public string Title => _title;

    public int LedCount => _ledCount;

    static int AuraLedCap()
    {
        var n = ZoneCatalog.LoadLighting().AuraLeds;
        return Math.Clamp(n, 1, CapabilityBuilder.MaxReportedLeds);
    }

    CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out IntPtr ppv)
    {
        // Log native QI traffic (helps diagnose LS EXCEPTION path)
        if (iid != Ole32.IidIUnknown)
            Log($"device QI {iid}");
        ppv = IntPtr.Zero;
        return CustomQueryInterfaceResult.NotHandled;
    }

    int IAacLedDevice.GetCapability(out string capability)
        => GetCapabilityCore(out capability);

    int IAacLedDevice.SetEffect(uint effectId, IntPtr colors, uint numberOfColors)
        => SetEffectCore(effectId, colors, numberOfColors, speed: 0, direction: 0, hasOpt: false);

    int IAacLedDevice.Synchronize(uint effectId, ulong milliseconds)
    {
        Log($"Synchronize effect={effectId} ms={milliseconds}");
        return 0;
    }

    int IAacLedDeviceOpt.GetCapability(out string capability)
        => GetCapabilityCore(out capability);

    int IAacLedDeviceOpt.SetEffect(uint effectId, IntPtr colors, uint numberOfColors)
        => SetEffectCore(effectId, colors, numberOfColors, speed: 0, direction: 0, hasOpt: false);

    int IAacLedDeviceOpt.Synchronize(uint effectId, ulong milliseconds)
        => ((IAacLedDevice)this).Synchronize(effectId, milliseconds);

    int IAacLedDeviceOpt.SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction)
        => SetEffectCore(effectId, colors, numberOfColors, speed, direction, hasOpt: true);

    int IAacLedDeviceVariedLedCount.GetCapability(out string capability)
        => GetCapabilityCore(out capability);

    int IAacLedDeviceVariedLedCount.SetEffect(uint effectId, IntPtr colors, uint numberOfColors)
        => SetEffectCore(effectId, colors, numberOfColors, speed: 0, direction: 0, hasOpt: false);

    int IAacLedDeviceVariedLedCount.Synchronize(uint effectId, ulong milliseconds)
        => ((IAacLedDevice)this).Synchronize(effectId, milliseconds);

    int IAacLedDeviceVariedLedCount.SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction)
        => SetEffectCore(effectId, colors, numberOfColors, speed, direction, hasOpt: true);

    int IAacLedDeviceVariedLedCount.GetManualLedCount(out uint ledCount)
    {
        ledCount = (uint)_ledCount;
        Log($"GetManualLedCount => {ledCount}");
        return 0;
    }

    int IAacLedDeviceVariedLedCount.SetManualLedCount(uint ledCount)
    {
        lock (_gate)
        {
            int n = Math.Clamp((int)ledCount, 1, AuraLedCap());
            if (n != _ledCount)
            {
                _ledCount = n;
                var next = new uint[_ledCount];
                for (int i = 0; i < _ledCount; i++)
                    next[i] = _lastColors.Length > 0 ? _lastColors[i % _lastColors.Length] : 0x00FFFFFF;
                _lastColors = next;
            }
            Log($"SetManualLedCount => {_ledCount}");
        }
        return 0;
    }

    // ---- IAacLedDevice2 / Opt2 (LS QIs Opt2 after Enumerate2) ----
    int IAacLedDevice2.GetCapability(out string capability) => GetCapabilityCore(out capability);
    int IAacLedDevice2.SetEffect(uint effectId, IntPtr colors, uint numberOfColors)
        => SetEffectCore(effectId, colors, numberOfColors, 0, 0, false);
    int IAacLedDevice2.Synchronize(uint effectId, ulong milliseconds)
        => ((IAacLedDevice)this).Synchronize(effectId, milliseconds);
    int IAacLedDevice2.SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction)
        => SetEffectCore(effectId, colors, numberOfColors, speed, direction, true);
    int IAacLedDevice2.SetEffect2(uint effectId, object colors, uint numberOfColors)
        => SetEffectFromVariant(effectId, colors, numberOfColors, 0, 0, false);

    int IAacLedDeviceOpt2.GetCapability(out string capability) => GetCapabilityCore(out capability);
    int IAacLedDeviceOpt2.SetEffect(uint effectId, IntPtr colors, uint numberOfColors)
        => SetEffectCore(effectId, colors, numberOfColors, 0, 0, false);
    int IAacLedDeviceOpt2.Synchronize(uint effectId, ulong milliseconds)
        => ((IAacLedDevice)this).Synchronize(effectId, milliseconds);
    int IAacLedDeviceOpt2.SetEffectOptSpeed(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction)
        => SetEffectCore(effectId, colors, numberOfColors, speed, direction, true);
    int IAacLedDeviceOpt2.SetEffect2(uint effectId, object colors, uint numberOfColors)
        => SetEffectFromVariant(effectId, colors, numberOfColors, 0, 0, false);
    int IAacLedDeviceOpt2.SetEffectOptSpeed2(uint effectId, object colors, uint numberOfColors, uint speed, uint direction)
        => SetEffectFromVariant(effectId, colors, numberOfColors, speed, direction, true);

    int SetEffectFromVariant(uint effectId, object? colors, uint numberOfColors, uint speed, uint direction, bool hasOpt)
    {
        try
        {
            var arr = ColorsFromVariant(colors, numberOfColors);
            // Pin array and call core via temp buffer
            if (arr.Length == 0)
                return SetEffectCore(effectId, IntPtr.Zero, 0, speed, direction, hasOpt);

            var gch = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                return SetEffectCore(effectId, gch.AddrOfPinnedObject(), (uint)arr.Length, speed, direction, hasOpt);
            }
            finally
            {
                gch.Free();
            }
        }
        catch (Exception ex)
        {
            Log("SetEffectFromVariant error: " + ex.Message);
            return unchecked((int)0x80004005);
        }
    }

    static uint[] ColorsFromVariant(object? colors, uint numberOfColors)
    {
        if (colors is null)
            return Array.Empty<uint>();

        if (colors is uint[] ua)
            return ua.Length == 0 ? ua : ua;
        if (colors is int[] ia)
        {
            var o = new uint[ia.Length];
            for (int i = 0; i < ia.Length; i++) o[i] = unchecked((uint)ia[i]);
            return o;
        }
        if (colors is Array arr)
        {
            int n = arr.Length;
            if (numberOfColors > 0 && numberOfColors < n) n = (int)numberOfColors;
            var o = new uint[n];
            for (int i = 0; i < n; i++)
            {
                var el = arr.GetValue(i);
                o[i] = el switch
                {
                    uint u => u,
                    int i32 => unchecked((uint)i32),
                    long l => unchecked((uint)l),
                    _ => Convert.ToUInt32(el ?? 0),
                };
            }
            return o;
        }

        // Single numeric?
        try
        {
            return new[] { Convert.ToUInt32(colors) };
        }
        catch
        {
            return Array.Empty<uint>();
        }
    }

    int GetCapabilityCore(out string capability)
    {
        capability = CapabilityBuilder.BuildDefault(_ledCount, _title, _argbId);
        Log($"GetCapability leds={_ledCount} bytes={capability.Length}");
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "last-capability.txt"), capability);
        }
        catch { /* ignore */ }
        return 0;
    }

    int SetEffectCore(uint effectId, IntPtr colors, uint numberOfColors, uint speed, uint direction, bool hasOpt)
    {
        try
        {
            var arr = ReadColors(colors, numberOfColors);
            lock (_gate)
            {
                _lastEffect = effectId;
                if (arr.Length > 0)
                {
                    _lastColors = new uint[_ledCount];
                    for (int i = 0; i < _ledCount; i++)
                        _lastColors[i] = arr[i % arr.Length];
                }

                uint s0 = arr.Length > 0 ? arr[0] : 0;
                uint s1 = arr.Length > 1 ? arr[arr.Length / 2] : s0;
                uint s2 = arr.Length > 2 ? arr[^1] : s0;
                Log($"SetEffect id={effectId} n={numberOfColors} speed={speed} dir={direction} opt={hasOpt} kb={_keyboard} samples=0x{s0:X8},0x{s1:X8},0x{s2:X8}");

                if (effectId == 101)
                    OffAll();
                else
                    PushColors(arr.Length > 0 ? Expand(arr) : _lastColors, effectId);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log("SetEffect error: " + ex.Message);
            return unchecked((int)0x80004005);
        }
    }

    uint[] Expand(uint[] arr)
    {
        var o = new uint[_ledCount];
        for (int i = 0; i < _ledCount; i++)
            o[i] = arr[i % arr.Length];
        return o;
    }

    void PushColors(uint[] colors, uint effectId = 0)
    {
        if (_zones.Length == 0)
            return;
        _sink.ApplyColors(_zones, colors, effectId);
    }

    void OffAll()
    {
        if (_zones.Length == 0)
            return;
        _sink.Off(_zones);
    }

    static uint[] ReadColors(IntPtr colors, uint numberOfColors)
    {
        if (colors == IntPtr.Zero || numberOfColors == 0)
            return Array.Empty<uint>();
        var n = (int)Math.Min(numberOfColors, 512);
        var arr = new uint[n];
        for (int i = 0; i < n; i++)
            arr[i] = (uint)Marshal.ReadInt32(colors, i * 4);
        return arr;
    }

    static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "RgbFx", "AacHal");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "aachal.log"),
                $"[{DateTime.Now:O}] {msg}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }
}
