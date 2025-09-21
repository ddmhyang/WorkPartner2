// 파일 이름: TaskItem.cs
// 역할: 시간 측정의 대상이 되는 '과목'을 저장합니다. (예: C# 공부, 운동)
namespace WorkPartner
{
    public class TaskItem
    {
        public string Text { get; set; }

        // TimeLogEntry 와의 호환성을 위해 ToString()을 오버라이드합니다.
        public override string ToString()
        {
            return Text;
        }
    }
}