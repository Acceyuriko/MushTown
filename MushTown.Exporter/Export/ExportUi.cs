using MushTown.Shared;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// UI exports: lobby art, animated frame sets, name tags, cursor, HUD, digits.
public static partial class Export
{
    // Saves lobby art: (node path, file name). Char-select, job and setup
    // buttons carry real 3-state art (_n/_h/_p).
    // Must match Menu.cs UiFiles: every file there must be listed here.
    private static readonly (string node, string file)[] UiFiles = new[]
    {
        ("LoginBack.img/WorldSelect/default/0/0", "worldbg.png"),
        ("Login.img/CharSelect/selectWorld", "selectworld.png"),
        ("Login.img/ClassSelect/back/0/1/0", "job_bg.png"),
        ("Login.img/ClassSelect/list/backgrnd", "listbg.png"),
        ("Login.img/ClassSelect/list/class/layer:classCover", "job_cover.png"),
        ("Login.img/ClassSelect/list/pagePrev/2", "job_page_l.png"),
        ("Login.img/ClassSelect/list/pageNext/2", "job_page_r.png"),
        ("Login.img/ClassSelect/button:make/normal/0", "btn_make_n.png"),
        ("Login.img/ClassSelect/button:make/mouseOver/0", "btn_make_h.png"),
        ("Login.img/ClassSelect/button:make/pressed/0", "btn_make_p.png"),
        ("LoginBack.img/CustomizeChar/000/0/0", "setupbg.png"),
        ("Login.img/CustomizeChar/12200/board/avatarTop", "setup_panel.png"),
        ("Login.img/CustomizeChar/12200/board/boardBottom", "setup_panel_bot.png"),
        ("Login.img/CustomizeChar/12200/BtYes/normal/0", "setup_ok_n.png"),
        ("Login.img/CustomizeChar/12200/BtYes/mouseOver/0", "setup_ok_h.png"),
        ("Login.img/CustomizeChar/12200/BtYes/pressed/0", "setup_ok_p.png"),
        ("Login.img/CustomizeChar/12200/BtNo/normal/0", "setup_cancel_n.png"),
        ("Login.img/CustomizeChar/12200/BtNo/mouseOver/0", "setup_cancel_h.png"),
        ("Login.img/CustomizeChar/12200/BtNo/pressed/0", "setup_cancel_p.png"),
        ("Login.img/CharSelect/BtSelect/normal/0", "cs_select_n.png"),
        ("Login.img/CharSelect/BtSelect/mouseOver/0", "cs_select_h.png"),
        ("Login.img/CharSelect/BtSelect/pressed/0", "cs_select_p.png"),
        ("Login.img/CharSelect/BtNew/normal/0", "cs_new_n.png"),
        ("Login.img/CharSelect/BtNew/mouseOver/0", "cs_new_h.png"),
        ("Login.img/CharSelect/BtNew/pressed/0", "cs_new_p.png"),
        ("Login.img/CharSelect/BtDelete/normal/0", "cs_delete_n.png"),
        ("Login.img/CharSelect/BtDelete/mouseOver/0", "cs_delete_h.png"),
        ("Login.img/CharSelect/BtDelete/pressed/0", "cs_delete_p.png"),
        ("Login.img/CharSelect/BtDelete/sprite/0/0", "cs_delglass.png"),
        ("Login.img/Common/BtPreview/normal/0", "cs_back_n.png"),
        ("Login.img/Common/BtPreview/mouseOver/0", "cs_back_h.png"),
        ("Login.img/Common/BtPreview/pressed/0", "cs_back_p.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/normal/0", "cs_delyes_n.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/mouseOver/0", "cs_delyes_h.png"),
        ("Login.img/CharSelect/BtDeleteConfirm/pressed/0", "cs_delyes_p.png"),
        ("Login.img/CharSelect/BtDeleteCancel/normal/0", "cs_delno_n.png"),
        ("Login.img/CharSelect/BtDeleteCancel/mouseOver/0", "cs_delno_h.png"),
        ("Login.img/CharSelect/BtDeleteCancel/pressed/0", "cs_delno_p.png"),
        ("Login.img/CharSelect/slotJobBoard/closed/canvas:closeBackgrnd", "cs_board.png"),
        ("Login.img/CharSelect/adventure/0", "cs_pillar.png"),
        ("Login.img/CharSelect/page/backgrnd", "cs_page_bg.png"),
        ("Login.img/CharSelect/page/sprite:base/0/0", "cs_page_base.png"),
        ("Login.img/CharSelect/page/button:pageL/normal/0", "cs_page_l_n.png"),
        ("Login.img/CharSelect/page/button:pageL/mouseOver/0", "cs_page_l_h.png"),
        ("Login.img/CharSelect/page/button:pageL/pressed/0", "cs_page_l_p.png"),
        ("Login.img/CharSelect/page/button:pageR/normal/0", "cs_page_r_n.png"),
        ("Login.img/CharSelect/page/button:pageR/mouseOver/0", "cs_page_r_h.png"),
        ("Login.img/CharSelect/page/button:pageR/pressed/0", "cs_page_r_p.png"),
        ("Login.img/CharSelect/page/button:page1/normal/0", "cs_page_1_n.png"),
        ("Login.img/CharSelect/page/button:page1/mouseOver/0", "cs_page_1_h.png"),
        ("Login.img/CharSelect/page/button:page1/pressed/0", "cs_page_1_p.png"),
        ("Login.img/CharSelect/page/button:page2/normal/0", "cs_page_2_n.png"),
        ("Login.img/CharSelect/page/button:page2/mouseOver/0", "cs_page_2_h.png"),
        ("Login.img/CharSelect/page/button:page2/pressed/0", "cs_page_2_p.png"),
        ("Login.img/CharSelect/page/button:page3/normal/0", "cs_page_3_n.png"),
        ("Login.img/CharSelect/page/button:page3/mouseOver/0", "cs_page_3_h.png"),
        ("Login.img/CharSelect/page/button:page3/pressed/0", "cs_page_3_p.png"),
        ("Login.img/CharSelect/page/button:page4/normal/0", "cs_page_4_n.png"),
        ("Login.img/CharSelect/page/button:page4/mouseOver/0", "cs_page_4_h.png"),
        ("Login.img/CharSelect/page/button:page4/pressed/0", "cs_page_4_p.png"),
        ("Login.img/CharSelect/page/button:page5/normal/0", "cs_page_5_n.png"),
        ("Login.img/CharSelect/page/button:page5/mouseOver/0", "cs_page_5_h.png"),
        ("Login.img/CharSelect/page/button:page5/pressed/0", "cs_page_5_p.png"),
    };

