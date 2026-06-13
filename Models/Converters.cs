using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileOrganizer.Models;

public sealed class OrganizeModeIsDateConverter : IValueConverter
{
    public static readonly OrganizeModeIsDateConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is OrganizeMode m && m == OrganizeMode.ByDate;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class TabIndexEqualsConverter : IValueConverter
{
    public static readonly TabIndexEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int idx) return false;
        if (parameter is string s && int.TryParse(s, out var expected)) return idx == expected;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class TabIndexNotEqualsConverter : IValueConverter
{
    public static readonly TabIndexNotEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int idx) return true;
        if (parameter is string s && int.TryParse(s, out var expected)) return idx != expected;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
