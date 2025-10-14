// 파일: WorkPartner/BackgroundSoundPlayer.cs
using System;
using System.IO;
using System.Windows.Media;

namespace WorkPartner
{
    public class BackgroundSoundPlayer
    {
        private readonly MediaPlayer _mediaPlayer;

        // ▼▼▼ 오류 수정: IsPlaying 속성 추가 ▼▼▼
        public bool IsPlaying { get; private set; }

        public BackgroundSoundPlayer(string soundFilePath)
        {
            _mediaPlayer = new MediaPlayer();
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, soundFilePath);

            if (File.Exists(fullPath))
            {
                _mediaPlayer.Open(new Uri(fullPath));
                _mediaPlayer.MediaEnded += (s, e) =>
                {
                    _mediaPlayer.Position = TimeSpan.Zero;
                    _mediaPlayer.Play();
                };
            }
        }

        public void Play()
        {
            _mediaPlayer.Play();
            IsPlaying = true; // 상태 업데이트
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            IsPlaying = false; // 상태 업데이트
        }

        public double Volume
        {
            get => _mediaPlayer.Volume;
            set => _mediaPlayer.Volume = value;
        }
    }
}