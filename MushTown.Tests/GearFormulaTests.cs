using System.Text.Json;
using MushTown.Shared;

namespace MushTown.Tests;

// The six Pensalir / Utgard pieces of a new Fire/Poison Mage, read from the exported defs (assets/item), against
// the numbers of the local WZ (info keys incINT, incLUK, incSTR, incDEX, incPDD, incMAD, incMHP, incMMP, incSpeed, incJump):
//   1004230 hat    INT 16 LUK 15 PDD 110 MDD 150 ACC 100 EVA 50 MHP 150 MMP 150
//   1052800 robe   INT 20 LUK 20 PDD 150 MDD 150 MAD 1 MMP 60
//   1082609 gloves INT 11 LUK 11 PDD 40 MDD 40 MAD 2 MHP 200
//   1072968 shoes  INT 11 LUK 10 PDD 50 MDD 70 MAD 1 Speed 5
//   1102719 cape   STR/DEX/INT/LUK 4 PDD 150 MDD 80 Jump 4
//   1372207 wand   PAD 93 MAD 153 (attackSpeed 6)
[TestClass]
public class GearFormulaTests
{
    private static readonly Dictionary<int, ItemData> Defs = LoadSix();

    private static Dictionary<int, ItemData> LoadSix()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var defs = new Dictionary<int, ItemData>();
        foreach (int id in ItemData.StarterEquips)
        {
            string file = Path.Combine(root, "assets", "item", id + ".json");
            Assert.IsTrue(File.Exists(file), "needs exported assets (run: dotnet run --project MushTown.Exporter -- export)");
            defs[id] = JsonSerializer.Deserialize<ItemData>(File.ReadAllText(file))!;
        }
        return defs;
    }

    private static StatSnapshot Worn(IEnumerable<int> ids) =>
        StatAggregator.Aggregate(ItemData.EquipModifiers(ids.ToList(), Defs));

    private static readonly List<int> All = ItemData.StarterEquips.ToList();

    [TestMethod]
    public void EquipModifiers_SumTheSixPieces_PerKey()
    {
        var s = Worn(All);
        Assert.AreEqual(4, s.Value(StatKey.STR));   // cape
        Assert.AreEqual(4, s.Value(StatKey.DEX));   // cape
        Assert.AreEqual(62, s.Value(StatKey.INT));  // 16 + 20 + 11 + 11 + 4
        Assert.AreEqual(60, s.Value(StatKey.LUK));  // 15 + 20 + 11 + 10 + 4
        Assert.AreEqual(350, s.Value(StatKey.MAX_HP)); // 150 + 200
        Assert.AreEqual(210, s.Value(StatKey.MAX_MP)); // 150 + 60
        Assert.AreEqual(500, s.Value(StatKey.DEF));    // 110 + 150 + 40 + 50 + 150
        Assert.AreEqual(157, s.Value(StatKey.MATT));   // 1 + 2 + 1 + the wand's 153
        Assert.AreEqual(5, s.Percent(StatKey.SPEED));  // shoes
        Assert.AreEqual(4, s.Percent(StatKey.JUMP));   // cape
        Assert.IsTrue(s.Modifiers.All(m => m.Source == StatSource.Equipment)); // one hover group: Equipment
    }

    [TestMethod]
    public void EquipModifiers_NothingWornAddsNothing()
    {
        var s = StatAggregator.Aggregate(ItemData.EquipModifiers(null, Defs));
        foreach (var key in new[] { StatKey.STR, StatKey.DEX, StatKey.INT, StatKey.LUK, StatKey.MAX_HP, StatKey.MAX_MP, StatKey.DEF, StatKey.MATT })
            Assert.AreEqual(0, s.Value(key), key.ToString());
        Assert.AreEqual(0, s.Percent(StatKey.SPEED));
        Assert.AreEqual(0, s.Percent(StatKey.JUMP));
    }

    // Each piece gives only its own keys, so a half-dressed mage gets part of the numbers.
    [TestMethod]
    public void EquipModifiers_OnePieceGivesItsOwnKeys()
    {
        var shoes = Worn(new[] { 1072968 });
        Assert.AreEqual(11, shoes.Value(StatKey.INT));
        Assert.AreEqual(10, shoes.Value(StatKey.LUK));
        Assert.AreEqual(50, shoes.Value(StatKey.DEF));
        Assert.AreEqual(1, shoes.Value(StatKey.MATT));
        Assert.AreEqual(5, shoes.Percent(StatKey.SPEED));
        Assert.AreEqual(0, shoes.Percent(StatKey.JUMP));
        var cape = Worn(new[] { 1102719 });
        Assert.AreEqual(4, cape.Percent(StatKey.JUMP));
        Assert.AreEqual(4, cape.Value(StatKey.STR));
    }

    // Walk speed and jump are percent points of the base value: +5% and +4%.
    [TestMethod]
    public void SpeedAndJump_ArePercentOfTheBase()
    {
        var mods = ItemData.EquipModifiers(All, Defs).Concat(new[]
        {
            new StatModifier(StatKey.SPEED, StatValueType.FLAT, PlayerMover.WalkSpeed, StatSource.Base),
            new StatModifier(StatKey.JUMP, StatValueType.FLAT, PlayerMover.JumpV0, StatSource.Base),
        });
        var s = StatAggregator.Aggregate(mods);
        Assert.AreEqual(105.0, s.SpeedPercent, 1e-9);
        Assert.AreEqual(104.0, s.JumpPercent, 1e-9);
    }

    // The wand brings 5% crit rate by itself; no other weapon type is given a number (not confirmed).
    [TestMethod]
    public void Wand_HasFivePercentCrit()
    {
        Assert.AreEqual(WeaponType.ShortStaff, StatTables.WeaponTypeOf(1372207));
        Assert.AreEqual(5.0, StatTables.Weapon(WeaponType.ShortStaff).CritPct);
        foreach (var type in Enum.GetValues<WeaponType>().Where(t => t != WeaponType.ShortStaff))
            Assert.AreEqual(0.0, StatTables.Weapon(type).CritPct, type.ToString());
    }

    // The mage hits with magic attack and INT/LUK: the damage range follows the MATT and INT the gear adds,
    // and the physical attack does not move it.
    [TestMethod]
    public void MageDamage_UsesMagicAttackAndInt()
    {
        var job = StatTables.Job(StatTables.FirePoisonMage);
        StatSnapshot Dressed(IEnumerable<int> ids, double att) => StatAggregator.Aggregate(ItemData.EquipModifiers(ids.ToList(), Defs).Concat(new[]
        {
            new StatModifier(StatKey.INT, StatValueType.FLAT, 1000, StatSource.Base),
            new StatModifier(StatKey.LUK, StatValueType.FLAT, 100, StatSource.Base),
            new StatModifier(StatKey.MATT, StatValueType.FLAT, 50, StatSource.Base),
            new StatModifier(StatKey.ATT, StatValueType.FLAT, att, StatSource.Base),
        }));

        var naked = Dressed(Array.Empty<int>(), 10);
        var (_, nakedMax) = DamageChain.PanelRange(naked, job);
        Assert.AreEqual((long)(1.2 * (4 * 1000 + 100) * 50 / 100.0), nakedMax); // 1.2 x (4 INT + LUK) x MATT / 100

        var dressed = Dressed(All, 10);
        var (_, max) = DamageChain.PanelRange(dressed, job);
        Assert.AreEqual((long)(1.2 * (4 * 1062 + 160) * 207 / 100.0), max); // INT 1062, LUK 160, MATT 50 + 157
        Assert.IsTrue(max > nakedMax * 4);

        var (_, maxMoreAtt) = DamageChain.PanelRange(Dressed(All, 999), job);
        Assert.AreEqual(max, maxMoreAtt); // physical attack is not read
    }

    // The same Build before and after the gear: same mob, same skill percent, no crit roll, middle roll.
    [TestMethod]
    public void MageHit_GearRaisesTheDamageRange()
    {
        var before = new Build(1000, 100, 50, 0, 0, 0, Level: 200, WeaponType: (int)WeaponType.ShortStaff);
        var after = new Build(1062, 160, 207, 0, 0, 0, Level: 200, WeaponType: (int)WeaponType.ShortStaff);
        var b = DamageChain.Calculate(before, 0.5, 0.99, 0.0, 100);
        var a = DamageChain.Calculate(after, 0.5, 0.99, 0.0, 100);
        Assert.AreEqual(4100, b.StatValue);
        Assert.AreEqual(4408, a.StatValue);
        Assert.IsTrue(a.MaxPanel > b.MaxPanel * 4);
        Assert.IsTrue(a.TotalDamage > b.TotalDamage * 4);
    }
}
