using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZenithModsLauncher.Models;
using ZenithModsLauncher.Utils;
using Shape = System.Windows.Shapes.Shape;

namespace ZenithModsLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static MainWindow instance;

        //TwoWay Bindings hack
        public List<ListViewModsModel> ModsList { get; set; }
        public List<ModSettingsViewInner> SettingsModList { get; set; }

        // statics
        private const string _configVersion = "2";

        private const string _zenithBundleUrl = "https://miinc.site/zenith/ModsBundle.zip";
        private const string _zenithConfigUrl = "https://miinc.site/zenith/vtzenith.json";
        private const string _zenithBetaUrl = "https://miinc.site/zenith/VTZenith.Beta.dll";
        private const string _zenithBetaConfigUrl = "https://miinc.site/zenith/vtzenith.Beta.json";
        private const string _zenithChangeLogsUrl = "https://zenith.miinc.site/api/changelog";
        private const string _zenithLatestVersionUrl = "https://miinc.site/zenith/installerversion";
        private const string _zenithInstallerUrl = "https://miinc.site/zenith/Installer.exe";

        private const string _launcherChangeLogsResourcePath = "ZenithModsLauncher.Resources.LauncherChangeLogs.json";

        private static List<GameDirectory> gameDirectories = new();
        private static int selectedGameDirectory => Properties.Settings.Default.selectedGameDirectory;
        private static string _targetDirectory => gameDirectories.ElementAtOrDefault(selectedGameDirectory)?.Path;
        private bool _isModConfigOpen = false;
        private bool _isModConfigSaved = true;
        private bool isPrerelease;
        private bool hasNewVersion = false;

        private CancellationTokenSource cts;
        private AnimationTimeline currentAnimation;

        private MainModsModel _modsModel;
        private ChangeLogsModel _changeLogsModel;

        private readonly List<string> _installEntites = new()
        {
            "version.dll",
            "vtzenith.json",
            "vtzenith.font.vta",
            "AutoTranslator",
            "MelonLoader",
            "Mods",
            "UserLibs",
            "Plugins",
            "UserData"
        };

        public MainWindow()
        {
            instance = this;

            InitializeComponent();
            FindGamePath();
            RefreshInstallationStatus();

            var ourVersion = Assembly.GetExecutingAssembly().GetName().Version;
            tbVersionInfo.Text = "Версия " + ourVersion.ToString(3);

            GetLatestVersion().ContinueWith(t =>
            {
                var remote = t.Result;
                if (remote == null) return;
                if (remote > ourVersion)
                {
                    hasNewVersion = true;
                    Dispatcher.Invoke(() =>
                    {
                        badgeChangeLogs.Visibility = Visibility.Visible;
                        var animation = new BrushAnimation
                        {
                            From = Brushes.Red,
                            To = Brushes.Orange,
                            Duration = TimeSpan.FromSeconds(1),
                            AutoReverse = true,
                            RepeatBehavior = RepeatBehavior.Forever
                        };
                        elChangeLogs.BeginAnimation(Shape.FillProperty, animation);
                    });
                }
            });

            GetChangeLogs().ContinueWith(t => Dispatcher.Invoke(() => btnVersionInfo.IsEnabled = t.Result));

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyUp += MainWindow_PreviewKeyUp;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.RightCtrl)
            {
                isPrerelease = true;
                RefreshInstallationStatus();
                e.Handled = true;
            }
        }

        private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.RightCtrl)
            {
                isPrerelease = false;
                RefreshInstallationStatus();
                e.Handled = true;
            }
        }

        #region Buttons UI
        private void Button_Install(object sender, RoutedEventArgs e) => InstallReinstall(isPrerelease);

        private void Button_Uninstall(object sender, RoutedEventArgs e)
        {
            if (IsInstalled(_targetDirectory))
            {
                ShowMessageAskDialog("Вы точно хотите удалить модификации? Все настройки будут утеряны!",
                    "Предупреждение", () => Uninstall().ContinueWith(t =>
                    {
                        if (t.IsCanceled)
                        {
                            SetIsTaskRunning(null);
                            ShowOperationCancelled();
                        }
                        else ShowMessageBox("Удаление завершено!");
                        Dispatcher.Invoke(() =>
                        {
                            RefreshInstallationStatus();
                            pbProgress.Value = 0;
                        });
                    }));
            }
            else
            {
                ShowOperationCancelled(". Не найдены файлы модификации!");
            }
        }

        private void Button_ChooseFolder(object sender, RoutedEventArgs e)
        {
            if (!_isModConfigSaved)
                ShowModConfigUnsavedWarning(ChooseFolder_Internal);
            else
                ChooseFolder_Internal();
        }

        private void ChooseFolder_Internal()
        {
            CloseModList_Internal();

            OpenInstallDirsDialog();
        }

        private void Button_OpenModsList(object sender, RoutedEventArgs e)
        {
            if (_isModConfigOpen)
            {
                if (!_isModConfigSaved)
                    ShowModConfigUnsavedWarning(CloseModList_Internal);
                else CloseModList_Internal();
            }
            else if (HasModConfig(_targetDirectory))
            {
                ModsList = GetModList();
                ModsListView.ItemsSource = ModsList;

                brdModConfig.Opacity = 0;
                brdModConfig.Visibility = Visibility.Visible;
                OpenModsListButton.IsEnabled = false;

                _isModConfigOpen = true;

                var animation = new DoubleAnimation
                {
                    To = 1,
                    BeginTime = TimeSpan.FromSeconds(0),
                    Duration = TimeSpan.FromSeconds(0.3f),
                    FillBehavior = FillBehavior.Stop
                };

                animation.Completed += (s, a) =>
                {
                    currentAnimation = null;
                    OpenModsListButton.IsEnabled = true;
                    brdModConfig.Opacity = 1;
                };

                if (currentAnimation != null) brdModConfig.BeginAnimation(UIElement.OpacityProperty, null);

                currentAnimation = animation;
                brdModConfig.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }

        private void CloseModList_Internal()
        {
            _isModConfigOpen = false;
            _isModConfigSaved = true;

            // Clean
            _modsModel = default;
            SettingsModList = default;
            ModsList = default;

            brdModConfig.Opacity = 1;
            OpenModsListButton.IsEnabled = false;

            var animation = new DoubleAnimation
            {
                To = 0,
                BeginTime = TimeSpan.FromSeconds(0),
                Duration = TimeSpan.FromSeconds(0.3f),
                FillBehavior = FillBehavior.Stop
            };

            animation.Completed += (s, a) =>
            {
                currentAnimation = null;
                ModsListView.ItemsSource = default;
                ModsListSettingsView.ItemsSource = default;
                OpenModsListButton.IsEnabled = true;
                brdModConfig.Opacity = 0;
                brdModConfig.Visibility = Visibility.Hidden;
            };

            if (currentAnimation != null) brdModConfig.BeginAnimation(UIElement.OpacityProperty, null);

            currentAnimation = animation;
            brdModConfig.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            var setting = (ModSettingsViewInner)((FrameworkElement)sender).DataContext;

            if (setting == null) return;

            var modName = setting.ModName;
            var settingName = setting.Name;

            if (setting.IsBoolValue)
                _modsModel.Mods[modName].Settings[settingName].Value = setting.BoolValue;
            else if (setting.IsIntValue)
                _modsModel.Mods[modName].Settings[settingName].Value = setting.IntValue;
            else if (setting.IsStringValue)
                _modsModel.Mods[modName].Settings[settingName].Value = setting.StringValue;
            else if (setting.IsColorValue)
                _modsModel.Mods[modName].Settings[settingName].Value = setting.ColorValue;

            _isModConfigSaved = false;
        }

        public void Button_SaveConfigurationMods(object sender, RoutedEventArgs e)
        {
            if (!SaveChangedModsConfig())
                ShowOperationCancelled(". Сохранение конфигурации не удалось!");
        }

        private void Button_ResetConfiguration(object sender, RoutedEventArgs e)
        {
            ShowMessageAskDialog("Все настройки модов будут сброшены!\nВы уверены, что хотите продолжить?",
                "Предупреждение", () =>
                {
                    CloseModList_Internal();
                    SetIsTaskRunning("Сбрасываем настройки");
                    _ = Task.Run(async () =>
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        var configString = await NetworkUtils.GetFile(_zenithConfigUrl);

                        cts.Token.ThrowIfCancellationRequested();

                        File.WriteAllText(ConfigFile, configString);

                        Dispatcher.Invoke(() =>
                        {
                            pbProgress.Value = 1;
                        });

                        await Task.Delay(500);

                        SetIsTaskRunning(null);
                        Dispatcher.Invoke(() =>
                        {
                            RefreshInstallationStatus();
                            Button_OpenModsList(sender, e);
                            pbProgress.Value = 0;
                        });
                    });
                });
        }

        private void Button_Exit(object sender, RoutedEventArgs e)
        {
            if (cts != null) cts.Cancel();
            else Close();
        }

        private void Button_DonationCredits(object sender, RoutedEventArgs e)
        {
            ShowMessageBox("Сбер: 4274 3200 6026 9662\n" +
                "Другие способы в личке:" +
                "\nТГ: @Miinc / @Unmori \nDiscord: Miinc#1707 / Unmori#2154",
                "Благодарим за любую копеечку ^_^");
        }

        private void Button_GetErrorLogs(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(Path.Combine(_targetDirectory, "MelonLoader\\Latest.log")))
            {
                ShowMessageBox("Не найден последний log файл. " +
                    "Возможно, игра ни разу не была запущена.", "Ошибка");
                return;
            }

            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Выберите путь для сохранения лога:";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    File.Copy(Path.Combine(_targetDirectory, "MelonLoader\\Latest.log"),
                        Path.Combine(dialog.SelectedPath,
                        "VTZenithLogs.log"), true);

                    ShowMessageBox($"\nПуть: {Path.Combine(dialog.SelectedPath, "MelonLoader\\VTZenithLogs.log")}",
                        "Сохранение лога прошло успешно!");
                }
            }
        }
        #endregion

        private void InstallReinstall(bool isPrerelease, bool isReinstall = false)
        {
            if (_targetDirectory != null)
            {
                if (IsInstalled(_targetDirectory))
                {
                    ShowMessageAskDialog("Это действие переустановит файлы модификации. Первый запуск" +
                        " игры снова будет достаточно долгим. Продолжить?",
                        "Предупреждение", () => Uninstall(true).ContinueWith(t =>
                        {
                            if (t.IsCanceled || t.IsFaulted)
                            {
                                var _ex = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception) : null;
                                ShowOperationCancelled(_ex != null ?
                                    ("\n" + (_ex is UnauthorizedAccessException ? "Возможно, вы не закрыли игру" : _ex.Message)) : "");
                                SetIsTaskRunning(null);
                            }
                            else InstallReinstall(isPrerelease, true);
                        }));
                    return;
                }

                Install(isPrerelease, isReinstall).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        RefreshInstallationStatus();
                        pbProgress.Value = 0;
                    });

                    SetIsTaskRunning(null);
                    if (t.IsCanceled) ShowMessageBox("Установка отменена");
                    else ShowMessageBox("Установка завершена");
                });
            }
            else ShowMessageBox("Путь до игры не указан." +
                        "\nВыберите путь и повторите попытку!", "Ошибка");
        }

        private void RefreshInstallationStatus()
        {
            RefreshSelectedInstallDirectory();

            bool isInstalled = IsInstalled(_targetDirectory);
            bool canConfigure = isInstalled &&
                TryGetModSettings(out var model) &&
                model.ConfigVersion.ToString() == _configVersion;

            if (isInstalled)
            {
                btnUninstall.Visibility = Visibility.Visible;
                btnInstall.SetValue(Grid.ColumnProperty, 2);
                btnInstall.SetValue(Grid.ColumnSpanProperty, 1);
                btnInstall.Content = isPrerelease ? "Переуст. (Бета)" : "Переустановить";
            }
            else
            {
                btnUninstall.Visibility = Visibility.Collapsed;
                btnInstall.SetValue(Grid.ColumnProperty, 1);
                btnInstall.SetValue(Grid.ColumnSpanProperty, 2);
                btnInstall.Content = "Установить" + (isPrerelease ? " (Бета)" : "");
            }

            OpenModsListButton.Opacity = canConfigure ? 1 : 0.5;
            ((UIElement)OpenModsListButton.ToolTip).Opacity = canConfigure ? 0 : 1;
        }

        private async Task Install(bool isPrerelease, bool isReinstall = false)
        {
            SetIsTaskRunning("Установка...");

            // Main bundle
            {
                await NetworkUtils.GetFile(_zenithBundleUrl, async (stream, length) =>
                {
                    using var ms = new MemoryStream();

                    await stream.CopyToAsync(ms, new Progress<long>(h =>
                        Dispatcher.Invoke(() =>
                            pbProgress.Value = (double)h / length)),
                        cts.Token);

                    ms.Position = 0;
                    new ZipArchive(ms).ExtractToDirectory(_targetDirectory);
                });
            }

            if (isPrerelease)
            {
                SetIsTaskRunning(null);
                SetIsTaskRunning("Обновляем до беты...");
                // Beta DLL
                {
                    await NetworkUtils.GetFile(_zenithBetaUrl, async (stream, length) =>
                    {
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms, new Progress<long>(h =>
                            Dispatcher.Invoke(() =>
                                pbProgress.Value = (double)h / length)),
                            cts.Token);

                        ms.Position = 0;
                        using var file = File.Create(Path.Combine(_targetDirectory, "Mods/VTZenith.dll"));

                        ms.CopyTo(file);
                    });
                }
                // Beta Config
                {
                    await NetworkUtils.GetFile(_zenithBetaConfigUrl, async (stream, length) =>
                    {
                        using var ms = new MemoryStream();

                        await stream.CopyToAsync(ms, new Progress<long>(h =>
                            Dispatcher.Invoke(() =>
                                pbProgress.Value = (double)h / length)),
                            cts.Token);

                        ms.Position = 0;
                        using var file = File.Create(ConfigFile);

                        ms.CopyTo(file);
                    });
                }
            }
            // Merge settings
            if (isReinstall && _modsModel != null && TryGetModSettings(out var _newConfig))
            {
                _newConfig.EnableMods = _modsModel.EnableMods;

                if (_modsModel.Mods != null)
                {
                    if (_newConfig.Mods == null) _newConfig.Mods = default;

                    var oldMods = _modsModel.Mods;
                    var newMods = _newConfig.Mods;

                    var allMods = oldMods.Keys.Concat(newMods.Keys).Distinct().ToList();
                    foreach (var mod in allMods)
                    {
                        var had = oldMods.TryGetValue(mod, out var oldMod);
                        var has = newMods.TryGetValue(mod, out var newMod);

                        if (had)
                        {
                            if (has)
                            {
                                newMod.IsEnabled = oldMod.IsEnabled;

                                if (oldMod.Settings == null) continue;
                                if (newMod.Settings == null) newMod.Settings = new();
                                var oldSettings = oldMod.Settings;
                                var newSettings = newMod.Settings;
                                var allSettings = oldSettings.Keys.Concat(newSettings.Keys).Distinct().ToList();

                                foreach (var setting in allSettings)
                                {
                                    var hadS = oldSettings.TryGetValue(setting, out var oldSetting);
                                    var hasS = newSettings.TryGetValue(setting, out var newSetting);

                                    if (hadS)
                                    {
                                        if (hasS) newSetting.Value = oldSetting.Value;
                                        else newSettings.Add(setting, oldSetting);
                                    }
                                }
                            }
                            else newMods.Add(mod, oldMod);
                        }
                    }

                    _modsModel = _newConfig;
                    if (!SaveChangedModsConfig())
                        ShowMessageBox("Ошибка сохранения настроек! Настройки могли быть сброшены");
                }
            }
        }

        private async Task Uninstall(bool isReinstall = false)
        {
            SetIsTaskRunning("Удаление...");
            for (int i = 0; i < 10; i++)
            {
                pbProgress.Value = (i + 1) / 10d;
                await Task.Delay(200);
                cts.Token.ThrowIfCancellationRequested();
            }

            if (isReinstall) _ = TryGetModSettings(out _modsModel);

            try
            {
                for (int i = 0; i < _installEntites.Count; i++)
                {
                    string entityName = _installEntites[i];
                    var path = Path.Combine(_targetDirectory, entityName);

                    if (File.Exists(path))
                        File.Delete(path);
                    else if (Directory.Exists(path))
                        Directory.Delete(path, true);
                    pbProgress.Value = (_installEntites.Count + i + 1) / (_installEntites.Count * 2d);
                };
            }
            catch
            {
                ShowMessageBox("Невозможно удалить файлы." +
                           "\nВозможно, вы не закрыли игру!", "Ошибка");
                throw;
            }

            pbProgress.Value = 1;

            SetIsTaskRunning(null);
        }

        private void SetIsTaskRunning(string name)
        {
            var running = !string.IsNullOrEmpty(name);
            if (running && cts != null)
                throw new InvalidOperationException("Cannot start two tasks");
            Dispatcher.Invoke(() =>
            {
                btnInstall.IsEnabled =
                    btnUninstall.IsEnabled =
                    btnSelectPath.IsEnabled =
                    DirectoryPathBox.IsEnabled =
                    OpenModsListButton.IsEnabled =
                    !running;
                CloseButton.Content = !running ? "Выход" : "Отмена";

                if (running)
                {
                    if (_isModConfigOpen) CloseModList_Internal();
                    OpenModsListButton.Opacity = 0.5;
                    pbProgress.Value = 0;
                }

                tbProgressInfo.Text = name;
            });
            if (running)
                cts = new CancellationTokenSource();
            else
                cts = null;
        }

        #region Mods
        private List<ListViewModsModel> GetModList()
        {
            if (HasModConfig(_targetDirectory))
            {
                if (TryGetModSettings(out _modsModel))
                {
                    return _modsModel.Mods.Select(x => new ListViewModsModel()
                    {
                        Name = x.Key,
                        HumanName = x.Value.HumanName,
                        Description = x.Value.Description,
                        IsEnabled = x.Value.IsPersistent ? null : x.Value.IsEnabled,
                        ModSettings = x.Value.Settings?.Select(y => new ModSettingsView()
                        {
                            Name = y.Key,
                            HumanName = y.Value.HumanName,
                            Description = y.Value.Description,
                            Type = y.Value.Type,
                            Value = y.Value.Value
                        }).ToList()
                    }).ToList();
                }
            }
            return new List<ListViewModsModel>();
        }

        private List<ListViewModsModel> GetSettingsModList(string modName)
        {
            if (_configVersion == _modsModel.ConfigVersion.ToString())
            {
                return _modsModel.Mods.Where(x => x.Key == modName)
                    .Select(x => new ListViewModsModel()
                    {
                        Name = x.Key,
                        ModSettings = x.Value.Settings.Any() ? x.Value.Settings?.DefaultIfEmpty().Select(y => new ModSettingsView()
                        {
                            Name = y.Key,
                            HumanName = y.Value.HumanName,
                            Description = y.Value.Description,
                            Type = y.Value.Type,
                            Value = y.Value.Value
                        })
                        .ToList()
                        : null
                    })
                    .ToList();
            }
            return new List<ListViewModsModel>();
        }

        private void ModsListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                // хак из-за нулябельного события от UI, когда мы чистим лист модов
                if (ModsListView.ItemsSource == default) return;

                var listViewObject = (ListView)sender;
                var item = listViewObject.SelectedItem;

                var modName = item.GetType()
                    .GetProperty("Name")
                    .GetValue(item, null)
                    .ToString();

                var modSettings = GetSettingsModList(modName)
                    .Where(x => x.ModSettings != null)
                    .SelectMany(x => x.ModSettings);

                SettingsModList = modSettings.Select(x => new ModSettingsViewInner()
                {
                    ModName = modName,
                    Name = x.Name,
                    HumanName = x.HumanName,
                    Description = x.Description,

                    BoolValue = x.Type == ModSettingsType.B
                    ? x.Value : default(bool),
                    IsBoolValue = x.Type == ModSettingsType.B,

                    IntValue = x.Type == ModSettingsType.I
                    ? x.Value : default(int),
                    IsIntValue = x.Type == ModSettingsType.I,

                    StringValue = x.Type == ModSettingsType.S
                    ? x.Value : default(string),
                    IsStringValue = x.Type == ModSettingsType.S,

                    ColorValue = x.Type == ModSettingsType.C
                    ? x.Value : default(string),
                    IsColorValue = x.Type == ModSettingsType.C
                })
                .ToList();

                ModsListSettingsView.ItemsSource = SettingsModList;
                if (ModsListSettingsView.GetScrollViewer() is ScrollViewer scroll) scroll.ScrollToTop();
            }
            catch { }
        }

        private void CheckEnableMod_Checked(object sender, RoutedEventArgs e)
        {
            var checkBoxObject = (CheckBox)sender;
            var mod = (ListViewModsModel)checkBoxObject.DataContext;
            if (mod == null) return;
            if (!mod.IsEnabled.HasValue)
            {
                checkBoxObject.IsChecked = null;
                e.Handled = true;
                return;
            }

            _modsModel.Mods[mod.Name].IsEnabled = checkBoxObject.IsChecked == true;
            _isModConfigSaved = false;
        }
        #endregion

        #region Other UI

        private void Grid_MouseDragMove(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public void ShowOperationCancelled(string additionalMessage = "")
        {
            ShowMessageBox("Операция отменена" + additionalMessage);
        }

        public void ShowModConfigUnsavedWarning(Action @continue)
        {
            ShowMessageAskDialog("Все несохранённые настройки будут утеряны. Продолжить?",
                 "Предупреждение", @continue);
        }

        private void ShowMessageAskDialog(string text, string title, Action onYes, Action onNo = null)
        {
            ShowMessageBox(text, title, ("Да", onYes), ("Нет", onNo));
        }
        #endregion

        #region Custom message boxes
        private static void PushAnimation(FrameworkElement obj, bool enable, Func<bool> cont = null)
        {
            if (cont != null && !cont()) return;
            if (enable)
            {
                obj.Opacity = 0;
                obj.Visibility = Visibility.Visible;
            }
            var animation = new DoubleAnimation
            {
                To = enable ? 1 : 0,
                BeginTime = TimeSpan.FromSeconds(0),
                Duration = TimeSpan.FromSeconds(0.3f),
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (s, a) =>
            {
                if (cont != null && !cont()) return;
                obj.Opacity = enable ? 1 : 0;
                if (!enable) obj.Visibility = Visibility.Collapsed;
            };
            obj.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private Action onButton1Click;
        private Action onButton2Click;
        private int dialogCounter = 0;

        private void ShowMessageBox(string text, string title = null,
            (string, Action) button1 = default, (string, Action) button2 = default)
        {
            int dialogIndex = ++dialogCounter;
            Dispatcher.Invoke(() =>
            {
                if (dialogIndex != dialogCounter) return;
                tblMboxText.Text = text;
                tblMboxTitle.Text = title ?? "Cообщение";
                (btnMbox1.Content, onButton1Click) = button1 == default ?
                     ("OK", null) : button1;

                if (button2 == default) btnMbox2.Visibility = Visibility.Collapsed;
                else
                {
                    btnMbox2.Visibility = Visibility.Visible;
                    (btnMbox2.Content, onButton2Click) = button2;
                }

                PushAnimation(brdDialog, true, () => dialogCounter == dialogIndex);
            });
        }

        private void HideMessageBox()
        {
            int dialogIndex = dialogCounter;
            Dispatcher.Invoke(() => PushAnimation(brdDialog, false, () => dialogCounter == dialogIndex));
        }

        private void btnMbox1_Click(object sender, RoutedEventArgs e)
        {
            HideMessageBox();
            onButton1Click?.Invoke();
        }

        private void btnMbox2_Click(object sender, RoutedEventArgs e)
        {
            HideMessageBox();
            onButton2Click?.Invoke();
        }
        #endregion

        #region Change Logs box
        private void Button_ChangeLogs(object sender, RoutedEventArgs e)
        {
            if (hasNewVersion)
            {
                ShowMessageAskDialog("Ваша версия лаунчера устарела!\nВы хотите скачать новую версию?",
                    "Обновление лаунчера", async () =>
                    {
                        SetIsTaskRunning("Обновление...");
                        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".exe");
                        await NetworkUtils.GetFile(_zenithInstallerUrl, async (stream, length) =>
                        {
                            using var ms = new MemoryStream();
                            await stream.CopyToAsync(ms, new Progress<long>(h =>
                                Dispatcher.Invoke(() =>
                                    pbProgress.Value = (double)h / length)),
                                cts.Token);

                            ms.Position = 0;
                            using (var file = File.Create(tempFile))
                                ms.CopyTo(file);

                            Process.Start(tempFile, $"--vtz.update \"{Assembly.GetExecutingAssembly().Location}\" \"{Process.GetCurrentProcess().Id}\"");

                            Environment.Exit(0);
                        });
                    });
                hasNewVersion = false;
                return;
            }

            SelectChangeLogsTab(0);

            PushAnimation(brdChangeLogs, true);
        }

        private void btnCloseChangeLogs_Click(object sender, RoutedEventArgs e)
        {
            PushAnimation(brdChangeLogs, false);
        }

        private void btnChangeLogsTab_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var allTabs = spChangeLogsTabs.Children;
            if (!allTabs.Contains(btn)) return;
            var index = int.Parse(btn.Tag.ToString());
            SelectChangeLogsTab(index);
        }

        private void SelectChangeLogsTab(int index)
        {
            foreach (var item in spChangeLogsTabs.Children)
                if (item is Button btnItem)
                    btnItem.Style = (Style)this.Resources[btnItem.Tag.ToString() == index.ToString() ?
                        "TabButtonSelected" : "TabButtonUnselected"];

            lvChangeLogs.ItemsSource = Enumerable.Reverse(index switch
            {
                0 => _changeLogsModel.ModsVersions,
                1 => _changeLogsModel.LauncherVersions,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            });
        }
        #endregion

        #region Installation directories
        private void RefreshSelectedInstallDirectory()
        {
            DirectoryPathBox.Text = string.IsNullOrEmpty(_targetDirectory) ?
                "Нажмите для выбора пути к игре" : _targetDirectory;
        }

        private bool FindGamePath()
        {
            var directories = GetGameDirectories();
            var savedDirectories = GetSavedGameDirectories();

            directories.RemoveAll(d => savedDirectories.Any(s => s.Path == d.Path));

            savedDirectories.AddRange(directories);

            if (directories.Any()) SaveGameDirectoryList();

            gameDirectories.AddRange(savedDirectories);

            return _targetDirectory != null;
        }

        private List<GameDirectory> GetGameDirectories()
        {
            var result = new List<GameDirectory>();

            // Steam
            try
            {
                var libraryFoldersPath = Path.Combine((string)Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam",
                    "InstallPath", "C:\\Program Files (x86)\\Steam"), "steamapps\\libraryfolders.vdf");

                if (File.Exists(libraryFoldersPath))
                {

                    var libraryFoldersFile = VdfConvert.Deserialize(File.ReadAllText(libraryFoldersPath));

                    var _json = libraryFoldersFile.ToJson();
                    var folders = _json.First.Children().Select(i => i.First().ToObject<DirectorySteam>()).ToList();

                    foreach (var item in folders)
                    {
                        // Steam App ID of Zenith
                        if (item.apps.ContainsKey("1403370"))
                        {
                            var fullPath = Path.Combine(item.path, "steamapps\\common\\Zenith MMO");
                            if (IsGameDirectory(fullPath))
                            {
                                result.Add(new GameDirectory
                                {
                                    Path = fullPath,
                                    Type = GameDirectoryType.Steam
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessage(ex);
            }

            // Oculus
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Oculus VR, LLC\\Oculus\\Libraries");
                if (key != null)
                {
                    var subkeys = key.GetSubKeyNames();
                    foreach (var item in subkeys)
                    {
                        var subkey = key.OpenSubKey(item);
                        if (subkey != null)
                        {
                            var path = subkey.GetValue("OriginalPath", null) ?? subkey.GetValue("Path", null);
                            if (path != null)
                            {
                                var fullPath = Path.Combine((string)path, "Software\\ramen-vr-zenith");
                                if (IsGameDirectory(fullPath))
                                {
                                    result.Add(new GameDirectory
                                    {
                                        Path = fullPath,
                                        Type = GameDirectoryType.Oculus
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowExceptionMessage(ex);
            }
            return result;
        }

        private List<GameDirectory> GetSavedGameDirectories() => JsonConvert
            .DeserializeObject<List<GameDirectory>>(Properties.Settings.Default.gameDirectories);

        private void SaveGameDirectoryList()
        {
            Properties.Settings.Default.gameDirectories = JsonConvert.SerializeObject(gameDirectories);
            Properties.Settings.Default.Save();
        }

        private void btnAddInstallPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var _dir = dialog.SelectedPath;
                    if (!IsGameDirectory(_dir))
                    {
                        ShowMessageBox("Не похоже, что в этой папке установлена игра!");
                        return;
                    }

                    var newDir = new GameDirectory
                    {
                        Path = _dir,
                        Type = GameDirectoryType.Other
                    };

                    gameDirectories.Add(newDir);

                    newDir.IsSelected = true;

                    RefreshInstallationStatus();
                }
            }
        }

        private void btnRemoveInstallPath_Click(object sender, RoutedEventArgs e)
        {
            var gameDir = (GameDirectory)((sender as FrameworkElement).DataContext);
            if (gameDir == null) return;
            gameDirectories.Remove(gameDir);
            SaveGameDirectoryList();
            RefreshInstallDirsList();
            RefreshSelectedInstallDirectory();
        }

        private void btnInstallDirsClose_Click(object sender, RoutedEventArgs e)
        {
            PushAnimation(brdInstallDirs, false);
        }

        private void OpenInstallDirsDialog()
        {
            RefreshInstallDirsList();
            PushAnimation(brdInstallDirs, true);
        }

        private void RefreshInstallDirsList()
        {
            lvDirectories.ItemsSource = null;
            lvDirectories.ItemsSource = gameDirectories;
        }

        private class GameDirectory
        {
            private static PathGeometry steam = new()
            {
                Figures = PathFigureCollection.Parse("M127.778579,0 C60.4203546,0 5.24030561,52.412282 0,119.013983 L68.7236558,147.68805 C74.5451924,143.665561 81.5845466,141.322185 89.1497766,141.322185 C89.8324924,141.322185 90.5059824,141.340637 91.1702465,141.377541 L121.735621,96.668877 L121.735621,96.0415165 C121.735621,69.1388208 143.425688,47.2457835 170.088511,47.2457835 C196.751333,47.2457835 218.441401,69.1388208 218.441401,96.0415165 C218.441401,122.944212 196.751333,144.846475 170.088511,144.846475 C169.719475,144.846475 169.359666,144.83725 168.99063,144.828024 L125.398299,176.205276 C125.425977,176.786507 125.444428,177.367738 125.444428,177.939743 C125.444428,198.144443 109.160732,214.575753 89.1497766,214.575753 C71.5836817,214.575753 56.8868387,201.917832 53.5655182,185.163615 L4.40997549,164.654462 C19.6326942,218.967277 69.0834655,258.786219 127.778579,258.786219 C198.596511,258.786219 256,200.847629 256,129.393109 C256,57.9293643 198.596511,0 127.778579,0 Z M80.3519677,196.332478 L64.6033732,189.763644 C67.389592,195.63131 72.2239585,200.539484 78.6359521,203.233444 C92.4932392,209.064206 108.472481,202.430791 114.247888,188.435116 C117.043333,181.663313 117.061785,174.190342 114.294018,167.400086 C111.526251,160.609831 106.295171,155.31417 99.5879487,152.491048 C92.9176301,149.695603 85.7767911,149.797088 79.5031858,152.186594 L95.777656,158.976849 C105.999942,163.276114 110.834309,175.122157 106.571948,185.436702 C102.318812,195.751247 90.574254,200.631743 80.3519677,196.332478 Z M202.30901,96.0424391 C202.30901,78.1165345 187.85204,63.5211763 170.092201,63.5211763 C152.323137,63.5211763 137.866167,78.1165345 137.866167,96.0424391 C137.866167,113.968344 152.323137,128.554476 170.092201,128.554476 C187.85204,128.554476 202.30901,113.968344 202.30901,96.0424391 Z M145.938821,95.9870838 C145.938821,82.4988323 156.779242,71.5661525 170.138331,71.5661525 C183.506646,71.5661525 194.347066,82.4988323 194.347066,95.9870838 C194.347066,109.475335 183.506646,120.408015 170.138331,120.408015 C156.779242,120.408015 145.938821,109.475335 145.938821,95.9870838 Z")
            };
            private static Brush steamColor = new SolidColorBrush(Color.FromRgb(0x1A, 0x19, 0x18));

            private static PathGeometry oculus = new()
            {
                Figures = PathFigureCollection.Parse("m 2758.22,805.43 c -48.75,-33.559 -102.73,-53.75 -160.8,-63.18 -58.03,-9.422 -115.72,-7.57 -173.82,-7.57 -399.07,0 -798.13,0 -1197.2,0 -58.15,0 -115.88,-1.852 -173.96,7.59 -58.1,9.449 -112.108,29.691 -160.874,63.308 -97.671,67.344 -156.316,177.684 -156.257,296.582 0.062,118.86 58.785,229.1 156.472,296.36 48.75,33.55 102.727,53.75 160.789,63.18 58.04,9.41 115.72,7.57 173.83,7.57 399.07,0 798.13,0 1197.2,0 58.15,0 115.88,1.85 173.96,-7.59 58.1,-9.45 112.11,-29.69 160.87,-63.31 97.68,-67.35 156.32,-177.69 156.26,-296.58 -0.06,-118.86 -58.78,-229.099 -156.47,-296.36 z m 482.66,1148.97 c -128.56,103.17 -275.62,174.44 -435.68,212.93 -91.68,22.04 -183.36,31.83 -277.25,34.9 -69.52,2.27 -139.01,1.64 -208.53,1.64 -329.61,0 -659.23,0 -988.84,0 -69.54,0 -139.07,0.63 -208.61,-1.64 -93.95,-3.07 -185.681,-12.89 -277.4,-34.96 C 684.531,2128.75 537.5,2057.47 408.977,1954.29 150.484,1746.76 -0.03125,1433.46 0,1101.87 0.03125,770.289 150.598,457.031 409.121,249.551 537.676,146.371 684.738,75.1094 844.805,36.6211 936.473,14.582 1028.16,4.78906 1122.05,1.71875 c 69.52,-2.277344 139.01,-1.6367187 208.53,-1.6367187 329.61,0 659.23,0 988.84,0 69.54,0 139.07,-0.6406253 208.61,1.6367187 93.95,3.07031 185.68,12.88285 277.4,34.96095 160.04,38.5117 307.07,109.8003 435.59,212.9803 258.49,207.531 409.01,520.82 408.98,852.42 -0.03,331.58 -150.6,644.84 -409.12,852.32")
            };
            private static Brush oculusColor = new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x20));

            public string Path { get; set; }
            public GameDirectoryType Type { get; set; }

            [JsonIgnore]
            public PathGeometry IconPath => Type switch
            {
                GameDirectoryType.Steam => steam,
                GameDirectoryType.Oculus => oculus,
                _ => null
            };
            [JsonIgnore]
            public Brush IconColor => Type switch
            {
                GameDirectoryType.Steam => steamColor,
                GameDirectoryType.Oculus => oculusColor,
                _ => null
            };
            [JsonIgnore]
            public bool IsSelected
            {
                get => this.Path == _targetDirectory;
                set
                {
                    if (!value) return;
                    Properties.Settings.Default.selectedGameDirectory = gameDirectories.IndexOf(this);
                    Properties.Settings.Default.Save();
                    instance.RefreshSelectedInstallDirectory();
                }
            }
            [JsonIgnore]
            public Brush TextColor => IsGameDirectory(Path) ? Brushes.Black : Brushes.Red;
        }

        private enum GameDirectoryType
        {
            Other = -1,
            Steam,
            Oculus,
        }
        #endregion

        #region Tech
        public bool TryGetModSettings(out MainModsModel modsModelValue)
        {
            modsModelValue = default;

            if (Directory.Exists(_targetDirectory) && File.Exists(ConfigFile))
            {
                try
                {
                    var stringFile = File.ReadAllText(ConfigFile);
                    if (string.IsNullOrEmpty(stringFile))
                        return false;

                    modsModelValue = JsonConvert
                         .DeserializeObject<MainModsModel>(stringFile);

                    return modsModelValue != null;
                }
                catch (Exception ex)
                {
                    App.ShowExceptionMessage(ex);
                }
            }

            return false;
        }

        public bool SaveChangedModsConfig()
        {
            if (Directory.Exists(_targetDirectory))
            {
                try
                {
                    File.WriteAllText(ConfigFile, JsonConvert
                        .SerializeObject(_modsModel, Formatting.Indented));

                    _isModConfigSaved = true;

                    return true;
                }
                catch (Exception ex)
                {
                    App.ShowExceptionMessage(ex);
                }
            }

            return false;
        }

        private async Task<Version> GetLatestVersion()
        {
            try
            {
                var remote = await NetworkUtils.GetFile(_zenithLatestVersionUrl);
                var remoteVersion = new Version(remote);

                return remoteVersion;
            }
            catch { return null; }
        }

        private async Task<bool> GetChangeLogs()
        {
            try
            {
                // Get local launcher changeLogs
                _changeLogsModel = new ChangeLogsModel(_launcherChangeLogsResourcePath);

                // Get external mods changeLogs
                var remoteChangeLogs = await NetworkUtils.GetFile(_zenithChangeLogsUrl);

                _changeLogsModel.ModsVersions = JsonConvert
                    .DeserializeObject<List<ChangeLogVersion>>(remoteChangeLogs);

                // Check is new added changes (badge)
                if (!File.Exists(ChangeLogsFile) || remoteChangeLogs != File.ReadAllText(ChangeLogsFile))
                    badgeChangeLogs.Visibility = Visibility.Visible;

                File.WriteAllText(ChangeLogsFile, remoteChangeLogs);

                return true;
            }
            catch { return false; }
        }

        private string ConfigFile => Path.Combine(_targetDirectory, "vtzenith.json");

        private string ChangeLogsFile => Path.Combine(_targetDirectory, "Mods", "vtzenithChangeLogs.json");
        #endregion

        #region Extensions
        public static bool IsGameDirectory(string endDirectory)
        {
            return Directory.Exists(endDirectory)
                && File.Exists($"{endDirectory}\\UnityClient@Windows.exe");
        }

        public static bool IsInstalled(string endDirectory)
        {
            return Directory.Exists(endDirectory + "\\AutoTranslator")
                || File.Exists(endDirectory + "\\version.dll");
        }

        public static bool HasModConfig(string endDirectory)
        {
            return Directory.Exists(endDirectory)
                && File.Exists($"{endDirectory}\\vtzenith.json");
        }

        #endregion
    }
}
