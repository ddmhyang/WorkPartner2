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
        private string _currentProcessType; // [추가] 팝업에서 어떤 종류의 프로세스를 추가할지 저장

        public ObservableCollection<ProcessViewModel> WorkProcessViewModels { get; set; }
        public ObservableCollection<ProcessViewModel> PassiveProcessViewModels { get; set; }
        public ObservableCollection<ProcessViewModel> DistractionProcessViewModels { get; set; }

        public SettingsPage()
        {
            InitializeComponent();
            WorkProcessViewModels = new ObservableCollection<ProcessViewModel>();
            PassiveProcessViewModels = new ObservableCollection<ProcessViewModel>();
            DistractionProcessViewModels = new ObservableCollection<ProcessViewModel>();
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
            PopulateTagRules(); // ✨ 이 줄을 추가하세요.
            _isLoaded = true;
        }

        // 🎯 수정 후
        private void LoadSettings()
        {
            Settings = DataManager.LoadSettings();

            if (Settings.IsMiniTimerEnabled == false &&
                Settings.MiniTimerShowInfo == false &&
                Settings.MiniTimerShowCharacter == false &&
                Settings.MiniTimerShowBackground == false)
            {
                Settings.MiniTimerShowInfo = true;
                Settings.MiniTimerShowCharacter = true;
                Settings.MiniTimerShowBackground = true;
            }
            // ✨ [수정 종료]

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
            CharacterPreview.UpdateCharacter();

            // ✨ 미리보기 UI 업데이트
            UsernameTextBlock.Text = Settings.Username;
            LevelTextBlock.Text = $"Lv.{Settings.Level}";
            ExperienceBar.Value = Settings.Experience;
            CurrentTaskTextBlock.Text = $"현재 작업 : {Settings.CurrentTask}";
            CoinTextBlock.Text = Settings.Coins.ToString("N0");
            UsernameTextBox.Text = Settings.Username;

            if (Settings.Theme == "Dark")
                DarkModeRadioButton.IsChecked = true;
            else
                LightModeRadioButton.IsChecked = true;

            // ✨ 미니 타이머 설정 UI 업데이트
            MiniTimerShowInfoCheckBox.IsChecked = Settings.MiniTimerShowInfo;
            MiniTimerShowCharacterCheckBox.IsChecked = Settings.MiniTimerShowCharacter;
            MiniTimerShowBackgroundCheckBox.IsChecked = Settings.MiniTimerShowBackground;

            // ✨ 테마 색상 라디오버튼 상태 업데이트
            var colorButton = FindName($"Color{Settings.AccentColor.Replace("#", "")}") as RadioButton;
            if (colorButton != null)
            {
                colorButton.IsChecked = true;
            }
        }
        #endregion

        #region Event Handlers
        private void GoToAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.NavigateToPage("Avatar");
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Settings.Username = UsernameTextBox.Text;
            SaveSettings();
            UpdateUIFromSettings();
        }

        // ✨ 테마 색상 변경 이벤트 핸들러
        private void AccentColor_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoaded && sender is RadioButton rb && rb.IsChecked == true)
            {
                Settings.AccentColor = rb.Tag.ToString();
                SaveSettings();
            }
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                Settings.Theme = rb.Name == "DarkModeRadioButton" ? "Dark" : "Light";
                SaveSettings();
            }
        }

        private void Setting_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isLoaded) SaveSettings();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            // --- ✨ 체크박스 상태 읽기 ---

            // 1. 미니 타이머
            Settings.IsMiniTimerEnabled = MiniTimerCheckBox.IsChecked ?? false;
            Settings.MiniTimerShowInfo = MiniTimerShowInfoCheckBox.IsChecked ?? false;
            Settings.MiniTimerShowCharacter = MiniTimerShowCharacterCheckBox.IsChecked ?? false;
            Settings.MiniTimerShowBackground = MiniTimerShowBackgroundCheckBox.IsChecked ?? false;

            // 2. ✨ [통합 수정] '집중 모드 활성화' 체크박스의 상태를 읽어옵니다.
            bool isFocusModeEnabled = IdleDetectionCheckBox.IsChecked ?? false;

            // 3. ✨ [통합 수정] '자리 비움 감지'와 '방해 앱 경고' 설정을 '동일한 값'으로 설정합니다.
            Settings.IsIdleDetectionEnabled = isFocusModeEnabled;
            Settings.IsFocusModeEnabled = isFocusModeEnabled;

            // --- 설정 저장 및 전파 ---
            SaveSettings(); // 👈 모든 변경 사항을 파일에 저장

            // (기존) 미니 타이머 토글 로직
            if (sender == MiniTimerCheckBox)
            {
                _mainWindow?.ToggleMiniTimer(Settings.IsMiniTimerEnabled);
            }
        }

        // 🎯 [수정 4] SettingsPage.xaml.cs (Setting_Changed 메서드 다음)

        private void Setting_Changed_LostFocus(object sender, RoutedEventArgs e)
        {
            // 이 이벤트는 Text가 아닌, 포커스를 잃었을 때 발생합니다.
            // 바인딩(Binding)이 TwoWay, UpdateSourceTrigger=LostFocus로 설정되어 있으므로
            // Settings 객체는 이미 업데이트되었습니다.
            // 우리는 저장만 호출해주면 됩니다.
            if (_isLoaded)
            {
                SaveSettings();
            }
        }
        #endregion

        #region AI Tag Rules

        private void PopulateTagRules()
        {
            // Dictionary를 ListBox에 바인딩하기 위해 KeyValuePair 리스트로 변환
            TagRulesListBox.ItemsSource = Settings.TagRules.ToList();
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

            // 이미 키워드가 존재하면 덮어쓰기
            Settings.TagRules[keyword] = task;
            SaveSettings();
            PopulateTagRules(); // 목록 새로고침

            // 입력 필드 초기화
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
                    PopulateTagRules(); // 목록 새로고침
                }
            }
        }

        #endregion

        #region Process Settings

        // 🎯 [수정 2] WorkPartner/SettingsPage.xaml.cs
        // (약 222줄 근처의 PopulateProcessViewModelsAsync 메서드를 교체하세요)
        // (GetAwaiter 오류 수정)

        private async Task PopulateProcessViewModelsAsync()
        {
            WorkProcessViewModels.Clear();
            var workVms = new List<ProcessViewModel>();
            foreach (var p in Settings.WorkProcesses)
            {
                var vm = ProcessViewModel.Create(p); // 1. 동기 생성 (await 제거)
                WorkProcessViewModels.Add(vm);        // 2. UI에 즉시 추가
                workVms.Add(vm);                      // 3. 아이콘 로드 목록에 추가
            }

            PassiveProcessViewModels.Clear();
            var passiveVms = new List<ProcessViewModel>();
            foreach (var p in Settings.PassiveProcesses)
            {
                var vm = ProcessViewModel.Create(p); // 1. 동기 생성 (await 제거)
                PassiveProcessViewModels.Add(vm);
                passiveVms.Add(vm);
            }

            DistractionProcessViewModels.Clear();
            var distractionVms = new List<ProcessViewModel>();
            foreach (var p in Settings.DistractionProcesses)
            {
                var vm = ProcessViewModel.Create(p); // 1. 동기 생성 (await 제거)
                DistractionProcessViewModels.Add(vm);
                distractionVms.Add(vm);
            }

            // 4. UI가 업데이트된 후, 아이콘을 비동기로 일괄 로드
            await Task.WhenAll(workVms.Select(vm => vm.LoadIconAsync()));
            await Task.WhenAll(passiveVms.Select(vm => vm.LoadIconAsync()));
            await Task.WhenAll(distractionVms.Select(vm => vm.LoadIconAsync()));
        }

        // [수정] '+' 버튼은 이제 팝업을 띄우는 역할만 합니다.
        private void AddProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string type)
            {
                _currentProcessType = type; // 어떤 종류(+작업, +수동, +방해)인지 저장
                AddProcessMethodPopup.PlacementTarget = button;
                AddProcessMethodPopup.IsOpen = true;
            }
        }

        // [추가] 팝업의 "실행 중인 앱에서 선택" 버튼 클릭 이벤트
        private async void Popup_SelectAppButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessMethodPopup.IsOpen = false; // 팝업 닫기

            var runningProcesses = Process.GetProcesses()
                                          .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero)
                                          .ToList();
            var appList = new List<InstalledProgram>();
            var addedProcesses = new HashSet<string>();

            foreach (var proc in runningProcesses)
            {
                try
                {
                    string processName = proc.ProcessName.ToLower();
                    if (addedProcesses.Contains(processName)) continue;
                    Icon icon = Icon.ExtractAssociatedIcon(proc.MainModule.FileName);
                    appList.Add(new InstalledProgram
                    {
                        DisplayName = proc.MainWindowTitle,
                        ProcessName = processName,
                        Icon = icon?.ToBitmapSource()
                    });
                    addedProcesses.Add(processName);
                }
                catch { /* Access denied */ }
            }

            var selectionWindow = new AppSelectionWindow(appList.OrderBy(a => a.DisplayName).ToList())
            {
                Owner = Window.GetWindow(this)
            };

            if (selectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(selectionWindow.SelectedAppKeyword))
            {
                await AddProcessInternalAsync(_currentProcessType, selectionWindow.SelectedAppKeyword);
            }
        }

        // [추가] 팝업의 "활성 브라우저 탭 추가" 버튼 클릭 이벤트
        private async void Popup_AddActiveTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessMethodPopup.IsOpen = false; // 팝업 닫기

            Mouse.OverrideCursor = Cursors.Wait;
            await Task.Delay(3000); // 3초 대기

            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
            Mouse.OverrideCursor = null;

            if (string.IsNullOrEmpty(activeUrl))
            {
                MessageBox.Show("웹 브라우저의 주소를 가져오지 못했습니다. (지원 브라우저: Chrome, Edge, Firefox, Whale 등)", "오류");
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

        private void DeleteProcess_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && sender is ListBox listBox && listBox.SelectedItem is ProcessViewModel selectedVm)
            {
                DeleteProcess(listBox.Tag as string, selectedVm);
            }
        }

        private void DeleteProcess(string type, ProcessViewModel selectedVm)
        {
            ObservableCollection<string> targetList = null;
            ObservableCollection<ProcessViewModel> targetViewModelList = null;

            if (type == "Work") { targetList = Settings.WorkProcesses; targetViewModelList = WorkProcessViewModels; }
            else if (type == "Passive") { targetList = Settings.PassiveProcesses; targetViewModelList = PassiveProcessViewModels; }
            else if (type == "Distraction") { targetList = Settings.DistractionProcesses; targetViewModelList = DistractionProcessViewModels; }

            if (targetList != null && targetViewModelList != null)
            {
                targetList.Remove(selectedVm.DisplayName);
                targetViewModelList.Remove(selectedVm);

                // ✨ [수정] 삭제 후 SaveSettings()를 호출하여 변경 사항을 즉시 전파합니다.
                SaveSettings();
            }
        }

        private void DeleteProcessMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 클릭된 메뉴 아이템과 연결된 프로그램 데이터를 가져옵니다.
            if (sender is MenuItem menuItem && menuItem.CommandParameter is ProcessViewModel selectedVm)
            {
                // 어떤 목록(작업/수동/방해)에 속해있는지 확인하고 삭제를 실행합니다.
                if (WorkProcessViewModels.Contains(selectedVm))
                {
                    DeleteProcess("Work", selectedVm);
                }
                else if (PassiveProcessViewModels.Contains(selectedVm))
                {
                    DeleteProcess("Passive", selectedVm);
                }
                else if (DistractionProcessViewModels.Contains(selectedVm))
                {
                    DeleteProcess("Distraction", selectedVm);
                }
            }
        }

        // 🎯 [수정 3] WorkPartner/SettingsPage.xaml.cs
        // (약 436줄 근처의 AddProcessInternalAsync 메서드를 교체하세요)

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
                    // ✨ [버그 1 수정] 로직 순서 변경 (저장/UI 업데이트를 먼저 수행)

                    // 1. 설정에 즉시 추가
                    targetList.Add(processName.ToLower());

                    // 2. 즉시 저장 (TimerService가 알 수 있도록)
                    SaveSettings();

                    // 3. 아이콘이 없는 VM을 동기 생성 (await 제거)
                    var vm = ProcessViewModel.Create(processName.ToLower());

                    // 4. UI에 즉시 추가 (리스트에 반영)
                    targetViewModelList.Add(vm);

                    // 5. 아이콘 로드를 "후발주자"로 실행 (완료되면 Icon 속성이 업데이트됨)
                    _ = vm.LoadIconAsync();
                }
            }
        }
        #endregion

        #region Data Management
        // ... (데이터 관리 부분은 수정 없이 그대로 유지됩니다)
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
                        DataManager.ModelFilePath
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

    // 🎯 [수정 4] WorkPartner/SettingsPage.xaml.cs
    // (파일 맨 아래 'ProcessViewModel' 클래스 전체를 교체하세요)

    public class ProcessViewModel : INotifyPropertyChanged
    {
        public string DisplayName { get; set; }
        private ImageSource _icon;
        public ImageSource Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        // ✨ [수정] Create 메서드를 동기(Sync)로 변경 (GetAwaiter 오류 수정)
        public static ProcessViewModel Create(string identifier)
        {
            var vm = new ProcessViewModel { DisplayName = identifier };
            return vm;
        }

        // ✨ [추가] 아이콘을 비동기(Async)로 로드하는 메서드
        public async Task LoadIconAsync()
        {
            Icon = await GetIconForIdentifier(DisplayName);
        }

        // ✨ [수정] public static으로 변경 (AddProcessInternalAsync에서도 호출)
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
                        // ✨ [수정] URL 하드코딩 오류 수정 (https://)
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