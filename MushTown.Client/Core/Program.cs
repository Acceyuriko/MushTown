using MushTown.Client;

// [--host | --join <ip>] [--shot <file.png> [--map <id>] [--action <name>] [--frame <n 0-based>] [--facing left|right] [--screen saves|server|chars|chars2|job|job2|setup|delete|inv|use|stat|hud|skill|quest|talk|settings|shop] [--skilltab <0|1|2>] [--talk <npc>] [--job <id>] [--hit | --skill <id> | --drop] [--aiticks <n>]]
// --host runs a server plus this client; --join connects to a host. Net and
// --shot never mix (a screenshot has no peers). No argument means menus.
// --hit plants the player mid-swing next to the first mob (game shots only).
// --skill <id> poses that skill: a buff casts for real (loop + timer), a
// damage skill freezes mid-effect with judged hit sparks, a utility only
// poses its effect. --skill rides with --screen skill for window shots
// (buff timers, cooldowns); --skilltab picks the window tab.
// --drop swings until the first kill and shows its drops (game shots only).
// --wear puts the demo's whole EQUIP bag on (game shots and game screens).
// --aiticks <n> runs n 100ms AI steps at the posed feet (moves show, touches
// don't: the demo must not die mid-pose). Game shots only.
// --screen quest accepts custom 90001 with 2/5 kills (window + tracker show);
// --screen talk needs --talk <npc> and opens that NPC's menu. Talk shots are
// game shots only.
// Anything else is a mistake, not something to ignore.
string? shotPath = null;
int shotMap = 10000;
string shotAction = "stand1";
int shotFrame = 0;
bool shotFacingRight = false;
bool host = false;
string? join = null;
string? shotScreen = null;
int shotTalk = 0;
int shotJob = 0;
int shotSkillTab = 0;
bool shotHit = false;
int shotSkill = 0;
bool shotDrop = false;
bool shotWear = false;
int shotAiticks = 0;
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
        else if (rest[i] == "--drop") { shotDrop = true; i++; }
        else if (rest[i] == "--wear") { shotWear = true; i++; }
        else if (i + 1 >= rest.Count) ok = false;
        else if (rest[i] == "--skill") { ok = int.TryParse(rest[i + 1], out shotSkill) && shotSkill > 0; i += 2; }
        else if (rest[i] == "--talk") { ok = int.TryParse(rest[i + 1], out shotTalk) && shotTalk > 0; i += 2; }
        else if (rest[i] == "--job") { ok = int.TryParse(rest[i + 1], out shotJob) && shotJob > 0; i += 2; }
        else if (rest[i] == "--aiticks") { ok = int.TryParse(rest[i + 1], out shotAiticks) && shotAiticks > 0; i += 2; }
        else if (rest[i] == "--action") { shotAction = rest[i + 1]; i += 2; }
        else if (rest[i] == "--map") { ok = int.TryParse(rest[i + 1], out shotMap); i += 2; }
        else if (rest[i] == "--frame") { ok = int.TryParse(rest[i + 1], out shotFrame) && shotFrame >= 0; i += 2; }
        else if (rest[i] == "--facing" && rest[i + 1] == "left") { shotFacingRight = false; i += 2; }
        else if (rest[i] == "--facing" && rest[i + 1] == "right") { shotFacingRight = true; i += 2; }
        else if (rest[i] == "--screen" && (rest[i + 1] is "saves" or "server" or "chars" or "chars2" or "job" or "job2" or "setup" or "delete" or "inv" or "use" or "stat" or "equip" or "hud" or "skill" or "quest" or "talk" or "settings" or "shop" or "minimap" or "worldmap" or "keys" or "menu")) { shotScreen = rest[i + 1]; i += 2; }
        else if (rest[i] == "--skilltab") { ok = int.TryParse(rest[i + 1], out shotSkillTab) && shotSkillTab is >= 0 and <= 2; i += 2; }
        else ok = false;
    }
}
if (shotPath != null && (host || join != null)) ok = false;
if ((shotHit || shotDrop || shotAiticks > 0) && shotScreen != null) ok = false; // poses and AI are plain game shots: no screens
if (shotSkill != 0 && shotScreen != null && shotScreen != "skill") ok = false; // --skill rides only with the skill window
if ((shotHit ? 1 : 0) + (shotSkill != 0 ? 1 : 0) + (shotDrop ? 1 : 0) > 1) ok = false; // one pose per shot
if (shotScreen != "skill" && shotSkillTab != 0) ok = false; // --skilltab needs the skill window
if (shotTalk != 0 && shotScreen is not ("talk" or "shop")) ok = false; // --talk rides only with the talk/shop screens
if (shotJob != 0 && shotScreen is not ("job" or "job2")) ok = false; // --job rides only with the job screens
if (shotScreen == "talk" && shotTalk == 0) ok = false; // the talk screen needs its NPC
if (shotScreen == "shop" && shotTalk == 0) ok = false; // the shop screen needs its keeper
if (!ok)
{
    Console.WriteLine("usage: MushTown.Client [--host | --join <ip>] | [--shot <file.png> [--map <id>] [--action <name>] [--frame <n 0-based>] [--facing left|right] [--screen saves|server|chars|chars2|job|job2|setup|delete|inv|use|stat|hud|skill|quest|talk|settings|shop] [--skilltab <0|1|2>] [--talk <npc>] [--job <id>] [--hit | --skill <id> | --drop] [--wear] [--aiticks <n>]]");
    Environment.ExitCode = 1;
    return;
}

try
{
    using var game = new MushTownGame(shotPath, shotMap, shotAction, shotFrame, shotFacingRight, host, join, shotScreen, shotTalk, shotHit, shotSkill, shotSkillTab, shotDrop, shotAiticks, shotWear, shotJob);
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
