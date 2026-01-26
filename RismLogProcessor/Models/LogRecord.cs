namespace RismLogProcessor.Models
{
    // ===== Models =====

    public sealed class LogRecord
    {
        public DateTime? Time { get; set; }
        public string Direction { get; set; } = "";
        public string TraceCode { get; set; } = "";
        public int? SeqNo { get; set; }
        public int? CmdNo { get; set; }
        public string DbId { get; set; } = "";
        public int? Attempt { get; set; }
        public int? MaxRetry { get; set; }
        public string HeaderHex { get; set; } = "";

        // 重要：Payload 不可 Trim，保留原樣（含尾端空白）
        public string Payload { get; set; } = "";

        public string Label { get; set; } = "";
        public ParsedPayload? Parsed { get; set; }
    }
}
