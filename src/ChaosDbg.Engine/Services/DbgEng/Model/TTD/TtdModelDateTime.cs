using System;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Provides facilities for parsing a TTD DbgEng Data Model DateTime into a CLR <see cref="DateTime"/>.
    /// </summary>
    public static class TtdModelDateTime
    {
        public static DateTime Parse(dynamic modelObject)
        {
            return new DateTime(
                year: (int) modelObject.Year,
                month: (int) modelObject.Month,
                day: (int) modelObject.Day,
                hour: (int) modelObject.Hour,
                minute: (int) modelObject.Minute,
                second: (int) modelObject.Second,
                millisecond: (int) modelObject.Milliseconds,
                kind: DateTimeKind.Local
            );
        }
    }
}
