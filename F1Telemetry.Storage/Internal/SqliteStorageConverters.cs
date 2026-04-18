using Microsoft.Data.Sqlite;

namespace F1Telemetry.Storage.Internal;

internal static class SqliteStorageConverters
{
    public static string ToStorageTimestamp(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("O");
    }

    public static DateTimeOffset FromStorageTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static bool ReadBoolean(SqliteDataReader reader, int ordinal)
    {
        return reader.GetInt64(ordinal) != 0;
    }
}
