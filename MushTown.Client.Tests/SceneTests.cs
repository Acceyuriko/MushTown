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

    // The rects a tree holds are local: Layout adds the parents' offsets.
    [TestMethod]
    public void Layout_NestsTheOffsets()
    {
        var scene = new Scene();
        var outer = new GameObject();
        outer.Add(new Rect(new Rectangle(100, 50, 80, 60)));
        var mid = new GameObject();
        mid.Add(new Rect(new Rectangle(10, 5, 40, 30)));
        var leaf = new GameObject();
        leaf.Add(new Rect(new Rectangle(2, 3, 4, 5)));
        outer.AddChild(mid);
        mid.AddChild(leaf);
        scene.Add(outer);
        scene.Layout();
        Assert.AreEqual(new Rectangle(112, 58, 4, 5), leaf.Get<Rect>()!.Screen);
        Assert.AreSame(leaf, scene.TopAt(113, 59)); // the hit test reads the screen box
    }

    // A window that only counts its builds.
    private sealed class StubWindow : UiWindow
    {
        public int Builds;
        public StubWindow() : base(HotkeyAction.Stat) { }
        public override Rectangle Rect() => new(0, 0, 40, 30);
        public override Rectangle TitleRect() => new(0, 0, 40, 10);
        public override Point LayoutPos() => new(0, 0);
        public override void Build()
        {
            Builds++;
            var child = new GameObject();
            child.Add(new Rect(new Rectangle(5, 5, 10, 10)));
            Host.AddChild(child);
        }
    }

    // The hit path skips a dead node: TreeDraw skips it too and TreeUpdate is
    // covered by WorldTests.
    [TestMethod]
    public void TopAt_SkipsADeadNode()
    {
        var scene = new Scene();
        var dead = Box(1);
        scene.Add(dead);
        dead.Dead = true;
        Assert.IsNull(scene.TopAt(5, 5));
        var live = Box(2);
        scene.Add(live);
        Assert.AreSame(live, scene.TopAt(5, 5)); // the dead one is still on the list
    }

    [TestMethod]
    public void TopAt_DeadModalDoesNotSwallowTheClick()
    {
        var scene = new Scene();
        var modal = new GameObject { Modal = true };
        var blocker = new GameObject();
        blocker.Add(new Rect(new Rectangle(0, 0, 10, 10)));
        modal.AddChild(blocker);
        scene.Add(modal);
        var target = Box(1);
        scene.Add(target);
        Assert.IsNull(scene.TopAt(5, 5)); // a live modal swallows everything below it
        modal.Dead = true;
        Assert.AreSame(target, scene.TopAt(5, 5));
    }

    // A move repoints the host rect: the tree stays as it was built.
    [TestMethod]
    public void Sync_MoveDoesNotRebuild()
    {
        var scene = new Scene();
        var w = new StubWindow { Pos = new Point(10, 10) };
        try
        {
            scene.Add(w.Host);
            w.Sync();
            scene.Layout();
            Assert.AreEqual(1, w.Builds);
            Assert.AreEqual(new Rectangle(10, 10, 40, 30), w.Host.Get<Rect>()!.Screen);
            var child = w.Host.Children[0];
            Assert.AreEqual(new Rectangle(15, 15, 10, 10), child.Get<Rect>()!.Screen);
            w.Pos = new Point(60, 20);
            w.Sync();
            scene.Layout();
            Assert.AreEqual(1, w.Builds); // no rebuild on a move
            Assert.AreEqual(new Rectangle(60, 20, 40, 30), w.Host.Get<Rect>()!.Screen);
            Assert.AreEqual(new Rectangle(65, 25, 10, 10), child.Get<Rect>()!.Screen);
        }
        finally
        {
            UiWindow.List.Remove(w); // the constructor registers: the game walks this list
        }
    }
}
