using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace WorkPartner
{
    public class TaskItem : INotifyPropertyChanged
    {
        private string _text;
        private TimeSpan _totalTime;

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

        [JsonIgnore] // ����ȭ �� ����
        public TimeSpan TotalTime
        {
            get => _totalTime;
            set
            {
                if (_totalTime != value)
                {
                    _totalTime = value;
                    OnPropertyChanged(nameof(TotalTime));
                }
            }
        }

        // UI�� ǥ���� �������� ��ȯ�ϴ� �Ӽ�
        [JsonIgnore]
        public string TotalTimeDisplay => $"{TotalTime:hh\\:mm\\:ss}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [JsonIgnore]
        public Brush ColorBrush
        {
            get
            {
                // DataManager���� ���� ������ ������ SolidColorBrush�� ��ȯ
                if (DataManager.LoadSettings().TaskColors.TryGetValue(this.Text, out string colorHex))
                {
                    try
                    {
                        return (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                    }
                    catch { }
                }
                return Brushes.Gray;
            }
        }
    }
}