// 파일: WorkPartner/ModelInput.cs
using Microsoft.ML.Data;

namespace WorkPartner.AI // (네임스페이스는 WorkPartner.AI가 맞습니다)
{
    public class ModelInput
    {
        // ▼▼▼ [수정] TextLoader가 파일 열을 읽을 수 있도록 LoadColumn 속성 추가 ▼▼▼

        [LoadColumn(0)] // 1번째 열
        public string TaskName { get; set; }

        [LoadColumn(1)] // 2번째 열
        public float DayOfWeek { get; set; }

        [LoadColumn(2)] // 3번째 열
        public float Hour { get; set; }

        [LoadColumn(3)] // 4번째 열
        public float Duration { get; set; }

        [LoadColumn(4)] // 5번째 열
        [ColumnName("Label")]
        public float FocusScore { get; set; }

        // ▲▲▲ [수정 완료] ▲▲▲
    }
}