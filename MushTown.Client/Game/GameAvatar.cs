using MushTown.Shared;

namespace MushTown.Client;

// Doll asset loading: six skins sharing one body outline, the gear parts,
// and the hair / face part sets that replace the baked defaults.
public partial class MushTownGame
{
    private void LoadDollAssets(List<int> artGears)
    {
        foreach (string action in Net.Actions)
        {
            var first = LoadBody(action, 0);
            _charas[(0, action)] = first;
            _delays[(0, action)] = first.Frames.Select(f => f.Delay).ToList();
            for (int skin = 1; skin < CharaSlot.SkinCount; skin++)
            {
                var body = LoadBody(action, skin);
                if (body.Frames.Count != first.Frames.Count)
                    throw new InvalidDataException(
                        $"skin {skin} {action} has {body.Frames.Count} frames, want {first.Frames.Count}");
                _charas[(skin, action)] = body;
                _delays[(skin, action)] = body.Frames.Select(f => f.Delay).ToList();
            }
            foreach (int id in artGears) // wearable art: same frames as the body (delays ride the body file)
            {
                string imgId = id.ToString("D8");
                var gear = LoadJson<CharaData>(Path.Combine(_assetsDir, "chara", "gear", imgId, $"{action}.json"));
                if (gear.Frames.Count != first.Frames.Count)
                    throw new InvalidDataException($"gear {imgId} {action} has {gear.Frames.Count} frames, want {first.Frames.Count}");
                if (gear.Frames.Any(f => f.Parts.Count == 0))
                    throw new InvalidDataException($"gear {imgId} {action} has an empty frame");
                _gear[(imgId, action)] = gear;
            }
        }
        LoadAvatarParts("hair", _hairs);
        LoadAvatarParts("face", _faces);
    }

    private CharaData LoadBody(string action, int skin)
    {
        string file = skin == 0 ? $"{action}.json" : $"{action}_s{skin}.json";
        var chara = LoadJson<CharaData>(Path.Combine(_assetsDir, "chara", "body", file));
        if (chara.Frames.Count == 0)
            throw new InvalidDataException($"character {file} has no frames");
        if (chara.Frames.Any(f => f.Delay <= 0))
            throw new InvalidDataException($"character {file} has a bad delay");
        if (chara.Frames.Any(f => f.Parts.Count == 0))
            throw new InvalidDataException($"character {file} has an empty frame");
        return chara;
    }

    // One folder per cosmetic id, one json per body action. Every action must
    // have the body's frame count; a face may leave a back-facing frame empty.
    private void LoadAvatarParts(string kind, Dictionary<(string img, string action), CharaData> target)
    {
        string root = Path.Combine(_assetsDir, "chara", kind);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"missing exported avatar parts: {Path.GetFullPath(root)} (run: export)");
        var dirs = Directory.GetDirectories(root);
        if (dirs.Length == 0)
            throw new InvalidDataException($"no {kind} avatar parts exported");
        foreach (string dir in dirs)
        {
            string id = Path.GetFileName(dir);
            if (!int.TryParse(id, out _))
                throw new InvalidDataException($"bad {kind} folder name {id}");
            foreach (string action in Net.Actions)
            {
                var data = LoadJson<CharaData>(Path.Combine(dir, $"{action}.json"));
                if (data.Frames.Count != _charas[(0, action)].Frames.Count)
                    throw new InvalidDataException(
                        $"{kind} {id} {action} has {data.Frames.Count} frames, want {_charas[(0, action)].Frames.Count}");
                target[(id, action)] = data;
            }
        }
    }
}
