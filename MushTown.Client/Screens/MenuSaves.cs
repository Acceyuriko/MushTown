using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Saves screen: three save rows, then Exit.
public partial class MushTownGame
{
    private static readonly string[] SaveNames = { "Astra", "Blackveil", "Crimson" };
    private static readonly string[] SaveBtnFiles = { "btn_astra.png", "btn_blackveil.png", "btn_crimson.png" };
    private int SaveRowH(int i) => MenuTex(i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png").Height;
    private int SaveRowsH()
    {
        int t = 0;
        for (int i = 0; i < SaveRows(); i++) t += SaveRowH(i);
        return t + MenuLayout.RowGap * (SaveRows() - 1);
    }
    private int SaveBodyH() => MenuLayout.BodyPadTop + SaveRowsH() + MenuLayout.BodyPadBottom + MenuLayout.MiddleExtraH;
    private Rectangle SaveRowRect(int i)
    {
        int bodyH = SaveBodyH();
        int y = BodyY(bodyH) + (bodyH - SaveRowsH()) / 2; // rows stay centered when the middle grows
        for (int k = 0; k < i; k++) y += SaveRowH(k) + MenuLayout.RowGap;
        var t = MenuTex(i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png");
        return MSR(new Rectangle((GameWidth - t.Width) / 2, y, t.Width, t.Height), bodyH);
    }

    private void BuildSavesScene()
    {
        _menuScene = new Scene();
        _menuScene.Add(MenuBg());
        int bodyH = SaveBodyH();
        var sign = new GameObject { NoHit = true };
        sign.Add(new Sign(bodyH));
        _menuScene.Add(sign);
        for (int i = 0; i < SaveRows(); i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            var b = MenuButton(SaveRowRect(i), "save" + i, () => ActivateSaveRow(r), ButtonKind.Click);
            string file = i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png";
            b.Add(new Picture("menu/" + file) { HoverAlpha = MenuAlpha.Hover, PressAlpha = MenuAlpha.Pressed });
            _menuScene.Add(b);
        }
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, PanelBottomS(bodyH) + 8, ErrRed));
        _menuScene.Add(err);
    }

    private void ToSaves()
    {
        _screen = Screen.Saves;
        _focus = "";
        BuildSavesScene();
        _scenes.Show(_menuScene);
    }

    // Three save rows, then Exit. Enter opens the first save with heroes
    // (or the first save when none have any yet).
    private void UpdateSaves(bool click, bool enter, int gx, int gy)
    {
        if (enter) { ActivateFirstSave(); return; }
        SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (click) ClickTree(_scenes.Current, gx, gy);
    }

    private int SaveRows() => SaveStore.MaxSaves + 1; // saves, then Exit

    private void ActivateFirstSave()
    {
        for (int i = 0; i < SaveStore.MaxSaves; i++)
            if (_store.Saves[i]!.Slots.Any(h => h != null)) { PickSave(i); return; }
        PickSave(0); // no heroes anywhere: Enter opens the first save
    }

    private void ActivateSaveRow(int i)
    {
        if (i < SaveStore.MaxSaves) { PickSave(i); return; }
        Exit();
    }

    // Save confirmed: pick a host next.
    private void PickSave(int slot)
    {
        _saveSlot = slot;
        _save = _store.Saves[slot];
        _hostPick = null;
        EnterServer();
    }
}
