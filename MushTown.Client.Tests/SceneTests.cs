using Microsoft.Xna.Framework;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The one Z rule: roots are sorted by Z, draw goes forward and hit backward.
[TestClass]
public class SceneTests
{
    // A root with a hit box at the top left and a Z.
    private static GameObject Box(int z)
    {
        var o = new GameObject { Z = z };
        o.Add(new Rect(new Rectangle(0, 0, 10, 10)));
        return o;
    }

    [TestMethod]
    public void TopAt_TakesTheBiggestZ()
    {
        var scene = new Scene();
        var high = Box(9);
        var low = Box(1);
        scene.Add(high); // added first, still on top
        scene.Add(low);
        Assert.AreSame(high, scene.TopAt(5, 5));
    }

    [TestMethod]
    public void Move_RaisesTheRoot()
    {
        var scene = new Scene();
        var a = Box(1);
        var b = Box(2);
        scene.Add(a);
        scene.Add(b);
        Assert.AreSame(b, scene.TopAt(5, 5));
        scene.Move(a, 3); // a click raises a
        Assert.AreEqual(3, a.Z);
        Assert.AreSame(a, scene.TopAt(5, 5));
    }

    [TestMethod]
    public void Move_BeforeAddKeepsTheZ()
    {
        var scene = new Scene();
        var rival = Box(5);
        scene.Add(rival); // a root that is already in the tree
        var a = Box(1);
        scene.Move(a, 7); // not in the tree yet: the shots raise panels before the HUD is built
        Assert.AreEqual(7, a.Z);
        scene.Add(a);
        Assert.AreSame(a, scene.TopAt(5, 5));
    }

    [TestMethod]
    public void SameZ_LaterAddWins()
    {
        var scene = new Scene();
        var first = Box(3);
        var second = Box(3);
        scene.Add(first);
        scene.Add(second);
        Assert.AreSame(second, scene.TopAt(5, 5));
    }

    [TestMethod]
    public void TopAt_MissReturnsNull()
    {
        var scene = new Scene();
        scene.Add(Box(1));
        Assert.IsNull(scene.TopAt(50, 50));
    }

    [TestMethod]
    public void TopAt_ChildWinsOverTheParent()
    {
        var scene = new Scene();
        var parent = new GameObject();
        parent.Add(new Rect(new Rectangle(0, 0, 100, 100)));
        var child = new GameObject();
        child.Add(new Rect(new Rectangle(10, 10, 20, 20)));
        parent.AddChild(child);
        scene.Add(parent);
        Assert.AreSame(child, scene.TopAt(15, 15));
        Assert.AreSame(parent, scene.TopAt(90, 90));
    }

    [TestMethod]
    public void TopAt_ModalBlanksTheOutside()
    {
        var scene = new Scene();
        var modal = new GameObject { Z = 5, Modal = true };
        modal.Add(new Rect(new Rectangle(0, 0, 10, 10)));
        var behind = new GameObject { Z = 1 };
        behind.Add(new Rect(new Rectangle(40, 40, 20, 20))); // a hit box at the probe point
        scene.Add(behind);
        scene.Add(modal);
        Assert.IsNull(scene.TopAt(50, 50)); // the modal swallows the world behind it
        Assert.AreSame(modal, scene.TopAt(5, 5));
    }
}
