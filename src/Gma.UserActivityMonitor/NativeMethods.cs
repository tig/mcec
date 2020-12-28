using System;
using System.Runtime.InteropServices;

#pragma warning disable CA1502, CA1508
namespace Gma.UserActivityMonitor {
    // See https://docs.microsoft.com/en-us/visualstudio/code-quality/ca1060-move-p-invokes-to-nativemethods-class?view=vs-2019
    internal static class NativeMethods {
        // See https://stackoverflow.com/questions/17897646/setwindowshookex-fails-with-error-126
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)]string lpFileName);

        // https://www.pinvoke.net/default.aspx/user32/RegisterPowerSettingNotification.html
        // https://docs.microsoft.com/en-us/archive/msdn-magazine/2007/june/net-matters-handling-messages-in-console-apps
        [DllImport(@"User32", SetLastError = true, EntryPoint = "RegisterPowerSettingNotification",  CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, Int32 Flags);

        [DllImport(@"User32", EntryPoint = "UnregisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
        internal static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        #region WM_POWERBROADCAST constants
        //values from Winuser.h in Microsoft SDK.
        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast
        /// </summary>
        public const int WM_POWERBROADCAST = 0x0218;

        // https://docs.microsoft.com/en-us/windows/win32/power/power-setting-guids

        // https://www.pinvoke.net/default.aspx/user32/RegisterPowerSettingNotification.html
        public static Guid GUID_BATTERY_PERCENTAGE_REMAINING = new Guid("A7AD8041-B45A-4CAE-87A3-EECBB468A9E1");
        public static Guid GUID_MONITOR_POWER_ON = new Guid(0x02731015, 0x4510, 0x4526, 0x99, 0xE6, 0xE5, 0xA1, 0x7E, 0xBD, 0x1A, 0xEA);
        public static Guid GUID_ACDC_POWER_SOURCE = new Guid(0x5D3E9A59, 0xE9D5, 0x4B00, 0xA6, 0xBD, 0xFF, 0x34, 0xFF, 0x51, 0x65, 0x48);
        public static Guid GUID_POWERSCHEME_PERSONALITY = new Guid(0x245D8541, 0x3943, 0x4422, 0xB0, 0x25, 0x13, 0xA7, 0x84, 0xF6, 0x79, 0xB7);
        public static Guid GUID_MAX_POWER_SAVINGS = new Guid(0xA1841308, 0x3541, 0x4FAB, 0xBC, 0x81, 0xF7, 0x15, 0x56, 0xF2, 0x0B, 0x4A);
        // No Power Savings - Almost no power savings measures are used.
        public static Guid GUID_MIN_POWER_SAVINGS = new Guid(0x8C5E7FDA, 0xE8BF, 0x4A96, 0x9A, 0x85, 0xA6, 0xE2, 0x3A, 0x8C, 0x63, 0x5C);
        // Typical Power Savings - Fairly aggressive power savings measures are used.
        public static Guid GUID_TYPICAL_POWER_SAVINGS = new Guid(0x381B4222, 0xF694, 0x41F0, 0x96, 0x85, 0xFF, 0x5B, 0xB2, 0x60, 0xDF, 0x2E);

        // Session specific notification indicating to listeners whether or not the display
        // related to the given session is on/off/dim
        //
        // N.B. This is a session-specific notification, sent only to interactive
        //      session registrants. Session 0 and kernel mode consumers do not receive
        //      this notification.
        //
        // {2B84C20E-AD23-4ddf-93DB-05FFBD7EFCA5}
        public static Guid GUID_SESSION_DISPLAY_STATUS = new Guid(0x2b84c20e, 0xad23, 0x4ddf, 0x93, 0xdb, 0x5, 0xff, 0xbd, 0x7e, 0xfc, 0xa5);

        //
        // Global notification indicating to listeners user activity/presence accross
        // all sessions in the system (Present, NotPresent, Inactive)
        //
        // {786E8A1D-B427-4344-9207-09E70BDCBEA9}
        public static Guid GUID_GLOBAL_USER_PRESENCE = new Guid(0x786e8a1d, 0xb427, 0x4344, 0x92, 0x7, 0x9, 0xe7, 0xb, 0xdc, 0xbe, 0xa9);

        //
        // Session specific notification indicating to listeners user activity/presence
        //(Present, NotPresent, Inactive)
        //
        // N.B. This is a session-specific notification, sent only to interactive
        //      session registrants. Session 0 and kernel mode consumers do not receive
        //      this notification.
        // {3C0F4548-C03F-4c4d-B9F2-237EDE686376}
        public static Guid GUID_SESSION_USER_PRESENCE = new Guid(0x3c0f4548, 0xc03f, 0x4c4d, 0xb9, 0xf2, 0x23, 0x7e, 0xde, 0x68, 0x63, 0x76);

        // Win32 decls and defs
        //
        public const int PBT_APMQUERYSUSPEND = 0x0000;
        public const int PBT_APMQUERYSTANDBY = 0x0001;
        public const int PBT_APMQUERYSUSPENDFAILED = 0x0002;
        public const int PBT_APMQUERYSTANDBYFAILED = 0x0003;
        public const int PBT_APMSUSPEND = 0x0004;
        public const int PBT_APMSTANDBY = 0x0005;
        public const int PBT_APMRESUMECRITICAL = 0x0006;
        public const int PBT_APMRESUMESUSPEND = 0x0007;
        public const int PBT_APMRESUMESTANDBY = 0x0008;
        public const int PBT_APMBATTERYLOW = 0x0009;
        public const int PBT_APMPOWERSTATUSCHANGE = 0x000A; // power status
        public const int PBT_APMOEMEVENT = 0x000B;
        public const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        public const int PBT_POWERSETTINGCHANGE = 0x8013; // DPPE
        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
        public const int DEVICE_NOTIFY_SERVICE_HANDLE = 0x00000001;

        #endregion
        // This structure is sent when the PBT_POWERSETTINGSCHANGE message is sent.
        // It describes the power setting that has changed and contains data about the change
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct POWERBROADCAST_SETTING {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data; 
        }

    }
}
