using System.Globalization;
using System.Runtime.InteropServices;

namespace F1Telemetry.App.Services;

/// <summary>
/// Captures joystick, gamepad, and multi-axis controller button edges through Windows Raw Input.
/// </summary>
public sealed class WindowsRawInputButtonService : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidevInputSink = 0x00000100;
    private const uint RimTypeHid = 2;
    private const ushort UsagePageGenericDesktop = 0x01;
    private const ushort UsageJoystick = 0x04;
    private const ushort UsageGamepad = 0x05;
    private const ushort UsageMultiAxisController = 0x08;
    private readonly Dictionary<IntPtr, byte[]> _lastReportsByDevice = new();
    private readonly Dictionary<IntPtr, string> _deviceNamesByHandle = new();
    private bool _disposed;

    /// <summary>
    /// Raised when a Raw Input HID report contains a button edge.
    /// </summary>
    public event EventHandler<VoiceAiButtonInput>? ButtonInput;

    /// <summary>
    /// Registers Raw Input devices against the supplied window handle.
    /// </summary>
    /// <param name="windowHandle">The WPF window handle.</param>
    /// <param name="statusText">A user-facing registration status.</param>
    public bool TryRegister(IntPtr windowHandle, out string statusText)
    {
        if (windowHandle == IntPtr.Zero)
        {
            statusText = "Raw Input 注册失败：窗口句柄不可用。";
            return false;
        }

        var devices = new[]
        {
            CreateDevice(UsageJoystick, windowHandle),
            CreateDevice(UsageGamepad, windowHandle),
            CreateDevice(UsageMultiAxisController, windowHandle)
        };

        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            statusText = $"Raw Input 注册失败：Win32 {Marshal.GetLastWin32Error()}。";
            return false;
        }

        statusText = "方向盘 Raw Input 已就绪。";
        return true;
    }

    /// <summary>
    /// Processes a WPF window message and emits any matching button edges.
    /// </summary>
    public bool TryProcessMessage(int message, IntPtr lParam)
    {
        if (message != WmInput || lParam == IntPtr.Zero)
        {
            return false;
        }

        var size = 0u;
        GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        if (size == 0)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var readSize = size;
            if (GetRawInputData(lParam, RidInput, buffer, ref readSize, (uint)Marshal.SizeOf<RawInputHeader>()) != size)
            {
                return false;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeHid)
            {
                return false;
            }

            var headerSize = Marshal.SizeOf<RawInputHeader>();
            var hidSize = Marshal.ReadInt32(buffer, headerSize);
            var hidCount = Marshal.ReadInt32(buffer, headerSize + sizeof(uint));
            var byteCount = hidSize * hidCount;
            if (byteCount <= 0 || headerSize + sizeof(uint) * 2 + byteCount > size)
            {
                return false;
            }

            var report = new byte[byteCount];
            Marshal.Copy(IntPtr.Add(buffer, headerSize + sizeof(uint) * 2), report, 0, byteCount);
            EmitChangedButtons(header.Device, report);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lastReportsByDevice.Clear();
        _deviceNamesByHandle.Clear();
    }

    private void EmitChangedButtons(IntPtr deviceHandle, byte[] currentReport)
    {
        var previousReport = _lastReportsByDevice.TryGetValue(deviceHandle, out var previous)
            ? previous
            : Array.Empty<byte>();
        var maxLength = Math.Max(previousReport.Length, currentReport.Length);
        var pressedBits = new List<int>();
        var releasedBits = new List<int>();
        var changedBitCount = 0;

        for (var index = 0; index < maxLength; index++)
        {
            var previousByte = index < previousReport.Length ? previousReport[index] : (byte)0;
            var currentByte = index < currentReport.Length ? currentReport[index] : (byte)0;
            var changed = previousByte ^ currentByte;
            if (changed == 0)
            {
                continue;
            }

            for (var bit = 0; bit < 8; bit++)
            {
                var mask = 1 << bit;
                if ((changed & mask) == 0)
                {
                    continue;
                }

                changedBitCount++;
                var bitIndex = index * 8 + bit;
                if ((currentByte & mask) != 0)
                {
                    pressedBits.Add(bitIndex);
                }
                else
                {
                    releasedBits.Add(bitIndex);
                }
            }
        }

        _lastReportsByDevice[deviceHandle] = currentReport.ToArray();
        if (changedBitCount == 0)
        {
            return;
        }

        var deviceName = GetDeviceName(deviceHandle);
        foreach (var bitIndex in pressedBits)
        {
            RaiseInput(deviceHandle, deviceName, bitIndex, isPressed: true, pressedBits.Count, changedBitCount);
        }

        foreach (var bitIndex in releasedBits)
        {
            RaiseInput(deviceHandle, deviceName, bitIndex, isPressed: false, pressedBits.Count, changedBitCount);
        }
    }

    private void RaiseInput(
        IntPtr deviceHandle,
        string deviceName,
        int bitIndex,
        bool isPressed,
        int pressedChangeCount,
        int changedBitCount)
    {
        ButtonInput?.Invoke(
            this,
            new VoiceAiButtonInput
            {
                DeviceId = GetDeviceId(deviceHandle, deviceName),
                DeviceName = deviceName,
                ButtonIndex = bitIndex + 1,
                ButtonMask = bitIndex < 64 ? 1UL << bitIndex : 0,
                IsPressed = isPressed,
                PressedChangeCount = pressedChangeCount,
                ChangedBitCount = changedBitCount,
                ReceivedAt = DateTimeOffset.UtcNow
            });
    }

    private string GetDeviceName(IntPtr deviceHandle)
    {
        if (_deviceNamesByHandle.TryGetValue(deviceHandle, out var cachedName))
        {
            return cachedName;
        }

        var size = 0u;
        GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, IntPtr.Zero, ref size);
        if (size == 0)
        {
            return "Raw Input 设备";
        }

        var buffer = Marshal.AllocHGlobal((int)size * 2);
        try
        {
            var readSize = size;
            var result = GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, buffer, ref readSize);
            var name = result == uint.MaxValue
                ? "Raw Input 设备"
                : (Marshal.PtrToStringUni(buffer) ?? "Raw Input 设备");
            _deviceNamesByHandle[deviceHandle] = name;
            return name;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetDeviceId(IntPtr deviceHandle, string deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName)
            ? deviceHandle.ToInt64().ToString(CultureInfo.InvariantCulture)
            : deviceName;
    }

    private static RawInputDevice CreateDevice(ushort usage, IntPtr windowHandle)
    {
        return new RawInputDevice
        {
            UsagePage = UsagePageGenericDesktop,
            Usage = usage,
            Flags = RidevInputSink,
            Target = windowHandle
        };
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] rawInputDevices,
        uint numDevices,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);
}
