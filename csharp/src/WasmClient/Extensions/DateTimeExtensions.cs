namespace WasmClient.Extensions;

/// <summary>
/// 日時表示に関する拡張メソッド
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// UTCの日時をローカル時刻に変換してフォーマットする
    /// </summary>
    /// <param name="utcDateTime">UTC日時</param>
    /// <returns>ローカル時刻での表示文字列</returns>
    public static string ToLocalTimeString(this DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Utc)
        {
            return utcDateTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        }

        // Already local or unspecified, treat as UTC and convert
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return utc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
    }

    /// <summary>
    /// UTCの日時をローカル時刻に変換して短いフォーマットで表示する
    /// </summary>
    /// <param name="utcDateTime">UTC日時</param>
    /// <returns>ローカル時刻での短い表示文字列</returns>
    public static string ToLocalTimeShortString(this DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Utc)
        {
            return utcDateTime.ToLocalTime().ToString("MM/dd HH:mm");
        }

        // Already local or unspecified, treat as UTC and convert
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return utc.ToLocalTime().ToString("MM/dd HH:mm");
    }

    /// <summary>
    /// 現在時刻からの相対時間を表示する
    /// </summary>
    /// <param name="utcDateTime">UTC日時</param>
    /// <returns>相対時間の表示文字列</returns>
    public static string ToRelativeTimeString(this DateTime utcDateTime)
    {
        var localTime = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime.ToLocalTime()
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc).ToLocalTime();

        var diff = DateTime.Now - localTime;

        if (diff.TotalMinutes < 1)
            return "たった今";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}分前";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}時間前";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays}日前";

        return localTime.ToString("yyyy/MM/dd");
    }
}
