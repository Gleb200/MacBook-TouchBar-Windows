using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TouchBarSettings.Models;

namespace TouchBarSettings
{
    public partial class MainWindow : Window
    {
        private AppConfig _config;
        private bool _isInitializing = true;
        public ObservableCollection<WidgetConfigItem> WidgetsList { get; } = new ObservableCollection<WidgetConfigItem>();

        public MainWindow()
        {
            InitializeComponent();
            LstWidgets.ItemsSource = WidgetsList;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfigToUI();
            _isInitializing = false;
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

            // AutoStart
            ChkAutoStart.IsChecked = AutoStartHelper.IsAutoStartEnabled();

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

            // Bottom bar
            TxtSavedAutoNote.Text = I18n.Get("SavedAutoNote", lang);
            BtnResetDefaults.Content = I18n.Get("BtnResetDefaults", lang);
            BtnApply.Content = I18n.Get("BtnApply", lang);

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
