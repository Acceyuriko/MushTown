using MushTown.Shared;

namespace MushTown.Client;

// One damage number in flight: the digits float up and fade over LifeMs, then
// the node ends its own life and the game pools it. The skin key picks the
// digit sets (0 = the base skin, NoRed0/NoRed1, for monster damage; -1 = NoViolet0/NoViolet1 for player hurt; any
// other key is a collected skin); Crit swaps in the NoCri0/NoCri1 sets and draws the skin's effect3 mark.
// The timing of a cast with several numbers lives here and only here: the numbers of one mob appear one after the
// other from the bottom up (each starts AppearStepMs after the one below, DelayMs), then each runs the same rise
// and fade, so they fade one after the other from the bottom up too. Where they stand is the spawn site's business:
// every number starts at the head of the mob it came from, and only the numbers of one mob and one attack (taken in
// the same frame) stack. A newer number is drawn over an older one (draw order), nothing replaces an old number.
public sealed class FloatingNumber : Component
{
    // The numbers below come from a clean official video (BV1qv4y1v7tK from 3:00: one mob, one attacker), measured in
    // .dsh/research/damage-number-display-v2.md (T-20261119091326). The recording shows the game window shrunk to
    // 1152x646; I take the game as 1366x768, a scale of 0.843. That scale is a GUESS (the recording window size is not
    // known); three numbers agree with it (the layer step and the rise speed match Journey, the v83 look-alike client,
    // and the big digit picture is 38 px). Not verified. The video has repeated frames: one frame is 33 ms, +-1 frame.
    // - Gap between two numbers of one mob: 3 or 4 frames, 3.6 on average, 30 samples, the same sequence in every cast
    //   of a skill: a constant 120 ms. High confidence.
    // - The k-th number stands on the fixed k-th layer, 26 video px = 31 game px apart (Journey: 30). Medium-high
    //   (3 casts); crit numbers look the same, not checked. In the video a new cast may replace the old numbers on the
    //   same layer (1 or 2 samples, low confidence): NOT done here.
    // - A number comes out at full size and full alpha (no fade-in, no pop) and floats up steadily until it is gone:
    //   26 video px/s = 31 game px/s (Journey: 31.25), medium-high (5 cycles).
    // - It is solid for 12.5 frames = 420 ms (medium), then fades about linearly: half at 22 to 23 frames, nothing at
    //   about 31 frames (1.03 s); to the eye it is gone at 0.9 s. Life taken as 1000 ms. Medium (2 cycles + 4 numbers).
    // - The lowest (oldest) number fades first, each by its own age (2 casts, by eye).
    // One set for every number (hits, the player hurt number, Miss).
    public const float HoldMs = 420; // solid for this long
    public const float FadeMs = 580; // then it fades linearly over this long
    public const float LifeMs = HoldMs + FadeMs;
    public const float RisePxPerSec = 31; // steady, from its start until it is gone
    public const int StackPx = 30;   // px between the numbers of one mob (the layers of a cast)
    public const int AppearStepMs = 120; // the next number of one mob appears this long after the one below
    public const int HurtSkin = -1;  // NoViolet0, kept apart from monster damage

    public const int NoMob = -1; // MobUid of a number that belongs to no mob (the player hurt number, Miss)

    // The layer a new number of this mob takes: how many numbers of the same mob were taken in this frame (the same
    // attack). Numbers of other mobs do not count, however close those mobs stand. Limit: on a network game two sources
    // that hit the same mob in the same frame stack as one attack; telling them apart needs an attack number in the hit
    // message (a protocol change, not made).
    public static int Layer(IEnumerable<FloatingNumber> numbers, int mobUid) => numbers.Count(n => n.Age == 0 && n.MobUid == mobUid);

    public string Text = "";
    public int MobUid = NoMob; // the mob this number came from: the numbers of one mob and one attack stack together
    public int Skin; // 0 = dmg.json, -1 = hurtdmg.json, other = dmg{key}.json
    public bool Crit; // draw the critical digit set with its effect3 mark
    public float X; // head position when the hit landed
    public float Y;
    public float Age; // ms since the node was spawned (it keeps counting while the number waits for its turn)
    public float DelayMs; // the number is not there until Age reaches this

    // A pooled number comes back to life: every field a spawn site does not
    // set has to start clean.
    public void Reset()
    {
        Age = 0;
        DelayMs = 0;
        MobUid = NoMob;
        Skin = 0;
        Crit = false;
        Owner.Dead = false;
    }

    public bool Visible => Age >= DelayMs;

    // Full for HoldMs, then it fades out linearly over FadeMs.
    public int Alpha => (int)(255 * Math.Clamp(1 - (Age - DelayMs - HoldMs) / FadeMs, 0f, 1f));

    // How far up it is now: it floats up steadily.
    public float Rise => RisePxPerSec * (Age - DelayMs) / 1000f;

    public override void Update(float dt)
    {
        Age += dt * 1000f;
        if (Age >= DelayMs + LifeMs) Owner.Dead = true;
    }
}
