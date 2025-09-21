// 파일: ModelInput.cs (수정)
// [수정] AI 학습에 필요한 속성만 남겨 구조를 단순화하고, [Ignore] 어트리뷰트를 모두 제거했습니다.
using Microsoft.ML.Data;

namespace WorkPartner.AI
{
    public class ModelInput
    {
        // 학습에 사용할 특성(Feature)들
        public float DayOfWeek { get; set; }
        public float Hour { get; set; }
        public float Duration { get; set; }
        public string TaskName { get; set; }

        // 예측할 값(Label)
        [ColumnName("Label")]
        public float FocusScore { get; set; }
    }
}