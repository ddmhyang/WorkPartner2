using System;
using System.Windows.Media;

namespace WorkPartner
{
    public class BackgroundSoundPlayer
    {
        private MediaPlayer _mediaPlayer;
        private bool _isPlaying = false;

        public BackgroundSoundPlayer(string soundFilePath)
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Open(new Uri(soundFilePath, UriKind.RelativeOrAbsolute));
            _mediaPlayer.MediaEnded += (s, e) =>
            {
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            };
        }

        public double Volume
        {
            get => _mediaPlayer.Volume;
            set
            {
                _mediaPlayer.Volume = value;
                // 볼륨이 0보다 크고, 재생 중이 아닐 때만 재생을 시작합니다.
                if (value > 0 && !_isPlaying)
                {
                    Play();
                }
                // 볼륨이 0이 되어도 Stop()을 호출하지 않도록 수정합니다.
            }
        }

        public void Play()
        {
            if (!_isPlaying)
            {
                _mediaPlayer.Play();
                _isPlaying = true;
            }
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            _isPlaying = false;
        }
    }
}