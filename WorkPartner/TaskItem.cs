using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WorkPartner
{
    public class TaskItem : INotifyPropertyChanged
    {
        [Key] // �⺻ Ű�� ����
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

        // Color�� ���� ������ �����Ƿ� ���ڿ��� �����մϴ�.
        public string ColorHex { get; set; } = "#FF808080"; // �⺻ ȸ��

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
