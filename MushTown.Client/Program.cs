using MushTown.Client;

// [--host | --join <ip>] [--shot <file.png> [--map <id>] [--action <name>] [--frame <n 0-based>] [--facing left|right] [--screen login|server|chars|job|setup|register|delete|panel|inv|use|stat|hud|skill] [--hit | --slash]]
// --host runs a server plus this client; --join connects to a host. Net and
// --shot never mix (a screenshot has no peers). No argument means menus.
// --hit plants the player mid-swing next to the first mob (game shots only).
// --slash does the same with a Slash Blast (cast effect + hit sparks).
// Anything else is a mistake, not something to ignore.
string? shotPath = null;
int shotMap = 10000;
string shotAction = "stand2";
int shotFrame = 0;
bool shotFacingRight = false;
bool host = false;
string? join = null;
string? shotScreen = null;
bool shotHit = false;
bool shotSlash = false;
var rest = args.ToList();
if (rest.Count >= 1 && rest[0] == "--host") { host = true; rest.RemoveAt(0); }
else if (rest.Count >= 2 && rest[0] == "--join") { join = rest[1]; rest.RemoveRange(0, 2); }
bool ok = rest.Count == 0;
if (rest.Count >= 2 && rest[0] == "--shot")
{
    shotPath = rest[1];
    ok = true;
    for (int i = 2; i < rest.Count && ok;)
    {
        if (rest[i] == "--hit") { shotHit = true; i++; }
        else if (rest[i] == "--slash") { shotSlash = true; i++; }
        else if (i + 1 >= rest.Count) ok = false;
        else if (rest[i] == "--action") { shotAction = rest[i + 1]; i += 2; }
        else if (rest[i] == "--map") { ok = int.TryParse(rest[i + 1], out shotMap); i += 2; }
        else if (rest[i] == "--frame") { ok = int.TryParse(rest[i + 1], out shotFrame) && shotFrame >= 0; i += 2; }
        else if (rest[i] == "--facing" && rest[i + 1] == "left") { shotFacingRight = false; i += 2; }
        else if (rest[i] == "--facing" && rest[i + 1] == "right") { shotFacingRight = true; i += 2; }
        else if (rest[i] == "--screen" && (rest[i + 1] is "login" or "server" or "chars" or "job" or "setup" or "register" or "delete" or "panel" or "inv" or "use" or "stat" or "hud" or "skill")) { shotScreen = rest[i + 1]; i += 2; }
        else ok = false;
    }
}
if (shotPath != null && (host || join != null)) ok = false;
if ((shotHit || shotSlash) && shotScreen != null) ok = false; // --hit/--slash are plain game shots: no screens
if (shotHit && shotSlash) ok = false; // one pose per shot
if (!ok)
{
    Console.WriteLine("usage: MushTown.Client [--host | --join <ip>] | [--shot <file.png> [--map <id>] [--action <name>] [--frame <n 0-based>] [--facing left|right] [--screen login|server|chars|job|setup|register|delete|panel|inv|use|stat|hud|skill] [--hit | --slash]]");
    Environment.ExitCode = 1;
    return;
}

try
{
    using var game = new MushTownGame(shotPath, shotMap, shotAction, shotFrame, shotFacingRight, host, join, shotScreen, shotHit, shotSlash);
    game.Run();
}
catch (Exception ex) when (ex is InvalidDataException or IOException)
{
    WriteCrashFile(ex);
    Console.WriteLine($"error: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (Exception ex) // anything else: leave a crash file, then fail loud
{
    WriteCrashFile(ex);
    Console.WriteLine($"error: unexpected crash, see logs/crash-{Environment.ProcessId}.log");
    Environment.ExitCode = 1;
}

// WinExe has no console on double-click, so every failure also lands in a file.
void WriteCrashFile(Exception ex)
{
    try
    {
        Directory.CreateDirectory("logs");
        File.WriteAllText(Path.Combine("logs", $"crash-{Environment.ProcessId}.log"), ex.ToString());
    }
    catch (Exception cex) when (cex is IOException or UnauthorizedAccessException) { } // last resort: console still gets the message
}
