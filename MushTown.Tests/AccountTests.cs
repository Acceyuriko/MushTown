using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class AccountTests
{
    [TestMethod]
    public void Register_Login_Roundtrips()
    {
        var store = new AccountStore();
        store.Register("bob", "secret");
        Assert.AreEqual("bob", store.Login("bob", "secret").User);
    }

    [TestMethod]
    public void Login_WrongPasswordOrUser_Throws()
    {
        var store = new AccountStore();
        store.Register("bob", "secret");
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "wrong"));
        Assert.ThrowsException<InvalidDataException>(() => store.Login("ann", "secret"));
    }

    [TestMethod]
    public void Register_DuplicateOrBad_Throws()
    {
        var store = new AccountStore();
        store.Register("bob", "secret");
        Assert.ThrowsException<InvalidDataException>(() => store.Register("bob", "other"));
        Assert.ThrowsException<InvalidDataException>(() => store.Register("", "secret"));
        Assert.ThrowsException<InvalidDataException>(() => store.Register("bob!", "secret"));
        Assert.ThrowsException<InvalidDataException>(() => store.Register("toolongusername", "secret"));
        Assert.ThrowsException<InvalidDataException>(() => store.Register("ann", ""));
        Assert.ThrowsException<InvalidDataException>(() => store.Register("ann", new string('x', 33)));
        Assert.ThrowsException<InvalidDataException>(() => store.Register("ann", "has\nnewline"));
        store.Register("abcdefghijkl", new string('y', 32)); // 12-char user and 32-char password are ok
        Assert.ThrowsException<InvalidDataException>(() => store.Register("abcdefghijklm", "secret"));
    }

    [TestMethod]
    public void CreateChara_ThreeSlots_NameUniquePerFile()
    {
        var store = new AccountStore();
        store.Register("bob", "secret");
        store.Register("ann", "secret");
        store.CreateChara("bob", 0, "hero");
        store.CreateChara("bob", 1, "hero2");
        store.CreateChara("bob", 2, "hero3");
        Assert.ThrowsException<InvalidDataException>(() => store.CreateChara("bob", 0, "other"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateChara("bob", 3, "other"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateChara("bob", -1, "neg"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateChara("ann", 0, "hero"));
        Assert.ThrowsException<InvalidDataException>(() => store.CreateChara("ann", 0, "bad name"));
        var chara = store.Login("bob", "secret").Slots[0]!;
        Assert.AreEqual("hero", chara.Name);
        Assert.AreEqual(1, chara.Level);
        Assert.AreEqual(0, chara.Exp);
        Assert.AreEqual(50, chara.Hp);
        Assert.AreEqual(5, chara.Mp);
        Assert.AreEqual(20, chara.Use![ItemData.RedPotion]); // starter kit
        Assert.AreEqual(10, chara.Use[ItemData.BluePotion]);
        Assert.IsNull(chara.X);
        Assert.IsNull(chara.Equips);
    }

    [TestMethod]
    public void Load_OldSlot_MissingVitalsDefault()
    {
        var slot = System.Text.Json.JsonSerializer.Deserialize<CharaSlot>("""{"Name":"hero"}""")!;
        Assert.AreEqual(1, slot.Level);
        Assert.AreEqual(0, slot.Exp);
        Assert.AreEqual(50, slot.Hp);
        Assert.AreEqual(5, slot.Mp);
        Assert.IsNull(slot.Use); // pre-W10 saves never got the starter kit
    }

    [TestMethod]
    public void DeleteChara_ClearsSlot()
    {
        var store = new AccountStore();
        store.Register("bob", "secret");
        store.CreateChara("bob", 0, "hero");
        store.DeleteChara("bob", 0);
        Assert.IsNull(store.Users["bob"].Slots[0]);
        store.DeleteChara("bob", 0); // deleting an empty slot is a no-op
        Assert.ThrowsException<InvalidDataException>(() => store.DeleteChara("bob", 3));
        Assert.ThrowsException<InvalidDataException>(() => store.DeleteChara("bob", -1));
        store.CreateChara("bob", 0, "hero"); // the freed name is reusable
        Assert.AreEqual("hero", store.Users["bob"].Slots[0]!.Name);
    }

    [TestMethod]
    public void Save_Load_RoundtripsPosition()
    {
        string path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AccountStore();
            store.Register("bob", "secret");
            var chara = store.CreateChara("bob", 0, "hero");
            chara.Map = 20000;
            chara.X = 300;
            chara.Y = 400;
            chara.Exp = 12;
            chara.Hp = 40;
            store.Save(path);
            Assert.IsFalse(File.Exists(path + ".tmp")); // atomic write leaves no tmp behind
            var back = AccountStore.Load(path);
            var slot = back.Login("bob", "secret").Slots[0]!;
            Assert.AreEqual(20000, slot.Map);
            Assert.AreEqual(300, slot.X);
            Assert.AreEqual(400, slot.Y);
            Assert.AreEqual(12, slot.Exp);
            Assert.AreEqual(40, slot.Hp);
            Assert.AreEqual(20, slot.Use![ItemData.RedPotion]); // int-keyed dict roundtrips
            Assert.AreEqual(10, slot.Use[ItemData.BluePotion]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_MissingFile_StartsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        Assert.AreEqual(0, AccountStore.Load(path).Users.Count);
    }

    [TestMethod]
    public void Save_MissingDirectory_CreatesIt()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}");
        string path = Path.Combine(dir, "accounts.json");
        try
        {
            var store = new AccountStore();
            store.Register("bob", "secret");
            store.Save(path); // first run: data/ does not exist yet
            Assert.AreEqual(1, AccountStore.Load(path).Users.Count);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Load_BadJson_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{not json");
            Assert.ThrowsException<InvalidDataException>(() => AccountStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Passwords_SamePassword_DifferentHashes()
    {
        var store = new AccountStore();
        var a = store.Register("bob", "secret");
        var b = store.Register("ann", "secret");
        Assert.AreNotEqual(a.Hash, b.Hash); // random salt per account
    }

    [TestMethod]
    public void Load_CorruptShape_ThrowsInvalidData()
    {
        string path = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Users":null}""");
            Assert.ThrowsException<InvalidDataException>(() => AccountStore.Load(path));
            File.WriteAllText(path, """{"Users":{"bob":{"User":"bob","Salt":"x","Hash":"y","Slots":[null,null]}}}""");
            Assert.ThrowsException<InvalidDataException>(() => AccountStore.Load(path));
            File.WriteAllText(path, """{"Users":{"bob":{"User":"bob","Salt":"x","Hash":"y","Slots":null}}}""");
            Assert.ThrowsException<InvalidDataException>(() => AccountStore.Load(path));
            File.WriteAllText(path, """{"Users":{"bob":{"User":"ann","Salt":"x","Hash":"y","Slots":[null,null,null]}}}""");
            Assert.ThrowsException<InvalidDataException>(() => AccountStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Register_NonAsciiPassword_Throws()
    {
        var store = new AccountStore();
        Assert.ThrowsException<InvalidDataException>(() => store.Register("ann", "密码secret"));
    }

    [TestMethod]
    public void Login_CorruptHash_ThrowsInvalidData()
    {
        var store = new AccountStore();
        store.Register("bob", "secret");
        store.Users["bob"].Hash = "!!!not-base64!!!";
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "secret"));
        store.Users["bob"].Salt = "";
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "secret"));
        store.Users["bob"].Hash = "";
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "secret"));
        store.Users["bob"].Hash = Convert.ToBase64String(new byte[32]);
        store.Users["bob"].Salt = "!!!not-base64!!!";
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "secret"));
        store.Users["bob"].Salt = Convert.ToBase64String(new byte[16]);
        store.Users["bob"].Hash = Convert.ToBase64String(new byte[8]); // valid base64, wrong length
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "secret"));
        store.Users["bob"].Hash = Convert.ToBase64String(new byte[32]);
        store.Users["bob"].Salt = Convert.ToBase64String(new byte[8]);
        Assert.ThrowsException<InvalidDataException>(() => store.Login("bob", "secret"));
    }

    [TestMethod]
    public void Session_Save_TryLoad_Roundtrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");
        try
        {
            new Session { User = "bob", Host = "10.0.0.5" }.Save(path);
            Assert.IsFalse(File.Exists(path + ".tmp")); // atomic write leaves no tmp behind
            var back = Session.TryLoad(path);
            Assert.IsNotNull(back);
            Assert.AreEqual("bob", back.User);
            Assert.AreEqual("10.0.0.5", back.Host);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Session_MissingOrCorrupt_TryLoadNull()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");
        Assert.IsNull(Session.TryLoad(missing)); // first run: no file, not an error
        string bad = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(bad, "{oops");
            Assert.IsNull(Session.TryLoad(bad)); // a bad cache logs out, never throws
        }
        finally
        {
            File.Delete(bad);
        }
    }

    [TestMethod]
    public void Session_OldFormat_LoadsAsOwnHost()
    {
        string path = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"User":"bob","Server":1,"Ip":"127.0.0.1"}""");
            var back = Session.TryLoad(path);
            Assert.IsNotNull(back);
            Assert.AreEqual("bob", back.User);
            Assert.IsNull(back.Host); // unknown props ignored: old picks become this PC
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Session_Save_MissingDirectory_CreatesIt()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}");
        string path = Path.Combine(dir, "session.json");
        try
        {
            new Session { User = "bob" }.Save(path); // first run: data/ does not exist yet (null host = this PC)
            Assert.AreEqual("bob", Session.TryLoad(path)!.User);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
