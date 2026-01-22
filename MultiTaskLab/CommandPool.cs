using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiTaskLab
{
    public class CommandPool
    {
        private readonly ConcurrentDictionary<long, TaskCompletionSource<CommandEntry>> _pending
            = new();

        private static long MakeKey(ushort seqNo, ushort cmdNo)
            => ((long)seqNo << 16) | cmdNo;

        public async Task<CommandEntry> SendAndWaitAsync(CommandEntry entry, string traceCode, TimeSpan timeout)
        {
            entry.TraceCode = traceCode;
            this.Add(entry);

            var key = MakeKey(entry.SequenceNo, entry.CommandNo);

            var tcs = new TaskCompletionSource<CommandEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(key, tcs))
                throw new InvalidOperationException($"Duplicate pending key: {entry.SequenceNo}/{entry.CommandNo}");

            try
            {
                // send
                await _udpClient.SendAsync(entry.Data);
                entry.LastSendTime = DateTime.UtcNow;

                // 等回覆或逾時（不阻塞 thread）
                using var cts = new CancellationTokenSource(timeout);
                await using var _ = cts.Token.Register(() => tcs.TrySetException(new TimeoutException(
                    $"UDP timeout: SeqNo={entry.SequenceNo}, CmdNo={entry.CommandNo}")));

                return await tcs.Task; // <-- 這裡才是真正「等到回覆/timeout」
            }
            finally
            {
                _pending.TryRemove(key, out _);
            }
        }

        private void Add(CommandEntry entry)
        {
            throw new NotImplementedException();
        }

        // 這個方法在你的 UDP Receive loop 裡被呼叫（收到封包時）
        public void OnUdpResponseReceived(ushort seqNo, ushort cmdNo, byte[] responseBytes)
        {
            var key = MakeKey(seqNo, cmdNo);
            if (_pending.TryGetValue(key, out var tcs))
            {
                // 你可以把 response parse 後寫入 entry / 狀態
                // 這裡示意：完成等待
                tcs.TrySetResult(new CommandEntry
                {
                    SequenceNo = seqNo,
                    CommandNo = cmdNo,
                    ResponseBytes = responseBytes,
                    // ...其他你需要的欄位
                });
            }
            // else: 找不到 pending，可能是逾時後回來、或重送/重覆封包，記 log 即可
        }
    }

    public class CommandEntry
    {
        public DateTime LastSendTime { get; internal set; }
        public string TraceCode { get; internal set; }
        public ushort CommandNo { get; internal set; }
        public ushort SequenceNo { get; internal set; }
        public byte[] Data { get; internal set; }
        public byte[] ResponseBytes { get; internal set; }
    }
}
