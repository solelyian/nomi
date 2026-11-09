using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Nomi;

// LLamaSharp 0.30 only ships a netstandard2.0 build for net8.0, whose loader does not probe
// runtimes/win-x64/native/<avx>. Load the matching CPU build first so its imports resolve to it.
public static class NativeLibraries
{
    private static readonly string[] Order = ["ggml-base", "ggml-cpu", "ggml", "llama", "mtmd"];
    private static readonly object Gate = new();
    private static bool loaded;

    public static string Variant() =>
        Avx512F.IsSupported ? "avx512" : Avx2.IsSupported ? "avx2" : Avx.IsSupported ? "avx" : "noavx";

    public static void Ensure()
    {
        lock (Gate)
        {
            if (loaded) return;
            var root = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
            foreach (var variant in new[] { Variant(), "avx2", "avx", "noavx" }.Distinct())
            {
                var folder = Path.Combine(root, variant);
                if (!File.Exists(Path.Combine(folder, "llama.dll"))) continue;
                try
                {
                    foreach (var name in Order) NativeLibrary.Load(Path.Combine(folder, name + ".dll"));
                    loaded = true;
                    return;
                }
                catch (DllNotFoundException) { }
                catch (BadImageFormatException) { }
            }
            loaded = true;
        }
    }
}
