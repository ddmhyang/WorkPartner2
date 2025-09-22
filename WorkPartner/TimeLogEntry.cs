// 파일: TimeLogEntry.cs (수정)
// [수정] AI 모델과의 의존성을 완전히 제거하여, [Ignore] 어트리뷰트가 더 이상 필요하지 않습니다.
using System;
using System.Collections.Generic;

namespace WorkPartner
{
    public class TimeLogEntry
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TaskText { get; set; }
        public int FocusScore { get; set; }
        public List<string> BreakActivities { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        public TimeLogEntry()
        {
            BreakActivities = new List<string>();
        }

        public override string ToString()
        {
            return $"{StartTime:HH:mm} - {EndTime:HH:mm} ({TaskText})";
        }
    }
}