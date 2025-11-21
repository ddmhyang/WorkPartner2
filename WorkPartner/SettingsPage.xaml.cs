using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private MainWindow _mainWindow;
        private AppSettings _settings;
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
            _settings = DataManager.LoadSettings();

            if (ShowMiniCharCheck != null) ShowMiniCharCheck.IsChecked = _settings.MiniTimerShowCharacter;
            if (ShowMiniInfoCheck != null) ShowMiniInfoCheck.IsChecked = _settings.MiniTimerShowInfo;
            if (ShowMiniBgCheck != null) ShowMiniBgCheck.IsChecked = _settings.MiniTimerShowBackground;
            // 프로세스 로드
            LoadProcesses(_settings.WorkProcesses, WorkProcessViewModels);
            LoadProcesses(_settings.PassiveProcesses, PassiveProcessViewModels);
            LoadProcesses(_settings.DistractionProcesses, DistractionProcessViewModels);

            // 태그 규칙 로드
            TagRuleViewModels.Clear();
            if (_settings.TagRules != null)
            {
                foreach (var rule in _settings.TagRules)
                {
                    TagRuleViewModels.Add(new TagRuleViewModel { Keyword = rule.Key, Subject = rule.Value });
                }
            }

            // UI 연결
            if (WorkProcessList != null) WorkProcessList.ItemsSource = WorkProcessViewModels;
            if (PassiveProcessList != null) PassiveProcessList.ItemsSource = PassiveProcessViewModels;
            if (DistractionProcessList != null) DistractionProcessList.ItemsSource = DistractionProcessViewModels;
            if (TagRuleList != null) TagRuleList.ItemsSource = TagRuleViewModels;
        }

        private void LoadProcesses(ObservableCollection<string> source, ObservableCollection<ProcessViewModel> target)
        {
            target.Clear();
            foreach (var p in source) target.Add(new ProcessViewModel(p));
        }

        private void SaveAllSettings()
        {
            _settings.WorkProcesses = new ObservableCollection<string>(WorkProcessViewModels.Select(vm => vm.ProcessName));
            _settings.PassiveProcesses = new ObservableCollection<string>(PassiveProcessViewModels.Select(vm => vm.ProcessName));
            _settings.DistractionProcesses = new ObservableCollection<string>(DistractionProcessViewModels.Select(vm => vm.ProcessName));
            _settings.TagRules = TagRuleViewModels.ToDictionary(vm => vm.Keyword, vm => vm.Subject);

            DataManager.SaveSettings(_settings);
        }

        // --- 앱 추가 버튼 핸들러 ---
        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e) { _currentProcessType = "Work"; OpenProcessSelectionWindow(); }
        private void AddPassiveProcessButton_Click(object sender, RoutedEventArgs e) { _currentProcessType = "Passive"; OpenProcessSelectionWindow(); }
        private void AddDistractionProcessButton_Click(object sender, RoutedEventArgs e) { _currentProcessType = "Distraction"; OpenProcessSelectionWindow(); }

        private void OpenProcessSelectionWindow()
        {
            var window = new AppSelectionWindow();
            window.Owner = Window.GetWindow(this);
            if (window.ShowDialog() == true)
            {
                AddProcessToCurrentList(window.SelectedAppName);
            }
        }

        private void AddProcessToCurrentList(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            switch (_currentProcessType)
            {
                case "Work":
                    if (!WorkProcessViewModels.Any(p => p.ProcessName == processName)) WorkProcessViewModels.Add(new ProcessViewModel(processName)); break;
                case "Passive":
                    if (!PassiveProcessViewModels.Any(p => p.ProcessName == processName)) PassiveProcessViewModels.Add(new ProcessViewModel(processName)); break;
                case "Distraction":
                    if (!DistractionProcessViewModels.Any(p => p.ProcessName == processName)) DistractionProcessViewModels.Add(new ProcessViewModel(processName)); break;
            }
            SaveAllSettings();
        }

        private void DeleteProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProcessViewModel vm)
            {
                if (WorkProcessViewModels.Contains(vm)) WorkProcessViewModels.Remove(vm);
                else if (PassiveProcessViewModels.Contains(vm)) PassiveProcessViewModels.Remove(vm);
                else if (DistractionProcessViewModels.Contains(vm)) DistractionProcessViewModels.Remove(vm);
                SaveAllSettings();
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // 체크박스 상태를 설정 객체에 반영
            if (ShowMiniCharCheck != null) _settings.MiniTimerShowCharacter = ShowMiniCharCheck.IsChecked == true;
            if (ShowMiniInfoCheck != null) _settings.MiniTimerShowInfo = ShowMiniInfoCheck.IsChecked == true;
            if (ShowMiniBgCheck != null) _settings.MiniTimerShowBackground = ShowMiniBgCheck.IsChecked == true;

            DataManager.SaveSettings(_settings);
        }

        // --- 태그 규칙 추가/삭제 ---
        private void AddTagRule_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TagKeywordBox.Text.Trim();
            string subject = TagSubjectBox.Text.Trim();

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(subject)) { MessageBox.Show("내용을 입력하세요."); return; }
            if (TagRuleViewModels.Any(x => x.Keyword == keyword)) { MessageBox.Show("이미 등록된 키워드입니다."); return; }

            TagRuleViewModels.Add(new TagRuleViewModel { Keyword = keyword, Subject = subject });
            TagKeywordBox.Clear();
            TagSubjectBox.Clear();
            SaveAllSettings();
        }

        private void DeleteTagRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TagRuleViewModel vm)
            {
                TagRuleViewModels.Remove(vm);
                SaveAllSettings();
            }
        }

        // --- 데이터 관리 ---
        private void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = $"Backup_{DateTime.Now:yyyyMMdd}.json" };
            if (dlg.ShowDialog() == true) { DataManager.ExportData(dlg.FileName); MessageBox.Show("내보내기 완료!"); }
        }

        private void ImportDataButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                if (MessageBox.Show("덮어쓰시겠습니까?", "경고", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DataManager.ImportData(dlg.FileName);
                    LoadData();
                    MessageBox.Show("복구 완료!");
                }
            }
        }

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말 초기화하시겠습니까?", "경고", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DataManager.ResetAllData();
                LoadData();
                MessageBox.Show("초기화 완료!");
            }
        }
    }


    public class ProcessViewModel
    {
        public string ProcessName { get; set; }
        public ProcessViewModel(string name) { ProcessName = name; }
    }

    public class TagRuleViewModel
    {
        public string Keyword { get; set; }
        public string Subject { get; set; }
    }
}