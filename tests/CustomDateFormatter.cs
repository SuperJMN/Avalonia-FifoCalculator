using System.Globalization;

namespace TestProject1;

class CustomDateFormatter : IFormatProvider
{
    readonly IFormatProvider basedOn;
    readonly string shortDatePattern;
    public CustomDateFormatter(string shortDatePattern, IFormatProvider basedOn)
    {
        this.shortDatePattern = shortDatePattern;
        this.basedOn = basedOn;
    }
    public object? GetFormat(Type? formatType)
    {
        if (formatType == typeof(DateTimeFormatInfo))
        {
            var basedOnFormatInfo = (DateTimeFormatInfo?)basedOn.GetFormat(formatType);
            if (basedOnFormatInfo is null)
            {
                return null;
            }

            var dateFormatInfo = (DateTimeFormatInfo)basedOnFormatInfo.Clone();
            dateFormatInfo.ShortDatePattern = shortDatePattern;
            return dateFormatInfo;
        }

        return basedOn.GetFormat(formatType);
    }
}
