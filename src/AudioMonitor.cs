using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace AudioDefaultsSync
{
    internal sealed class Change
    {
        public int Flow;
        public int Role;
        public string Id;
    }

    internal sealed class ExpectedChange
    {
        public string Id;
        public DateTime Until;
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class Notifications : INotifications
    {
        private readonly Monitor owner;
        internal Notifications(Monitor value) { owner = value; }
        public int OnDeviceStateChanged(string id, uint state) { owner.TopologyChanged(); return 0; }
        public int OnDeviceAdded(string id) { owner.TopologyChanged(); return 0; }
        public int OnDeviceRemoved(string id) { owner.TopologyChanged(); return 0; }
        public int OnDefaultDeviceChanged(int flow, int role, string id) { owner.DefaultChanged(flow, role, id); return 0; }
        public int OnPropertyValueChanged(string id, PropertyKey key) { return 0; }
    }

    internal sealed class Monitor : IDisposable
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly List<Change> pending = new List<Change>();
        private readonly Dictionary<int, ExpectedChange> expected = new Dictionary<int, ExpectedChange>();
        private readonly string directory;
        private readonly ManualResetEvent stop;
        private volatile Settings settings;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly DateTime started = DateTime.Now;
        private IDeviceEnumerator enumerator;
        private Notifications notifications;
        private bool waitingForInput;

        internal Monitor(Settings initialSettings, string dataDirectory, ManualResetEvent stopSignal)
        {
            settings = initialSettings;
            directory = dataDirectory;
            stop = stopSignal;
            Directory.CreateDirectory(directory);
        }

        internal void UpdateSettings(Settings value) { settings = value; wake.Set(); }

        internal void TopologyChanged() { wake.Set(); }

        internal void DefaultChanged(int flow, int role, string id)
        {
            // Callbacks only queue work. COM reads and writes run on the worker thread.
            if (flow < 0 || flow > 1 || role < 0 || role > 2) return;
            lock (gate)
            {
                int key = flow * 3 + role;
                ExpectedChange own;
                if (expected.TryGetValue(key, out own))
                {
                    expected.Remove(key);
                    if (own.Until >= DateTime.UtcNow && Same(own.Id, id)) return;
                }
                pending.Add(new Change { Flow = flow, Role = role, Id = id });
            }
            wake.Set();
        }

        private static bool Same(string a, string b) { return String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        private void Set(int flow, int role, string id)
        {
            lock (gate)
                expected[flow * 3 + role] = new ExpectedChange { Id = id, Until = DateTime.UtcNow.AddSeconds(5) };
            try { Defaults.SetRole(id, flow, role); }
            catch
            {
                lock (gate) expected.Remove(flow * 3 + role);
                throw;
            }
        }

        private void Log(object value)
        {
            string path = Path.Combine(directory, "monitor-log.jsonl");
            if (File.Exists(path) && new FileInfo(path).Length > 524288)
                File.Copy(path, Path.Combine(directory, "monitor-log.previous.jsonl"), true);
            if (File.Exists(path) && new FileInfo(path).Length > 524288) File.WriteAllText(path, "");
            File.AppendAllText(path, json.Serialize(value) + Environment.NewLine);
        }

        private void Reconcile(List<Change> changes, bool initial)
        {
            Settings currentSettings = settings;
            string targetInput = currentSettings.PinnedInputId;
            if (!Defaults.IsActive(targetInput, 1))
                targetInput = SettingsStore.ResolveInput(currentSettings, Defaults.List(1));
            string[] inputBefore = Defaults.Read(1);
            string[] outputBefore = Defaults.Read(0);
            int writes = 0;
            bool inputActive = targetInput != null && Defaults.IsActive(targetInput, 1);
            if (inputActive)
            {
                for (int role = 0; role < 3; role++)
                    if (!Same(inputBefore[role], targetInput)) { Set(1, role, targetInput); writes++; }
            }

            string outputTarget = SyncPolicy.SelectOutput(outputBefore, changes, id => Defaults.IsActive(id, 0));
            if (currentSettings.SynchronizeOutput && outputTarget != null)
            {
                for (int role = 0; role < 3; role++)
                    if (!Same(outputBefore[role], outputTarget)) { Set(0, role, outputTarget); writes++; }
            }

            string[] inputAfter = Defaults.Read(1);
            string[] outputAfter = Defaults.Read(0);
            var status = new {
                Time = DateTime.Now.ToString("o"), Started = started.ToString("o"),
                ProcessId = Process.GetCurrentProcess().Id, TargetInput = targetInput,
                PinnedInputName = currentSettings.PinnedInputName,
                InputAvailable = inputActive, Capture = inputAfter, Playback = outputAfter,
                Mode = "Input pinned; latest playback selection synchronized across all roles"
            };
            File.WriteAllText(Path.Combine(directory, "monitor-status.json"), json.Serialize(status));
            if (initial || writes > 0 || waitingForInput == inputActive)
                Log(new { Time = status.Time, Initial = initial, Writes = writes, InputAvailable = inputActive,
                    CaptureBefore = inputBefore, CaptureAfter = inputAfter,
                    PlaybackBefore = outputBefore, PlaybackAfter = outputAfter });
            waitingForInput = !inputActive;
        }

        internal void Run()
        {
            enumerator = (IDeviceEnumerator)new EnumeratorClient();
            notifications = new Notifications(this);
            Marshal.ThrowExceptionForHR(enumerator.RegisterEndpointNotificationCallback(notifications));
            Reconcile(new List<Change>(), true);
            while (true)
            {
                // Event driven; the idle timeout also recovers from missed service/resume notifications.
                if (WaitHandle.WaitAny(new WaitHandle[] { stop, wake }, settings.HealthCheckSeconds * 1000) == 0) break;
                Thread.Sleep(60);
                List<Change> changes;
                lock (gate) { changes = new List<Change>(pending); pending.Clear(); }
                try { Reconcile(changes, false); }
                catch (Exception error)
                {
                    Log(new { Time = DateTime.Now.ToString("o"), Error = error.ToString() });
                    // Re-evaluate after transient device changes settle.
                    if (stop.WaitOne(1000)) break;
                    wake.Set();
                }
            }
        }

        public void Dispose()
        {
            if (enumerator != null)
            {
                if (notifications != null) enumerator.UnregisterEndpointNotificationCallback(notifications);
                Marshal.ReleaseComObject(enumerator);
            }
            wake.Dispose();
        }
    }

}
