using Spectre.Console;

namespace Squirrel.Cli
{
    public static class SquirrelTheme
    {
        public static readonly Style Command = new Style(Color.Cyan1, Color.Default, Decoration.Bold);
        public static readonly Style Option = new Style(Color.Yellow, Color.Default, Decoration.None);
        public static readonly Style Required = new Style(Color.Red, Color.Default, Decoration.Bold);
        public static readonly Style DefaultValue = new Style(Color.Grey, Color.Default, Decoration.Italic);
        public static readonly Style Error = new Style(Color.Red, Color.Default, Decoration.Bold);
        public static readonly Style Success = new Style(Color.Green, Color.Default, Decoration.Bold);
        public static readonly Style Warning = new Style(Color.Yellow, Color.Default, Decoration.None);
        public static readonly Style Info = new Style(Color.Blue, Color.Default, Decoration.None);
        public static readonly Style Dim = new Style(Color.Grey, Color.Default, Decoration.Dim);
        public static readonly Style Title = new Style(Color.White, Color.Blue, Decoration.Bold);
        public static readonly Style PanelBorder = new Style(Color.Cyan1, Color.Default, Decoration.None);
        public static readonly Style PanelHeader = new Style(Color.White, Color.Blue, Decoration.Bold);
        public static readonly Style ProgressBar = new Style(Color.Cyan1, Color.Default, Decoration.None);
        public static readonly Style ProgressCompleted = new Style(Color.Green, Color.Default, Decoration.None);
        public static readonly Style TableHeader = new Style(Color.White, Color.Blue, Decoration.Bold);
        public static readonly Style TableBorder = new Style(Color.Cyan1, Color.Default, Decoration.None);

        public static void ApplyTheme(this IAnsiConsole console, bool noColor = false)
        {
            if (noColor)
            {
                console.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
            }
        }
    }
}