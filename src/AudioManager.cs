// AudioManager.cs — enumerate audio output devices and change the default one.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AudioSwitch
{
    /// <summary>One audio endpoint (a "sound device" as the user sees it).</summary>
    internal sealed class AudioEndpoint
    {
        /// <summary>Endpoint ID, e.g. "{0.0.0.00000000}.{07b9b8a1-...}". Stable across reboots.</summary>
        public string Id = "";

        /// <summary>Full name, e.g. "扬声器 (AB13X USB Audio)".</summary>
        public string FriendlyName = "";

        /// <summary>Short name, e.g. "扬声器".</summary>
        public string Description = "";

        /// <summary>Adapter/driver name, e.g. "AB13X USB Audio".</summary>
        public string InterfaceName = "";

        public DeviceStateFlags State = 0;

        public bool IsActive
        {
            get { return (State & DeviceStateFlags.ACTIVE) == DeviceStateFlags.ACTIVE; }
        }

        /// <summary>Name to show if the full friendly name is unavailable.</summary>
        public string BestName
        {
            get
            {
                if (!string.IsNullOrEmpty(FriendlyName)) return FriendlyName;
                if (!string.IsNullOrEmpty(Description) && !string.IsNullOrEmpty(InterfaceName))
                    return Description + " (" + InterfaceName + ")";
                if (!string.IsNullOrEmpty(Description)) return Description;
                if (!string.IsNullOrEmpty(InterfaceName)) return InterfaceName;
                return Id;
            }
        }

        public string StateText
        {
            get
            {
                if ((State & DeviceStateFlags.ACTIVE) != 0) return "已启用";
                if ((State & DeviceStateFlags.DISABLED) != 0) return "已禁用";
                if ((State & DeviceStateFlags.NOTPRESENT) != 0) return "未连接";
                if ((State & DeviceStateFlags.UNPLUGGED) != 0) return "已拔出";
                return "未知";
            }
        }

        public override string ToString()
        {
            return BestName;
        }
    }

    /// <summary>Which of the three Windows default-device slots to change.</summary>
    [Flags]
    internal enum RoleSelection
    {
        None = 0,
        Console = 1,
        Multimedia = 2,
        Communications = 4,
        All = Console | Multimedia | Communications
    }

    internal static class AudioManager
    {
        /// <summary>Roles in the order Windows uses them.</summary>
        public static readonly ERole[] AllRoles =
        {
            ERole.eConsole, ERole.eMultimedia, ERole.eCommunications
        };

        public static RoleSelection RoleToFlag(ERole role)
        {
            switch (role)
            {
                case ERole.eConsole: return RoleSelection.Console;
                case ERole.eMultimedia: return RoleSelection.Multimedia;
                default: return RoleSelection.Communications;
            }
        }

        public static ERole FlagToRole(RoleSelection flag)
        {
            switch (flag)
            {
                case RoleSelection.Console: return ERole.eConsole;
                case RoleSelection.Multimedia: return ERole.eMultimedia;
                default: return ERole.eCommunications;
            }
        }

        public static string RoleName(ERole role)
        {
            switch (role)
            {
                case ERole.eConsole: return "控制台";
                case ERole.eMultimedia: return "多媒体";
                default: return "通讯";
            }
        }

        private static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)(new MMDeviceEnumerator());
        }

        /// <summary>
        /// Enumerate output (render) endpoints. Pass DeviceStateFlags.ALL to include
        /// disabled/unplugged devices, which the Sound control panel also lists.
        /// </summary>
        public static List<AudioEndpoint> GetOutputDevices(DeviceStateFlags stateMask)
        {
            List<AudioEndpoint> result = new List<AudioEndpoint>();
            IMMDeviceEnumerator enumerator = CreateEnumerator();
            IMMDeviceCollection collection = null;
            try
            {
                int hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, (int)stateMask, out collection);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                int count;
                hr = collection.GetCount(out count);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                for (int i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        hr = collection.Item(i, out device);
                        if (hr != 0) continue;
                        AudioEndpoint endpoint = ReadEndpoint(device);
                        if (endpoint != null) result.Add(endpoint);
                    }
                    catch (Exception)
                    {
                        // A device can disappear mid-enumeration; skip it.
                    }
                    finally
                    {
                        ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                ReleaseComObject(collection);
                ReleaseComObject(enumerator);
            }

            // Stable, human-friendly ordering: active first, then by name.
            result.Sort(delegate(AudioEndpoint a, AudioEndpoint b)
            {
                if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
                int byName = string.Compare(a.BestName, b.BestName, StringComparison.CurrentCulture);
                if (byName != 0) return byName;
                return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static AudioEndpoint ReadEndpoint(IMMDevice device)
        {
            string id;
            if (device.GetId(out id) != 0 || string.IsNullOrEmpty(id)) return null;

            AudioEndpoint endpoint = new AudioEndpoint();
            endpoint.Id = id;

            int state;
            if (device.GetState(out state) == 0) endpoint.State = (DeviceStateFlags)state;

            IPropertyStore store = null;
            try
            {
                if (device.OpenPropertyStore(ComInterop.STGM_READ, out store) == 0 && store != null)
                {
                    endpoint.FriendlyName = ReadString(store, PropertyKeys.DeviceFriendlyName);
                    endpoint.Description = ReadString(store, PropertyKeys.DeviceDesc);
                    endpoint.InterfaceName = ReadString(store, PropertyKeys.DeviceInterfaceFriendlyName);
                }
            }
            finally
            {
                ReleaseComObject(store);
            }

            return endpoint;
        }

        private static string ReadString(IPropertyStore store, PROPERTYKEY key)
        {
            PROPVARIANT value = new PROPVARIANT();
            try
            {
                if (store.GetValue(ref key, out value) != 0) return "";
                if (value.vt != ComInterop.VT_LPWSTR || value.pointerValue == IntPtr.Zero) return "";
                return Marshal.PtrToStringUni(value.pointerValue) ?? "";
            }
            finally
            {
                if (value.vt != ComInterop.VT_EMPTY)
                {
                    try { ComInterop.PropVariantClear(ref value); }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>Endpoint ID of the current default output device for a role, or "" if none.</summary>
        public static string GetDefaultDeviceId(ERole role)
        {
            IMMDeviceEnumerator enumerator = CreateEnumerator();
            IMMDevice device = null;
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, role, out device) != 0) return "";
                string id;
                if (device.GetId(out id) != 0) return "";
                return id ?? "";
            }
            catch (Exception)
            {
                return "";
            }
            finally
            {
                ReleaseComObject(device);
                ReleaseComObject(enumerator);
            }
        }

        /// <summary>
        /// Set the default output device for the selected roles.
        /// Returns null on success, or a human-readable error message.
        /// </summary>
        public static string SetDefaultDevice(string endpointId, RoleSelection roles)
        {
            if (string.IsNullOrEmpty(endpointId)) return "未指定音频设备。";
            if (roles == RoleSelection.None) roles = RoleSelection.All;

            IPolicyConfig policy = null;
            try
            {
                try
                {
                    policy = (IPolicyConfig)(new PolicyConfigClient());
                }
                catch (COMException ex)
                {
                    return "无法访问 Windows 音频策略接口 (IPolicyConfig)，错误码 0x"
                           + ex.ErrorCode.ToString("X8") + "。";
                }

                StringBuilder failures = new StringBuilder();
                foreach (ERole role in AllRoles)
                {
                    if ((roles & RoleToFlag(role)) == 0) continue;

                    int hr = policy.SetDefaultEndpoint(endpointId, role);
                    if (hr != 0)
                    {
                        if (failures.Length > 0) failures.Append("、");
                        failures.Append(RoleName(role));
                    }
                }

                if (failures.Length > 0)
                    return "以下默认设备角色设置失败：" + failures + "。";

                return null;
            }
            catch (Exception ex)
            {
                return "切换音频设备时出错：" + ex.Message;
            }
            finally
            {
                ReleaseComObject(policy);
            }
        }

        /// <summary>Find a device by endpoint ID, falling back to its friendly name.</summary>
        public static AudioEndpoint Resolve(string endpointId, string fallbackName)
        {
            List<AudioEndpoint> devices = GetOutputDevices(DeviceStateFlags.ALL);
            foreach (AudioEndpoint device in devices)
            {
                if (string.Equals(device.Id, endpointId, StringComparison.OrdinalIgnoreCase))
                    return device;
            }

            if (!string.IsNullOrEmpty(fallbackName))
            {
                foreach (AudioEndpoint device in devices)
                {
                    if (string.Equals(device.BestName, fallbackName, StringComparison.CurrentCultureIgnoreCase))
                        return device;
                }
            }
            return null;
        }

        public static void ReleaseComObject(object instance)
        {
            if (instance == null) return;
            try
            {
                if (Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
            }
            catch (Exception)
            {
            }
        }
    }
}
