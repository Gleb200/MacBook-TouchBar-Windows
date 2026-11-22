using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TouchBarSettings.Models;

namespace TouchBarSettings
{
    public partial class MainWindow : Window
    {
        private AppConfig _config;
        private bool _isInitializing = true;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private System.Windows.Forms.ContextMenuStrip _trayMenu;
        private bool _isRealExit = false;
        private bool _hasShownBalloon = false;
        public ObservableCollection<WidgetConfigItem> WidgetsList { get; } = new ObservableCollection<WidgetConfigItem>();

        public MainWindow()
        {
            InitializeComponent();
            LstWidgets.ItemsSource = WidgetsList;
            SetupSystemTray();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfigToUI();
            _isInitializing = false;

            // Start Touch Bar hardware display host immediately
            TouchBarEngine.StartHost();

            // If launched with --tray or -minimized or --autostart, start directly in tray
            string[] args = Environment.GetCommandLineArgs();
            if (args != null && args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "-tray", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "-minimized", StringComparison.OrdinalIgnoreCase)))
            {
                Hide();
            }
        }

        private void LoadConfigToUI()
        {
            _isInitializing = true;
            _config = ConfigRepository.Load();

            // Language ComboBox
            string currentLang = _config.Language ?? "Auto";
            foreach (ComboBoxItem item in CmbLanguage.Items)
            {
                if (string.Equals(item.Tag?.ToString(), currentLang, StringComparison.OrdinalIgnoreCase))
                {
                    CmbLanguage.SelectedItem = item;
                    break;
                }
            }
            if (CmbLanguage.SelectedItem == null && CmbLanguage.Items.Count > 0)
            {
                CmbLanguage.SelectedIndex = 0;
            }

            // Apply active language to all UI strings
            ApplyLanguage();

            // 1. General & Display
            string escMode = _config.ShowEscButtonMode ?? "Auto";
            foreach (ComboBoxItem item in CmbEscMode.Items)
            {
                if (string.Equals(item.Tag?.ToString(), escMode, StringComparison.OrdinalIgnoreCase))
                {
                    CmbEscMode.SelectedItem = item;
                    break;
                }
            }
            if (CmbEscMode.SelectedItem == null && CmbEscMode.Items.Count > 0)
            {
                CmbEscMode.SelectedIndex = 0;
            }

            SliderTouchBarBrightness.Value = Math.Max(10, Math.Min(100, _config.TouchBarBrightness));
            TxtBrightnessValue.Text = $"{(int)SliderTouchBarBrightness.Value}%";
            ChkSyncBrightness.IsChecked = _config.SyncWithMainDisplayBrightness;

            // Sleep Timeout ComboBox
            string timeoutStr = _config.SleepTimeoutSeconds.ToString();
            foreach (ComboBoxItem item in CmbSleepTimeout.Items)
            {
                if (item.Tag?.ToString() == timeoutStr)
                {
                    CmbSleepTimeout.SelectedItem = item;
                    break;
                }
            }

            ChkShowFnKeys.IsChecked = _config.ShowFnKeysOnFnPress;

            // AutoStart (queried in background to ensure instant zero-latency UI display)
            System.Threading.Tasks.Task.Run(() =>
            {
                bool enabled = AutoStartHelper.IsAutoStartEnabled();
                Dispatcher.Invoke(() =>
                {
                    UpdateAutoStartUI(enabled);
                    UpdateTrayMenu();
                });
            });

            // 2. Pets & Custom Characters
            ChkPetEnabled.IsChecked = _config.PetEnabled;
            SliderPetSpeed.Value = Math.Max(0.5, Math.Min(3.0, _config.PetSpeed));
            TxtPetSpeedValue.Text = $"{SliderPetSpeed.Value:0.0}x";
            ChkPetReactToTouch.IsChecked = _config.PetReactToTouch;
            ChkPetCpuSpeedup.IsChecked = _config.PetSpeedUpOnHighCpu;
            ChkPetPinned.IsChecked = _config.PetPinned;
            SliderPetSpeed.IsEnabled = !_config.PetPinned;

            RefreshSkinsList(_config.SelectedPetSkin);

            // 3. Widgets & Overlays
            PopulateWidgetsList();

            // CPU Temp Source
            foreach (ComboBoxItem item in CmbCpuTempSource.Items)
            {
                if (string.Equals(item.Tag?.ToString(), _config.CpuTempSource, StringComparison.OrdinalIgnoreCase))
                {
                    CmbCpuTempSource.SelectedItem = item;
                    break;
                }
            }

            ChkOverlayTouchId.IsChecked = _config.OverlayTouchIdPrompt;
            ChkOverlayUac.IsChecked = _config.OverlayUacDialogs;
            ChkOverlayCharging.IsChecked = _config.OverlayChargingBanner;
        }

        private void PopulateWidgetsList()
        {
            string effLang = _config.GetEffectiveLanguage();
            WidgetsList.Clear();
            if (_config.Widgets != null)
            {
                var sorted = _config.Widgets.OrderBy(w => w.Order).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].Order = i;
                    sorted[i].Name = I18n.GetWidgetName(sorted[i].Id, effLang);
                    WidgetsList.Add(sorted[i]);
                }
            }
        }

        private void ApplyLanguage()
        {
            string lang = _config.GetEffectiveLanguage();

            // Header
            TxtAppTitle.Text = I18n.Get("AppTitle", lang);
            TxtAppSubtitle.Text = I18n.Get("AppSubtitle", lang);
            TxtLanguageLabel.Text = I18n.Get("LanguageLabel", lang);
            TxtServiceActive.Text = I18n.Get("ServiceActive", lang);
            CmbItemLangAuto.Content = I18n.Get("LangAuto", lang);
            CmbItemLangRu.Content = I18n.Get("LangRu", lang);
            CmbItemLangEn.Content = I18n.Get("LangEn", lang);

            // Tab 1: General & Display
            TabGeneral.Header = I18n.Get("TabDisplay", lang);
            TxtEscKeyTitle.Text = I18n.Get("EscKeyTitle", lang);
            TxtEscKeyDesc.Text = I18n.Get("EscKeyDesc", lang);
            CmbItemEscAuto.Content = I18n.Get("EscModeAuto", lang);
            CmbItemEscOn.Content = I18n.Get("EscModeAlwaysOn", lang);
            CmbItemEscOff.Content = I18n.Get("EscModeAlwaysOff", lang);

            TxtOledBrightnessTitle.Text = I18n.Get("OledBrightnessTitle", lang);
            TxtOledBrightnessDesc.Text = I18n.Get("OledBrightnessDesc", lang);
            ChkSyncBrightness.Content = I18n.Get("SyncBrightness", lang);

            TxtSleepTimeoutTitle.Text = I18n.Get("SleepTimeoutTitle", lang);
            TxtSleepTimeoutDesc.Text = I18n.Get("SleepTimeoutDesc", lang);
            CmbItemSleep15.Content = I18n.Get("Timeout15s", lang);
            CmbItemSleep30.Content = I18n.Get("Timeout30s", lang);
            CmbItemSleep60.Content = I18n.Get("Timeout1m", lang);
            CmbItemSleep180.Content = I18n.Get("Timeout3m", lang);
            CmbItemSleep0.Content = I18n.Get("TimeoutNever", lang);

            TxtFnKeysTitle.Text = I18n.Get("FnKeysTitle", lang);
            TxtFnKeysDesc.Text = I18n.Get("FnKeysDesc", lang);
            TxtAutoStartTitle.Text = I18n.Get("AutoStartTitle", lang);
            TxtAutoStartDesc.Text = I18n.Get("AutoStartDesc", lang);

            // Tab 2: Pets
            TabPets.Header = I18n.Get("TabPets", lang);
            TxtPetMasterTitle.Text = I18n.Get("PetMasterTitle", lang);
            TxtPetMasterDesc.Text = I18n.Get("PetMasterDesc", lang);
            TxtCurrentSkinLabel.Text = I18n.Get("CurrentSkinLabel", lang);
            BtnImportPet.Content = I18n.Get("ImportPetBtn", lang);
            TxtImportPetHint.Text = I18n.Get("ImportPetHint", lang);
            TxtPetBehaviorTitle.Text = I18n.Get("PetBehaviorTitle", lang);
            TxtPetWalkSpeed.Text = I18n.Get("PetWalkSpeed", lang);
            ChkPetReactToTouch.Content = I18n.Get("PetTouchReact", lang);
            ChkPetCpuSpeedup.Content = I18n.Get("PetCpuSpeedup", lang);
            ChkPetPinned.Content = I18n.Get("PetPinned", lang);

            // Tab 3: Widgets & Overlays
            TabWidgets.Header = I18n.Get("TabWidgets", lang);
            TxtWidgetsOrderTitle.Text = I18n.Get("WidgetsOrderTitle", lang);
            TxtWidgetsOrderDesc.Text = I18n.Get("WidgetsOrderDesc", lang);
            BtnUpWidget.Content = I18n.Get("BtnMoveUp", lang);
            BtnDownWidget.Content = I18n.Get("BtnMoveDown", lang);

            TxtCpuTempTitle.Text = I18n.Get("CpuTempTitle", lang);
            TxtCpuTempDesc.Text = I18n.Get("CpuTempDesc", lang);
            CmbItemCpuAuto.Content = I18n.Get("CpuTempAuto", lang);
            CmbItemCpuWmf.Content = I18n.Get("CpuTempWmf", lang);
            CmbItemCpuDisabled.Content = I18n.Get("CpuTempDisabled", lang);

            TxtOverlaysTitle.Text = I18n.Get("OverlaysTitle", lang);
            ChkOverlayTouchId.Content = I18n.Get("OverlayTouchId", lang);
            ChkOverlayUac.Content = I18n.Get("OverlayUac", lang);
            ChkOverlayCharging.Content = I18n.Get("OverlayCharging", lang);

            // Bottom bar & Tray
            TxtSavedAutoNote.Text = I18n.Get("SavedAutoNote", lang);
            BtnResetDefaults.Content = I18n.Get("BtnResetDefaults", lang);
            BtnApply.Content = I18n.Get("BtnApply", lang);
            BtnHideToTray.Content = I18n.Get("BtnHideToTray", lang);
            BtnHeaderTray.Content = I18n.Get("BtnHideToTray", lang);

            UpdateAutoStartUI(AutoStartHelper.IsAutoStartEnabled());
            UpdateTrayMenu();

            // Refresh widget names in list
            foreach (var widget in WidgetsList)
            {
                widget.Name = I18n.GetWidgetName(widget.Id, lang);
            }
            LstWidgets.Items.Refresh();
        }

        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && CmbLanguage.SelectedItem is ComboBoxItem item)
            {
                _config.Language = item.Tag?.ToString() ?? "Auto";
                ApplyLanguage();
                SaveConfigFromUI();
            }
        }

        private void RefreshSkinsList(string selectedSkin = null)
        {
            CmbPetSkin.Items.Clear();
            var skins = PetImporter.GetAvailableSkins();
            int selectedIndex = 0;

            for (int i = 0; i < skins.Count; i++)
            {
                CmbPetSkin.Items.Add(skins[i]);
                if (string.Equals(skins[i], selectedSkin ?? _config.SelectedPetSkin, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (CmbPetSkin.Items.Count > 0)
            {
                CmbPetSkin.SelectedIndex = selectedIndex;
            }
        }

        private void OnBrightnessSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrightnessValue != null)
            {
                TxtBrightnessValue.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing)
            {
                SaveConfigFromUI();
            }
        }

        private void OnEscModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && CmbEscMode.SelectedItem is ComboBoxItem item)
            {
                _config.ShowEscButtonMode = item.Tag?.ToString() ?? "Auto";
                SaveConfigFromUI();
            }
        }

        private void OnSleepTimeoutChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && CmbSleepTimeout.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int timeout))
                {
                    _config.SleepTimeoutSeconds = timeout;
                    SaveConfigFromUI();
                }
            }
        }

        private void OnPetSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPetSpeedValue != null)
            {
                TxtPetSpeedValue.Text = $"{e.NewValue:0.0}x";
            }
            if (!_isInitializing)
            {
                SaveConfigFromUI();
            }
        }

        private void OnPetSkinSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && CmbPetSkin.SelectedItem is string skin)
            {
                _config.SelectedPetSkin = skin;
                SaveConfigFromUI();
            }
        }

        private void OnCpuTempSourceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && CmbCpuTempSource.SelectedItem is ComboBoxItem item)
            {
                _config.CpuTempSource = item.Tag?.ToString() ?? "Auto";
                SaveConfigFromUI();
            }
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SaveConfigFromUI();
            }
        }

        private void OnWidgetListChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SaveConfigFromUI();
            }
        }

        private void OnAutoStartToggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                bool enabled = ChkAutoStart.IsChecked == true;
                AutoStartHelper.SetAutoStart(enabled);
                _config.AutoStartWithWindows = enabled;
                SaveConfigFromUI();
                UpdateAutoStartUI(enabled);
                UpdateTrayMenu();
            }
        }

        private void OnWidgetMoveUpClicked(object sender, RoutedEventArgs e)
        {
            int index = LstWidgets.SelectedIndex;
            if (index > 0)
            {
                var item = WidgetsList[index];
                WidgetsList.RemoveAt(index);
                WidgetsList.Insert(index - 1, item);
                LstWidgets.SelectedIndex = index - 1;

                UpdateWidgetOrders();
                SaveConfigFromUI();
            }
        }

        private void OnWidgetMoveDownClicked(object sender, RoutedEventArgs e)
        {
            int index = LstWidgets.SelectedIndex;
            if (index >= 0 && index < WidgetsList.Count - 1)
            {
                var item = WidgetsList[index];
                WidgetsList.RemoveAt(index);
                WidgetsList.Insert(index + 1, item);
                LstWidgets.SelectedIndex = index + 1;

                UpdateWidgetOrders();
                SaveConfigFromUI();
            }
        }

        private void UpdateWidgetOrders()
        {
            for (int i = 0; i < WidgetsList.Count; i++)
            {
                WidgetsList[i].Order = i;
            }
        }

        private void OnImportPetClicked(object sender, RoutedEventArgs e)
        {
            string lang = _config.GetEffectiveLanguage();
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = I18n.Get("ImportDlgTitle", lang),
                    Filter = I18n.Get("ImportFilter", lang),
                    Multiselect = true
                };

                if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
                {
                    string defaultName = Path.GetFileNameWithoutExtension(dlg.FileNames[0]);
                    string petName = PromptPetName(defaultName, lang);

                    if (string.IsNullOrWhiteSpace(petName))
                    {
                        return;
                    }

                    bool success;
                    string errorMsg;

                    if (dlg.FileNames.Length == 1 && dlg.FileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    {
                        success = PetImporter.ImportGif(dlg.FileName, petName, out errorMsg);
                    }
                    else
                    {
                        success = PetImporter.ImportPngSprites(dlg.FileNames, petName, out errorMsg);
                    }

                    if (success)
                    {
                        RefreshSkinsList(petName);
                        _config.SelectedPetSkin = petName;
                        SaveConfigFromUI();
                        MessageBox.Show(string.Format(I18n.Get("ImportSuccess", lang), petName),
                            I18n.Get("AppliedTitle", lang), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(string.Format(I18n.Get("ImportError", lang), errorMsg),
                            I18n.Get("ErrorTitle", lang), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{I18n.Get("ErrorTitle", lang)}: {ex.Message}",
                    I18n.Get("ErrorTitle", lang), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string PromptPetName(string defaultName, string lang)
        {
            var window = new Window
            {
                Title = I18n.Get("PetPromptTitle", lang),
                Width = 380,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (System.Windows.Media.Brush)FindResource("CardBackground"),
                Foreground = System.Windows.Media.Brushes.White,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock
            {
                Text = I18n.Get("PetPromptMsg", lang),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var txt = new TextBox
            {
                Text = defaultName,
                Background = (System.Windows.Media.Brush)FindResource("ControlBackground"),
                Foreground = System.Windows.Media.Brushes.White,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 14)
            };
            stack.Children.Add(txt);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = I18n.Get("BtnOk", lang), Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancel = new Button { Content = I18n.Get("BtnCancel", lang), Width = 75, IsCancel = true };

            btnOk.Click += (s, e) => { window.DialogResult = true; window.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            window.Content = stack;
            return (window.ShowDialog() == true) ? txt.Text.Trim() : null;
        }

        private void SaveConfigFromUI()
        {
            try
            {
                if (CmbLanguage.SelectedItem is ComboBoxItem langItem)
                {
                    _config.Language = langItem.Tag?.ToString() ?? "Auto";
                }

                if (CmbEscMode.SelectedItem is ComboBoxItem escItem)
                {
                    _config.ShowEscButtonMode = escItem.Tag?.ToString() ?? "Auto";
                }

                _config.TouchBarBrightness = (int)SliderTouchBarBrightness.Value;
                _config.SyncWithMainDisplayBrightness = ChkSyncBrightness.IsChecked == true;

                if (CmbSleepTimeout.SelectedItem is ComboBoxItem timeoutItem &&
                    int.TryParse(timeoutItem.Tag?.ToString(), out int timeout))
                {
                    _config.SleepTimeoutSeconds = timeout;
                }

                _config.ShowFnKeysOnFnPress = ChkShowFnKeys.IsChecked == true;
                _config.AutoStartWithWindows = ChkAutoStart.IsChecked == true;

                _config.PetEnabled = ChkPetEnabled.IsChecked == true;
                _config.PetSpeed = Math.Round(SliderPetSpeed.Value, 2);
                _config.PetReactToTouch = ChkPetReactToTouch.IsChecked == true;
                _config.PetSpeedUpOnHighCpu = ChkPetCpuSpeedup.IsChecked == true;
                _config.PetPinned = ChkPetPinned.IsChecked == true;
                SliderPetSpeed.IsEnabled = !_config.PetPinned;

                if (CmbPetSkin.SelectedItem is string skin)
                {
                    _config.SelectedPetSkin = skin;
                }

                _config.Widgets = WidgetsList.ToList();

                if (CmbCpuTempSource.SelectedItem is ComboBoxItem tempItem)
                {
                    _config.CpuTempSource = tempItem.Tag?.ToString() ?? "Auto";
                }

                _config.OverlayTouchIdPrompt = ChkOverlayTouchId.IsChecked == true;
                _config.OverlayUacDialogs = ChkOverlayUac.IsChecked == true;
                _config.OverlayChargingBanner = ChkOverlayCharging.IsChecked == true;

                ConfigRepository.Save(_config);
            }
            catch { }
        }

        #region System Tray & AutoStart

        private void SetupSystemTray()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();

                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(icoPath);
                }
                else
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                _notifyIcon.Text = "MacBook Touch Bar";
                _notifyIcon.Visible = true;

                _trayMenu = new System.Windows.Forms.ContextMenuStrip();
                _notifyIcon.ContextMenuStrip = _trayMenu;
                _notifyIcon.DoubleClick += (s, e) => ShowAndRestoreWindow();
                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        ShowAndRestoreWindow();
                    }
                };
            }
            catch { }
        }

        private void UpdateTrayMenu()
        {
            if (_trayMenu == null) return;
            try
            {
                _trayMenu.Items.Clear();

                string lang = _config?.GetEffectiveLanguage() ?? "ru";
                bool autoStart = AutoStartHelper.IsAutoStartEnabled();

                var itemShow = new System.Windows.Forms.ToolStripMenuItem(I18n.Get("TrayOpen", lang), null, (s, e) => ShowAndRestoreWindow());
                itemShow.Font = new System.Drawing.Font(itemShow.Font, System.Drawing.FontStyle.Bold);
                _trayMenu.Items.Add(itemShow);

                string stateText = autoStart ? I18n.Get("AutoStartOn", lang) : I18n.Get("AutoStartOff", lang);
                string autoText = string.Format(I18n.Get("TrayAutoStart", lang), stateText);
                var itemAuto = new System.Windows.Forms.ToolStripMenuItem(autoText, null, (s, e) => ToggleAutoStart());
                itemAuto.Checked = autoStart;
                _trayMenu.Items.Add(itemAuto);

                var itemRestart = new System.Windows.Forms.ToolStripMenuItem(I18n.Get("TrayRestart", lang), null, (s, e) => RestartTouchBar());
                _trayMenu.Items.Add(itemRestart);

                var itemDonate = new System.Windows.Forms.ToolStripMenuItem("Donate / Support...");
                itemDonate.DropDownItems.Add("Boosty (Card / Global)", null, (s, e) => OpenUrl("https://boosty.to/gringleb/donate"));
                itemDonate.DropDownItems.Add("NOWPayments (Crypto: USDT / TON / BTC)", null, (s, e) => OpenUrl("https://nowpayments.io/donation/glebgrin"));
                _trayMenu.Items.Add(itemDonate);

                _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                var itemExit = new System.Windows.Forms.ToolStripMenuItem(I18n.Get("TrayExit", lang), null, (s, e) =>
                {
                    _isRealExit = true;
                    Close();
                });
                _trayMenu.Items.Add(itemExit);
            }
            catch { }
        }

        public void ShowAndRestoreWindow()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
        }

        private void RestartTouchBar()
        {
            TouchBarEngine.RestartHost();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isRealExit)
            {
                e.Cancel = true;
                Hide();
                if (!_hasShownBalloon)
                {
                    _hasShownBalloon = true;
                    string lang = _config?.GetEffectiveLanguage() ?? "ru";
                    _notifyIcon?.ShowBalloonTip(2000, I18n.Get("TrayBalloonTitle", lang), I18n.Get("TrayBalloonText", lang), System.Windows.Forms.ToolTipIcon.Info);
                }
                return;
            }

            try
            {
                TouchBarEngine.StopHost();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch { }

            base.OnClosing(e);
        }

        private void OnHideToTrayClicked(object sender, RoutedEventArgs e)
        {
            Hide();
            if (!_hasShownBalloon)
            {
                _hasShownBalloon = true;
                string lang = _config?.GetEffectiveLanguage() ?? "ru";
                _notifyIcon?.ShowBalloonTip(2000, I18n.Get("TrayBalloonTitle", lang), I18n.Get("TrayBalloonText", lang), System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void OnBtnToggleAutoStartQuickClicked(object sender, RoutedEventArgs e)
        {
            ToggleAutoStart();
        }

        private void OnBtnToggleAutoStartLargeClicked(object sender, RoutedEventArgs e)
        {
            ToggleAutoStart();
        }

        private void ToggleAutoStart()
        {
            bool currentlyEnabled = AutoStartHelper.IsAutoStartEnabled();
            bool newState = !currentlyEnabled;
            AutoStartHelper.SetAutoStart(newState);
            _config.AutoStartWithWindows = newState;
            ConfigRepository.Save(_config);

            UpdateAutoStartUI(newState);
            UpdateTrayMenu();
        }

        private void UpdateAutoStartUI(bool enabled)
        {
            string lang = _config?.GetEffectiveLanguage() ?? "ru";
            string stateText = enabled ? I18n.Get("AutoStartOn", lang) : I18n.Get("AutoStartOff", lang);

            ChkAutoStart.IsChecked = enabled;

            if (BtnAutoStartQuick != null)
            {
                BtnAutoStartQuick.Content = string.Format(I18n.Get("BtnAutoStartQuick", lang), stateText);
                BtnAutoStartQuick.Foreground = enabled
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 209, 88))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
            }

            if (BtnToggleAutoStartLarge != null)
            {
                BtnToggleAutoStartLarge.Content = enabled
                    ? I18n.Get("BtnAutoStartDisable", lang)
                    : I18n.Get("BtnAutoStartEnable", lang);
            }
        }

        #endregion

        #region Donate / Support Developer

        private void OnDonateBtnClicked(object sender, RoutedEventArgs e)
        {
            if (DonatePopup != null)
            {
                DonatePopup.IsOpen = !DonatePopup.IsOpen;
                if (DonatePopup.IsOpen)
                {
                    if (BtnCopyBoosty != null) BtnCopyBoosty.Content = "Copy";
                    if (BtnCopyNowPayments != null) BtnCopyNowPayments.Content = "Copy";
                }
            }
        }

        private void OnDonateBoostyClicked(object sender, MouseButtonEventArgs e)
        {
            if (DonatePopup != null) DonatePopup.IsOpen = false;
            OpenUrl("https://boosty.to/gringleb/donate");
        }

        private void OnDonateNowPaymentsClicked(object sender, MouseButtonEventArgs e)
        {
            if (DonatePopup != null) DonatePopup.IsOpen = false;
            OpenUrl("https://nowpayments.io/donation/glebgrin");
        }

        private void OnCopyBoostyClicked(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            try
            {
                Clipboard.SetText("https://boosty.to/gringleb/donate");
                if (BtnCopyBoosty != null) BtnCopyBoosty.Content = "Copied!";
            }
            catch { }
        }

        private void OnCopyNowPaymentsClicked(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            try
            {
                Clipboard.SetText("https://nowpayments.io/donation/glebgrin");
                if (BtnCopyNowPayments != null) BtnCopyNowPayments.Content = "Copied!";
            }
            catch { }
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL {url}: {ex.Message}");
            }
        }

        #endregion

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();
            string lang = _config.GetEffectiveLanguage();
            MessageBox.Show(I18n.Get("AppliedMsg", lang),
                I18n.Get("AppliedTitle", lang), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnResetDefaultsClicked(object sender, RoutedEventArgs e)
        {
            string lang = _config.GetEffectiveLanguage();
            var res = MessageBox.Show(I18n.Get("ResetConfirmMsg", lang),
                I18n.Get("ResetConfirmTitle", lang), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _config = AppConfig.CreateDefault();
                ConfigRepository.Save(_config);
                LoadConfigToUI();
            }
        }
    }
}
