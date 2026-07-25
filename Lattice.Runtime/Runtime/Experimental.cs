using System;
using System.Collections.Generic;
using System.Linq;

namespace lattice.Runtime;

[Flags]
public enum ExperimentalFeature
{
    None        = 0,
    Jit         = 1 << 0,
    Heap        = 1 << 1,
    ManualMalloc = 1 << 2,
    GeneralizedJit = 1 << 3,
    NativeTranspile = 1 << 4,
}

public static class Experimental
{
    private static ExperimentalFeature _active = ExperimentalFeature.None;

    public static ExperimentalFeature Active => _active;

    public static void Set(ExperimentalFeature features) => _active = features;

    public static bool IsEnabled(ExperimentalFeature feature) => _active.HasFlag(feature);

    public static void Enable(ExperimentalFeature feature) => _active |= feature;
    public static void Disable(ExperimentalFeature feature) => _active &= ~feature;

    public static ExperimentalFeature Parse(IEnumerable<string> names)
    {
        ExperimentalFeature result = ExperimentalFeature.None;
        foreach (var name in names)
        {
            if (Enum.TryParse<ExperimentalFeature>(name, ignoreCase: true, out var parsed))
                result |= parsed;
            else
                Console.Error.WriteLine($"[experimental] Unknown feature '{name}'. Valid: {string.Join(", ", Enum.GetNames<ExperimentalFeature>().Where(n => n != "None"))}");
        }
        return result;
    }
}
