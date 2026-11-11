using System;
using System.Globalization;

namespace ThreeCats
{
    internal enum Costume { None, Christmas, Halloween, SpringFestival }

    internal struct CelebrationDay
    {
        internal Costume Costume { get; private set; }
        internal string Message { get; private set; }
        internal bool Anniversary { get; private set; }

        internal CelebrationDay(Costume costume, string message, bool anniversary = false) : this()
        {
            Costume = costume;
            Message = message;
            Anniversary = anniversary;
        }

    }

    internal static class CelebrationCalendar
    {
        internal const int FirstAnniversaryYear = 2026;
        internal const int LastCalendarYear = 2100;
        private static readonly ChineseLunisolarCalendar lunar = new ChineseLunisolarCalendar();

        // December days from the Hong Kong Observatory's Gregorian-Lunar conversion tables (UTC+8).
        // https://www.hko.gov.hk/en/gts/time/conversion.htm
        private static readonly byte[] winterSolsticeDays =
        {
            22, 22, 21, 21, // 2026–2029
            22, 22, 21, 21, 22, 22, 21, 21, 22, 22, // 2030–2039
            21, 21, 22, 22, 21, 21, 22, 22, 21, 21, // 2040–2049
            22, 22, 21, 21, 22, 22, 21, 21, 21, 22, // 2050–2059
            21, 21, 21, 22, 21, 21, 21, 22, 21, 21, // 2060–2069
            21, 22, 21, 21, 21, 22, 21, 21, 21, 22, // 2070–2079
            21, 21, 21, 22, 21, 21, 21, 22, 21, 21, // 2080–2089
            21, 21, 21, 21, 21, 21, 21, 21, 21, 21, // 2090–2099
            22, // 2100
        };

        internal static CelebrationDay Describe(DateTime date)
        {
            Costume costume = CostumeOn(date);
            if (date.Month == 4 && date.Day == 6) return new CelebrationDay(costume, "伊伊酱生日快乐喵");
            if (date.Month == 1 && date.Day == 18) return new CelebrationDay(costume, "珣珣酱生日快乐喵");
            int anniversary = date.Year - FirstAnniversaryYear;
            if (date.Month == 12 && anniversary >= 0 && anniversary < winterSolsticeDays.Length
                && date.Day == winterSolsticeDays[anniversary])
                return new CelebrationDay(costume, "主人们" + (anniversary + 1).ToString(CultureInfo.InvariantCulture) + "周年快乐！", true);
            return new CelebrationDay(costume, null);
        }

        private static Costume CostumeOn(DateTime date)
        {
            if (date.Month == 12 && date.Day == 25) return Costume.Christmas;
            if (date.Month == 10 && date.Day == 31) return Costume.Halloween;
            if (date.Month <= 3 && date.Year >= 1901 && date.Year <= LastCalendarYear)
            {
                DateTime newYear = lunar.ToDateTime(date.Year, 1, 1, 0, 0, 0, 0);
                int offset = (date.Date - newYear).Days;
                if (offset >= -7 && offset <= 6) return Costume.SpringFestival;
            }
            return Costume.None;
        }
    }
}
