using System;
using System.Collections.Generic;
using System.IO;

namespace AudioDefaultsSync
{
    internal static class Tests
    {
        private static int passed;
        private static void Equal(object expected, object actual, string name)
        {
            if (!Object.Equals(expected, actual)) throw new Exception(name + ": expected " + expected + ", got " + actual);
            Console.WriteLine("PASS " + name);
            passed++;
        }
        private static void Throws(Action action, string name)
        {
            bool threw = false;
            try { action(); } catch (InvalidDataException) { threw = true; }
            Equal(true, threw, name);
        }
        private static int Main()
        {
            try
            {
                Func<string, bool> active = id => id == "A" || id == "B";
                Equal("B", SyncPolicy.SelectOutput(new[] { "A", "B", "A" },
                    new[] { new Change { Flow = 0, Role = 1, Id = "B" } }, active), "normal output follows to communications");
                Equal("B", SyncPolicy.SelectOutput(new[] { "A", "A", "B" },
                    new[] { new Change { Flow = 0, Role = 2, Id = "B" } }, active), "communications output follows to normal");
                Equal("B", SyncPolicy.SelectOutput(new[] { "A", "B", "A" },
                    new[] { new Change { Flow = 0, Role = 1, Id = "B" }, new Change { Flow = 0, Role = 1, Id = "A" } }, active), "stale callback cannot undo latest current device");
                Equal("A", SyncPolicy.SelectOutput(new[] { "A", "A", "A" },
                    new[] { new Change { Flow = 1, Role = 1, Id = "B" } }, active), "input notifications do not redirect playback");
                Equal("A", SyncPolicy.SelectOutput(new[] { "B", "A", "B" }, new Change[0], active), "startup favors normal multimedia output");
                Equal(null, SyncPolicy.SelectOutput(new string[3], new Change[0], active), "no active output is safe");
                Equal(true, SyncPolicy.Same("DEVICE", "device"), "endpoint comparisons ignore case");

                var configuration = new Settings { PinnedInputId = "old", PinnedInputName = "入力マイク" };
                var devices = new List<AudioDevice> {
                    new AudioDevice { Id = "old", Name = "renamed", Flow = 1 },
                    new AudioDevice { Id = "new", Name = "入力マイク", Flow = 1 } };
                Equal("old", SettingsStore.ResolveInput(configuration, devices), "device identity takes priority over its name");
                devices.RemoveAt(0);
                Equal("new", SettingsStore.ResolveInput(configuration, devices), "unique name recovers a changed device ID");
                devices.Add(new AudioDevice { Id = "another", Name = "入力マイク", Flow = 1 });
                Equal(null, SettingsStore.ResolveInput(configuration, devices), "duplicate names are not chosen automatically");
                Equal(null, SettingsStore.ResolveInput(configuration, new List<AudioDevice>()), "missing input remains unresolved");
                Throws(() => new Settings().Validate(), "empty configuration is rejected");
                configuration.HealthCheckSeconds = 0;
                Throws(configuration.Validate, "busy polling interval is rejected");
                configuration.HealthCheckSeconds = 30;

                string temporary = Path.Combine(Path.GetTempPath(), "AudioDefaultsSync-tests-" + Guid.NewGuid().ToString("N"));
                try
                {
                    SettingsStore.Save(temporary, configuration);
                    var loaded = SettingsStore.Load(temporary);
                    Equal("入力マイク", loaded.PinnedInputName, "Unicode device name round trips");
                    loaded.PinnedInputId = "replacement";
                    SettingsStore.Save(temporary, loaded);
                    Equal("replacement", SettingsStore.Load(temporary).PinnedInputId, "configuration updates replace the previous file");
                    loaded.PinnedInputId = "";
                    Throws(() => SettingsStore.Save(temporary, loaded), "invalid settings cannot overwrite a valid configuration");
                    Equal("replacement", SettingsStore.Load(temporary).PinnedInputId, "previous valid configuration survives rejected save");
                }
                finally
                {
                    string path = Path.Combine(temporary, "config.json");
                    if (File.Exists(path)) File.Delete(path);
                    if (Directory.Exists(temporary)) Directory.Delete(temporary);
                }
                Console.WriteLine(passed + " tests passed.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
