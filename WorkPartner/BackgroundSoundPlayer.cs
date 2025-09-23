using System;
using System.Windows.Media;

namespace WorkPartner
{
    public class BackgroundSoundPlayer
    {
        private readonly MediaPlayer _mediaPlayer;
        private readonly Uri _soundSource;

        public BackgroundSoundPlayer(string soundFilePath)
        {
            _mediaPlayer = new MediaPlayer();
            _soundSource = new Uri(soundFilePath, UriKind.RelativeOrAbsolute);

            _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        public void PlayLooping()
        {
            _mediaPlayer.Open(_soundSource);
            _mediaPlayer.Play();
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            _mediaPlayer.Position = TimeSpan.Zero;
            _mediaPlayer.Play();
        }

        public double Volume
        {
            get => _mediaPlayer.Volume;
            set => _mediaPlayer.Volume = Math.Min(Math.Max(value, 0.0), 1.0);
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
        }
    }
}