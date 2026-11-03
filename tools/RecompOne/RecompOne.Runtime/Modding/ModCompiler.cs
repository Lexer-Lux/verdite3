using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace RecompOne.Runtime.Modding;

public static class ModCompiler
{
    public static byte[]? Compile(string modId, IReadOnlyList<(string Path, string Text)> sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var trees = sources.Select(s =>
            CSharpSyntaxTree.ParseText(SourceText.From(s.Text, Encoding.UTF8), parseOptions, s.Path)).ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true)
            .WithOptimizationLevel(OptimizationLevel.Release);

        var compilation = CSharpCompilation.Create($"mod-{modId}", trees, References(), options);
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));
        if (!result.Success)
        {
            foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                Console.Error.WriteLine($"[Mods] {modId}: {diag}");
            return null;
        }

        return ms.ToArray();
    }

    private static unsafe List<MetadataReference> References()
    {
        var refs = new List<MetadataReference>();
        var pending = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var a))
        {
            if (a.IsDynamic || !seen.Add(a.FullName ?? a.GetName().Name ?? "")) continue;

            // Public mod APIs can depend on assemblies not JIT-loaded yet,
            // especially during headless startup. Resolve available managed
            // dependencies without running their initialization.
            foreach (var dependency in a.GetReferencedAssemblies())
                try { pending.Enqueue(Assembly.Load(dependency)); }
                catch (FileNotFoundException) { } // Optional/unavailable dependency.
                catch (FileLoadException) { }
                catch (BadImageFormatException) { }

            if (!string.IsNullOrEmpty(a.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(a.Location));
                continue;
            }

            //use the budnled dll instead of trying to find ones that odnt exist, should fix mod on single file publish
            if (a.TryGetRawMetadata(out var blob, out var length))
                refs.Add(
                    AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
        }

        // A later mod/overlay may add assemblies; never freeze the first snapshot.
        return refs;
    }
}
