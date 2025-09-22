// 파일: TodoItem.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace WorkPartner
{
    public class TodoItem : INotifyPropertyChanged
    {
        private string _text;
        private bool _isCompleted;
        private DateTime _date;

        public string Text { get => _text; set { _text = value; OnPropertyChanged(nameof(Text)); } }
        public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); } }
        public DateTime Date { get => _date; set { _date = value; OnPropertyChanged(nameof(Date)); } }
        public ObservableCollection<TodoItem> SubTasks { get; set; }
        public ObservableCollection<string> Tags { get; set; }

        // [속성 추가] 이 할 일에 대해 보상이 지급되었는지 확인하는 플래그입니다.
        public bool HasBeenRewarded { get; set; }

        public TodoItem()
        {
            Text = "새로운 할 일";
            SubTasks = new ObservableCollection<TodoItem>();
            Tags = new ObservableCollection<string>();
            HasBeenRewarded = false; // 기본값은 '보상 안 됨'
            Date = DateTime.Today;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
