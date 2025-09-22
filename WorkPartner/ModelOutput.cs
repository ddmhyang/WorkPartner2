// 파일 이름: ModelOutput.cs
// 역할: AI 모델이 예측한 결과를 담을 데이터 형식입니다.
using Microsoft.ML.Data;

namespace WorkPartner.AI
{
    public class ModelOutput
    {
        [ColumnName("Score")]
        public float PredictedFocusScore { get; set; } // 예측된 집중도 점수
    }
}