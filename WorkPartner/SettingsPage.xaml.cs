using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Globalization;
using System.IO.Compression;

namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private MainWindow _mainWindow;
        public AppSettings Settings { get; private set; }
        private bool _isLoaded;
        private string _currentProcessType;

        public ObservableCollection<ProcessViewModel> WorkProcessViewModels { get; set; }
        public ObservableCollection<ProcessViewModel> PassiveProcessViewModels { get; set; }
        public ObservableCollection<ProcessViewModel> DistractionProcessViewModels { get; set; }
        public ObservableCollection<TagRuleViewModel> TagRuleViewModels { get; set; }

        public SettingsPage()
        {
            InitializeComponent();
            WorkProcessViewModels = new ObservableCollection<ProcessViewModel>();
            PassiveProcessViewModels = new ObservableCollection<ProcessViewModel>();
            DistractionProcessViewModels = new ObservableCollection<ProcessViewModel>();
            TagRuleViewModels = new ObservableCollection<TagRuleViewModel>();
        }

        public void SetParentWindow(MainWindow window)
        {
            _mainWindow = window;
        }

        public void LoadData()
        {
            _isLoaded = false;
            LoadSettings();
            UpdateUIFromSettings();
            PopulateTagRules();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            Settings = DataManager.LoadSettings();

            if (Settings.IsMiniTimerEnabled == false &&
                Settings.MiniTimerShowInfo == false &&
                Settings.MiniTimerShowCharacter == false)
            {
                Settings.MiniTimerShowInfo = true;
                Settings.MiniTimerShowCharacter = true;
                Settings.MiniTimerShowBackground = false;
            }

            this.DataContext = this;
            _ = PopulateProcessViewModelsAsync();
        }

        private void SaveSettings()
        {
            if (!_isLoaded) return;
            DataManager.SaveSettings(Settings);
            if (Application.Current is App app)
            {
                app.ApplyTheme(Settings);
            }
        }

        #region UI Update
        private void UpdateUIFromSettings()
        {
            MiniTimerCheckBox.IsChecked = Settings.IsMiniTimerEnabled;
            MiniTimerShowInfoCheckBox.IsChecked = Settings.MiniTimerShowInfo;
            MiniTimerShowCharacterCheckBox.IsChecked = Settings.MiniTimerShowCharacter;

            IdleDetectionCheckBox.IsChecked = Settings.IsIdleDetectionEnabled;

            if (Settings.Theme == "Dark")
                DarkModeRadioButton.IsChecked = true;
            else
                LightModeRadioButton.IsChecked = true;

            var colorButton = FindName($"Color{Settings.AccentColor.Replace("#", "")}") as RadioButton;
            if (colorButton != null)
            {
                colorButton.IsChecked = true;
            }
        }
        #endregion

        #region Event Handlers

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                Settings.Theme = rb.Name == "DarkModeRadioButton" ? "Dark" : "Light";
                SaveSettings();
            }
        }

        private void AccentColor_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoaded && sender is RadioButton rb && rb.IsChecked == true)
            {
                Settings.AccentColor = rb.Tag.ToString();
                SaveSettings();
            }
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            Settings.IsMiniTimerEnabled = MiniTimerCheckBox.IsChecked ?? false;
            Settings.MiniTimerShowInfo = MiniTimerShowInfoCheckBox.IsChecked ?? false;
            Settings.MiniTimerShowCharacter = MiniTimerShowCharacterCheckBox.IsChecked ?? false;
            Settings.MiniTimerShowBackground = false;

            bool isFocusModeEnabled = IdleDetectionCheckBox.IsChecked ?? false;
            Settings.IsIdleDetectionEnabled = isFocusModeEnabled;
            Settings.IsFocusModeEnabled = isFocusModeEnabled;

            SaveSettings();
            _mainWindow?.ToggleMiniTimer(Settings.IsMiniTimerEnabled);
        }
        #endregion

        #region AI Tag Rules

        private void PopulateTagRules()
        {
            TagRuleViewModels.Clear();
            if (Settings.TagRules != null)
            {
                foreach (var rule in Settings.TagRules)
                {
                    TagRuleViewModels.Add(new TagRuleViewModel { Keyword = rule.Key, Subject = rule.Value });
                }
            }
            TagRulesListBox.ItemsSource = TagRuleViewModels;
        }

        private void AddTagRule_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TagRuleKeywordTextBox.Text.Trim();
            string task = TagRuleTaskTextBox.Text.Trim();

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(task))
            {
                MessageBox.Show("키워드와 과목을 모두 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Settings.TagRules.ContainsKey(keyword))
            {
                MessageBox.Show("이미 등록된 키워드입니다.");
                return;
            }

            Settings.TagRules[keyword] = task;
            SaveSettings();
            PopulateTagRules();

            TagRuleKeywordTextBox.Clear();
            TagRuleTaskTextBox.Clear();
        }

        private void DeleteTagRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string keyword)
            {
                if (Settings.TagRules.ContainsKey(keyword))
                {
                    Settings.TagRules.Remove(keyword);
                    SaveSettings();
                    PopulateTagRules();
                }
            }
        }

        #endregion

        #region Process Settings

        private async Task PopulateProcessViewModelsAsync()
        {
            WorkProcessViewModels.Clear();
            var workVms = new List<ProcessViewModel>();
            foreach (var p in Settings.WorkProcesses)
            {
                var vm = ProcessViewModel.Create(p);
                WorkProcessViewModels.Add(vm);
                workVms.Add(vm);
            }

            PassiveProcessViewModels.Clear();
            var passiveVms = new List<ProcessViewModel>();
            foreach (var p in Settings.PassiveProcesses)
            {
                var vm = ProcessViewModel.Create(p);
                PassiveProcessViewModels.Add(vm);
                passiveVms.Add(vm);
            }

            DistractionProcessViewModels.Clear();
            var distractionVms = new List<ProcessViewModel>();
            foreach (var p in Settings.DistractionProcesses)
            {
                var vm = ProcessViewModel.Create(p);
                DistractionProcessViewModels.Add(vm);
                distractionVms.Add(vm);
            }

            await Task.WhenAll(workVms.Select(vm => vm.LoadIconAsync()));
            await Task.WhenAll(passiveVms.Select(vm => vm.LoadIconAsync()));
            await Task.WhenAll(distractionVms.Select(vm => vm.LoadIconAsync()));
        }

        private void AddProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string type)
            {
                _currentProcessType = type;
                AddProcessMethodPopup.PlacementTarget = button;
                AddProcessMethodPopup.IsOpen = true;
            }
        }

        // ▼▼▼ [수정] 복잡한 로직 다 지우고, Branch 9처럼 단순하게 창만 띄움 ▼▼▼
        private async void Popup_SelectAppButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessMethodPopup.IsOpen = false;

            // 생성자에 목록을 넘기지 않음 -> 창이 알아서 로드함 (Branch 9 방식)
            var selectionWindow = new AppSelectionWindow()
            {
                Owner = Window.GetWindow(this)
            };

            if (selectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(selectionWindow.SelectedAppKeyword))
            {
                // 선택된 앱 이름을 받아서 추가
                await AddProcessInternalAsync(_currentProcessType, selectionWindow.SelectedAppKeyword);
            }
        }

        private async void Popup_AddActiveTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessMethodPopup.IsOpen = false;

            Mouse.OverrideCursor = Cursors.Wait;
            await Task.Delay(3000);

            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
            Mouse.OverrideCursor = null;

            if (string.IsNullOrEmpty(activeUrl))
            {
                MessageBox.Show("웹 브라우저의 주소를 가져오지 못했습니다.", "오류");
                return;
            }
            try
            {
                if (Uri.TryCreate(activeUrl, UriKind.Absolute, out Uri uriResult))
                {
                    var idn = new IdnMapping();
                    string domain = idn.GetAscii(uriResult.Host).ToLower();
                    await AddProcessInternalAsync(_currentProcessType, domain);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"주소 처리 중 오류: {ex.Message}", "오류");
            }
        }

        private void DeleteProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProcessViewModel vm)
            {
                if (WorkProcessViewModels.Contains(vm))
                {
                    Settings.WorkProcesses.Remove(vm.DisplayName);
                    WorkProcessViewModels.Remove(vm);
                }
                else if (PassiveProcessViewModels.Contains(vm))
                {
                    Settings.PassiveProcesses.Remove(vm.DisplayName);
                    PassiveProcessViewModels.Remove(vm);
                }
                else if (DistractionProcessViewModels.Contains(vm))
                {
                    Settings.DistractionProcesses.Remove(vm.DisplayName);
                    DistractionProcessViewModels.Remove(vm);
                }
                SaveSettings();
            }
        }

        private async Task AddProcessInternalAsync(string type, string processName)
        {
            ObservableCollection<string> targetList = null;
            ObservableCollection<ProcessViewModel> targetViewModelList = null;

            if (type == "Work") { targetList = Settings.WorkProcesses; targetViewModelList = WorkProcessViewModels; }
            else if (type == "Passive") { targetList = Settings.PassiveProcesses; targetViewModelList = PassiveProcessViewModels; }
            else if (type == "Distraction") { targetList = Settings.DistractionProcesses; targetViewModelList = DistractionProcessViewModels; }

            if (targetList != null && targetViewModelList != null && !string.IsNullOrEmpty(processName))
            {
                if (!targetList.Contains(processName.ToLower()))
                {
                    targetList.Add(processName.ToLower());
                    SaveSettings();

                    var vm = ProcessViewModel.Create(processName.ToLower());
                    targetViewModelList.Add(vm);
                    // 앱 추가 후에 아이콘을 로드함 (Branch 9 방식)
                    _ = vm.LoadIconAsync();
                }
            }
        }
        #endregion

        #region Data Management
        private void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "WorkPartner Backup (*.zip)|*.zip",
                FileName = $"WorkPartner_Backup_{DateTime.Now:yyyyMMdd_HHmm}.zip"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var filesToZip = new[]
                    {
                        DataManager.SettingsFilePath, DataManager.TimeLogFilePath,
                        DataManager.TasksFilePath, DataManager.TodosFilePath, DataManager.MemosFilePath
                    };

                    if (File.Exists(saveFileDialog.FileName)) File.Delete(saveFileDialog.FileName);

                    using (var zipArchive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Create))
                    {
                        foreach (var filePath in filesToZip)
                        {
                            if (File.Exists(filePath))
                            {
                                zipArchive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                            }
                        }
                    }
                    MessageBox.Show("데이터를 성공적으로 내보냈습니다.", "내보내기 완료");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 내보내기 중 오류: {ex.Message}");
                }
            }
        }

        private void ImportDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("데이터를 가져오면 현재 모든 데이터가 덮어씌워집니다. 계속하시겠습니까?", "가져오기 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "WorkPartner Backup (*.zip)|*.zip",
                Title = "백업 파일 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string dataDirectory = Path.GetDirectoryName(DataManager.SettingsFilePath);
                    if (!Directory.Exists(dataDirectory))
                    {
                        Directory.CreateDirectory(dataDirectory);
                    }

                    using (var archive = ZipFile.OpenRead(openFileDialog.FileName))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            var destinationPath = Path.Combine(dataDirectory, entry.FullName);
                            var directoryName = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(directoryName))
                            {
                                Directory.CreateDirectory(directoryName);
                            }
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }

                    MessageBox.Show("데이터를 성공적으로 가져왔습니다.\n프로그램을 다시 시작해야 변경 사항이 적용됩니다.", "가져오기 완료");
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 가져오기 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var filesToDelete = new string[]
                    {
                        DataManager.SettingsFilePath,
                        DataManager.TimeLogFilePath,
                        DataManager.TasksFilePath,
                        DataManager.TodosFilePath,
                        DataManager.MemosFilePath,
                    };

                    foreach (var filePath in filesToDelete)
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램을 다시 시작해주세요.", "초기화 완료");
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }
        #endregion
    }

    // ProcessViewModel 등은 동일하게 유지
    public class ProcessViewModel : INotifyPropertyChanged
    {
        public string DisplayName { get; set; }
        private ImageSource _icon;
        public ImageSource Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        public static ProcessViewModel Create(string identifier)
        {
            var vm = new ProcessViewModel { DisplayName = identifier };
            return vm;
        }

        public async Task LoadIconAsync()
        {
            Icon = await GetIconForIdentifier(DisplayName);
        }

        public static async Task<BitmapSource> GetIconForIdentifier(string identifier)
        {
            try
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(identifier));
                if (processes.Any() && processes[0].MainModule != null)
                {
                    string path = processes[0].MainModule.FileName;
                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        return icon?.ToBitmapSource();
                    }
                }
            }
            catch { }

            if (identifier.Contains("."))
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        var response = await client.GetAsync($"https://www.google.com/s2/favicons?sz=32&domain_url={identifier}");
                        if (response.IsSuccessStatusCode)
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            if (bytes.Length > 0)
                            {
                                var image = new BitmapImage();
                                using (var mem = new MemoryStream(bytes))
                                {
                                    mem.Position = 0;
                                    image.BeginInit();
                                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                    image.CacheOption = BitmapCacheOption.OnLoad;
                                    image.UriSource = null;
                                    image.StreamSource = mem;
                                    image.EndInit();
                                }
                                image.Freeze();
                                return image;
                            }
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TagRuleViewModel
    {
        public string Keyword { get; set; }
        public string Subject { get; set; }
    }

    public static class IconExtensions
    {
        public static BitmapSource ToBitmapSource(this Icon icon)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
               icon.Handle,
               Int32Rect.Empty,
               BitmapSizeOptions.FromEmptyOptions());
        }
    }
}