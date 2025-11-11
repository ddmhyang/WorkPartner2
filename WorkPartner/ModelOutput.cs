// 파일: WorkPartner/ModelOutput.cs
using Microsoft.ML.Data; // 👈 [추가]

namespace WorkPartner
{
    public class ModelOutput
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }
}