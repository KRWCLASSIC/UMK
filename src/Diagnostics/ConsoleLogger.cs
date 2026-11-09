namespace UMK.Diagnostics;

public static class ConsoleLogger
{
    public static void Info(string message)
    {
        WriteColored("[UMK] ", ConsoleColor.Cyan);
        Console.WriteLine(message);
    }

    public static void Success(string message)
    {
        WriteColored("[UMK] ", ConsoleColor.Green);
        Console.WriteLine(message);
    }

    public static void Warn(string message)
    {
        WriteColored("[UMK] ", ConsoleColor.Yellow);
        Console.WriteLine(message);
    }

    public static void Error(string message)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("[UMK Error] ");
        Console.ForegroundColor = original;
        Console.Error.WriteLine(message);
    }

    public static void Step(string scriptName, string command, bool isDryRun = false)
    {
        if (isDryRun)
        {
            WriteColored("[DRY-RUN] ", ConsoleColor.Magenta);
        }
        else
        {
            WriteColored("[EXEC] ", ConsoleColor.DarkCyan);
        }

        WriteColored($"({scriptName}) ", ConsoleColor.DarkGray);
        Console.WriteLine(command);
    }

    public static void WriteColored(string text, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = original;
    }

    public static void WriteLineColored(string text, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = original;
    }
}

