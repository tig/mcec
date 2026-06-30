using System;
using System.Runtime.InteropServices;

namespace Gma.UserActivityMonitor;

// This structure is sent when the PBT_POWERSETTINGSCHANGE message is sent.
// It describes the power setting that has changed and contains data about the change
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct POWERBROADCAST_SETTING {
    public Guid PowerSetting;
    public uint DataLength;
    public byte Data;
}
