using System;
using System.ComponentModel;
using System.Linq;

namespace WorkPartner
{
    public class MemoItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; }

        private string _title;
        public string Title
        {
            get { return _title; }
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        private string _content;
        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                OnPropertyChanged(nameof(Content));
                OnPropertyChanged(nameof(Snippet));
            }
        }

        /// <summary>
        /// [속성 추가] 메모가 생성된 날짜입니다.
        /// </summary>
        public DateTime CreatedDate { get; set; }


        public string Snippet
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return string.Empty;
                return new string(Content.Replace('\n', ' ').Replace('\r', ' ').Take(30).ToArray());
            }
        }

        public MemoItem()
        {
            Id = Guid.NewGuid();
            Title = "새 메모";
            Content = "";
            CreatedDate = DateTime.Now; // 새 메모 생성 시 현재 날짜를 기록합니다.
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}