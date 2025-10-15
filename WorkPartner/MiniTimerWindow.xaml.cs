using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        private readonly SolidColorBrush _runningBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255)) { Opacity = 0.6 };
        private readonly SolidColorBrush _stoppedBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.6 };
        private AppSettings _settings;

        public MiniTimerWindow()
        {
            InitializeComponent();
            (this.Content as Border).Background = _stoppedBrush;
            LoadSettings();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // ✨ UI 스레드에서 안전하게 업데이트하도록 수정
        public void UpdateTime(string time)
        {
            Dispatcher.Invoke(() =>
            {
                TimeTextBlock.Text = time;
            });
        }

        public void UpdateTaskInfo(string taskName)
        {
            Dispatcher.Invoke(() =>
            {
                InfoTextBlock.Text = taskName;
            });
        }

        public void SetRunningStyle()
        {
            (this.Content as Border).Background = _runningBrush;
        }

        public void SetStoppedStyle()
        {
            (this.Content as Border).Background = _stoppedBrush;
        }

        public void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (_settings.MiniTimerShowBackground)
            {
                TimerBorder.Background = Brushes.Transparent;
            }
            else
            {
                BackgroundImage.Source = null;
                SetStoppedStyle();
            }

            MiniCharacterDisplay.Visibility = _settings.MiniTimerShowCharacter ? Visibility.Visible : Visibility.Collapsed;
            if (_settings.MiniTimerShowCharacter) MiniCharacterDisplay.UpdateCharacter();

            InfoTextBlock.Visibility = _settings.MiniTimerShowInfo ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}