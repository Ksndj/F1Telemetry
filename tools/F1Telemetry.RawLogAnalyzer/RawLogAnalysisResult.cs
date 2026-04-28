using F1Telemetry.Udp.Packets;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Contains the aggregate counters and per-session summaries produced from a raw UDP JSONL file.
/// </summary>
public sealed class RawLogAnalysisResult
{
    /// <summary>
    /// Initializes an analysis result for an input raw log.
    /// </summary>
    public RawLogAnalysisResult(string inputPath, string reportPath)
    {
        InputPath = inputPath;
        ReportPath = reportPath;
    }

    public string InputPath { get; }

    public string ReportPath { get; internal set; }

    public long TotalLines { get; internal set; }

    public long ParsedPacketCount { get; internal set; }

    public long InvalidJsonLineCount { get; internal set; }

    public long MissingPayloadLineCount { get; internal set; }

    public long InvalidBase64LineCount { get; internal set; }

    public long HeaderParseFailureCount { get; internal set; }

    public long PayloadLengthMismatchCount { get; internal set; }

    public long UnknownPacketIdCount { get; internal set; }

    public long UnsupportedPacketIdCount { get; internal set; }

    public long PacketParseFailureCount { get; internal set; }

    public long DispatchFailureCount { get; internal set; }

    public Dictionary<PacketId, long> PacketIdCounts { get; } = new();

    public SortedDictionary<byte, long> UnknownPacketIdCounts { get; } = new();

    public SortedDictionary<PacketId, long> UnsupportedPacketIdCounts { get; } = new();

    public SortedDictionary<ulong, RawLogSessionSummary> Sessions { get; } = new();

    public List<long> InvalidJsonLineExamples { get; } = new();

    public List<long> InvalidBase64LineExamples { get; } = new();

    public List<long> PayloadLengthMismatchLineExamples { get; } = new();

    public List<long> PacketParseFailureLineExamples { get; } = new();

    internal RawLogSessionSummary GetOrCreateSession(ulong sessionUid)
    {
        if (!Sessions.TryGetValue(sessionUid, out var session))
        {
            session = new RawLogSessionSummary(sessionUid);
            Sessions[sessionUid] = session;
        }

        return session;
    }
}
