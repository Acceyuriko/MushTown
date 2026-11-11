using Microsoft.Xna.Framework.Input;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The input line: the typed characters, the key repeat and the two keys the
// window reads (Enter and Escape). The field holds no art, so a bare layout
// with the defaults is enough.
[TestClass]
public class TextFieldTests
{
    private static TextField Focused()
    {
        var field = new TextField(new PanelLayout());
        field.Focus(new KeyboardState());
        return field;
    }

    // One frame per call, 40 ms each, with A held down.
    private static void Hold(TextField field, int frames)
    {
        for (int i = 0; i < frames; i++) field.Read(new KeyboardState(Keys.A), 40);
    }

    [TestMethod]
    public void Read_TypesThePressedKeys()
    {
        var field = Focused();
        field.Read(new KeyboardState(Keys.H, Keys.I), 16);
        Assert.AreEqual("hi", field.Text); // the keys of one frame type in the order the state lists them
    }

    [TestMethod]
    public void Read_TypesUppercaseAndUnderscore()
    {
        var field = Focused();
        field.Read(new KeyboardState(Keys.LeftShift, Keys.A), 16);
        field.Read(new KeyboardState(), 16);
        field.Read(new KeyboardState(Keys.OemMinus), 16);
        field.Read(new KeyboardState(), 16);
        field.Read(new KeyboardState(Keys.LeftShift, Keys.OemMinus), 16);
        Assert.AreEqual("A-_", field.Text);
    }

    [TestMethod]
    public void Type_ReplaceOnTypeReplacesTheDefaultOnce()
    {
        var field = Focused();
        field.SetText("1");
        field.ReplaceOnType = true;
        field.Read(new KeyboardState(Keys.D5), 16);
        Assert.AreEqual("5", field.Text);
        field.Read(new KeyboardState(), 16); // the key comes up
        field.Read(new KeyboardState(Keys.D2), 16);
        Assert.AreEqual("52", field.Text);
    }

    // A held key types once, then waits 400 ms and repeats every 40 ms.
    [TestMethod]
    public void Read_RepeatsAHeldKeyAfterTheDelay()
    {
        var field = Focused();
        Hold(field, 1);
        Assert.AreEqual("a", field.Text);
        Hold(field, 8); // 360 ms in: still one
        Assert.AreEqual("a", field.Text);
        Hold(field, 1); // 400 ms: the second
        Assert.AreEqual("aa", field.Text);
        Hold(field, 1); // 40 ms more: the third
        Assert.AreEqual("aaa", field.Text);
    }

    [TestMethod]
    public void Read_EnterSubmitsAndEscapeLeaves()
    {
        var field = Focused();
        Assert.AreEqual(FieldKey.Submit, field.Read(new KeyboardState(Keys.Enter), 16));
        Assert.AreEqual(FieldKey.Leave, field.Read(new KeyboardState(Keys.Escape), 16));
        Assert.AreEqual("", field.Text); // neither key types
        Assert.AreEqual(FieldKey.None, field.Read(new KeyboardState(Keys.A), 16));
    }

    // The keys held when the field takes the focus are already down: the one
    // that opened the field must not type.
    [TestMethod]
    public void Focus_HeldKeysDoNotType()
    {
        var field = new TextField(new PanelLayout());
        field.Focus(new KeyboardState(Keys.A));
        field.Read(new KeyboardState(Keys.A), 16);
        Assert.AreEqual("", field.Text);
    }

    [TestMethod]
    public void Read_UnfocusedFieldTakesNoKeys()
    {
        var field = Focused();
        field.Unfocus();
        field.Read(new KeyboardState(Keys.A), 16);
        Assert.AreEqual("", field.Text);
    }

    [TestMethod]
    public void Read_BackspaceDeletesTheLastCharacter()
    {
        var field = Focused();
        field.Type('a');
        field.Type('b');
        field.Read(new KeyboardState(Keys.Back), 16);
        Assert.AreEqual("a", field.Text);
        field.Read(new KeyboardState(), 16); // the key comes up: the next press counts again
        field.Read(new KeyboardState(Keys.Back), 16);
        Assert.AreEqual("", field.Text);
        field.Read(new KeyboardState(), 16);
        field.Read(new KeyboardState(Keys.Back), 16); // an empty line loses nothing
        Assert.AreEqual("", field.Text);
    }

    // Leaving and coming back must not carry the old key over: the key held
    // through the change is down, not a press waiting to repeat.
    [TestMethod]
    public void Unfocus_ThenFocus_StopsTheOldRepeat()
    {
        var field = Focused();
        Hold(field, 10); // 400 ms: the repeat has started
        Assert.AreEqual("aa", field.Text);
        field.Unfocus();
        field.Focus(new KeyboardState(Keys.A));
        Hold(field, 1);
        Assert.AreEqual("aa", field.Text);
    }

    [TestMethod]
    public void Type_DigitsOnlyAndShortLimit()
    {
        var field = Focused();
        field.DigitsOnly = true;
        field.Limit = 3;
        field.Type('a');
        field.Type('7');
        field.Type('2');
        field.Type('9');
        field.Type('5');
        Assert.AreEqual("729", field.Text);
        field.SetText("12345");
        Assert.AreEqual("123", field.Text);
    }

    [TestMethod]
    public void Type_StopsAtTheLimit()
    {
        var field = Focused();
        for (int i = 0; i < 120; i++) field.Type('a');
        Assert.AreEqual(100, field.Text.Length);
    }
}
