// 파일: WorkPartner/ViewModels/DashboardViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WorkPartner.Commands; // RelayCommand가 있는 네임스페이스
using WorkPartner.Services; // ITaskService, IDialogService 등 서비스 인터페이스가 있는 네임스페이스

namespace WorkPartner.ViewModels
{
    /// <summary>
    /// DashboardPage의 모든 데이터와 로직을 관리하는 ViewModel입니다.
    /// 이 클래스는 '어떻게' 보일지에 대해서는 전혀 모르며, 오직 '무엇을' 할지만을 정의합니다.
    /// </summary>
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region --- 서비스 및 멤버 변수 선언 ---

        // 외부 세계(UI, 파일 시스템)와의 소통을 담당할 전문가(서비스)들입니다.
        // readonly로 선언하여 생성자에서만 할당되도록 강제합니다.
        private readonly ITaskService _taskService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService; // 설정 관리를 위한 서비스 추가

        #endregion

        #region --- UI와 바인딩될 속성 (Properties) ---

        // Task 입력 텍스트박스와 바인딩될 속성입니다.
        private string _newTaskText;
        public string NewTaskText
        {
            get => _newTaskText;
            set => SetProperty(ref _newTaskText, value);
        }

        // 메인 타이머 디스플레이와 바인딩될 속성입니다.
        private string _mainTimeDisplayText = "00:00:00";
        public string MainTimeDisplayText
        {
            get => _mainTimeDisplayText;
            set => SetProperty(ref _mainTimeDisplayText, value);
        }

        // Task 리스트박스와 바인딩될 컬렉션입니다.
        // ObservableCollection을 사용해야 아이템 추가/삭제 시 UI가 자동으로 업데이트됩니다.
        public ObservableCollection<TaskItem> TaskItems { get; private set; }

        // 로딩 중일 때 ProgressBar 등을 보여주기 위한 속성입니다.
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        #endregion

        #region --- UI와 바인딩될 명령 (Commands) ---

        // '과목 추가' 버튼과 바인딩될 Command입니다.
        public ICommand AddTaskCommand { get; }
        // '데이터 로드'를 위한 Command입니다. (예: 새로고침 버튼)
        public ICommand LoadDataCommand { get; }

        #endregion

        #region --- 생성자 (Constructor) ---

        /// <summary>
        /// ViewModel이 생성될 때 외부에서 서비스 전문가들을 주입받습니다.
        /// 이를 '의존성 주입(Dependency Injection)'이라고 하며, 클래스 간의 결합도를 낮춰줍니다.
        /// </summary>
        public DashboardViewModel(ITaskService taskService, IDialogService dialogService, ISettingsService settingsService)
        {
            // 전달받은 서비스들을 멤버 변수에 할당합니다.
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // 데이터 컬렉션을 초기화합니다.
            TaskItems = new ObservableCollection<TaskItem>();

            // Command들을 실제 실행될 메서드와 연결합니다.
            AddTaskCommand = new RelayCommand(async _ => await AddTaskAsync(), _ => !IsLoading && !string.IsNullOrWhiteSpace(NewTaskText));
            LoadDataCommand = new RelayCommand(async _ => await LoadDataAsync());
        }

        #endregion

        #region --- 핵심 로직 (Methods) ---

        /// <summary>
        /// 과목 추가 로직을 비동기로 처리합니다.
        /// </summary>
        private async Task AddTaskAsync()
        {
            if (TaskItems.Any(t => t.Text.Equals(NewTaskText, StringComparison.OrdinalIgnoreCase)))
            {
                // ViewModel은 MessageBox의 존재를 모릅니다. 단지 서비스에게 메시지를 보여달라고 요청할 뿐입니다.
                _dialogService.ShowMessageBox("이미 존재하는 과목입니다.");
                return;
            }

            var newTask = new TaskItem { Text = NewTaskText };
            TaskItems.Add(newTask);

            // 설정에 색상을 저장하는 로직도 서비스에게 위임합니다.
            await _settingsService.SetTaskColorAsync(newTask.Text, "#808080"); // 기본 색상 지정

            // 파일 저장 로직 역시 서비스에게 위임합니다.
            await _taskService.SaveTasksAsync(TaskItems);

            NewTaskText = string.Empty; // 입력창 초기화
        }

        /// <summary>
        /// 모든 데이터를 비동기적으로 로드합니다.
        /// </summary>
        private async Task LoadDataAsync()
        {
            IsLoading = true; // 로딩 시작을 UI에 알림
            try
            {
                // TaskService를 통해 데이터를 가져옵니다. ViewModel은 데이터가 파일에 있는지 DB에 있는지 모릅니다.
                var tasks = await _taskService.LoadTasksAsync();
                TaskItems.Clear();
                foreach (var task in tasks)
                {
                    TaskItems.Add(task);
                }

                // (이곳에 Todo, TimeLog 등 다른 데이터 로딩 로직을 추가합니다.)
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageBox($"데이터 로딩 중 오류가 발생했습니다: {ex.Message}");
            }
            finally
            {
                IsLoading = false; // 로딩 완료를 UI에 알림
            }
        }

        #endregion

        #region --- INotifyPropertyChanged 구현 ---

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 속성 값이 변경될 때 UI에 자동으로 알려주는 헬퍼 메서드입니다.
        /// </summary>
        /// <typeparam name="T">속성의 타입</typeparam>
        /// <param name="field">속성의 기반이 되는 private 필드</param>
        /// <param name="newValue">새로운 값</param>
        /// <param name="propertyName">속성의 이름</param>
        /// <returns>값의 변경 여부</returns>
        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, newValue))
            {
                return false;
            }
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        #endregion
    }
}