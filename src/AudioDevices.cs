// Adapted Core Audio COM declarations from AudioDeviceCmdlets; this is an altered,
// minimal implementation. https://github.com/frgnca/AudioDeviceCmdlets/tree/master/SOURCE
/*
  Copyright (C) 2007-2010 Ray Molenkamp
  This source code is provided 'as-is', without any express or implied warranty.
  In no event will the authors be held liable for any damages arising from the
  use of this source code or the software it produces.
  Permission is granted to anyone to use this source code for any purpose,
  including commercial applications, and to alter it and redistribute it freely,
  subject to the following restrictions:
  1. The origin of this source code must not be misrepresented; you must not claim
     that you wrote the original source code. If you use this source code in a
     product, an acknowledgment in the product documentation would be appreciated
     but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original source code.
  3. This notice may not be removed or altered from any source distribution.
*/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioDefaultsSync
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class EnumeratorClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint states, out IDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(INotifications client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(INotifications client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, out IntPtr result);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IDevice device);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropertyValue
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [ComImport, Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEndpoint
    {
        [PreserveSig] int GetDataFlow(out int flow);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    [ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface INotifications
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class PolicyClient { }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicy
    {
        [PreserveSig] int GetMixFormat(string id, IntPtr format);
        [PreserveSig] int GetDeviceFormat(string id, bool defaults, IntPtr format);
        [PreserveSig] int ResetDeviceFormat(string id);
        [PreserveSig] int SetDeviceFormat(string id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string id, bool defaults, IntPtr period, IntPtr minimum);
        [PreserveSig] int SetProcessingPeriod(string id, IntPtr period);
        [PreserveSig] int GetShareMode(string id, IntPtr mode);
        [PreserveSig] int SetShareMode(string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string id, bool fx, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue(string id, bool fx, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility(string id, bool visible);
    }

    public static class Defaults
    {
        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropertyValue value);

        public static List<AudioDevice> List(int flow)
        {
            if (flow != 0 && flow != 1) throw new ArgumentOutOfRangeException("flow");
            var result = new List<AudioDevice>();
            var enumerator = (IDeviceEnumerator)new EnumeratorClient();
            try
            {
                IDeviceCollection collection;
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(flow, 1, out collection));
                try
                {
                    uint count;
                    Marshal.ThrowExceptionForHR(collection.GetCount(out count));
                    for (uint index = 0; index < count; index++)
                    {
                        IDevice device;
                        Marshal.ThrowExceptionForHR(collection.Item(index, out device));
                        try
                        {
                            string id;
                            Marshal.ThrowExceptionForHR(device.GetId(out id));
                            string name = id;
                            IPropertyStore store;
                            if (device.OpenPropertyStore(0, out store) >= 0)
                            {
                                try
                                {
                                    var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                                    PropertyValue value;
                                    if (store.GetValue(ref key, out value) >= 0)
                                    {
                                        try { if (value.Type == 31) name = Marshal.PtrToStringUni(value.Pointer) ?? id; }
                                        finally { PropVariantClear(ref value); }
                                    }
                                }
                                finally { Marshal.ReleaseComObject(store); }
                            }
                            result.Add(new AudioDevice { Id = id, Name = name, Flow = flow });
                        }
                        finally { Marshal.ReleaseComObject(device); }
                    }
                }
                finally { Marshal.ReleaseComObject(collection); }
            }
            finally { Marshal.ReleaseComObject(enumerator); }
            return result;
        }

        public static string[] Read(int flow)
        {
            if (flow != 0 && flow != 1) throw new ArgumentOutOfRangeException("flow");
            var enumerator = (IDeviceEnumerator)new EnumeratorClient();
            try
            {
                var result = new string[3];
                for (int role = 0; role < 3; role++)
                {
                    IDevice device;
                    int hr = enumerator.GetDefaultAudioEndpoint(flow, role, out device);
                    if (hr < 0) { result[role] = null; continue; }
                    try { Marshal.ThrowExceptionForHR(device.GetId(out result[role])); }
                    finally { Marshal.ReleaseComObject(device); }
                }
                return result;
            }
            finally { Marshal.ReleaseComObject(enumerator); }
        }

        public static void RestoreCapture(string id)
        {
            for (int role = 0; role < 3; role++) SetRole(id, 1, role);
        }

        public static bool IsActive(string id, int flow)
        {
            if (String.IsNullOrEmpty(id)) return false;
            var enumerator = (IDeviceEnumerator)new EnumeratorClient();
            try
            {
                IDevice device;
                int hr = enumerator.GetDevice(id, out device);
                if (hr < 0) return false;
                try
                {
                    uint state;
                    Marshal.ThrowExceptionForHR(device.GetState(out state));
                    int actualFlow;
                    Marshal.ThrowExceptionForHR(((IEndpoint)device).GetDataFlow(out actualFlow));
                    return state == 1 && actualFlow == flow;
                }
                finally { Marshal.ReleaseComObject(device); }
            }
            finally { Marshal.ReleaseComObject(enumerator); }
        }

        public static void SetRole(string id, int flow, int role)
        {
            if (role < 0 || role > 2 || flow < 0 || flow > 1) throw new ArgumentOutOfRangeException();
            if (!IsActive(id, flow)) throw new InvalidOperationException("The requested audio endpoint is not active for this flow.");
            var policy = (IPolicy)new PolicyClient();
            try
            {
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, role));
            }
            finally { Marshal.ReleaseComObject(policy); }
        }
    }

    public sealed class AudioDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Flow { get; set; }
        public override string ToString() { return Name; }
    }
}