    // Animated char-select overlays: (node path, file stem). Each numbered
    // child is one frame; ExportUiFrames writes ui/{stem}_{f}.png plus a
    // ui/{stem}.json of origins, delays, and fades.
    private static readonly (string node, string stem)[] UiFrameSets = new[]
    {
        ("Login.img/CharSelect/character/0", "cs_ghost"),
        ("Login.img/CharSelect/effect/0", "cs_portal"),
        ("Login.img/CharSelect/effect/1", "cs_motes"),
    };

    // Name tag plates, one per shipped ring (the ring's info/nameTag names
    // its node): 58 bamboo, 59 ink-and-wash, 129 island travel.
    private static readonly int[] NameTagNodes = ExportList.Load("nametags");

    // Basic damage skin digits: BasicEff.img NoRed0 0-9 (yellow; the set that
    // also carries Miss/guard words). Miss and the words stay for the week
    // that adds dodge and guard.
    // The OS cursor (Basic Cursor state 0, the gloved hand): the png plus
    // its WZ origin as the click hotspot (cursor.json). States 1/12/5 ride
    // along: button hover, pressed, inventory grab.
    private static bool ExportCursor(WzRoot ui, string outDir)
    {
        if (!UiPng.TryRead(ui, "UI", @"Basic.img\Cursor\0\0", out var png, out var origin) || origin == null)
        {
            Console.WriteLine("cursor 0 has no image or origin");
            return false;
        }
        WzNodeUtil.SavePng(png, Path.Combine(outDir, "ui", "cursor.png"));
        WriteJson(Path.Combine(outDir, "ui", "cursor.json"), new CursorData { X = origin.X, Y = origin.Y });
        Console.WriteLine($"cursor: {png.Width}x{png.Height} origin={origin.X},{origin.Y}");
        return ExportCursorSet(ui, outDir, 1, "hover")
            && ExportCursorSet(ui, outDir, 12, "click")
            && ExportCursorSet(ui, outDir, 5, "item");
    }

