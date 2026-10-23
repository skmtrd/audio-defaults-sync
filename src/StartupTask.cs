using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;

namespace AudioDefaultsSync
{
    internal static class StartupTask
    {
        internal const string Name = "Audio Defaults Sync";
        private const string Description = "Audio Defaults Sync: synchronize playback roles and pin the configured recording device.";
        internal static bool IsEnabled()
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            try
            {
                service.Connect();
                dynamic folder = service.GetFolder("\\");
                try
                {
                    dynamic task = null;
                    try { task = folder.GetTask(Name); return (bool)task.Enabled; }
                    catch (Exception error) { if (error.HResult == unchecked((int)0x80070002)) return false; throw; }
                    finally { if (task != null) Marshal.ReleaseComObject(task); }
                }
                finally { Marshal.ReleaseComObject(folder); }
            }
            finally { Marshal.ReleaseComObject(service); }
        }

        internal static void SetEnabled(bool enabled, string executable, string dataDirectory)
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            try
            {
                service.Connect();
                dynamic folder = service.GetFolder("\\");
                try
                {
                    dynamic existing = null;
                    try { existing = folder.GetTask(Name); }
                    catch (Exception error) { if (error.HResult != unchecked((int)0x80070002)) throw; }
                    if (existing != null)
                    {
                        try
                        {
                            if (!((string)existing.Xml).Contains(Description))
                                throw new InvalidOperationException("Another application owns the startup task named " + Name + ".");
                            if (!enabled) { existing.Enabled = false; return; }
                        }
                        finally { Marshal.ReleaseComObject(existing); }
                    }
                    if (!enabled) return;
                    string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().User.Value);
                    string command = SecurityElement.Escape(executable);
                    string args = SecurityElement.Escape("--background --data-dir \"" + dataDirectory + "\"");
                    string xml = "<Task version=\"1.3\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                        "<RegistrationInfo><Description>" + Description + "</Description></RegistrationInfo>" +
                        "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId></LogonTrigger></Triggers>" +
                        "<Principals><Principal id=\"Author\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
                        "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                        "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><StartWhenAvailable>true</StartWhenAvailable><AllowStartOnDemand>true</AllowStartOnDemand>" +
                        "<Enabled>true</Enabled><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings>" +
                        "<Actions Context=\"Author\"><Exec><Command>" + command + "</Command><Arguments>" + args + "</Arguments></Exec></Actions></Task>";
                    dynamic registered = folder.RegisterTask(Name, xml, 6, null, null, 3, null);
                    Marshal.ReleaseComObject(registered);
                }
                finally { Marshal.ReleaseComObject(folder); }
            }
            finally { Marshal.ReleaseComObject(service); }
        }
    }
}
