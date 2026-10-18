using System.Text.Json.Serialization;

namespace MushTown.Shared;

// Baked class-select clip (assets/spine/{id}/{id}.json): the exporter
// samples the Spine rig to plain per-frame meshes, the client only steps
// frames and draws triangle lists. Short keys keep the ~1MB numeric
// payloads small; declaration order sets the json key order.
public sealed class SpineFrame
{
    [JsonPropertyName("v")] public float[] V { get; set; } = Array.Empty<float>();
    [JsonPropertyName("c")] public float[] C { get; set; } = Array.Empty<float>();
    [JsonPropertyName("p")] public int P { get; set; }
    [JsonPropertyName("uv")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float[]? Uv { get; set; }
}

public sealed class SpineSlot
{
    [JsonPropertyName("blend")] public int Blend { get; set; }
    [JsonPropertyName("tris")] public int[] Tris { get; set; } = Array.Empty<int>();
    [JsonPropertyName("uvs")] public float[] Uvs { get; set; } = Array.Empty<float>();
    [JsonPropertyName("frames")] public List<SpineFrame> Frames { get; set; } = new();
}

public sealed class SpineData
{
    [JsonPropertyName("fps")] public float Fps { get; set; }
    [JsonPropertyName("pages")] public List<string> Pages { get; set; } = new();
    [JsonPropertyName("slots")] public List<SpineSlot> Slots { get; set; } = new();
}
