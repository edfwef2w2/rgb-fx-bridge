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
    private int _ledCount;
    private readonly object _gate = new();
    private uint[] _lastColors;
    private uint _lastEffect = 1;
    private CancellationTokenSource? _animCts;

    public RgbFxLedDevice(MsiFrameSink sink, int ledCount = CapabilityBuilder.DefaultLedCount)
    {
        _sink = sink;
        _ledCount = Math.Clamp(ledCount, 1, 120);
        _lastColors = new uint[_ledCount];
        for (int i = 0; i < _ledCount; i++)
            _lastColors[i] = 0x00FFFFFF;
    }

    public int LedCount => _ledCount;

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
            int n = Math.Clamp((int)ledCount, 1, 120);
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
        capability = CapabilityBuilder.BuildDefault(_ledCount);
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

                Log($"SetEffect id={effectId} n={numberOfColors} speed={speed} dir={direction} opt={hasOpt} sample=0x{(arr.Length > 0 ? arr[0] : 0):X8}");

                StopAnim_NoLock();

                switch (effectId)
                {
                    case 101:
                        _sink.Off();
                        break;
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 17:
                        if (arr.Length > 0)
                            _sink.ApplyColors(Expand(arr));
                        break;
                    case 5:
                        StartRainbow_NoLock(speed);
                        break;
                    case 13:
                        StartStarry_NoLock(speed);
                        break;
                    default:
                        if (arr.Length > 0)
                            _sink.ApplyColors(Expand(arr));
                        else
                            StartRainbow_NoLock(speed);
                        break;
                }
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

    void StopAnim_NoLock()
    {
        try { _animCts?.Cancel(); } catch { /* ignore */ }
        _animCts = null;
    }

    void StartRainbow_NoLock(uint speed)
    {
        var cts = new CancellationTokenSource();
        _animCts = cts;
        var period = speed switch
        {
            0 => 80,
            1 => 50,
            2 => 30,
            _ => 60,
        };
        _ = Task.Run(async () =>
        {
            double t = 0;
            while (!cts.IsCancellationRequested)
            {
                var cols = new uint[_ledCount];
                for (int i = 0; i < _ledCount; i++)
                {
                    var hue = (t + i * (360.0 / _ledCount)) % 360.0;
                    cols[i] = HsvToBgr(hue, 1, 1);
                }
                _sink.ApplyColors(cols);
                t += 8;
                try { await Task.Delay(period, cts.Token); }
                catch { break; }
            }
        }, cts.Token);
    }

    void StartStarry_NoLock(uint speed)
    {
        var cts = new CancellationTokenSource();
        _animCts = cts;
        var period = speed >= 2 ? 40 : 90;
        var rng = new Random();
        _ = Task.Run(async () =>
        {
            var cols = new uint[_ledCount];
            while (!cts.IsCancellationRequested)
            {
                Array.Clear(cols);
                int sparks = Math.Max(1, _ledCount / 4);
                for (int s = 0; s < sparks; s++)
                {
                    int i = rng.Next(_ledCount);
                    byte v = (byte)rng.Next(80, 256);
                    cols[i] = (uint)(v | (v << 8) | (v << 16));
                }
                _sink.ApplyColors(cols);
                try { await Task.Delay(period, cts.Token); }
                catch { break; }
            }
        }, cts.Token);
    }

    static uint HsvToBgr(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;
        double r1 = 0, g1 = 0, b1 = 0;
        if (h < 60) { r1 = c; g1 = x; }
        else if (h < 120) { r1 = x; g1 = c; }
        else if (h < 180) { g1 = c; b1 = x; }
        else if (h < 240) { g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; b1 = c; }
        else { r1 = c; b1 = x; }
        byte R = (byte)((r1 + m) * 255);
        byte G = (byte)((g1 + m) * 255);
        byte B = (byte)((b1 + m) * 255);
        return (uint)(B << 16 | G << 8 | R);
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
