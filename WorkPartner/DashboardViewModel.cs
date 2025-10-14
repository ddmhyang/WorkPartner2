// 파일: WorkPartner/ViewModels/DashboardViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WorkPartner.Commands;
using WorkPartner.Services;

namespace WorkPartner.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region --- 서비스 및 멤버 변수 선언 ---

        private readonly ITaskService _taskService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;
        private readonly ITimerService _timerService; // 타이머 서비스 추가

        private AppSettings _settings;
        private string _lastActiveProcessName = string.Empty;
        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private TimeSpan _totalTimeTodayFromLogs; // 파일에서 읽어온 오늘 총 학습 시간
        private bool _isPausedForIdle = false;

        #endregion

        #region --- UI와 바인딩될 속성 (Properties) ---

        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        public ObservableCollection<TaskItem> TaskItems { get; private set; }

        private TaskItem _selectedTaskItem;
        public TaskItem SelectedTaskItem
        {
            get => _selectedTaskItem;
            set
            {
                if (SetProperty(ref _selectedTaskItem, value))
                {
                    // 선택된 과목이 바뀌면 로직 수행 (예: 스톱워치 리셋)
                    OnSelectedTaskChanged(value);
                }
            }
        }

        // ... (다른 UI 속성들 추가 가능) ...

        #endregion

        #region --- 생성자 (Constructor) ---

        public DashboardViewModel(ITaskService taskService, IDialogService dialogService, ISettingsService settingsService, ITimerService timerService)
        {
            _taskService = taskService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _timerService = timerService; // 타이머 서비스 주입

            TaskItems = new ObservableCollection<TaskItem>();

            // 서비스의 Tick 이벤트가 발생할 때마다 OnTimerTick 메서드를 실행하도록 구독합니다.
            _timerService.Tick += OnTimerTick;

            // ViewModel이 생성될 때 초기 데이터를 로드합니다.
            LoadInitialData();
        }

        #endregion

        #region --- 핵심 로직 (Methods) ---

        private void LoadInitialData()
        {
            _settings = _settingsService.LoadSettings();
            // TODO: 서비스에서 Task, Todo, TimeLog 등을 비동기로 불러오는 로직 필요

            // 데이터 로딩이 끝나면 타이머를 시작합니다.
            _timerService.Start();
        }

        private void OnSelectedTaskChanged(TaskItem newSelectedTask)
        {
            if (_currentWorkingTask != newSelectedTask)
            {
                // TODO: 현재 진행중인 작업 세션을 기록하는 로직 (LogWorkSession)

                _currentWorkingTask = newSelectedTask;
                // TODO: 선택된 과목의 총 시간을 다시 계산하고 UI 업데이트
            }
        }

        /// <summary>
        /// TimerService로부터 1초마다 호출되는 메서드. 앱의 핵심 두뇌 역할을 합니다.
        /// </summary>
        private void OnTimerTick(TimeSpan stopwatchElapsed)
        {
            string activeProcess = ActiveWindowHelper.GetActiveProcessName();

            if (activeProcess == _lastActiveProcessName && !string.IsNullOrEmpty(activeProcess))
            {
                UpdateLiveTimeDisplays(stopwatchElapsed);
                return;
            }

            _lastActiveProcessName = activeProcess;

            HandleStopwatchMode(stopwatchElapsed);
        }

        /// <summary>
        /// 활성 창 상태에 따라 작업 시간을 측정하거나 정지하는 로직입니다.
        /// DashboardPage.xaml.cs에 있던 코드를 그대로 가져왔습니다.
        /// </summary>
        private void HandleStopwatchMode(TimeSpan stopwatchElapsed)
        {
            if (_settings == null) return;

            string activeProcess = ActiveWindowHelper.GetActiveProcessName();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
            string keywordToCheck = !string.IsNullOrEmpty(activeUrl) ? activeUrl : activeProcess;

            if (keywordToCheck == null)
            {
                // 휴식 상태 처리
                return;
            }

            bool isDistraction = _settings.DistractionProcesses.Any(p => keywordToCheck.Contains(p));
            if (isDistraction)
            {
                // 딴짓 상태 처리
                return;
            }

            bool isTrackable = _settings.WorkProcesses.Any(p => keywordToCheck.Contains(p));
            if (isTrackable)
            {
                // 작업 상태 처리
                if (_currentWorkingTask == null && TaskItems.Any())
                {
                    SelectedTaskItem = TaskItems.First(); // 자동으로 첫 과목 선택
                }

                if (_currentWorkingTask != null)
                {
                    // 스톱워치 시작/정지 로직
                    // _sessionStartTime = DateTime.Now;
                }
            }

            UpdateLiveTimeDisplays(stopwatchElapsed);
        }

        /// <summary>
        /// 화면의 시간 표시를 업데이트합니다.
        /// </summary>
        private void UpdateLiveTimeDisplays(TimeSpan stopwatchElapsed)
        {
            // 파일에서 읽어온 오늘 총 시간 + 현재 스톱워치 시간
            var timeToDisplay = _totalTimeTodayFromLogs + stopwatchElapsed;
            MainTimeDisplayText = timeToDisplay.ToString(@"hh\:mm\:ss");

            // TODO: 미니 타이머 업데이트 로직 (이벤트나 콜백 사용)
        }

        #endregion

        #region --- INotifyPropertyChanged 구현 ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, newValue)) return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion
    }
}