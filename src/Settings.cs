using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace AudioDefaultsSync
{
    public sealed class Settings
    {
        public string PinnedInputId { get; set; }
        public string PinnedInputName { get; set; }
        public bool SynchronizeOutput { get; set; }
        public int HealthCheckSeconds { get; set; }
        public Settings() { SynchronizeOutput = true; HealthCheckSeconds = 30; }
        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(PinnedInputId) || String.IsNullOrWhiteSpace(PinnedInputName))
                throw new InvalidDataException("Choose an input device first.");
            if (HealthCheckSeconds < 5 || HealthCheckSeconds > 3600)
                throw new InvalidDataException("HealthCheckSeconds must be between 5 and 3600.");
        }
    }

    public static class SettingsStore
    {
        public static Settings Load(string directory)
        {
            string path = Path.Combine(directory, "config.json");
            if (!File.Exists(path)) return null;
            var settings = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(path));
            if (settings == null) throw new InvalidDataException("The configuration is empty.");
            settings.Validate();
            return settings;
        }
        public static void Save(string directory, Settings settings)
        {
            settings.Validate();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "config.json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(settings));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        public static string ResolveInput(Settings settings, IList<AudioDevice> available)
        {
            var exact = available.FirstOrDefault(device => String.Equals(device.Id, settings.PinnedInputId, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Id;
            var byName = available.Where(device => String.Equals(device.Name, settings.PinnedInputName, StringComparison.OrdinalIgnoreCase)).ToList();
            return byName.Count == 1 ? byName[0].Id : null;
        }
    }

    internal static class SyncPolicy
    {
        internal static bool Same(string a, string b) { return String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        internal static string SelectOutput(string[] current, IList<Change> changes, Func<string, bool> active)
        {
            for (int index = changes.Count - 1; index >= 0; index--)
            {
                Change change = changes[index];
                if (change.Flow == 0 && change.Role >= 0 && change.Role < 3 &&
                    Same(current[change.Role], change.Id) && active(change.Id)) return change.Id;
            }
            foreach (int role in new int[] { 1, 0, 2 })
                if (active(current[role])) return current[role];
            return null;
        }
    }
}
