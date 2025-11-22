using System;
using System.IO;
using System.Windows.Media;

namespace WorkPartner
{
    public class BackgroundSoundPlayer
    {
        private readonly MediaPlayer _mediaPlayer;

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
            IsPlaying = true;
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            IsPlaying = false;
        }

        public double Volume
        {
            get => _mediaPlayer.Volume;
            set => _mediaPlayer.Volume = value;
        }
    }
}