using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// The key settings window art (StatusBar3.img/KeyConfig) and the options
// window art (StatusBar3.img/OptionMenu). The file names must match the
// client (MushTown.Client/Panels/HudKeyConfig.cs and HudSettings.cs).
public static partial class Export
{
    // One key of the official keyboard: the WZ keyPos spot and its cap art.
    private sealed class KeySpot
    {
        public int Code { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Cap { get; set; } = "";
    }

    // assets/ui/keys.json: where every key sits and which cap it uses.
    private sealed class KeyFile
    {
        public List<KeySpot> Keys { get; set; } = new();
    }

    // assets/ui/keys_filter.json: the filter names and the category of
    // every action icon.
    private sealed class FilterFile
    {
        public List<string> Filters { get; set; } = new();
        public Dictionary<int, int> Categories { get; set; } = new();
    }

    public static bool ExportKeyUi(WzRoot ui, string outDir)
    {
        string dir = Path.Combine(outDir, "ui");
        Directory.CreateDirectory(dir);

        // the key window: the frame, the official keyboard and the buttons
        if (!UiPng.Save(ui, "StatusBar3.img/KeyConfig/backgrnd", Path.Combine(dir, "keys_bg.png"))) return false;
        if (!UiPng.Save(ui, "StatusBar3.img/KeyConfig/backgrnd3", Path.Combine(dir, "keys_kb.png"))) return false;
        foreach (var (node, name) in new[]
        {
            ("StatusBar3.img/KeyConfig/button:presetSave", "keys_save"),
            ("StatusBar3.img/KeyConfig/button:Default", "keys_reset"),
            ("StatusBar3.img/KeyConfig/button:presetUndo", "keys_cancel"),
            ("StatusBar3.img/KeyConfig/button:presetCopy", "keys_copy"),
            ("StatusBar3.img/KeyConfig/button:presetApplication", "keys_apply"),
            ("StatusBar3.img/KeyConfig/button:customDefalultKey", "keys_custom"),
            ("StatusBar3.img/KeyConfig/button:presetPage1", "keys_pg1"),
            ("StatusBar3.img/KeyConfig/button:presetPage2", "keys_pg2"),
            ("StatusBar3.img/KeyConfig/button:presetPage3", "keys_pg3"),
            ("StatusBar3.img/KeyConfig/button:skill", "keys_skill"),
            ("StatusBar3.img/KeyConfig/button:keySetting", "keys_qslot"),
            ("StatusBar3.img/KeyConfig/button:unionKeySetting", "keys_combo"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        // the filter dropdown: the combo box is three pieces (left, middle, right)
        foreach (var (state, suffix) in new[] { ("normal", "n"), ("pressed", "p") })
            for (int piece = 0; piece < 3; piece++)
                if (!UiPng.Save(ui, $"Basic.img/ComboBox25/{state}/{piece}", Path.Combine(dir, $"keys_cbox_{suffix}{piece}.png"))) return false;

        // the action icons: the grid cells under the keyboard show them
        var icons = ui.Find("StatusBar3.img/KeyConfig/icon");
        if (icons == null)
        {
            Console.WriteLine("KeyConfig: icon missing");
            return false;
        }
        int iconCount = 0;
        var filterFile = new FilterFile();
        foreach (Wz_Node child in icons.Nodes)
        {
            if (!int.TryParse(child.Text, out int iconId)) continue;
            if (!UiPng.Save(ui, $"StatusBar3.img/KeyConfig/icon/{iconId}", Path.Combine(dir, $"keys_icon_{iconId}.png"))) return false;
            filterFile.Categories[iconId] = Int(child, "category");
            iconCount++;
        }

        // the filter names: the official combo box items
        var filterItems = ui.Find("StatusBar3.img/KeyConfig/combo:filter/contents/filter");
        if (filterItems != null)
            foreach (Wz_Node child in filterItems.Nodes)
                filterFile.Filters.Add(child.Nodes["string"]?.GetValueEx<string>("") ?? "");
        WriteJson(Path.Combine(dir, "keys_filter.json"), filterFile);

        // the key caps and the key positions (the cap id is the PC scancode)
        var pos = ui.Find("StatusBar3.img/KeyConfig/keyPos");
        var caps = ui.Find("StatusBar3.img/KeyConfig/key");
        if (pos == null || caps == null)
        {
            Console.WriteLine("KeyConfig: keyPos/key missing");
            return false;
        }
        var file = new KeyFile();
        foreach (Wz_Node child in pos.Nodes)
        {
            if (child.Value is not Wz_Vector v) continue;
            if (!int.TryParse(child.Text, out int code)) continue;
            string cap = "";
            if (caps.Nodes[child.Text] != null)
            {
                cap = $"keys_cap_{code}.png";
                if (!UiPng.Save(ui, $"StatusBar3.img/KeyConfig/key/{code}", Path.Combine(dir, cap))) return false;
            }
            file.Keys.Add(new KeySpot { Code = code, X = v.X, Y = v.Y, Cap = cap });
        }
        file.Keys.Sort((a, b) => a.Code.CompareTo(b.Code));
        WriteJson(Path.Combine(dir, "keys.json"), file);

        // the options window: the frame, the sound tab, the slider knob
        if (!UiPng.Save(ui, "StatusBar3.img/OptionMenu/backgrnd", Path.Combine(dir, "opt_bg.png"))) return false;
        if (!UiPng.Save(ui, "StatusBar3.img/OptionMenu/sound/layer:backgrnd", Path.Combine(dir, "opt_sound.png"))) return false;
        if (!UiPng.Save(ui, "StatusBar3.img/OptionMenu/tab/enabled/2", Path.Combine(dir, "opt_tab.png"))) return false;
        if (!UiPng.Save(ui, "Basic.img/Slider3/0", Path.Combine(dir, "opt_knob.png"))) return false;
        if (!UiPng.Save(ui, "StatusBar.img/BtShort/normal/0", Path.Combine(dir, "opt_shortcut.png"))) return false;
        foreach (var (node, name) in new[]
        {
            ("StatusBar3.img/OptionMenu/button:OK", "opt_ok"),
            ("StatusBar3.img/OptionMenu/button:Cancel", "opt_cancel"),
        })
            foreach (var (state, suffix) in new[] { ("normal", "n"), ("mouseOver", "h"), ("pressed", "p") })
                if (!UiPng.Save(ui, $"{node}/{state}/0", Path.Combine(dir, $"{name}_{suffix}.png"))) return false;

        Console.WriteLine($"key ui: {file.Keys.Count} keys, {file.Keys.Count(k => k.Cap != "")} caps, {iconCount} icons");
        return true;
    }
}
