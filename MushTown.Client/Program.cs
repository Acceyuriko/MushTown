using MushTown.Client;

// [--host | --join <ip>] [--shot <file.png> [--action <name>] [--frame <n 0-based>] [--facing left|right]]
// --host runs a server plus this client; --join connects to a host. Net and
// --shot never mix (a screenshot has no peers). No argument means solo play.
// Anything else is a mistake, not something to ignore.
string? shotPath = null;
string shotAction = "stand2";
int shotFrame = 0;
bool shotFacingRight = false;
bool host = false;
string? join = null;
var rest = args.ToList();
if (rest.Count >= 1 && rest[0] == "--host") { host = true; rest.RemoveAt(0); }
else if (rest.Count >= 2 && rest[0] == "--join") { join = rest[1]; rest.RemoveRange(0, 2); }
bool ok = rest.Count == 0;
if (rest.Count >= 2 && rest[0] == "--shot")
{
    shotPath = rest[1];
    ok = true;
    for (int i = 2; i < rest.Count && ok; i += 2)
    {
        if (i + 1 >= rest.Count) ok = false;
        else if (rest[i] == "--action") shotAction = rest[i + 1];
        else if (rest[i] == "--frame") ok = int.TryParse(rest[i + 1], out shotFrame) && shotFrame >= 0;
        else if (rest[i] == "--facing" && rest[i + 1] == "left") shotFacingRight = false;
        else if (rest[i] == "--facing" && rest[i + 1] == "right") shotFacingRight = true;
        else ok = false;
    }
}
if (shotPath != null && (host || join != null)) ok = false;
if (!ok)
{
    Console.WriteLine("usage: MushTown.Client [--host | --join <ip>] | [--shot <file.png> [--action <name>] [--frame <n 0-based>] [--facing left|right]]");
    Environment.ExitCode = 1;
    return;
}

try
{
    using var game = new MushTownGame(shotPath, shotAction, shotFrame, shotFacingRight, host, join);
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
