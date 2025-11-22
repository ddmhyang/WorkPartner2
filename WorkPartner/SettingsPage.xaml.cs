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

        public void LoadSettings()
        {
            _isLoaded = false;

            // 1. 설정 파일 불러오기
            Settings = DataManager.LoadSettings();

            // 2. 강제로 설정값 고정
            Settings.IsMiniTimerEnabled = true;
            Settings.MiniTimerShowInfo = true;
            Settings.MiniTimerShowCharacter = true;
            Settings.MiniTimerShowBackground = false;

            // 3. 변경된 설정 저장
            DataManager.SaveSettings(Settings);

            // 4. 메인 윈도우에 타이머 켜기 요청
            _mainWindow?.ToggleMiniTimer(true);

            // 5. [중요] 화면과 코드를 연결 (이게 없으면 목록이 안 보입니다!)
            this.DataContext = this;

            // 6. 데이터 목록 불러오기
            UpdateUIFromSettings();
            PopulateTagRules();
            _ = PopulateProcessViewModelsAsync(); // 작업/수동/방해 앱 목록 로드

            _isLoaded = true;
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
        private void UpdateUIFromSettings() // [메서드명 확인됨]
        {
            // [삭제됨] MiniTimerCheckBox.IsChecked = ... (변수가 XAML에서 사라졌으므로 삭제)
            // [삭제됨] MiniTimerShowInfoCheckBox.IsChecked = ...
            // [삭제됨] MiniTimerShowCharacterCheckBox.IsChecked = ...

            // [유지됨] 집중 모드(IdleDetectionCheckBox)는 XAML에 남아있으므로 유지
            IdleDetectionCheckBox.IsChecked = Settings.IsIdleDetectionEnabled;

            // [유지됨] 테마 설정 로직
            if (Settings.Theme == "Dark")
                DarkModeRadioButton.IsChecked = true;
            else
                LightModeRadioButton.IsChecked = true;

            // [유지됨] 강조 색상 로직
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

        private void Setting_Changed(object sender, RoutedEventArgs e) // [이벤트 핸들러 확인됨]
        {
            if (!_isLoaded) return;

            // [수정] 미니 타이머 관련 설정은 항상 true로 고정하여 저장
            Settings.IsMiniTimerEnabled = true;
            Settings.MiniTimerShowInfo = true;
            Settings.MiniTimerShowCharacter = true;
            Settings.MiniTimerShowBackground = false;

            // [유지됨] 집중 모드 설정값 읽기 (IdleDetectionCheckBox는 존재함)
            bool isFocusModeEnabled = IdleDetectionCheckBox.IsChecked ?? false;
            Settings.IsIdleDetectionEnabled = isFocusModeEnabled;
            Settings.IsFocusModeEnabled = isFocusModeEnabled;

            // 설정 저장
            SaveSettings();

            // 메인 윈도우에 반영 (항상 true 전달)
            _mainWindow?.ToggleMiniTimer(true);
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

        // SettingsPage.xaml.cs 내부의 Popup_SelectAppButton_Click 메서드를 교체하세요

        private async void Popup_SelectAppButton_Click(object sender, RoutedEventArgs e)
        {
            AddProcessMethodPopup.IsOpen = false; // 팝업 닫기

            // 1. 실행 중인 프로세스 목록 만들기
            var appList = new List<InstalledProgram>();
            var addedProcesses = new HashSet<string>(); // 중복 방지용

            // 비동기로 처리하여 UI 멈춤 방지
            await Task.Run(() =>
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        // 창 타이틀이 있는(눈에 보이는) 앱만 가져오기
                        if (proc.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                            continue;

                        string processName = proc.ProcessName.ToLower();
                        if (addedProcesses.Contains(processName)) continue;

                        // 아이콘 가져오기 (권한 에러 시 아이콘 없이 진행)
                        ImageSource iconSrc = null;
                        try
                        {
                            if (proc.MainModule != null)
                            {
                                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(proc.MainModule.FileName))
                                {
                                    if (icon != null)
                                    {
                                        // 아이콘을 UI 스레드에서 쓸 수 있게 Freeze
                                        iconSrc = icon.ToBitmapSource();
                                        iconSrc.Freeze();
                                    }
                                }
                            }
                        }
                        catch { /* 아이콘 로드 실패는 무시 */ }

                        appList.Add(new InstalledProgram
                        {
                            DisplayName = proc.MainWindowTitle,
                            ProcessName = processName,
                            Icon = iconSrc
                        });
                        addedProcesses.Add(processName);
                    }
                    catch { /* 프로세스 접근 권한 없음 등은 무시 */ }
                }
            });

            // 2. 목록을 정렬하여 창에 전달 (Tree/9 방식)
            var sortedList = appList.OrderBy(a => a.DisplayName).ToList();
            var selectionWindow = new AppSelectionWindow(sortedList) // [중요] 생성자로 목록 전달
            {
                Owner = Window.GetWindow(this)
            };

            // 3. 결과 받기
            if (selectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(selectionWindow.SelectedAppKeyword))
            {
                await AddProcessInternalAsync(_currentProcessType, selectionWindow.SelectedAppKeyword);
            }
        }

        private void ListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((Control)sender).Parent as UIElement;
                parent.RaiseEvent(eventArg);
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
                    // ✨ [핵심 수정] 백업하기 직전에 현재 설정을 강제로 한 번 저장합니다.
                    // (방금 바꾼 설정이 파일에 아직 안 쓰여졌을 수도 있기 때문)
                    DataManager.SaveSettings(Settings);

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
            if (MessageBox.Show("정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n타임테이블, 태그, 프로세스 설정 등 모든 내용이 사라집니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    // ✨ [핵심 수정 1] 이제부터 그 어떤 데이터 저장도 하지 말라고 깃발을 듭니다.
                    // (프로그램 종료 시 발생하는 자동 저장까지 모두 막아버립니다.)
                    DataManager.IsResetting = true;

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
                    
                    MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램이 종료됩니다.", "초기화 완료");
                    
                    // ✨ [핵심 수정 2] 즉시 종료
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    // 만약 오류가 나면 다시 저장이 가능하도록 복구해줘야 함
                    DataManager.IsResetting = false; 
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