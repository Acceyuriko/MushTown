using MushTown.Shared;

namespace MushTown.Client;

// Doll asset loading: the two body sets, the gear parts shared by both, and
// the hair / face part sets that replace the baked defaults.
public partial class MushTownGame
{
    private void LoadDollAssets(List<int> artGears)
    {
        foreach (string action in Net.Actions)
        {
            var male = LoadBody(action, "");
            var female = LoadBody(action, "_f");
            _charasMale[action] = male;
            _charasFemale[action] = female;
            _delaysMale[action] = male.Frames.Select(f => f.Delay).ToList();
            _delaysFemale[action] = female.Frames.Select(f => f.Delay).ToList();
            foreach (int id in artGears) // wearable art: same frames as the body (delays ride the body file)
            {
                string imgId = id.ToString("D8");
                var gear = LoadJson<CharaData>(Path.Combine(_assetsDir, "chara", "gear", imgId, $"{action}.json"));
                if (gear.Frames.Count != male.Frames.Count || gear.Frames.Count != female.Frames.Count)
                    throw new InvalidDataException($"gear {imgId} {action} has {gear.Frames.Count} frames, want {male.Frames.Count}");
                if (gear.Frames.Any(f => f.Parts.Count == 0))
                    throw new InvalidDataException($"gear {imgId} {action} has an empty frame");
                _gear[(imgId, action)] = gear;
            }
        }
        LoadAvatarParts("hair", _hairs);
        LoadAvatarParts("face", _faces);
    }

    private CharaData LoadBody(string action, string suffix)
    {
        string file = $"{action}{suffix}.json";
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
                if (data.Frames.Count != _charasMale[action].Frames.Count)
                    throw new InvalidDataException(
                        $"{kind} {id} {action} has {data.Frames.Count} frames, want {_charasMale[action].Frames.Count}");
                target[(id, action)] = data;
            }
        }
    }
}
