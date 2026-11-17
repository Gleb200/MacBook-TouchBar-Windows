using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Avalonia.Threading;

namespace DFRContentHost.Models
{
    public sealed class ConfigManager
    {
        private static readonly Lazy<ConfigManager> _lazy = new Lazy<ConfigManager>(() => new ConfigManager());
        public static ConfigManager Instance => _lazy.Value;

        private AppConfig _current;
        private readonly object _lock = new object();
        private FileSystemWatcher _watcher;
        private Timer _debounceTimer;
        private string _configFilePath;

        public event Action<AppConfig> ConfigChanged;

        public AppConfig Current
        {
            get
            {
                lock (_lock)
                {
                    return _current ?? (_current = AppConfig.CreateDefault());
                }
            }
            private set
            {
                lock (_lock)
                {
                    _current = value;
                }
            }
        }

        public string ConfigPath => _configFilePath;

        private ConfigManager()
        {
            InitializePath();
            Load();
            SetupWatcher();
        }

        private void InitializePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _configFilePath = Path.Combine(baseDir, "config.json");

            // Fallback to working directory if not writable
            if (!Directory.Exists(baseDir))
            {
                _configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json, opts);
                    if (loaded != null)
                    {
                        EnsureWidgetsList(loaded);
                        Current = loaded;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_err.log"),
                        $"[{DateTime.Now}] Load error: {ex}\n");
                }
                catch { }
            }

            // If file doesn't exist or failed to load, initialize default and save
            Current = AppConfig.CreateDefault();
            Save(Current);
        }

        private static void EnsureWidgetsList(AppConfig config)
        {
            if (config.Widgets == null || config.Widgets.Count == 0)
            {
                config.Widgets = AppConfig.CreateDefault().Widgets;
            }
        }

        public void Save(AppConfig config)
        {
            lock (_lock)
            {
                try
                {
                    if (_watcher != null) _watcher.EnableRaisingEvents = false;

                    var opts = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(config, opts);
                    File.WriteAllText(_configFilePath, json);

                    Current = config;
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_err.log"),
                            $"[{DateTime.Now}] Save error: {ex}\n");
                    }
                    catch { }
                }
                finally
                {
                    if (_watcher != null) _watcher.EnableRaisingEvents = true;
                }
            }
        }

        private void SetupWatcher()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configFilePath);
                string file = Path.GetFileName(_configFilePath);

                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    _watcher = new FileSystemWatcher(dir, file)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };

                    _watcher.Changed += OnConfigFileChanged;
                    _watcher.Created += OnConfigFileChanged;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_err.log"),
                        $"[{DateTime.Now}] Watcher setup error: {ex}\n");
                }
                catch { }
            }
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid writes
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ =>
            {
                try
                {
                    // Allow brief file release
                    Thread.Sleep(50);
                    ReloadAndNotify();
                }
                catch { }
            }, null, 100, Timeout.Infinite);
        }

        public void ReloadAndNotify()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    // Retry loop to ensure file handle is freed by writer
                    string json = null;
                    for (int retry = 0; retry < 5; retry++)
                    {
                        try
                        {
                            using var fs = new FileStream(_configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var reader = new StreamReader(fs);
                            json = reader.ReadToEnd();
                            break;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(40);
                        }
                    }

                    if (!string.IsNullOrEmpty(json))
                    {
                        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var updated = JsonSerializer.Deserialize<AppConfig>(json, opts);
                        if (updated != null)
                        {
                            EnsureWidgetsList(updated);
                            Current = updated;

                            // Post notification to UI thread
                            Dispatcher.UIThread.Post(() =>
                            {
                                try
                                {
                                    ConfigChanged?.Invoke(Current);
                                }
                                catch { }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_err.log"),
                        $"[{DateTime.Now}] Reload error: {ex}\n");
                }
                catch { }
            }
        }
    }
}
