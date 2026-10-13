using System.Security.Cryptography;
using System.Text.Json;

namespace MushTown.Shared;

// Local accounts: one file per PC (data/accounts.json), PBKDF2 passwords.
// The server never sees these; on LAN your character travels with you and
// same names on two PCs are the players' own fault.
public sealed class AccountStore
{
    public Dictionary<string, Account> Users { get; set; } = new();

    // Missing file means first run: start empty. Bad JSON fails loud.
    public static AccountStore Load(string path)
    {
        if (!File.Exists(path)) return new AccountStore();
        AccountStore store;
        try
        {
            store = JsonSerializer.Deserialize<AccountStore>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"corrupt save file: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"corrupt save file: {path}", ex);
        }
        if (store.Users == null)
            throw new InvalidDataException($"corrupt save file: {path}");
        foreach (var pair in store.Users)
            if (pair.Key != pair.Value?.User || pair.Value?.Slots is not { Length: 3 })
                throw new InvalidDataException($"corrupt save file: {path}");
        return store;
    }

    // Atomic write: a crash leaves the old file or the new one, never half.
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, path, true);
    }

    public Account Register(string user, string password)
    {
        CheckId(user, "username");
        CheckPassword(password);
        if (Users.ContainsKey(user))
            throw new InvalidDataException($"username {user} is taken");
        var account = new Account { User = user };
        account.SetPassword(password);
        Users[user] = account;
        return account;
    }

    public Account Login(string user, string password)
    {
        if (!Users.TryGetValue(user, out var account) || !account.CheckPassword(password))
            throw new InvalidDataException("wrong username or password");
        return account;
    }

    public CharaSlot CreateChara(string user, int slot, string name)
    {
        CheckId(name, "character name");
        if ((uint)slot >= 3)
            throw new InvalidDataException($"bad slot {slot}");
        foreach (var a in Users.Values)
            foreach (var s in a.Slots)
                if (s?.Name == name)
                    throw new InvalidDataException($"character name {name} is taken");
        var account = Users[user];
        if (account.Slots[slot] != null)
            throw new InvalidDataException($"slot {slot} is taken");
        var chara = new CharaSlot { Name = name };
        account.Slots[slot] = chara;
        return chara;
    }

    // One charset for usernames and character names: the client reuses this
    // while typing so the two can never drift apart.
    public static bool IsIdChar(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';

    private static void CheckId(string id, string what)
    {
        if (id.Length is < 1 or > 12 || !id.All(IsIdChar))
            throw new InvalidDataException($"bad {what} {id} (1-12 letters, digits, _ or -)");
    }

    private static void CheckPassword(string password)
    {
        if (password.Length is < 1 or > 32 || !password.All(c => c is >= ' ' and <= '~'))
            throw new InvalidDataException("bad password (1-32 printable ASCII)");
    }
}

public sealed class Account
{
    public string User { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Hash { get; set; } = "";
    public CharaSlot?[] Slots { get; set; } = new CharaSlot?[3];

    public void SetPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        Salt = Convert.ToBase64String(salt);
        Hash = Convert.ToBase64String(HashPassword(password, salt));
    }

    public bool CheckPassword(string password)
    {
        try
        {
            byte[] salt = Convert.FromBase64String(Salt);
            byte[] want = Convert.FromBase64String(Hash);
            if (salt.Length != 16 || want.Length != 32)
                throw new InvalidDataException($"corrupt password entry for {User}, fix the file or re-register");
            byte[] got = HashPassword(password, salt);
            return CryptographicOperations.FixedTimeEquals(want, got);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new InvalidDataException($"corrupt password entry for {User}, fix the file or re-register", ex);
        }
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
}

// One character. X/Y null means never played: spawn at the map portal.
// Equips null means the beginner set (real loadouts arrive with W17).
public sealed class CharaSlot
{
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Map { get; set; } = 10000;
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Mesos { get; set; }
    public int Str { get; set; } = 4;
    public int Dex { get; set; } = 4;
    public int Int { get; set; } = 4;
    public int Luk { get; set; } = 4;
    public List<string>? Equips { get; set; }
    public List<int> Skills { get; set; } = new();
    public List<int> Quests { get; set; } = new();
}
