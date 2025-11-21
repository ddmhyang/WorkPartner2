using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WorkPartner
{
    public static class GifHelper
    {
        // 이미지 컨트롤별로 [타이머, 프레임 목록, 프레임별 지연시간]을 관리
        private class GifState
        {
            public DispatcherTimer Timer;
            public List<BitmapSource> Frames;
            public List<int> Delays; // 각 프레임의 지연 시간 (1/100초 단위)
            public int CurrentFrameIndex;
        }

        private static readonly Dictionary<Image, GifState> _states
            = new Dictionary<Image, GifState>();

        public static void PlayGif(Image imageControl, string filePath)
        {
            StopGif(imageControl); // 기존 재생 중지

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                imageControl.Source = null;
                return;
            }

            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".gif")
                {
                    // GIF가 아니면 그냥 이미지로 표시
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imageControl.Source = bitmap;
                    return;
                }

                // GIF 디코더 생성
                var decoder = new GifBitmapDecoder(new Uri(filePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

                if (decoder.Frames.Count <= 1)
                {
                    imageControl.Source = decoder.Frames[0];
                    return;
                }

                // 프레임과 속도 정보 추출
                var state = new GifState
                {
                    Timer = new DispatcherTimer(),
                    Frames = new List<BitmapSource>(),
                    Delays = new List<int>(),
                    CurrentFrameIndex = 0
                };

                foreach (var frame in decoder.Frames)
                {
                    state.Frames.Add(frame);

                    // 프레임 속도(Delay) 읽기 (메타데이터)
                    int delay = 10; // 기본값 10 (0.1초)
                    if (frame.Metadata is BitmapMetadata metadata)
                    {
                        try
                        {
                            // "/grctlext"는 GIF Control Extension 블록입니다.
                            // 여기서 Delay Time을 가져옵니다. (단위: 1/100초)
                            var query = metadata.GetQuery("/grctlext/Delay") as ushort?;
                            if (query.HasValue)
                            {
                                delay = query.Value;
                            }
                        }
                        catch { }
                    }
                    // 너무 빠르면(0) 기본값 10으로 설정
                    if (delay < 2) delay = 10;
                    state.Delays.Add(delay);
                }

                // 첫 프레임 표시
                imageControl.Source = state.Frames[0];

                // 타이머 설정
                state.Timer.Tick += (s, e) =>
                {
                    state.CurrentFrameIndex++;
                    if (state.CurrentFrameIndex >= state.Frames.Count)
                        state.CurrentFrameIndex = 0;

                    imageControl.Source = state.Frames[state.CurrentFrameIndex];

                    // 다음 프레임의 속도에 맞춰 타이머 간격 조절
                    int nextDelay = state.Delays[state.CurrentFrameIndex] * 10; // 1/100초 -> ms 변환
                    state.Timer.Interval = TimeSpan.FromMilliseconds(nextDelay);
                };

                // 첫 번째 간격 설정 후 시작
                state.Timer.Interval = TimeSpan.FromMilliseconds(state.Delays[0] * 10);
                state.Timer.Start();

                _states[imageControl] = state;
            }
            catch
            {
                imageControl.Source = null;
            }
        }

        public static void StopGif(Image imageControl)
        {
            if (_states.ContainsKey(imageControl))
            {
                _states[imageControl].Timer.Stop();
                _states.Remove(imageControl);
            }
        }
    }
}