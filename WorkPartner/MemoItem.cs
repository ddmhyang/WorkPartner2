using System;
using System.ComponentModel;

namespace WorkPartner
{
    public class MemoItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        private string _content;
        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged(nameof(Content));
                    OnPropertyChanged(nameof(Title)); // Title 속성이 Content의 첫 줄을 반환하도록 설정
                }
            }
        }

        // 메모 내용의 첫 줄을 제목처럼 사용하기 위한 속성
        public string Title => string.IsNullOrEmpty(Content) ? "(새 메모)" : Content.Split('\n')[0];

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}