using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Text.Json.Serialization;

namespace WorkPartner
{
    /// <summary>
    /// 하나의 과목에 대한 데이터를 나타내는 순수한 모델 클래스입니다.
    /// 이 클래스는 데이터가 어떻게 저장되는지, UI가 어떻게 보이는지에 대해 전혀 알지 못합니다.
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
        /// 이 과목의 총 학습 시간입니다. ViewModel에 의해 계산되어 설정됩니다.
        /// JsonIgnore 특성을 사용하여 이 데이터는 파일에 저장되지 않도록 합니다.
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
                    OnPropertyChanged(nameof(TotalTimeFormatted)); // TotalTime이 바뀌면, UI에 표시되는 텍스트도 업데이트되어야 함을 알립니다.
                }
            }
        }

        /// <summary>
        /// TotalTime을 UI에 표시하기 위한 포맷된 문자열입니다.
        /// XAML에서 직접 이 속성에 바인딩하여 사용합니다.
        /// </summary>
        [JsonIgnore]
        public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours:00}:{TotalTime.Minutes:00}:{TotalTime.Seconds:00}";

        public event PropertyChangedEventHandler PropertyChanged;

        // OnPropertyChanged 메서드를 하나로 통합하고, [CallerMemberName]을 활용하여 편리하게 사용합니다.
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ToString()을 오버라이드하면 디버깅 시나 특정 컨트롤에서 객체를 더 쉽게 식별할 수 있습니다.
        public override string ToString()
        {
            return Text;
        }

        private Brush _colorBrush = Brushes.Gray;
        [JsonIgnore] // 파일에 저장할 필요 없는 UI 전용 속성
        public Brush ColorBrush
        {
            get => _colorBrush;
            set
            {
                _colorBrush = value;
                OnPropertyChanged(); // 속성이 변경되면 UI에 알립니다.
            }
        }
    }
}