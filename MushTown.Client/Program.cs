using MushTown.Client;

// --shot <file.png>   save one screenshot of the first frame, then exit (weekly acceptance)
// No argument at all means "play". Anything else is a mistake, not something to ignore.
string? shotPath = null;
if (args.Length == 2 && args[0] == "--shot")
{
    shotPath = args[1];
}
else if (args.Length != 0)
{
    Console.WriteLine("usage: MushTown.Client [--shot <file.png>]");
    Environment.ExitCode = 1;
    return;
}

using var game = new MushTownGame(shotPath);
game.Run();