    // One animated cursor set (Basic Cursor <state>): frames plus per-frame
    // hotspots and delays. Single-frame sets (click) carry no delay.
    private static bool ExportCursorSet(WzRoot ui, string outDir, int state, string stem)
    {
        var set = WzNodeUtil.ResolveUol(ui.Find($@"Basic.img\Cursor\{state}"));
        if (set == null)
        {
            Console.WriteLine($"cursor {state} not found");
            return false;
        }
        var data = new CursorAnimData();
        for (int f = 0; ; f++)
        {
            var frame = WzNodeUtil.ResolveUol(set.Nodes[f.ToString()]);
            if (frame == null) break;
            var o = frame.Nodes["origin"]?.Value as Wz_Vector;
            if (o == null)
            {
                Console.WriteLine($"cursor {state}/{f}: no origin");
                return false;
            }
            string path = $"ui/cursor_{stem}_{f}.png";
            if (!UiPng.Save(ui, "UI", $@"Basic.img\Cursor\{state}\{f}", Path.Combine(outDir, path))) return false;
            data.Frames.Add(new CursorAnimFrame { Image = path, X = o.X, Y = o.Y, Delay = Int(frame, "delay", 0) });
        }
        if (data.Frames.Count == 0)
        {
            Console.WriteLine($"cursor {state}: no frames");
            return false;
        }
        if (data.Frames.Count > 1 && data.Frames.Any(f => f.Delay <= 0))
        {
            Console.WriteLine($"cursor {state}: a frame has no delay");
            return false;
        }
        WriteJson(Path.Combine(outDir, "ui", $"cursor_{stem}.json"), data);
        Console.WriteLine($"cursor {stem}: {data.Frames.Count} frames");
        return true;
    }

    // W9 HUD art: bottom bar (StatusBar3), inventory frame + tab pieces,
    // stat body. Buttons take normal/mouseOver/pressed; disabled never shows.
    // Must match Hud.cs: every file there must be listed here.
    private static bool ExportHud(WzRoot ui, string outDir)
    {
        string sb3 = "StatusBar3.img";
        var files = new List<(string node, string file)>
        {
            ($"{sb3}/main/namePlate/backgrnd", "hud_plate.png"),
            ($"{sb3}/main/namePlate/layer:lv", "hud_lvtag.png"),
            ($"{sb3}/main/status/normal/backgrnd", "hud_hpbg.png"),
            ($"{sb3}/main/status/normal/layer:cover", "hud_hpcover.png"),
            ($"{sb3}/main/status/normal/gauge/hp/layer:0", "hud_hpfill.png"),
            ($"{sb3}/main/status/normal/gauge/mp/layer:0", "hud_mpfill.png"),
            ($"{sb3}/mainBar/EXPBar/1366/layer:back", "hud_expback.png"),
            ($"{sb3}/mainBar/EXPBar/1366/layer:cover", "hud_expcover.png"),
            ($"{sb3}/mainBar/EXPBar/1366/layer:gauge", "hud_expgauge.png"),
            ($"{sb3}/mainBar/quickSlot/backgrnd", "hud_qslot.png"),
            ($"{sb3}/mainBar/quickSlot/layer:cover", "hud_qslotcover.png"),
            ($"{sb3}/KeyConfig/key/29", "hud_key_ctrl.png"),
            ($"{sb3}/KeyConfig/key/56", "hud_key_alt.png"),
            ($"{sb3}/KeyConfig/key/23", "hud_key_i.png"),
            ($"{sb3}/KeyConfig/key/46", "hud_key_c.png"),
            ("UIWindow.img/Item/BtCoin/normal/0", "hud_coin.png"),
            ("UICharacterInfo.img/common/detailStat/canvas:highlightLong", "hud_stathilong.png"),
            ("UICharacterInfo.img/common/main/button:close/normal/0", "hud_statx.png"),
        };
        for (int d = 0; d <= 9; d++)
            files.Add(($"{sb3}/main/namePlate/levelNum/{d}", $"hud_lv{d}.png"));
        for (int d = 0; d <= 9; d++)
            files.Add(($"{sb3}/main/status/normal/gauge/number/{d}", $"hud_num{d}.png"));
        // number's 11th child is "\" (the HP/MP separator): its name breaks
        // path lookup, so the client bakes "/" instead. No other symbols exist.
        (string state, string suffix)[] states = new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") };
        foreach ((string state, string suffix) in states)
        {
            files.Add(($"{sb3}/mainBar/menu/button:Character/{state}/0", $"hud_btn_char_{suffix}.png"));
            files.Add(($"{sb3}/mainBar/menu/button:Menu/{state}/0", $"hud_btn_menu_{suffix}.png"));
            files.Add(($"{sb3}/mainBar/submenu/character/button:character/{state}/0", $"hud_sub_char_{suffix}.png"));
            files.Add(($"{sb3}/mainBar/submenu/character/button:Item/{state}/0", $"hud_sub_item_{suffix}.png"));
        }
        for (int i = 0; i <= 2; i++)
            files.Add(($"{sb3}/mainBar/submenu/backgrnd/{i}", $"hud_sub_bg{i}.png"));
        foreach ((string node, string file) in files)
        {
            if (!UiPng.Save(ui, node, Path.Combine(outDir, "ui", file)))
                return false;
        }
        Console.WriteLine($"hud: {files.Count} files");
        return true;
    }

