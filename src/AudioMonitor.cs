using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThreeCats
{
    public sealed class AudioSourceInfo
    {
        public string ProcessName;
        public uint ProcessId;
        public float Peak;
        public bool Selected;
    }

    // Each peak belongs to one process session. Endpoint-wide peaks are never used.
    public sealed class AudioMonitor : IDisposable
    {
        private sealed class Session
        {
            public IAudioSessionControl2 Control;
            public IAudioMeterInformation Meter;
            public ISimpleAudioVolume Volume;
            public string Name;
            public uint Pid;
        }

        private readonly List<Session> sessions = new List<Session>();
        private DateTime nextRefresh = DateTime.MinValue;
        public bool Available { get; private set; }
        public string Error { get; private set; }
        public HashSet<string> Selected = PlayerNames.Parse("cloudmusic,qqmusic");

        public void Refresh()
        {
            Clear();
            object enumeratorObject = null;
            IMMDeviceCollection devices = null;
            Available = false;
            Error = null;
            try
            {
                enumeratorObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                var enumerator = (IMMDeviceEnumerator)enumeratorObject;
                Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 1, out devices));
                uint count;
                Marshal.ThrowExceptionForHR(devices.GetCount(out count));
                Available = count > 0;
                if (!Available) Error = "没有可用的音频输出设备";
                for (uint index = 0; index < count; index++) ReadDevice(devices, index);
            }
            catch (Exception exception)
            {
                Error = "音频服务暂不可用（" + exception.GetType().Name + "）";
            }
            finally
            {
                Release(devices);
                Release(enumeratorObject);
                nextRefresh = DateTime.UtcNow.AddSeconds(1);
            }
        }

        private void ReadDevice(IMMDeviceCollection devices, uint index)
        {
            IMMDevice device = null;
            object managerObject = null;
            IAudioSessionEnumerator enumerator = null;
            try
            {
                Marshal.ThrowExceptionForHR(devices.Item(index, out device));
                Guid id = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                Marshal.ThrowExceptionForHR(device.Activate(ref id, 23, IntPtr.Zero, out managerObject));
                var manager = (IAudioSessionManager2)managerObject;
                Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out enumerator));
                int count;
                Marshal.ThrowExceptionForHR(enumerator.GetCount(out count));
                for (int i = 0; i < count; i++)
                {
                    object sessionObject = null;
                    try
                    {
                        Marshal.ThrowExceptionForHR(enumerator.GetSession(i, out sessionObject));
                        var control = (IAudioSessionControl2)sessionObject;
                        uint pid;
                        Marshal.ThrowExceptionForHR(control.GetProcessId(out pid));
                        if (pid == 0) continue;
                        string name;
                        using (var process = Process.GetProcessById((int)pid)) name = PlayerNames.Normalize(process.ProcessName);
                        var meter = control as IAudioMeterInformation;
                        if (meter == null) continue;
                        sessions.Add(new Session { Control = control, Meter = meter, Volume = control as ISimpleAudioVolume, Name = name, Pid = pid });
                        sessionObject = null;
                    }
                    catch (Exception) { /* Processes may exit while sessions are enumerated. */ }
                    finally { Release(sessionObject); }
                }
            }
            catch (COMException) { /* Other devices remain usable if one is unplugged. */ }
            finally
            {
                Release(enumerator);
                Release(managerObject);
                Release(device);
            }
        }

        public List<AudioSourceInfo> ReadSources()
        {
            if (DateTime.UtcNow >= nextRefresh) Refresh();
            var result = new List<AudioSourceInfo>();
            foreach (var session in sessions)
            {
                float peak = 0;
                try
                {
                    int state;
                    Marshal.ThrowExceptionForHR(session.Control.GetState(out state));
                    if (state == 1)
                    {
                        Marshal.ThrowExceptionForHR(session.Meter.GetPeakValue(out peak));
                        if (session.Volume != null)
                        {
                            bool mute;
                            float volume;
                            session.Volume.GetMute(out mute);
                            session.Volume.GetMasterVolume(out volume);
                            peak = mute ? 0 : peak * volume;
                        }
                    }
                }
                catch (COMException) { nextRefresh = DateTime.MinValue; }
                result.Add(new AudioSourceInfo { ProcessName = session.Name, ProcessId = session.Pid, Peak = peak, Selected = Selected.Contains(session.Name) });
            }
            return result;
        }

        public float ReadPeak()
        {
            float peak = 0;
            foreach (var source in ReadSources()) if (source.Selected) peak = Math.Max(peak, source.Peak);
            return peak;
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }

        private void Clear()
        {
            foreach (var session in sessions) Release(session.Control);
            sessions.Clear();
        }

        public void Dispose() { Clear(); }
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid id, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid id, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid id, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object control);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr callback);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
        [PreserveSig] int GetMeteringChannelCount(out uint count);
        [PreserveSig] int GetChannelsPeakValues(uint count, IntPtr peaks);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
