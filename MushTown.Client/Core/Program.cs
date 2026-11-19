using System.Globalization;
using MushTown.Client;
using MushTown.Shared;

// UI numbers use English formatting: thousands separators stay ','. The HUD
// comma comes from Widgets.cs's GetSmall, not from the digit art.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// [--host | --join <ip>] [--shot <file.png> [--skin <0..5>] [--map <id>] [--travel <id>] [--action <name>] [--frame <n 0-based>] [--facing left|right] [--screen saves|server|chars|chars2|job|job2|setup|delete|inv|use|dropqty|dropqtymax|cashuserename|invscroll|stat|stat2|stat200|death|equipui|equipsetpart|decorations|damageskins|damageskinsempty|damageskinsselect|damageskinsdeleted|damageskinsask|dec|hud|fashionworn|skill|skillscroll|quest|questask|questforfeit|questpick|talk|settings|shop|shopscroll|shopnotice|minimap|minimapbig|minimapnpc|minimapfilter|worldmap|worldmapsearch|worldmapask|worldmapcombo|worldmapworld|worldmapmaps|worldmaproot|keys|quickedit|quickeditswap|quickeditnocap|menu|guide|guidecard|macro|macrohold|macrobind|macromove|party|partysearch|partyask|cashshop|cashshopfashion|cashshopfashionoutfits|cashshopreset|cashshopempty|cashshopcart|cashshopladder|cashshopbeauty|cashshopbeautygear|cashshopface|cashshopfacegear|cashshopcheckout|cashshopbuyall|cashshopnotice] [--skilltab <0|1|2|3|4|5>] [--talk <npc>] [--job <id>] [--hit | --skill <id> [--weak] [--flyms <ms> [--recast <ms>]] [--standoff <px>] | --drop | --hurt [--hurtblink | --hurtmiss] | --hurtpre | --teleport <left|right|up|down> --at <x>[,<y>] [--hold <ms>] [--teleportpre] [--teleportfrom <rope|swing>] | --manawave <boost|air|float> --at <x>[,<y>] [--hold <ms>]] [--dmg <n>] [--dmgskin <id>] [--crit] [--show] [--wear] [--wear-only <slots>] [--aiticks <n>] [--hover <x,y>]]
// --host runs a server plus this client; --join connects to a host. Net and
// --shot never mix (a screenshot has no peers). No argument means menus.
// --shot is headless: no window is ever shown, so no window takes the focus and no black window flashes (the shot is drawn
// into a render target and saved from there; MushTownGame.RunHidden). --show opens the window instead (full screen, shown
// until the shot is saved). The notes of each pose live in the comment blocks below and beside the shot block in Menu.cs.
// Adding or removing a --shot option touches three files: Program.cs (the parse, the checks and the usage), Menu.cs (the screen
// table and the pose block) and MushTownGame.cs (the gameShot list).
// --skin <0..5> picks the shot demo's skin (0 is the default).
// --map <id> picks the map. --frame <n> counts from 0; when it is not 0 it also
// overrides the frozen Spine frame, and the create-hero page uses it to pose the
// idle frame of the little character. --skilltab <0..5> picks the skill window
// page (0 the beginner page, 1..5 the pages I..V; default 1): the job pages hold
// the skills of the --job class, a job with no skills has empty pages.
// --hover <x,y> parks the mouse at that point (1366x768 game coordinates, not
// screen pixels); every tooltip is drawn from it.
// --hit plants the player mid-swing next to the first mob (game shots only).
// --dmg <n> poses one floating damage number with value n (game shots only);
// --dmgskin <id> draws that number, or the --hit number, with the given damage
// skin (0 = basic). --crit forces the posed number's critical art (and a
// --hit/--skill shot's build to 100% crit rate, so the real chain crits).
// --skill <id> poses that skill: a buff casts for real (loop + timer), a
// damage skill freezes mid-effect with judged hit sparks, a utility only
// poses its effect. --skill rides with --screen skill for window shots
// (buff timers, cooldowns); --skilltab picks the window tab.
// A WZ skill (Fire/Poison, ids 20xxxxx) follows --facing: left poses the caster
// right of the first mob, facing left. It stands 250 px from the first mob (the other poses 50), so a ball has room to fly. With --job (a game pose shot, no --screen) the demo is a fresh
// level 200 hero of that class, so the numbers are real ones. --aiticks runs BEFORE the
// WZ cast (the mobs gather around the caster), the cast is the last thing the shot does.
// --weak (with --skill) casts once first with a 100% Elemental Weakness chance and shows the second cast: the same
// rolls, with the mark on the mobs (+20 damage %). A shown cast never rolls the mark itself.
// --flyms <ms> (with --skill, a WZ skill that throws a ball) lets the world run that long after the cast, in 16 ms
// steps like the game: 0 (the default) shows the cast the moment it is thrown; a value before the body action's
// attack frame (Energy Bolt: 390 ms) shows only the cast effect; from that frame it shows the ball at the caster,
// the flight time (Projectile.FlightMs) shows the explosion, and the numbers come one server tick (100 ms) later.
// The big blue shape in front of the caster in these shots is the cast effect (the WZ effect node), not a second ball.
// --recast <ms> (with --flyms) then casts the same skill again at the same mobs (another seed, never a crit, so the two
// casts look different) and runs the world that long again: the numbers of the second cast start at the heads of the
// mobs while those of the first are still up, drawn over them.
// --standoff <px> (with --skill, a WZ skill) sets how far from the first mob the caster stands (default 250): a big
// number puts the caster out of reach, so the ball has no target and flies the whole range.
// --drop swings until the first kill and shows its drops (game shots only).
// --teleport <left|right|up|down> --at <x>[,<y>] poses a Fire/Poison hero (--job 210) standing at feet x <x> on the ground
// at feet height <y> (default: the map's spawn height; the ground nearest below <y> is used), the direction key held,
// casts Teleport that way for real (the landing rules, the MP, the effect) and shows the
// landing; --teleportpre is the same stage before the cast. --hold <ms> then keeps the key held that long (the hero
// walks on in that direction, as with a real key held), to show a wall stopping the walk. Walls, the map edge and the empty sky are posed with the
// map and the x: map 1010000, --at -200,215 right goes over the 60 px cliff onto the plateau (a cast is stopped only by a wall
// more than 75 px high), and the same stand with --teleportpre --hold 2000 walks into the cliff and stops at x -130; map 40000,
// --at -200 left stops at the map edge (x -260); --at -77 up has no ground above, so the cast is refused (the hero stays and keeps its MP).
// --teleportfrom <rope|swing> (with --teleport) is the stage the cast starts from: rope hangs the hero on the ladder or rope
// nearest to --at (it must be on a rope: x within 10 px of it, height inside it); swing starts a weapon swing (the attack lock
// is on) just before the cast. Teleport casts from both, and the landing is the usual one (the hero is off the rope).
// map 1010000: --teleport left --teleportfrom rope --at 432,100 lands on the floating platform at x 242.
// --manawave <boost|air|float> --at <x>[,<y>] [--hold <ms>] poses a Fire/Poison hero (--job 210) at that stand and plays Mana
// Wave for real (the Client's own check, the MP, the effect, the air state, the mover steps at 60 a second) and shows it
// <ms> after the move starts (default 250 for boost and air, 1000 for float): boost presses Up and Jump together on the ground
// (it shoots up); air jumps first and presses Up and Jump together in the air 100 ms later (the boost in the air); float boosts
// to the top (667 ms), lets the keys go and presses Jump again (no Up) and holds it (the slow fall, for the --hold ms; pick
// a stand with free air above it: map 1010000 --at 100,155). It prints the feet height and speed.
// --hurt makes the demo take one hit from a mob behind it and shows the knock
// arc in the normal-colour flash step. --hurtblink is the same arc in the
// purple flash step. --hurtpre is the same stage before the hit. --hurtmiss makes the
// touch dodge (the roll is forced to 0), so the shot shows the word Miss and no damage. The other --hurt
// shots force the roll to 1: they always take the hit.
// --wear puts the demo's whole EQUIP bag on; --wear-only <slots> keeps only
// the demo items whose slot is in a comma-separated list (game shots and screens).
// --aiticks <n> runs n 100ms AI steps at the posed feet (moves show, touches
// don't: the demo must not die mid-pose). Game shots only.
// --travel <id> travels to that map first (the world map travel), so a shot
// can show where the world map sent the player.
// --screen quest accepts custom 90001 with 2/5 kills (window + helper show);
// questask shows the Start teleport question, questforfeit the Forfeit ask,
// questpick selects 90001 and shows its detail panel.
// --screen talk needs --talk <npc> and opens that NPC's menu. Talk shots are
// game shots only. --screen shopscroll also needs --talk: it opens the shop
// with twelve stock rows scrolled to the bottom and the six bag tabs.
// --screen shopnotice also needs --talk: it opens the shop with the "Not enough mesos" notice.
// --screen invscroll fills the demo USE tab with 40 cells and scrolls the
// small window to its bottom. --screen cashuserename opens the
// CASH rename popup over the inventory. --screen equipui opens
// the equipment window; --screen equipsetpart is the same with only the hat, overall and wand worn (three of the six 8th Magician Set pieces); --screen decorations opens its Outfits page; --screen
// damageskins opens its Damage Skins page with a filled storage,
// damageskinsempty the same page with an empty storage, damageskinsselect
// poses a storage cell picked up in hand, damageskinsdeleted the page after that
// cell is deleted and damageskinsask its delete question; --screen dec opens
// only the inventory DEC tab. --screen dropqty opens the inventory
// with five potions held and the drop quantity popup; dropqtymax fills MAX.
// Anything else is a mistake, not something to ignore.
string? shotPath = null;
int shotMap = 10000;
int shotTravel = 0;
string shotAction = "stand1";
int shotFrame = 0;
bool shotFacingRight = false;
int? shotSkin = null;
bool host = false;
string? join = null;
string? shotScreen = null;
int shotTalk = 0;
int shotJob = 0;
int shotSkillTab = 1;
bool shotHit = false;
int shotSkill = 0;
bool shotDrop = false;
long shotDmg = 0;
int? shotDmgSkin = null;
bool shotCrit = false;
bool shotShow = false;
bool shotHurt = false;
bool shotHurtBlink = false;
bool shotHurtPre = false;
bool shotHurtMiss = false;
bool shotWeak = false;
int shotFlyms = 0;
int shotRecastMs = 0;
int shotStandoff = 0;
bool shotWear = false;
string? shotWearOnly = null;
string? shotTeleport = null;
string? shotTeleportFrom = null;
string? shotManaWave = null;
(int X, int? Y)? shotAt = null;
int shotHoldMs = 0;
bool shotTeleportPre = false;
int shotAiticks = 0;
(int X, int Y)? shotHover = null;
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
        else if (rest[i] == "--hurt") { shotHurt = true; i++; }
        else if (rest[i] == "--hurtblink") { shotHurtBlink = true; i++; }
        else if (rest[i] == "--hurtmiss") { shotHurtMiss = true; i++; }
        else if (rest[i] == "--hurtpre") { shotHurt = true; shotHurtPre = true; i++; }
        else if (rest[i] == "--wear") { shotWear = true; i++; }
        else if (rest[i] == "--weak") { shotWeak = true; i++; }
        else if (rest[i] == "--crit") { shotCrit = true; i++; }
        else if (rest[i] == "--show") { shotShow = true; i++; }
        else if (rest[i] == "--teleportpre") { shotTeleportPre = true; i++; }
        else if (i + 1 >= rest.Count) ok = false;
        else if (rest[i] == "--wear-only") { shotWearOnly = rest[i + 1]; i += 2; }
        else if (rest[i] == "--dmg") { ok = long.TryParse(rest[i + 1], out shotDmg) && shotDmg > 0; i += 2; }
        else if (rest[i] == "--dmgskin") { ok = int.TryParse(rest[i + 1], out int dmgSkin) && dmgSkin >= 0; if (ok) shotDmgSkin = dmgSkin; i += 2; }
        else if (rest[i] == "--teleportfrom") { ok = rest[i + 1] is "rope" or "swing"; shotTeleportFrom = rest[i + 1]; i += 2; }
        else if (rest[i] == "--manawave") { ok = rest[i + 1] is "boost" or "air" or "float"; shotManaWave = rest[i + 1]; i += 2; }
        else if (rest[i] == "--teleport") { ok = rest[i + 1] is "left" or "right" or "up" or "down"; shotTeleport = rest[i + 1]; i += 2; }
        else if (rest[i] == "--at")
        {
            string[] at = rest[i + 1].Split(',');
            int atX = 0, atY = 0;
            ok = at.Length is 1 or 2 && int.TryParse(at[0], out atX) && (at.Length == 1 || int.TryParse(at[1], out atY));
            if (ok) shotAt = (atX, at.Length == 2 ? atY : null);
            i += 2;
        }
        else if (rest[i] == "--hold") { ok = int.TryParse(rest[i + 1], out shotHoldMs) && shotHoldMs > 0; i += 2; }
        else if (rest[i] == "--skill") { ok = int.TryParse(rest[i + 1], out shotSkill) && shotSkill > 0; i += 2; }
        else if (rest[i] == "--talk") { ok = int.TryParse(rest[i + 1], out shotTalk) && shotTalk > 0; i += 2; }
        else if (rest[i] == "--job") { ok = int.TryParse(rest[i + 1], out shotJob) && shotJob > 0; i += 2; }
        else if (rest[i] == "--recast") { ok = int.TryParse(rest[i + 1], out shotRecastMs) && shotRecastMs > 0; i += 2; }
        else if (rest[i] == "--flyms") { ok = int.TryParse(rest[i + 1], out shotFlyms) && shotFlyms > 0; i += 2; }
        else if (rest[i] == "--standoff") { ok = int.TryParse(rest[i + 1], out shotStandoff) && shotStandoff > 0; i += 2; }
        else if (rest[i] == "--aiticks") { ok = int.TryParse(rest[i + 1], out shotAiticks) && shotAiticks > 0; i += 2; }
        else if (rest[i] == "--action") { shotAction = rest[i + 1]; i += 2; }
        else if (rest[i] == "--skin")
        {
            ok = int.TryParse(rest[i + 1], out int skin) && skin is >= 0 and < CharaSlot.SkinCount;
            if (ok) shotSkin = skin;
            i += 2;
        }
        else if (rest[i] == "--map") { ok = int.TryParse(rest[i + 1], out shotMap); i += 2; }
        else if (rest[i] == "--travel") { ok = int.TryParse(rest[i + 1], out shotTravel) && shotTravel > 0; i += 2; }
        else if (rest[i] == "--frame") { ok = int.TryParse(rest[i + 1], out shotFrame) && shotFrame >= 0; i += 2; }
        else if (rest[i] == "--facing" && rest[i + 1] == "left") { shotFacingRight = false; i += 2; }
        else if (rest[i] == "--facing" && rest[i + 1] == "right") { shotFacingRight = true; i += 2; }
        else if (rest[i] == "--hover" && rest[i + 1].Split(',') is [var hx, var hy] && int.TryParse(hx, out int px) && int.TryParse(hy, out int py)) { shotHover = (px, py); i += 2; }
        else if (rest[i] == "--screen" && (rest[i + 1] is "saves" or "server" or "chars" or "chars2" or "job" or "job2" or "setup" or "delete" or "inv" or "use" or "cashuserename" or "invscroll" or "stat" or "stat2" or "stat200" or "death" or "equipui" or "equipsetpart" or "decorations" or "damageskins" or "damageskinsempty" or "damageskinsselect" or "damageskinsdeleted" or "damageskinsask" or "dec" or "dropqty" or "dropqtymax" or "hud" or "fashionworn" or "skill" or "skillscroll" or "quest" or "questask" or "questforfeit" or "questpick" or "talk" or "settings" or "shop" or "shopscroll" or "shopnotice" or "minimap" or "minimapbig" or "minimapnpc" or "minimapfilter" or "worldmap" or "worldmapsearch" or "worldmapask" or "worldmapcombo" or "worldmapworld" or "worldmapmaps" or "worldmaproot" or "keys" or "quickedit" or "quickeditswap" or "quickeditnocap" or "menu" or "guide" or "guidecard" or "macro" or "macrohold" or "macrobind" or "macromove" or "party" or "partysearch" or "partyask" or "cashshop" or "cashshopfashion" or "cashshopfashionoutfits" or "cashshopreset" or "cashshopempty" or "cashshopcart" or "cashshopladder" or "cashshopbeauty" or "cashshopbeautygear" or "cashshopface" or "cashshopfacegear" or "cashshopcheckout" or "cashshopbuyall" or "cashshopnotice")) { shotScreen = rest[i + 1]; i += 2; }
        else if (rest[i] == "--skilltab") { ok = int.TryParse(rest[i + 1], out shotSkillTab) && shotSkillTab is >= 0 and <= 5; i += 2; }
        else ok = false;
    }
}
if (shotPath != null && (host || join != null)) ok = false;
if (shotWearOnly != null && !shotWear) ok = false; // --wear-only narrows --wear
if (shotDmgSkin != null && shotDmg == 0 && !shotHit) ok = false; // --dmgskin draws a --dmg or --hit number
if (shotCrit && !shotHit && shotSkill == 0 && shotDmg == 0) ok = false; // --crit rides a --dmg, --hit or --skill number
if ((shotTeleport != null || shotManaWave != null) != (shotAt != null)) ok = false; // --teleport or --manawave and --at come together
if (shotTeleportFrom != null && shotTeleport == null) ok = false; // --teleportfrom is the stage of the --teleport cast
if (shotHoldMs > 0 && shotManaWave == null && shotTeleport is null or "up" or "down") ok = false; // --hold keeps a Left/Right key held after the cast (or with --teleportpre, with no cast), or runs the --manawave move that long
if (shotTeleportPre && shotTeleport == null) ok = false; // --teleportpre is the stage before the --teleport cast
if ((shotHit || shotDrop || shotHurt || shotDmg > 0 || shotAiticks > 0 || shotTeleport != null || shotManaWave != null) && shotScreen != null) ok = false; // poses and AI are plain game shots: no screens
if (shotHurtBlink && !shotHurt) ok = false; // the blink shot is the hurt pose's second frame
if (shotHurtPre && shotHurtBlink) ok = false; // one hurt stage per shot
if (shotHurtMiss && (!shotHurt || shotHurtPre || shotHurtBlink)) ok = false; // the miss is the hurt pose's own hit, not the pre or blink stage
if (shotWeak && shotSkill == 0) ok = false; // --weak marks the mobs before the --skill cast
if (shotFlyms > 0 && shotSkill == 0) ok = false; // --flyms runs the world after the --skill cast
if (shotRecastMs > 0 && shotFlyms == 0) ok = false; // --recast casts again after the --flyms run
if (shotStandoff > 0 && shotSkill == 0) ok = false; // --standoff places the --skill caster
if (shotSkill != 0 && shotScreen != null && shotScreen != "skill") ok = false; // --skill rides only with the skill window
if ((shotHit ? 1 : 0) + (shotSkill != 0 ? 1 : 0) + (shotDrop ? 1 : 0) + (shotDmg > 0 ? 1 : 0) + (shotHurt ? 1 : 0) + (shotTeleport != null ? 1 : 0) + (shotManaWave != null ? 1 : 0) > 1) ok = false; // one pose per shot
if (shotScreen is not ("skill" or "skillscroll") && shotSkillTab != 1) ok = false; // --skilltab needs the skill window
if (shotTalk != 0 && shotScreen is not ("talk" or "shop" or "shopscroll" or "shopnotice")) ok = false; // --talk rides only with the talk/shop screens
if (shotJob != 0 && shotScreen is not ("job" or "job2" or "setup" or "stat" or "stat2" or "stat200" or "skill") && !(shotScreen == null && (shotHit || shotSkill != 0 || shotHurt || shotTeleport != null || shotManaWave != null))) ok = false; // --job rides only with the job screens, the stat window, the skill window and the game poses (--hit, --skill, --hurt, --teleport, --manawave)
if (shotScreen == "talk" && shotTalk == 0) ok = false; // the talk screen needs its NPC
if ((shotScreen is "shop" or "shopscroll" or "shopnotice") && shotTalk == 0) ok = false; // the shop screens need their keeper
if (shotTravel != 0 && shotScreen is not (null or "worldmap" or "worldmapsearch" or "worldmapask" or "worldmapcombo" or "worldmapworld" or "worldmapmaps" or "worldmaproot" or "minimap" or "minimapbig" or "minimapnpc" or "minimapfilter")) ok = false; // --travel needs the game, the world map or the minimap
if (!ok)
{
    Console.WriteLine("usage: MushTown.Client [--host | --join <ip>] | [--shot <file.png> [--skin <0..5>] [--map <id>] [--travel <id>] [--action <name>] [--frame <n 0-based>] [--facing left|right] [--screen saves|server|chars|chars2|job|job2|setup|delete|inv|use|dropqty|dropqtymax|cashuserename|invscroll|stat|stat2|stat200|death|equipui|equipsetpart|decorations|damageskins|damageskinsempty|damageskinsselect|damageskinsdeleted|damageskinsask|dec|hud|fashionworn|skill|skillscroll|quest|questask|questforfeit|questpick|talk|settings|shop|shopscroll|shopnotice|minimap|minimapbig|minimapnpc|minimapfilter|worldmap|worldmapsearch|worldmapask|worldmapcombo|worldmapworld|worldmapmaps|worldmaproot|keys|quickedit|quickeditswap|quickeditnocap|menu|guide|guidecard|macro|macrohold|macrobind|macromove|party|partysearch|partyask|cashshop|cashshopfashion|cashshopfashionoutfits|cashshopreset|cashshopempty|cashshopcart|cashshopladder|cashshopbeauty|cashshopbeautygear|cashshopface|cashshopfacegear|cashshopcheckout|cashshopbuyall|cashshopnotice] [--skilltab <0|1|2|3|4|5>] [--talk <npc>] [--job <id>] [--hit | --skill <id> [--weak] [--flyms <ms> [--recast <ms>]] [--standoff <px>] | --drop | --hurt [--hurtblink | --hurtmiss] | --hurtpre | --teleport <left|right|up|down> --at <x>[,<y>] [--hold <ms>] [--teleportpre] [--teleportfrom <rope|swing>] | --manawave <boost|air|float> --at <x>[,<y>] [--hold <ms>]] [--dmg <n>] [--dmgskin <id>] [--crit] [--show] [--wear] [--wear-only <slots>] [--aiticks <n>] [--hover <x,y>]]");
    Console.WriteLine("--shot is headless: no window is ever shown and the focus stays where it is. --show opens the window (full screen, shown until the shot is saved).");
    Environment.ExitCode = 1;
    return;
}

try
{
    using var game = new MushTownGame(shotPath, shotMap, shotTravel, shotAction, shotFrame, shotFacingRight, host, join, shotScreen, shotTalk, shotHit, shotSkill, shotSkillTab, shotDrop, shotAiticks, shotWear, shotJob, shotHover, shotSkin, shotDmg, shotWearOnly, shotHurt, shotHurtBlink, shotHurtPre, shotDmgSkin, shotHurtMiss, shotWeak, shotFlyms, shotStandoff, shotCrit, shotRecastMs, shotTeleport, shotAt, shotTeleportPre, shotHoldMs, shotManaWave, shotTeleportFrom);
    if (shotPath != null && !shotShow) game.RunHidden();
    else game.Run();
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