    private static bool ExportUiFrames(WzRoot ui, string outDir)
    {
        foreach ((string node, string stem) in UiFrameSets)
        {
            var set = WzNodeUtil.ResolveUol(ui.Find(node));
            if (set == null)
            {
                Console.WriteLine($"node not found: {node}");
                return false;
            }
            var data = new UiFramesData();
            int slash = node.LastIndexOf('/');
            var off = ui.Find(node[..slash] + "/offset" + node[(slash + 1)..])?.Value as Wz_Vector;
            data.OffX = off?.X ?? 0; // no offset node (ghost): anchor on the feet
            data.OffY = off?.Y ?? 0;
            for (int f = 0; ; f++)
            {
                var frame = WzNodeUtil.ResolveUol(set.Nodes[f.ToString()]);
                if (frame == null) break;
                var origin = frame.Nodes["origin"]?.Value as Wz_Vector;
                if (origin == null)
                {
                    Console.WriteLine($"{node}/{f}: no origin");
                    return false;
                }
                int delay = Int(frame, "delay", -1);
                if (delay < 0)
                {
                    Console.WriteLine($"{node}/{f}: no delay");
                    return false;
                }
                string path = $"ui/{stem}_{f}.png";
                if (!UiPng.Save(ui, "UI", $"{node}/{f}", Path.Combine(outDir, path)))
                    return false;
                int a0 = Int(frame, "a0", 255);
                data.Frames.Add(new UiFrame
                {
                    Image = path,
                    Ox = origin.X,
                    Oy = origin.Y,
                    Delay = delay,
                    A0 = a0,
                    A1 = Int(frame, "a1", a0),
                });
            }
            if (data.Frames.Count == 0)
            {
                Console.WriteLine($"{node}: no frames");
                return false;
            }
            WriteJson(Path.Combine(outDir, "ui", $"{stem}.json"), data);
            Console.WriteLine($"{stem}: {data.Frames.Count} frames off={data.OffX},{data.OffY}");
        }
        return true;
    }

    // Class roster (Login.img/ClassSelect): one illustration, card, name art,
    // and branch mark per class, in WZ card order, plus ui/jobs.json with the
    // one-line blurb and stats the page shows. The card container also holds
    // pos/space grid hints; only the numeric names are classes.
    private static bool ExportJobs(WzRoot ui, string outDir)
    {
        var cards = ui.Find("Login.img/ClassSelect/list/class/button:classEnabled");
        if (cards == null)
        {
            Console.WriteLine("node not found: class cards");
            return false;
        }
        var data = new JobListData();
        foreach (Wz_Node child in cards.Nodes)
        {
            if (!int.TryParse(child.Text, out int id)) continue;
            var info = ui.Find($"Login.img/ClassSelect/desc/info/{id}");
            if (info == null)
            {
                Console.WriteLine($"no desc/info for job {id}");
                return false;
            }
            JobData job;
            try
            {
                job = new JobData
                {
                    Id = id,
                    Art = $"ui/job_{id}_art.png",
                    Card = $"ui/job_{id}_card.png",
                    Name = $"ui/job_{id}_name.png",
                    Mark = $"ui/job_{id}_mark.png",
                    Sub = Text(info, "subName"),
                    Lines = Text(info, "desc").Replace("\\n", "\n").Split('\n').ToList(),
                    Race = Text(info, "race"),
                    Move = Text(info, "move"),
                    Stat = Text(info, "stat"),
                };
            }
            catch (InvalidDataException ex) // missing text fails like missing art: one line, no stack
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            // Every class art has its own origin and size (713x679 to 1443x992):
            // the page must draw each one where its origin says, or the small
            // ones hug the left edge and the big ones show a corner.
            if (!SaveArt(ui, outDir, $"Login.img/ClassSelect/back/1/{id}/0", job.Art, out var artOrigin)
                || !SaveArt(ui, outDir, $"Login.img/ClassSelect/list/class/button:classEnabled/{id}/normal/0", job.Card)
                || !SaveArt(ui, outDir, $"Login.img/ClassSelect/desc/info/{id}/className", job.Name)
                || !SaveArt(ui, outDir, $"Login.img/ClassSelect/desc/info/{id}/jobMark", job.Mark))
                return false;
            job.ArtX = artOrigin?.X ?? 0;
            job.ArtY = artOrigin?.Y ?? 0;
            data.Jobs.Add(job);
        }
        if (data.Jobs.Count == 0)
        {
            Console.WriteLine("no class cards");
            return false;
        }
        WriteJson(Path.Combine(outDir, "ui", "jobs.json"), data);
        Console.WriteLine($"jobs: {data.Jobs.Count} classes, first {data.Jobs[0].Id}");
        return true;
    }

