using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AudioDefaultsSync
{
    internal sealed class SettingsDialog : Form
    {
        private readonly ComboBox inputs = new ComboBox();
        private readonly CheckBox syncOutput = new CheckBox();
        private readonly CheckBox startup = new CheckBox();
        private readonly Label message = new Label();
        private readonly string directory;
        private readonly Settings previous;
        internal Settings Selected { get; private set; }

        internal SettingsDialog(Settings current, string dataDirectory)
        {
            directory = dataDirectory;
            previous = current;
            Text = "Audio Defaults Sync — 設定";
            Font = new Font("Yu Gothic UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(540, 310);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            var heading = new Label { Text = "固定する入力デバイス", AutoSize = true, Location = new Point(20, 20) };
            inputs.SetBounds(20, 53, 500, 30);
            inputs.DropDownStyle = ComboBoxStyle.DropDownList;
            inputs.AccessibleName = "固定する入力デバイス";
            var refresh = new Button { Text = "一覧を更新", Location = new Point(400, 93), Size = new Size(120, 30) };
            refresh.Click += delegate { LoadInputs(); };
            syncOutput.Text = "通常用と通話用の出力を、最後の選択にそろえる";
            syncOutput.SetBounds(20, 137, 500, 28);
            syncOutput.Checked = current == null || current.SynchronizeOutput;
            startup.Text = "Windowsへのログイン時に自動起動する";
            startup.SetBounds(20, 173, 500, 28);
            try { startup.Checked = current == null || StartupTask.IsEnabled(); }
            catch { startup.Checked = false; }
            message.SetBounds(20, 208, 500, 42);
            message.Text = "入力は通常用・通話用とも、選んだデバイスへ戻します。";
            var save = new Button { Text = "保存して開始", Location = new Point(282, 264), Size = new Size(128, 32) };
            var cancel = new Button { Text = "キャンセル", Location = new Point(420, 264), Size = new Size(100, 32), DialogResult = DialogResult.Cancel };
            save.Click += Save;
            AcceptButton = save;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { heading, inputs, refresh, syncOutput, startup, message, save, cancel });
            LoadInputs();
        }

        private void LoadInputs()
        {
            try
            {
                string selectedId = inputs.SelectedItem is AudioDevice ? ((AudioDevice)inputs.SelectedItem).Id : previous == null ? null : previous.PinnedInputId;
                var devices = Defaults.List(1).OrderBy(device => device.Name).ToList();
                inputs.Items.Clear();
                foreach (var device in devices) inputs.Items.Add(device);
                var selected = devices.FirstOrDefault(device => SyncPolicy.Same(device.Id, selectedId));
                if (selected == null && previous != null)
                    selected = devices.FirstOrDefault(device => SyncPolicy.Same(device.Id, SettingsStore.ResolveInput(previous, devices)));
                if (selected == null)
                    selected = devices.FirstOrDefault(device => SyncPolicy.Same(device.Id, Defaults.Read(1)[1]));
                if (selected != null) inputs.SelectedItem = selected;
                else if (inputs.Items.Count > 0) inputs.SelectedIndex = 0;
                if (devices.Count == 0) message.Text = "有効な入力デバイスがありません。接続して「一覧を更新」を押してください。";
            }
            catch (Exception error) { message.Text = error.Message; }
        }

        private void Save(object sender, EventArgs args)
        {
            var selected = inputs.SelectedItem as AudioDevice;
            if (selected == null) { message.Text = "入力デバイスを選んでください。"; return; }
            try
            {
                var result = new Settings { PinnedInputId = selected.Id, PinnedInputName = selected.Name,
                    SynchronizeOutput = syncOutput.Checked, HealthCheckSeconds = previous == null ? 30 : previous.HealthCheckSeconds };
                result.Validate();
                StartupTask.SetEnabled(startup.Checked, Application.ExecutablePath, directory);
                SettingsStore.Save(directory, result);
                Selected = result;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "保存できませんでした", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon icon;
        private readonly Control dispatcher = new Control();
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly Thread worker;
        private readonly EventWaitHandle openSettings;
        private readonly RegisteredWaitHandle settingsWait;
        private readonly string directory;
        private Settings settings;
        private Monitor monitor;
        private bool dialogOpen;

        internal TrayContext(Settings initial, string dataDirectory, string eventName)
        {
            directory = dataDirectory;
            settings = initial;
            var unused = dispatcher.Handle;
            openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var menu = new ContextMenuStrip();
            menu.Items.Add("設定…", null, delegate { Configure(); });
            menu.Items.Add("ログと状態を開く", null, delegate { Process.Start("explorer.exe", "\"" + directory + "\""); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("終了", null, delegate { ExitThread(); });
            icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "Audio Defaults Sync", ContextMenuStrip = menu, Visible = true };
            icon.DoubleClick += delegate { Configure(); };
            settingsWait = ThreadPool.RegisterWaitForSingleObject(openSettings,
                delegate { dispatcher.BeginInvoke((Action)Configure); }, null, Timeout.Infinite, false);
            monitor = new Monitor(settings, directory, stop);
            worker = new Thread(delegate()
            {
                try { using (monitor) monitor.Run(); }
                catch (Exception error)
                {
                    File.AppendAllText(Path.Combine(directory, "monitor-fatal.log"), DateTime.Now.ToString("o") + " " + error + Environment.NewLine);
                    dispatcher.BeginInvoke((Action)delegate
                    {
                        MessageBox.Show("音声の監視が停止しました。ログを確認して再起動してください。", "Audio Defaults Sync", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Environment.ExitCode = 1;
                        ExitThread();
                    });
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }

        private void Configure()
        {
            if (dialogOpen) return;
            dialogOpen = true;
            try
            {
                using (var dialog = new SettingsDialog(settings, directory))
                    if (dialog.ShowDialog() == DialogResult.OK) { settings = dialog.Selected; monitor.UpdateSettings(settings); }
            }
            finally { dialogOpen = false; }
        }

        protected override void ExitThreadCore()
        {
            settingsWait.Unregister(null);
            stop.Set();
            worker.Join(3000);
            icon.Visible = false;
            icon.Dispose();
            openSettings.Dispose();
            dispatcher.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioDefaultsSync");
            for (int index = 0; index < args.Length; index++)
                if (args[index] == "--data-dir" && index + 1 < args.Length) directory = Path.GetFullPath(args[++index]);
            if (args.Length >= 2 && args[0] == "--list-devices")
            {
                File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(new { Inputs = Defaults.List(1), Outputs = Defaults.List(0) }));
                return 0;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string identity = WindowsIdentity.GetCurrent().User.Value;
            string eventName = "Local\\AudioDefaultsSync-Settings-" + identity;
            using (var singleton = new Mutex(false, "Local\\AudioDefaultsSync-" + identity))
            {
                bool ownsMutex;
                try { ownsMutex = singleton.WaitOne(0); }
                catch (AbandonedMutexException) { ownsMutex = true; }
                if (!ownsMutex)
                {
                    try { using (var signal = EventWaitHandle.OpenExisting(eventName)) signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException)
                    { MessageBox.Show("別の版の音声同期プログラムが実行中です。先に終了してください。", "Audio Defaults Sync"); }
                    return 0;
                }
                try
                {
                    Directory.CreateDirectory(directory);
                    Settings settings = null;
                    try { settings = SettingsStore.Load(directory); }
                    catch (Exception error)
                    {
                        File.AppendAllText(Path.Combine(directory, "monitor-fatal.log"), DateTime.Now.ToString("o") + " " + error + Environment.NewLine);
                        if (!args.Contains("--background")) MessageBox.Show(error.Message, "設定を読み込めませんでした");
                    }
                    if (settings == null || args.Contains("--configure"))
                    {
                        if (args.Contains("--background"))
                        {
                            File.AppendAllText(Path.Combine(directory, "monitor-fatal.log"), DateTime.Now.ToString("o") + " No valid configuration. Run the app interactively to select an input." + Environment.NewLine);
                            return 1;
                        }
                        using (var dialog = new SettingsDialog(settings, directory))
                        {
                            if (dialog.ShowDialog() != DialogResult.OK) return 0;
                            settings = dialog.Selected;
                        }
                    }
                    using (var context = new TrayContext(settings, directory, eventName)) Application.Run(context);
                    return Environment.ExitCode;
                }
                catch (Exception error)
                {
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(Path.Combine(directory, "monitor-fatal.log"), DateTime.Now.ToString("o") + " " + error + Environment.NewLine);
                    if (!args.Contains("--background")) MessageBox.Show(error.Message, "Audio Defaults Sync", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { singleton.ReleaseMutex(); }
            }
        }
    }
}
