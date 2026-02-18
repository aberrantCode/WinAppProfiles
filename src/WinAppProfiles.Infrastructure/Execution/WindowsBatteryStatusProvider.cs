using System.Runtime.InteropServices;
using WinAppProfiles.Core.Abstractions;

namespace WinAppProfiles.Infrastructure.Execution;

public sealed class WindowsBatteryStatusProvider : IBatteryStatusProvider
{
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;       // 0 = battery, 1 = AC, 255 = unknown
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    public bool IsOnBattery() =>
        GetSystemPowerStatus(out var status) && status.ACLineStatus == 0;
}
