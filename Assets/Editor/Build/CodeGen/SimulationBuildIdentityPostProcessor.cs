using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

/// <summary>
/// Embeds the production source identity before Unity loads or builds the game assembly.
/// </summary>
internal sealed class SimulationBuildIdentityPostProcessor : ILPostProcessor
{
    private const string _metadataKey = "SimulationBuild";
    private const string _attributeTypeName = "System.Reflection.AssemblyMetadataAttribute";
    private const string _legacyGeneratedPath = "Assets/Scripts/AssemblyBuildIdentity.g.cs";
    private static readonly string[] _dependencyInputs =
    {
        "Assets/Scripts/GameAssembly.asmdef",
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "ProjectSettings/ProjectVersion.txt",
    };

    /// <summary>
    /// Returns this stateless compiler processor.
    /// </summary>
    /// <returns>The processor used for the current compilation.</returns>
    public override ILPostProcessor GetInstance() => this;

    /// <summary>
    /// Selects the assembly containing gameplay and multiplayer admission.
    /// </summary>
    /// <param name="compiledAssembly">The compiler output under consideration.</param>
    /// <returns>True for the game assembly.</returns>
    public override bool WillProcess(ICompiledAssembly compiledAssembly) =>
        compiledAssembly.Name == "GameAssembly";

    /// <summary>
    /// Writes the source identity into assembly metadata while preserving debugging symbols.
    /// </summary>
    /// <param name="compiledAssembly">The game assembly emitted by the compiler.</param>
    /// <returns>The assembly with exactly one simulation build identity.</returns>
    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
    {
        using DefaultAssemblyResolver resolver = new();
        foreach (
            string directory in compiledAssembly.References.Select(Path.GetDirectoryName).Distinct()
        )
            resolver.AddSearchDirectory(directory);
        using MemoryStream input = new(compiledAssembly.InMemoryAssembly.PeData);
        using MemoryStream symbols = new(
            compiledAssembly.InMemoryAssembly.PdbData ?? Array.Empty<byte>()
        );
        bool hasSymbols = symbols.Length > 0;
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
            input,
            new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = hasSymbols,
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                SymbolStream = symbols,
            }
        );
        ModuleDefinition module = assembly.MainModule;
        TypeReference attributeType = module
            .GetTypeReferences()
            .Single(type => type.FullName == _attributeTypeName);
        for (int index = assembly.CustomAttributes.Count - 1; index >= 0; index--)
        {
            CustomAttribute attribute = assembly.CustomAttributes[index];
            if (
                attribute.AttributeType.FullName == _attributeTypeName
                && (string)attribute.ConstructorArguments[0].Value == _metadataKey
            )
                assembly.CustomAttributes.RemoveAt(index);
        }
        MethodReference constructor = new(".ctor", module.TypeSystem.Void, attributeType)
        {
            HasThis = true,
        };
        constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        CustomAttribute identity = new(constructor);
        identity.ConstructorArguments.Add(
            new CustomAttributeArgument(module.TypeSystem.String, _metadataKey)
        );
        identity.ConstructorArguments.Add(
            new CustomAttributeArgument(module.TypeSystem.String, ComputeIdentity())
        );
        assembly.CustomAttributes.Add(identity);

        using MemoryStream output = new();
        using MemoryStream outputSymbols = new();
        assembly.Write(
            output,
            new WriterParameters
            {
                WriteSymbols = hasSymbols,
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = outputSymbols,
            }
        );
        return new ILPostProcessResult(
            new InMemoryAssembly(output.ToArray(), outputSymbols.ToArray()),
            new List<DiagnosticMessage>()
        );
    }

    /// <summary>
    /// Hashes normalized source and dependency inputs independently of checkout root and line endings.
    /// </summary>
    /// <returns>The compiled source-input digest.</returns>
    private static string ComputeIdentity()
    {
        using MemoryStream bytes = new();
        foreach (
            string input in Directory
                .GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path != _legacyGeneratedPath)
                .Concat(_dependencyInputs)
                .OrderBy(path => path, StringComparer.Ordinal)
        )
        {
            byte[] part = Encoding.UTF8.GetBytes(
                input
                    + "\0"
                    + File.ReadAllText(input).Replace("\r\n", "\n").Replace('\r', '\n')
                    + "\0"
            );
            bytes.Write(part, 0, part.Length);
        }
        bytes.Position = 0;
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}
