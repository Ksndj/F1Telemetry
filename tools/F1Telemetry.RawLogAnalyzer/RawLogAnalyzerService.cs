using System.Globalization;
using System.Net;
using System.Text.Json;
using F1Telemetry.Core.Models;
using F1Telemetry.Udp.Packets;
using F1Telemetry.Udp.Parsers;
using F1Telemetry.Udp.Services;

namespace F1Telemetry.RawLogAnalyzer;

/// <summary>
/// Streams UDP raw JSONL logs and aggregates decoded packet data without retaining raw payloads.
/// </summary>
public sealed class RawLogAnalyzerService
{
    private const int MaxExampleLines = 5;
    private static readonly HashSet<PacketId> SupportedTypedPacketIds =
    [
        PacketId.Motion,
        PacketId.Session,
        PacketId.LapData,
        PacketId.Event,
        PacketId.Participants,
        PacketId.CarTelemetry,
        PacketId.CarStatus,
        PacketId.FinalClassification,
        PacketId.CarDamage,
        PacketId.SessionHistory,
        PacketId.TyreSets,
        PacketId.MotionEx,
        PacketId.LapPositions
    ];

    private readonly PacketHeaderParser _headerParser = new();

    /// <summary>
    /// Analyzes a JSONL raw log and writes the markdown report to the requested or default output path.
    /// </summary>
    public async Task<RawLogAnalysisResult> AnalyzeAsync(
        RawLogAnalyzerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new ArgumentException("Input path is required.", nameof(options));
        }

