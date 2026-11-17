using MushTown.Shared;

namespace MushTown.Client;

// A macro that is running: its skills cast one after another. A skill casts only
// while the pose is free, so the next one waits until the one before it has finished.
// A skill that cannot cast (no MP, cooling down, wrong place) sets no pose lock, so it is
// skipped at once and the next one goes on.
public sealed class MacroRun : Component
{
    private int[]? _skills;
    private int _next;

    // A macro key while one runs does nothing.
    public void Start(IReadOnlyList<int> skills)
    {
        if (_skills != null) return;
        _skills = skills.ToArray(); // a copy: editing the macro does not change the run
        _next = 0;
    }

    public void Stop() => _skills = null;

    // Once a frame: cast the next skills while the pose is free. The run ends when the last
    // skill has finished.
    public void Step(Func<bool> poseLocked, Action<int> cast)
    {
        while (_skills != null && !poseLocked())
        {
            if (_next == _skills.Length) { _skills = null; return; }
            int id = _skills[_next++];
            if (id != 0) cast(id);
        }
    }
}

// The macros of the character: the list window edits them, a bound key runs one.
public partial class MushTownGame
{
    private readonly MacroRun _macro = new();
    private (int Macro, int Slot)? _heldMacroSlot; // the macro slot the skill in hand came from: a click on the world clears it

    private MacroWindow _macroWindow = null!;

    // The skill window's MACRO NOTIFIER button.
    internal void OpenMacroList() => ShowWindow(HotkeyAction.MacroList);

    internal int MacroSkill(int macro, int slot) => Hero?.Macros?[macro][slot] ?? 0;

    internal bool MacroEmpty(int macro) => Hero == null || MacroBook.IsEmpty(Hero, macro);

    // A click on a macro slot with a skill in it and nothing in hand: the skill comes up.
    internal void PickMacroSlot(int macro, int slot)
    {
        int skill = MacroSkill(macro, slot);
        if (skill == 0 || !_hand.Empty) return;
        Hold(new BindTarget(BindKind.Skill, skill));
        _heldMacroSlot = (macro, slot);
        PlaySfx("click"); // the slot is silent: only a slot that gave something up clicks
    }

    // A click on a macro icon: the macro comes up (an empty macro stays).
    internal void PickMacro(int macro)
    {
        if (MacroEmpty(macro) || !_hand.Empty) return;
        Hold(new BindTarget(BindKind.Macro, macro));
        PlaySfx("click");
    }

    // 0 clears the slot; a skill that came from another macro slot moves (that slot empties).
    internal void PlaceMacroSkill(int macro, int slot, int skill, (int Macro, int Slot)? from = null)
    {
        MacroBook.Place(Hero!, macro, slot, skill, from); // the window takes no drop without a character
        SaveChara();
    }

    // The skill in hand lands on a slot.
    internal void DropMacroSkill(int macro, int slot, int skill) => PlaceMacroSkill(macro, slot, skill, _heldMacroSlot);

    // What a slot draws: nothing while its skill is in hand (the drop or the cancel brings it back).
    internal string MacroSlotIcon(int macro, int slot) =>
        _heldMacroSlot == (macro, slot) ? "" : SkillIconTex(MacroSkill(macro, slot));

    // A skill picked up from a macro slot was dropped on the world: the slot loses it.
    private bool CancelMacroHoldOnWorldClick(int gx, int gy, GameObject? top)
    {
        if (_heldMacroSlot is not { } from || _uiScene.ModalOpen || top != null || NpcAt(gx, gy) != null) return false;
        PlaceMacroSkill(from.Macro, from.Slot, 0);
        ClearHeld();
        PlaySfx("click");
        return true;
    }

    // A bound macro key runs its macro; every frame the run casts what is next.
    private void UpdateMacro(bool blocked)
    {
        if (blocked) { _macro.Stop(); return; }
        foreach (var action in _input.Actions)
            if (action.Target.Kind == BindKind.Macro && action.Fire && Hero is { } chara && !MacroBook.IsEmpty(chara, action.Target.Id))
                _macro.Start(chara.Macros![action.Target.Id]);
        _macro.Step(() => _fighter.AttackLock > 0, id => CastSkill(id, false));
    }
}
