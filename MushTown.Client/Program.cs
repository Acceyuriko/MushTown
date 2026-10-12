using MushTown.Client;

// --shot <file.png> [--action <name>] [--frame <n 0-based>] [--facing left|right]
// Save one screenshot of the requested frame, then exit (weekly acceptance).
// No argument at all means "play". Anything else is a mistake, not something to ignore.
string? shotPath = null;
string shotAction = "stand2";
int shotFrame = 0;
bool shotFacingRight = false;
bool ok = args.Length == 0;
if (args.Length >= 2 && args[0] == "--shot")
{
    shotPath = args[1];
    ok = true;
    for (int i = 2; i < args.Length && ok; i += 2)
    {
        if (i + 1 >= args.Length) ok = false;
        else if (args[i] == "--action") shotAction = args[i + 1];
        else if (args[i] == "--frame") ok = int.TryParse(args[i + 1], out shotFrame) && shotFrame >= 0;
        else if (args[i] == "--facing" && args[i + 1] == "left") shotFacingRight = false;
        else if (args[i] == "--facing" && args[i + 1] == "right") shotFacingRight = true;
        else ok = false;
    }
}
if (!ok)
{
    Console.WriteLine("usage: MushTown.Client [--shot <file.png> [--action <name>] [--frame <n 0-based>] [--facing left|right]]");
    Environment.ExitCode = 1;
    return;
}

try
{
    using var game = new MushTownGame(shotPath, shotAction, shotFrame, shotFacingRight);
    game.Run();
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"error: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (IOException ex)
{
    Console.WriteLine($"error: {ex.Message}");
    Environment.ExitCode = 1;
}
