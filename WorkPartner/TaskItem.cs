using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WorkPartner
{
    public class TaskItem : INotifyPropertyChanged
    {
        [Key] // 기본 키로 지정
        public int Id { get; set; }

        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        // Color는 직접 매핑이 어려우므로 문자열로 저장합니다.
        public string ColorHex { get; set; } = "#FF808080"; // 기본 회색

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