        var inputPath = Path.GetFullPath(options.InputPath);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Raw log input file was not found.", inputPath);
        }

        var reportPath = ResolveReportPath(inputPath, options.OutputPath);
        var result = new RawLogAnalysisResult(inputPath, reportPath);
        var dispatcher = CreateDispatcher(result);

        using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            result.TotalLines++;
            ProcessLine(line, result.TotalLines, dispatcher, result, options.SessionUid);
        }

        var selectedSession = SelectRaceSession(result, options.SessionUid);
        result.RaceReport = RaceAnalysisReportBuilder.Build(result, selectedSession, DateTimeOffset.UtcNow);
        var markdown = RawLogMarkdownReportWriter.Build(result);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, markdown, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private PacketDispatcher CreateDispatcher(RawLogAnalysisResult result)
    {
        var dispatcher = new PacketDispatcher(new PacketHeaderParser());
        dispatcher.PacketParsed += (_, parsedPacket) => ApplyParsedPacket(result, parsedPacket);
        dispatcher.PacketParseFailed += (_, failure) =>
        {
            result.PacketParseFailureCount++;
            AddExample(result.PacketParseFailureLineExamples, result.TotalLines);
            var session = result.GetOrCreateSession(failure.Header.SessionUid);
            session.PacketParseFailureCount++;
        };

        return dispatcher;
    }

    private void ProcessLine(
        string line,
        long lineNumber,
        PacketDispatcher dispatcher,
        RawLogAnalysisResult result,
        ulong? requestedSessionUid)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            result.InvalidJsonLineCount++;
            AddExample(result.InvalidJsonLineExamples, lineNumber);
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            result.InvalidJsonLineCount++;
            AddExample(result.InvalidJsonLineExamples, lineNumber);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryReadPayload(root, out var payload))
            {
                result.InvalidBase64LineCount++;
                AddExample(result.InvalidBase64LineExamples, lineNumber);
                return;
            }

            if (TryReadLength(root, out var declaredLength) && declaredLength != payload.Length)
            {
                result.PayloadLengthMismatchCount++;
                AddExample(result.PayloadLengthMismatchLineExamples, lineNumber);
            }

            if (!_headerParser.TryParse(payload, out var header, out _))
            {
                result.HeaderParseFailureCount++;
                return;
            }

            if (ShouldSkipSession(header.SessionUid, requestedSessionUid))
            {
                return;
            }

            var receivedAt = ReadTimestamp(root);
            var session = result.GetOrCreateSession(header.SessionUid);
            session.ObserveDatagram(header, receivedAt);

            if (!header.IsKnownPacketId)
            {
                result.UnknownPacketIdCount++;
                Increment(result.UnknownPacketIdCounts, header.RawPacketId);
                session.UnknownPacketIdCount++;
            }
            else
            {
                Increment(result.PacketIdCounts, header.PacketId);
                if (!SupportedTypedPacketIds.Contains(header.PacketId))
                {
                    result.UnsupportedPacketIdCount++;
                    Increment(result.UnsupportedPacketIdCounts, header.PacketId);
                    session.UnsupportedPacketIdCount++;
                }
            }

            var datagram = new UdpDatagram(payload, new IPEndPoint(IPAddress.Loopback, 0), receivedAt);
            if (!dispatcher.TryDispatch(datagram, out _))
            {
                result.DispatchFailureCount++;
            }
        }
    }

    private static bool TryReadPayload(JsonElement root, out byte[] payload)
    {
        payload = [];
        if (!root.TryGetProperty("payloadBase64", out var payloadElement) ||
            payloadElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var payloadBase64 = payloadElement.GetString();
        if (string.IsNullOrWhiteSpace(payloadBase64))
        {
            return false;
        }

        try
        {
            payload = Convert.FromBase64String(payloadBase64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadLength(JsonElement root, out int length)
    {
        length = 0;
        if (!root.TryGetProperty("length", out var lengthElement) ||
            lengthElement.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return lengthElement.TryGetInt32(out length);
    }

    private static DateTimeOffset ReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestampUtc", out var timestampElement) &&
            timestampElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                timestampElement.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return timestamp;
        }

        return DateTimeOffset.MinValue;
    }

    private static void ApplyParsedPacket(RawLogAnalysisResult result, ParsedPacket parsedPacket)
    {
        result.ParsedPacketCount++;

        var session = result.GetOrCreateSession(parsedPacket.Header.SessionUid);
        Increment(session.PacketCounts, parsedPacket.PacketId);

        switch (parsedPacket.Packet)
        {
            case SessionPacket packet:
                session.ApplySessionPacket(packet);
                break;
            case LapDataPacket packet:
                session.ApplyLapDataPacket(packet, parsedPacket.Header);
                break;
            case CarTelemetryPacket packet:
                session.ApplyCarTelemetryPacket(packet, parsedPacket.Header);
                break;
            case CarStatusPacket packet:
                session.ApplyCarStatusPacket(packet, parsedPacket.Header);
                break;
            case CarDamagePacket packet:
                session.ApplyCarDamagePacket(packet, parsedPacket.Header);
                break;
            case TyreSetsPacket packet:
                session.ApplyTyreSetsPacket(packet, parsedPacket.Header);
                break;
            case EventPacket packet:
                session.ApplyEventPacket(packet);
                break;
            case SessionHistoryPacket packet:
                session.ApplySessionHistoryPacket(packet, parsedPacket.Header);
                break;
            case FinalClassificationPacket packet:
                session.ApplyFinalClassificationPacket(packet, parsedPacket.Header);
                break;
            case LapPositionsPacket packet:
                session.ApplyLapPositionsPacket(packet, parsedPacket.Header);
                break;
        }
    }

    private static RawLogSessionSummary SelectRaceSession(
        RawLogAnalysisResult result,
        ulong? requestedSessionUid)
    {
        if (requestedSessionUid is not null)
        {
            if (result.Sessions.TryGetValue(requestedSessionUid.Value, out var requestedSession)
                && RaceAnalysisReportBuilder.IsValidRaceSession(requestedSession))
            {
                return requestedSession;
            }

            throw new InvalidOperationException(
                $"No valid Race session was found for sessionUid {requestedSessionUid.Value}.");
        }

        var selectedSession = result.Sessions.Values
            .Where(RaceAnalysisReportBuilder.IsValidRaceSession)
            .OrderByDescending(session => session.DatagramCount)
            .FirstOrDefault();

        return selectedSession ?? throw new InvalidOperationException(
            "No valid Race session was found. A Race session requires sessionType=15 and sessionUid != 0.");
    }

    private static bool ShouldSkipSession(ulong sessionUid, ulong? requestedSessionUid)
    {
        if (sessionUid == 0)
        {
            return true;
        }

        return requestedSessionUid is not null && sessionUid != requestedSessionUid.Value;
    }

    private static string ResolveReportPath(string inputPath, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.GetFullPath(outputPath);
        }

        var inputName = Path.GetFileNameWithoutExtension(inputPath);
        var outputName = string.IsNullOrWhiteSpace(inputName)
            ? "raw-log-analysis.md"
            : $"{inputName}-analysis.md";
        return Path.Combine(Directory.GetCurrentDirectory(), ".logs", "analysis", outputName);
    }

    private static void Increment<TKey>(IDictionary<TKey, long> counts, TKey key)
        where TKey : notnull
    {
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }

    private static void AddExample(List<long> examples, long lineNumber)
    {
        if (examples.Count < MaxExampleLines)
        {
            examples.Add(lineNumber);
        }
    }
}
