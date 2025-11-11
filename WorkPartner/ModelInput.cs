// 파일: ModelInput.cs (수정)
using Microsoft.ML.Data;

namespace WorkPartner.AI
{
    public class ModelInput
    {
        // ▼▼▼ [수정] [LoadColumn] 속성들을 추가합니다. ▼▼▼

        [LoadColumn(1)] // (model_input.json의 2번째 열)
        public float DayOfWeek { get; set; }

        [LoadColumn(2)] // (model_input.json의 3번째 열)
        public float Hour { get; set; }

        [LoadColumn(3)] // (model_input.json의 4번째 열)
        public float Duration { get; set; }

        [LoadColumn(0)] // (model_input.json의 1번째 열)
        public string TaskName { get; set; }

        [LoadColumn(4)] // (model_input.json의 5번째 열)
        [ColumnName("Label")]
        public float FocusScore { get; set; }

        // ▲▲▲ [수정 완료] ▲▲▲
    }
}