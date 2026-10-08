// CoreAudioInterop.cs — Windows Core Audio (WASAPI) COM interop declarations.
//
// Two independent COM surfaces are declared here:
//   1. The public, documented IMMDeviceEnumerator / IMMDevice / IPropertyStore trio,
//      used to enumerate audio endpoints and read their friendly names.
//   2. The undocumented IPolicyConfig, used to change the *default* endpoint.
//      Windows has never shipped a public API for that, so every "default device
//      switcher" (NirCmd, SoundVolumeView, AudioDeviceCmdlets, SoundSwitch, ...)
//      drives this interface. Its vtable order is load-bearing: do not reorder.
//
// Written against C# 5 (the .NET Framework csc.exe shipping with Windows).

using System;
using System.Runtime.InteropServices;

namespace AudioSwitch
{
    internal enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    internal enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [Flags]
    internal enum DeviceStateFlags
    {
        ACTIVE = 0x00000001,
        DISABLED = 0x00000002,
        NOTPRESENT = 0x00000004,
        UNPLUGGED = 0x00000008,
        ALL = 0x0000000F
    }

    /// <summary>Property key: a format GUID plus a property id (replaces PROPID).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public int pid;

        public PROPERTYKEY(Guid formatId, int propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }
    }

    /// <summary>
    /// PROPVARIANT, laid out for the one thing we need from it: reading a
    /// VT_LPWSTR string. The union always begins at offset 8 because of the
    /// three reserved shorts that follow the 2-byte vt tag.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct PROPVARIANT
    {
        [FieldOffset(0)] public short vt;
        [FieldOffset(2)] public short wReserved1;
        [FieldOffset(4)] public short wReserved2;
        [FieldOffset(6)] public short wReserved3;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    internal static class PropertyKeys
    {
        /// <summary>e.g. "扬声器 (AB13X USB Audio)" — the name NirCmd matches on.</summary>
        public static readonly PROPERTYKEY DeviceFriendlyName =
            new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

        /// <summary>e.g. "扬声器" — the short name shown in the Sound control panel.</summary>
        public static readonly PROPERTYKEY DeviceDesc =
            new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);

        /// <summary>e.g. "AB13X USB Audio" — the hardware/driver name.</summary>
        public static readonly PROPERTYKEY DeviceInterfaceFriendlyName =
            new PROPERTYKEY(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 6);
    }

    internal static class ComInterop
    {
        public const int STGM_READ = 0x00000000;
        public const int CLSCTX_ALL = 23;
        public const short VT_LPWSTR = 31;
        public const short VT_EMPTY = 0;

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = false)]
        public static extern void PropVariantClear(ref PROPVARIANT pvar);

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = false)]
        public static extern void CoCreateInstance(
            ref Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
            int dwClsContext,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    // ---------------------------------------------------------------------
    // Public Core Audio API
    // ---------------------------------------------------------------------

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        int GetCount(out int count);
        int Item(int index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                     [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        int GetCount(out int propertyCount);
        int GetAt(int propertyIndex, out PROPERTYKEY key);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        int Commit();
    }

    // ---------------------------------------------------------------------
    // Undocumented IPolicyConfig — the only way to change the default endpoint
    // ---------------------------------------------------------------------

    /// <summary>CPolicyConfigClient. Registered on Windows 7 through 11.</summary>
    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    internal class PolicyConfigClient
    {
    }

    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, out IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool isDefault, out IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool isDefault, IntPtr defaultPeriod, IntPtr minimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, out IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool fxStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool fxStore, IntPtr key, IntPtr value);

        /// <summary>
        /// Despite the parameter name this takes an *endpoint ID*
        /// ("{0.0.0.00000000}.{guid}"), not a friendly name.
        /// </summary>
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ERole role);

        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceName, [MarshalAs(UnmanagedType.Bool)] bool visible);
    }
}
