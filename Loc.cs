using System;
using System.Globalization;

namespace JiePing
{
    internal enum AppLanguage
    {
        Chinese,
        English
    }

    internal static class Loc
    {
        public static bool IsEnglish { get; set; }

        public static AppLanguage Current
        {
            get { return IsEnglish ? AppLanguage.English : AppLanguage.Chinese; }
        }

        public static string T(string chinese, string english)
        {
            return IsEnglish ? english : chinese;
        }

        public static string Format(string chinese, string english, params object[] args)
        {
            return string.Format(IsEnglish ? english : chinese, args);
        }

        public static string TimeLabel(DateTime time)
        {
            DateTime today = DateTime.Today;
            if (time.Date == today)
            {
                return T("今天 ", "Today ") + time.ToString("HH:mm");
            }
            if (time.Date == today.AddDays(-1))
            {
                return T("昨天 ", "Yesterday ") + time.ToString("HH:mm");
            }
            if (IsEnglish)
            {
                return time.ToString("MMM d  HH:mm", CultureInfo.InvariantCulture);
            }
            return time.ToString("M月d日  HH:mm");
        }

        public static string LongDateTime(DateTime time)
        {
            return IsEnglish
                ? time.ToString("yyyy-MM-dd  HH:mm", CultureInfo.InvariantCulture)
                : time.ToString("yyyy年M月d日  HH:mm");
        }

        public static string Items(int count)
        {
            return IsEnglish
                ? string.Format("{0} {1}", count, count == 1 ? "item" : "items")
                : string.Format("共 {0} 张", count);
        }
    }
}
