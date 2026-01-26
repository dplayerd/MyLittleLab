namespace RismLogProcessor.Models
{
    public sealed class ParsedPayload
    {
        // "89" / "76" / "f6" / "80" / "OK" / other
        public string Prefix { get; set; } = "";

        public string MachineNo { get; set; } = "";
        public string CardNo { get; set; } = "";
        public string Effect { get; set; } = "";

        // yymmddHHmm (10 chars) e.g. 2601201019
        public string SendTimeToken { get; set; } = "";

        // Machine|Card|Effect|SendTimeToken
        public string MatchKey { get; set; } = "";
    }
}
