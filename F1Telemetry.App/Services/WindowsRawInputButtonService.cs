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
    private const uint RidiPreparsedData = 0x20000005;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidevInputSink = 0x00000100;
    private const uint RimTypeHid = 2;
    private const int HidpStatusSuccess = 0x00110000;
    private const ushort UsagePageGenericDesktop = 0x01;
    private const ushort UsagePageButton = 0x09;
    private const ushort UsageJoystick = 0x04;
    private const ushort UsageGamepad = 0x05;
    private const ushort UsageMultiAxisController = 0x08;
    private readonly RawInputButtonStateTracker _buttonStateTracker = new();
    private readonly Dictionary<IntPtr, string> _deviceNamesByHandle = new();
    private readonly Dictionary<IntPtr, IntPtr> _preparsedDataByHandle = new();
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

            var reportOffset = headerSize + sizeof(uint) * 2;
            for (var reportIndex = 0; reportIndex < hidCount; reportIndex++)
            {
                var report = new byte[hidSize];
                Marshal.Copy(IntPtr.Add(buffer, reportOffset + reportIndex * hidSize), report, 0, hidSize);
                EmitChangedButtons(header.Device, report);
            }

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
        _buttonStateTracker.Clear();
        _deviceNamesByHandle.Clear();
        foreach (var preparsedData in _preparsedDataByHandle.Values)
        {
            Marshal.FreeHGlobal(preparsedData);
        }

        _preparsedDataByHandle.Clear();
    }

    private void EmitChangedButtons(IntPtr deviceHandle, byte[] currentReport)
    {
        var deviceName = GetDeviceName(deviceHandle);
        var deviceId = GetDeviceId(deviceHandle, deviceName);
        var pressedButtonIndexes = GetPressedButtonIndexes(deviceHandle, currentReport);
        if (pressedButtonIndexes is null)
        {
            return;
        }

        foreach (var edge in _buttonStateTracker.Observe(deviceId, GetReportId(currentReport), pressedButtonIndexes))
        {
            RaiseInput(deviceId, deviceName, edge);
        }
    }

    private void RaiseInput(
        string deviceId,
        string deviceName,
        RawInputButtonEdge edge)
    {
        ButtonInput?.Invoke(
            this,
            new VoiceAiButtonInput
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                ButtonIndex = edge.ButtonIndex,
                ButtonMask = edge.ButtonIndex is > 0 and <= 64 ? 1UL << (edge.ButtonIndex - 1) : 0,
                IsPressed = edge.IsPressed,
                PressedChangeCount = edge.PressedChangeCount,
                ChangedBitCount = edge.ChangedButtonCount,
                ReceivedAt = DateTimeOffset.UtcNow
            });
    }

    private IReadOnlyList<int>? GetPressedButtonIndexes(IntPtr deviceHandle, byte[] report)
    {
        var preparsedData = GetPreparsedData(deviceHandle);
        if (preparsedData == IntPtr.Zero ||
            HidP_GetCaps(preparsedData, out var capabilities) != HidpStatusSuccess ||
            capabilities.NumberInputDataIndices == 0)
        {
            return null;
        }

        var usageList = new ushort[Math.Clamp(capabilities.NumberInputDataIndices, (ushort)1, (ushort)1024)];
        var usageLength = (uint)usageList.Length;
        var status = HidP_GetUsages(
            HidpReportType.Input,
            UsagePageButton,
            0,
            usageList,
            ref usageLength,
            preparsedData,
            report,
            (uint)report.Length);
        if (status != HidpStatusSuccess || usageLength == 0)
        {
            return status == HidpStatusSuccess ? [] : null;
        }

        return usageList
            .Take((int)usageLength)
            .Where(usage => usage > 0)
            .Select(usage => (int)usage)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static int GetReportId(byte[] report)
    {
        return report.Length == 0 ? 0 : report[0];
    }

    private IntPtr GetPreparsedData(IntPtr deviceHandle)
    {
        if (_preparsedDataByHandle.TryGetValue(deviceHandle, out var cachedPreparsedData))
        {
            return cachedPreparsedData;
        }

        var size = 0u;
        GetRawInputDeviceInfo(deviceHandle, RidiPreparsedData, IntPtr.Zero, ref size);
        if (size == 0)
        {
            return IntPtr.Zero;
        }

        var preparsedData = Marshal.AllocHGlobal((int)size);
        var readSize = size;
        var result = GetRawInputDeviceInfo(deviceHandle, RidiPreparsedData, preparsedData, ref readSize);
        if (result == uint.MaxValue)
        {
            Marshal.FreeHGlobal(preparsedData);
            return IntPtr.Zero;
        }

        _preparsedDataByHandle[deviceHandle] = preparsedData;
        return preparsedData;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    private enum HidpReportType
    {
        Input = 0,
        Output = 1,
        Feature = 2
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

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(
        IntPtr preparsedData,
        out HidpCaps capabilities);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(
        HidpReportType reportType,
        ushort usagePage,
        ushort linkCollection,
        [Out] ushort[] usageList,
        ref uint usageLength,
        IntPtr preparsedData,
        byte[] report,
        uint reportLength);
}
