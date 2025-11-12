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
        public string Title
        {
            get
            {
                if (string.IsNullOrEmpty(Content))
                    return "(새 메모)";

                // 윈도우(\r\n), Mac(\r), Unix(\n) 모든 줄바꿈 문자를 기준으로 분리합니다.
                string[] lines = Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                // 무조건 첫 번째 줄만 반환합니다.
                return lines[0];
            }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}