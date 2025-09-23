using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // [NotMapped]를 위해 추가

namespace WorkPartner
{
    public class TimeLogEntry
    {
        [Key] // 기본 키로 지정
        public int Id { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TaskText { get; set; }
        public int FocusScore { get; set; }

        // 리스트는 직접 매핑이 복잡하므로 JSON 문자열로 저장
        public string BreakActivitiesJson { get; set; }

        [NotMapped] // 이 속성은 DB에 저장하지 않음
        public List<string> BreakActivities
        {
            get => string.IsNullOrEmpty(BreakActivitiesJson)
                   ? new List<string>()
                   : System.Text.Json.JsonSerializer.Deserialize<List<string>>(BreakActivitiesJson);
            set => BreakActivitiesJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        [NotMapped] // 이 속성은 DB에 저장하지 않음
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
