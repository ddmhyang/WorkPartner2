using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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

namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private MainWindow _mainWindow;
        public AppSettings Settings { get; private set; }
        private bool _isLoaded;

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
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            Settings = DataManager.LoadSettings();
            this.DataContext = this;
            _ = PopulateProcessViewModelsAsync();
        }

        private void SaveSettings()
        {
            if (!_isLoaded) return;
            DataManager.SaveSettingsAndNotify(Settings);
            if (Application.Current is App app)
            {
                app.ApplyTheme(Settings);
            }
        }

        #region UI Update
        private void UpdateUIFromSettings()
        {
            CharacterPreview.UpdateCharacter();
            UsernameTextBlock.Text = Settings.Username;
            CoinTextBlock.Text = Settings.Coins.ToString("N0");
            UsernameTextBox.Text = Settings.Username;

            if (Settings.Theme == "Dark")
                DarkModeRadioButton.IsChecked = true;
            else
                LightModeRadioButton.IsChecked = true;
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

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

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
            if (_isLoaded) SaveSettings();
            if (sender == MiniTimerCheckBox)
            {
                _mainWindow?.ToggleMiniTimer();
            }
        }
        #endregion

        #region Process Settings

        private async Task PopulateProcessViewModelsAsync()
        {
            WorkProcessViewModels.Clear();
            foreach (var p in Settings.WorkProcesses) { WorkProcessViewModels.Add(await ProcessViewModel.Create(p)); }

            PassiveProcessViewModels.Clear();
            foreach (var p in Settings.PassiveProcesses) { PassiveProcessViewModels.Add(await ProcessViewModel.Create(p)); }

            DistractionProcessViewModels.Clear();
            foreach (var p in Settings.DistractionProcesses) { DistractionProcessViewModels.Add(await ProcessViewModel.Create(p)); }
        }

        private async void AddProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string type)
            {
                TextBox inputTextBox = null;
                ObservableCollection<string> targetList = null;
                ObservableCollection<ProcessViewModel> targetViewModelList = null;

                if (type == "Work") { inputTextBox = WorkProcessInput; targetList = Settings.WorkProcesses; targetViewModelList = WorkProcessViewModels; }
                else if (type == "Passive") { inputTextBox = PassiveProcessInput; targetList = Settings.PassiveProcesses; targetViewModelList = PassiveProcessViewModels; }
                else if (type == "Distraction") { inputTextBox = DistractionProcessInput; targetList = Settings.DistractionProcesses; targetViewModelList = DistractionProcessViewModels; }

                if (inputTextBox != null && targetList != null)
                {
                    string process = inputTextBox.Text.Trim().ToLower();
                    if (!string.IsNullOrEmpty(process) && !targetList.Contains(process))
                    {
                        targetList.Add(process);
                        targetViewModelList.Add(await ProcessViewModel.Create(process));
                        inputTextBox.Clear();
                        SaveSettings();
                    }
                }
            }
        }

        // [추가] 키보드 Delete 키로 프로세스를 삭제하는 이벤트 핸들러
        private void DeleteProcess_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && sender is ListBox listBox && listBox.SelectedItem is ProcessViewModel selectedVm)
            {
                string type = listBox.Tag as string;
                ObservableCollection<string> targetList = null;
                ObservableCollection<ProcessViewModel> targetViewModelList = null;

                if (type == "Work") { targetList = Settings.WorkProcesses; targetViewModelList = WorkProcessViewModels; }
                else if (type == "Passive") { targetList = Settings.PassiveProcesses; targetViewModelList = PassiveProcessViewModels; }
                else if (type == "Distraction") { targetList = Settings.DistractionProcesses; targetViewModelList = DistractionProcessViewModels; }

                if (targetList != null && targetViewModelList != null)
                {
                    targetList.Remove(selectedVm.DisplayName);
                    targetViewModelList.Remove(selectedVm);
                    SaveSettings();
                }
            }
        }

        private void SelectAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string type)
            {
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

                        Icon icon = null;
                        string path = proc.MainModule.FileName;
                        if (!string.IsNullOrEmpty(path))
                        {
                            icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                        }

                        appList.Add(new InstalledProgram
                        {
                            DisplayName = proc.MainWindowTitle,
                            ProcessName = processName,
                            Icon = icon?.ToBitmapSource()
                        });
                        addedProcesses.Add(processName);
                    }
                    catch { /* Access denied to process will be ignored */ }
                }

                var selectionWindow = new AppSelectionWindow(appList.OrderBy(a => a.DisplayName).ToList()) { Owner = Window.GetWindow(this) };
                if (selectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(selectionWindow.SelectedAppKeyword))
                {
                    TextBox inputTextBox = null;
                    if (type == "Work") inputTextBox = WorkProcessInput;
                    else if (type == "Passive") inputTextBox = PassiveProcessInput;
                    else if (type == "Distraction") inputTextBox = DistractionProcessInput;

                    if (inputTextBox != null)
                        inputTextBox.Text = selectionWindow.SelectedAppKeyword;
                }
            }
        }

        private void AddActiveTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string type)
            {
                string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
                if (string.IsNullOrEmpty(activeUrl))
                {
                    MessageBox.Show("웹 브라우저의 주소를 가져오지 못했습니다. (지원 브라우저: Chrome, Edge, Firefox, Whale 등)", "오류");
                    return;
                }
                try
                {
                    string urlKeyword;
                    if (Uri.IsWellFormedUriString(activeUrl, UriKind.Absolute))
                    {
                        urlKeyword = new Uri(activeUrl).Host.ToLower();
                    }
                    else
                    {
                        urlKeyword = activeUrl.ToLower();
                    }

                    TextBox inputTextBox = null;
                    if (type == "Work") inputTextBox = WorkProcessInput;
                    else if (type == "Passive") inputTextBox = PassiveProcessInput;
                    else if (type == "Distraction") inputTextBox = DistractionProcessInput;

                    if (inputTextBox != null)
                        inputTextBox.Text = urlKeyword;
                }
                catch
                {
                    MessageBox.Show($"유효한 URL 또는 제목이 아닙니다: {activeUrl}", "오류");
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

    public class ProcessViewModel : INotifyPropertyChanged
    {
        public string DisplayName { get; set; }
        private ImageSource _icon;
        public ImageSource Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(nameof(Icon)); }
        }

        public static async Task<ProcessViewModel> Create(string identifier)
        {
            var vm = new ProcessViewModel { DisplayName = identifier };
            vm.Icon = await GetIconForIdentifier(identifier);
            return vm;
        }

        private static async Task<BitmapSource> GetIconForIdentifier(string identifier)
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