    private static bool SaveArt(WzRoot ui, string outDir, string node, string file)
        => SaveArt(ui, outDir, node, file, out _);

    private static bool SaveArt(WzRoot ui, string outDir, string node, string file, out Wz_Vector? origin)
    {
        if (!UiPng.TryRead(ui, "UI", node, out var png, out origin)) return false;
        WzNodeUtil.SavePng(png, Path.Combine(outDir, file));
        return true;
    }

    private static string Text(Wz_Node node, string key) =>
        node.Nodes[key]?.Value as string ?? throw new InvalidDataException($"{node.FullPath}/{key}: no text");

    // Name tag plates: 3 slices + origins + name color per node.
    private static bool ExportNameTag(WzRoot ui, string outDir)
    {
        foreach (int node in NameTagNodes)
        {
            string tag = $"NameTag.img/{node}";
            if (!UiPng.TryRead(ui, "UI", $"{tag}/w", out var w, out var wO)
                || !UiPng.TryRead(ui, "UI", $"{tag}/c", out var c, out var cO)
                || !UiPng.TryRead(ui, "UI", $"{tag}/e", out var e, out var eO))
                return false;
            if (wO == null || cO == null || eO == null)
            {
                Console.WriteLine($"{tag}: a slice has no origin");
                return false;
            }
            int clr = Int(ui.Find(tag), "clr", int.MinValue);
            if (clr == int.MinValue)
            {
                Console.WriteLine($"{tag}: no clr");
                return false;
            }
            WzNodeUtil.SavePng(w, Path.Combine(outDir, "ui", "tag", $"ring{node}_w.png"));
            WzNodeUtil.SavePng(c, Path.Combine(outDir, "ui", "tag", $"ring{node}_c.png"));
            WzNodeUtil.SavePng(e, Path.Combine(outDir, "ui", "tag", $"ring{node}_e.png"));
            WriteJson(Path.Combine(outDir, "ui", "tag", $"ring{node}.json"), new NameTagData
            {
                Clr = clr,
                WOx = wO.X,
                WOy = wO.Y,
                COx = cO.X,
                COy = cO.Y,
                EOx = eO.X,
                EOy = eO.Y,
            });
            Console.WriteLine($"nametag {node}: {w.Width}x{w.Height}/{c.Width}x{c.Height}/{e.Width}x{e.Height} clr={clr}");
        }
        return true;
    }

    private static bool ExportDamageSkin(WzRoot effect, string outDir)
    {
        var data = new DmgData();
        for (int d = 0; d <= 9; d++)
        {
            string node = $"BasicEff.img/NoRed0/{d}";
            var origin = WzNodeUtil.ResolveUol(effect.Find(node))?.Nodes["origin"]?.Value as Wz_Vector;
            if (origin == null)
            {
                Console.WriteLine($"{node}: no origin");
                return false;
            }
            string path = $"effect/dmg/{d}.png";
            if (!UiPng.Save(effect, "Effect", node, Path.Combine(outDir, path)))
                return false;
            data.Digits.Add(new DmgDigit { Image = path, Oy = origin.Y });
        }
        WriteJson(Path.Combine(outDir, "effect", "dmg.json"), data);
        Console.WriteLine("damage skin: NoRed0 0-9");
        return true;
    }
}
