using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Rebellion.Analyzers.Tests
{
    [TestFixture]
    public sealed class GameModelBoundaryAnalyzerTests
    {
        private const string _modelPath = "Assets/Scripts/Game/Units/Unit.cs";
        private const string _rootSource = """
            namespace Rebellion.Game
            {
                public sealed class GameRoot
                {
                    public int Tick => 0;

                    [System.Obsolete(nameof(Tick))]
                    public int PreviousTick => Tick;
                }
            }
            """;

        private const string _supportSource = """
            public static class Runtime
            {
                public static Rebellion.Game.GameRoot Game => new();
                public static event System.EventHandler Changed;
                public static System.Action GetCallback() => () => { };
                public static void Register(System.Action callback) { }
            }
            """;

        [TestCase("Rebellion.Game.GameRoot _game;")]
        [TestCase("object GameType => typeof(global::Rebellion.Game.GameRoot);")]
        [TestCase("System.Collections.Generic.List<Rebellion.Game.GameRoot> _games;")]
        [TestCase("int ReadTick() => Runtime.Game.Tick;")]
        [TestCase("void ReadGame() { var game = Runtime.Game; }")]
        public async Task GameRoot_ReferencedByModel_ReportsDiagnosticAsync(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                $"class Unit {{ {member} }}",
                _modelPath
            );

            Assert.AreEqual(
                GameModelBoundaryAnalyzer.GameRootDiagnosticId,
                diagnostics.Single().Id
            );
        }

        [Test]
        public async Task GameRoot_AliasedInModel_ReportsBothReferencesAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "using Campaign = Rebellion.Game.GameRoot; class Unit { Campaign _game; }",
                @"C:\repo\Assets\Scripts\Game\Units\Unit.cs"
            );

            Assert.AreEqual(2, diagnostics.Length);
            Assert.IsTrue(
                diagnostics.All(diagnostic =>
                    diagnostic.Id == GameModelBoundaryAnalyzer.GameRootDiagnosticId
                )
            );
        }

        [TestCase("Missions/Mission.cs")]
        [TestCase("GameEvents/GameEvent.cs")]
        [TestCase("Scripting/GameTrigger.cs")]
        public async Task GameRoot_InMissionOrEventFile_AllowsReferenceOnlyAsync(string file)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Unit { Rebellion.Game.GameRoot _game; System.Action _callback; }",
                "Assets/Scripts/Game/" + file
            );

            Assert.AreEqual(
                GameModelBoundaryAnalyzer.CallbackDiagnosticId,
                diagnostics.Single().Id
            );
        }

        [Test]
        public async Task GameRoot_RootDeclarationAndSameNamedType_AreAllowedAsync()
        {
            const string source = """
                namespace Other
                {
                    class GameRoot { }
                    class Unit { GameRoot _otherRoot; }
                }
                """;

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, _modelPath);

            Assert.IsEmpty(diagnostics);
        }

        [TestCase("event System.Action Changed;")]
        [TestCase("event System.EventHandler Changed { add { } remove { } }")]
        [TestCase("System.Action<int> _callback;")]
        [TestCase("void Inspect(System.Func<int, bool> predicate) { }")]
        [TestCase("System.EventHandler _handler;")]
        [TestCase("delegate void Listener();")]
        [TestCase("void Inspect() { var callback = Runtime.GetCallback(); }")]
        [TestCase("void Listen() { Runtime.Register(() => { }); }")]
        [TestCase("void Listen() { Runtime.Register(Handle); } void Handle() { }")]
        [TestCase("void Listen() { Runtime.Changed += (sender, args) => { }; }")]
        [TestCase(
            "void StopListening() { Runtime.Changed -= Handle; } void Handle(object sender, System.EventArgs args) { }"
        )]
        public async Task NativeCallback_InGameDirectory_ReportsDiagnosticAsync(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                $"class Unit {{ {member} }}",
                _modelPath
            );

            Assert.AreEqual(
                GameModelBoundaryAnalyzer.CallbackDiagnosticId,
                diagnostics.Single().Id
            );
        }

        [TestCase("Assets/Scripts/Gameplay/Command.cs")]
        [TestCase("Assets/Scripts/GameTools/Unit.cs")]
        public async Task ArchitectureRules_OutsideGameDirectory_AllowsReferencesAsync(string path)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Unit { Rebellion.Game.GameRoot _game; event System.Action Changed; }",
                path
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task NativeCallback_InlineCollectionPredicate_AllowsCollectionOperationAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Unit { bool HasValue(int[] values) => System.Array.Exists(values, value => value > 0); }",
                _modelPath
            );

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>
        /// Runs the architecture analyzer against one source file and its referenced types.
        /// </summary>
        /// <param name="source">The C# source to analyze.</param>
        /// <param name="path">The file path used to scope the rules.</param>
        /// <returns>The analyzer diagnostics.</returns>
        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
            string source,
            string path
        )
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "AnalyzerTests",
                new[]
                {
                    CSharpSyntaxTree.ParseText(source, path: path),
                    CSharpSyntaxTree.ParseText(
                        _rootSource,
                        path: "Assets/Scripts/Game/GameRoot.cs"
                    ),
                    CSharpSyntaxTree.ParseText(
                        _supportSource,
                        path: "Assets/Scripts/Gameplay/Runtime.cs"
                    ),
                },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            Assert.IsEmpty(
                compilation
                    .GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            );
            ImmutableArray<DiagnosticAnalyzer> analyzers =
                ImmutableArray.Create<DiagnosticAnalyzer>(new GameModelBoundaryAnalyzer());

            return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        }
    }
}
