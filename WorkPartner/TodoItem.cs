using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // [NotMapped]를 위해 추가

namespace WorkPartner
{
    public class TodoItem : INotifyPropertyChanged
    {
        [Key] // 기본 키로 지정
        public int Id { get; set; }
        private string _text;
        private bool _isCompleted;
        private DateTime _date;

        public string Text { get => _text; set { _text = value; OnPropertyChanged(nameof(Text)); } }
        public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); } }
        public DateTime Date { get => _date; set { _date = value; OnPropertyChanged(nameof(Date)); } }

        [NotMapped] // EF Core는 복잡한 계층 구조를 직접 매핑하기 어려우므로, 필요하다면 별도의 테이블로 관리해야 합니다.
                    // 간단한 구현을 위해 여기서는 매핑에서 제외합니다.
        public ObservableCollection<TodoItem> SubTasks { get; set; }

        [NotMapped] // 이것도 마찬가지로 별도 테이블 또는 JSON 문자열로 저장해야 합니다.
        public ObservableCollection<string> Tags { get; set; }

        public bool HasBeenRewarded { get; set; }

        public TodoItem()
        {
            Text = "새로운 할 일";
            SubTasks = new ObservableCollection<TodoItem>();
            Tags = new ObservableCollection<string>();
            HasBeenRewarded = false;
            Date = DateTime.Today;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
