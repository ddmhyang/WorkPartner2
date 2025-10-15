using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Text.Json.Serialization;

namespace WorkPartner
{
    /// <summary>
    /// �ϳ��� ���� ���� �����͸� ��Ÿ���� ������ �� Ŭ�����Դϴ�.
    /// �� Ŭ������ �����Ͱ� ��� ����Ǵ���, UI�� ��� ���̴����� ���� ���� ���� ���մϴ�.
    /// </summary>
    public class TaskItem : INotifyPropertyChanged
    {
        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        private TimeSpan _totalTime;
        /// <summary>
        /// �� ������ �� �н� �ð��Դϴ�. ViewModel�� ���� ���Ǿ� �����˴ϴ�.
        /// JsonIgnore Ư���� ����Ͽ� �� �����ʹ� ���Ͽ� ������� �ʵ��� �մϴ�.
        /// </summary>
        [JsonIgnore]
        public TimeSpan TotalTime
        {
            get => _totalTime;
            set
            {
                if (_totalTime != value)
                {
                    _totalTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalTimeFormatted)); // TotalTime�� �ٲ��, UI�� ǥ�õǴ� �ؽ�Ʈ�� ������Ʈ�Ǿ�� ���� �˸��ϴ�.
                }
            }
        }

        /// <summary>
        /// TotalTime�� UI�� ǥ���ϱ� ���� ���˵� ���ڿ��Դϴ�.
        /// XAML���� ���� �� �Ӽ��� ���ε��Ͽ� ����մϴ�.
        /// </summary>
        [JsonIgnore]
        public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours:00}:{TotalTime.Minutes:00}:{TotalTime.Seconds:00}";

        public event PropertyChangedEventHandler PropertyChanged;

        // OnPropertyChanged �޼��带 �ϳ��� �����ϰ�, [CallerMemberName]�� Ȱ���Ͽ� ���ϰ� ����մϴ�.
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ToString()�� �������̵��ϸ� ����� �ó� Ư�� ��Ʈ�ѿ��� ��ü�� �� ���� �ĺ��� �� �ֽ��ϴ�.
        public override string ToString()
        {
            return Text;
        }

        private Brush _colorBrush = Brushes.Gray;
        [JsonIgnore] // ���Ͽ� ������ �ʿ� ���� UI ���� �Ӽ�
        public Brush ColorBrush
        {
            get => _colorBrush;
            set
            {
                _colorBrush = value;
                OnPropertyChanged(); // �Ӽ��� ����Ǹ� UI�� �˸��ϴ�.
            }
        }
    }
}