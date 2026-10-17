using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Server screen: This PC first, then every host the LAN scan found.
public partial class MushTownGame
{
    private int ServerRowsH(int rows) => rows * MenuLayout.ServerRowH + MenuLayout.RowGap * (rows - 1);
    private int ServerBodyH(int rows) => MenuLayout.BodyPadTop + ServerRowsH(rows) + MenuLayout.BodyPadBottom + MenuLayout.MiddleExtraH;
    private Rectangle ServerRowRect(int i, int rows)
    {
        int bodyH = ServerBodyH(rows);
        return MSR(new Rectangle((GameWidth - MenuLayout.ServerRowW) / 2,
            BodyY(bodyH) + (bodyH - ServerRowsH(rows)) / 2 + i * (MenuLayout.ServerRowH + MenuLayout.RowGap),
            MenuLayout.ServerRowW, MenuLayout.ServerRowH), bodyH);
    }

    // Mode for StartGame: null pick = host here, else join that IP.
    private string ServerMode() => _hostPick == null ? "host" : _hostPick;

    private string ServerLabel() => _hostPick == null ? $"Host {Net.Port}" : _hostPick;

    private void BuildServerScene()
    {
        _menuScene = new Scene();
        _menuScene.Add(MenuBg());
        int rows = 2 + _hosts.Count; // This PC + LAN hosts + Cancel at the end
        int bodyH = ServerBodyH(rows);
        var sign = new GameObject { NoHit = true };
        sign.Add(new Sign(bodyH));
        _menuScene.Add(sign);
        for (int i = 0; i < rows; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            GameObject b;
            if (i == rows - 1)
            {
                b = MenuButton(ServerRowRect(i, rows), "server" + i, () => ToSaves(), ButtonKind.Click);
                b.Add(new CancelRow(MenuInk));
            }
            else
            {
                b = MenuButton(ServerRowRect(i, rows), "server" + i, () => EnterChars(r == 0 ? null : _hosts[r - 1].Ip), ButtonKind.Click);
                string name = r == 0 ? "This PC" : _hosts[r - 1].Name;
                string ip = r == 0 ? "host here" : _hosts[r - 1].Ip;
                b.Add(new ServerRow(name, ip, MenuInk, ListGray));
            }
            _menuScene.Add(b);
        }
        var hint = new GameObject { NoHit = true };
        hint.Add(new CenterText(() => _scanTask != null ? "Searching..." : "R: Refresh", 683, PanelBottomS(bodyH) + 8, Hint));
        _menuScene.Add(hint);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, PanelBottomS(bodyH) + 32, ErrRed));
        _menuScene.Add(err);
    }

    // This PC first, then every host the LAN scan found. Enter picks this
    // PC (the default row); R re-scans while waiting for a host to appear.
    private void UpdateServer(bool click, bool enter, bool esc, int gx, int gy, bool r)
    {
        if (_scanTask != null && _scanTask.IsCompleted) { _hosts = _scanTask.Result; _scanTask = null; BuildServerScene(); _scenes.Show(_menuScene); }
        if (esc) { ToSaves(); return; }
        if (enter) { EnterChars(null); return; }
        SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (click) ClickTree(_scenes.Current, gx, gy);
        if (_screen == Screen.Server && r && _scanTask == null) _scanTask = Discovery.ScanAsync(); // row clicks may have left this screen
    }

    // Reaching the server list always re-scans: the LAN may have changed.
    private void EnterServer()
    {
        _screen = Screen.Server;
        _focus = "";
        _hosts = new();
        _scanTask = Discovery.ScanAsync();
        BuildServerScene();
        _scenes.Show(_menuScene);
    }
}
