using System;

namespace WorkPartner
{
    public class MemoItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } // 날짜 정보
    }
}

