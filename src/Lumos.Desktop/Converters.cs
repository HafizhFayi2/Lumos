using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Lumos.Desktop;

public static class BoolConvertersEx
{
    public static readonly IValueConverter HiddenOpacity =
        new FuncValueConverter<bool, double>(hidden => hidden ? 0.3 : 1.0);
}
