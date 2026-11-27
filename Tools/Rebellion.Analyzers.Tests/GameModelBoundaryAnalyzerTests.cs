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
                public static System.IObserver<int> GetObserver() => null;
                public static void Register(System.Action callback) { }
            }
            """;

        private const string _gameplaySource = """
            namespace Rebellion.Gameplay
            {
                public sealed class PlanetQueries
                {
                    public static int Read() => 0;
                }
            }

            namespace PlanetExtensions
            {
                public static class SupportExtensions
                {
                    public static int Forecast(this int support) => support;
                }
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
        [TestCase("System.IObserver<Unit> SupportObserver { get; set; }")]
        [TestCase("global::System.IObserver<int>[] _observers;")]
        [TestCase("void Inspect() { var observer = Runtime.GetObserver(); }")]
        public async Task Callback_InGameDirectory_ReportsDiagnosticAsync(string member)
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

        [Test]
        public async Task Observer_AliasedInGameDirectory_ReportsBothReferencesAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "using Listener = System.IObserver<int>; class Unit { Listener _observer; }",
                _modelPath
            );

            Assert.AreEqual(2, diagnostics.Length);
            Assert.IsTrue(
                diagnostics.All(diagnostic =>
                    diagnostic.Id == GameModelBoundaryAnalyzer.CallbackDiagnosticId
                )
            );
        }

        [Test]
        public async Task Observer_SameNamedInterface_IsAllowedAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "interface IObserver<T> { } class Unit { IObserver<int> _observer; }",
                _modelPath
            );

            Assert.IsEmpty(diagnostics);
        }

        [TestCase("Units/Unit.cs")]
        [TestCase("Missions/Mission.cs")]
        [TestCase("GameEvents/GameEvent.cs")]
        [TestCase("Scripting/GameTrigger.cs")]
        public async Task GameplayType_InGameDirectory_ReportsDependencyAsync(string file)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Unit { Rebellion.Gameplay.PlanetQueries _queries; }",
                "Assets/Scripts/Game/" + file
            );

            Assert.AreEqual(
                GameModelBoundaryAnalyzer.GameplayDependencyDiagnosticId,
                diagnostics.Single().Id
            );
        }

        [TestCase(
            "using PlanetExtensions; class Unit { int Read(int support) => support.Forecast(); }",
            "Forecast"
        )]
        [TestCase(
            "using static Rebellion.Gameplay.PlanetQueries; class Unit { int Value => Read(); }",
            "Read"
        )]
        [TestCase(
            "using Queries = Rebellion.Gameplay.PlanetQueries; class Unit { int Value => Queries.Read(); }",
            "Read"
        )]
        public async Task GameplayCall_InGameDirectory_ReportsMemberAsync(
            string source,
            string member
        )
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, _modelPath);

            Assert.That(
                diagnostics.Any(diagnostic =>
                    diagnostic.Id == GameModelBoundaryAnalyzer.GameplayDependencyDiagnosticId
                    && source.Substring(
                        diagnostic.Location.SourceSpan.Start,
                        diagnostic.Location.SourceSpan.Length
                    ) == member
                ),
                Is.True
            );
        }

        [Test]
        public async Task GameplayType_InReferencedAssembly_ReportsDependencyAsync()
        {
            CSharpCompilation gameplayAssembly = CSharpCompilation.Create(
                "Rebellion.Gameplay",
                new[]
                {
                    CSharpSyntaxTree.ParseText("namespace Compiled { public class Query { } }"),
                },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Unit { Compiled.Query _query; }",
                _modelPath,
                gameplayAssembly.ToMetadataReference()
            );

            Assert.AreEqual(
                GameModelBoundaryAnalyzer.GameplayDependencyDiagnosticId,
                diagnostics.Single().Id
            );
        }

        [TestCase("Assets/Scripts/Gameplay/Command.cs")]
        [TestCase("Assets/Scripts/GameTools/Unit.cs")]
        public async Task ArchitectureRules_OutsideGameDirectory_AllowsReferencesAsync(string path)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Unit { Rebellion.Game.GameRoot _game; event System.Action Changed; System.IObserver<int> _observer; Rebellion.Gameplay.PlanetQueries _queries; }",
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
        /// <param name="references">Additional assemblies referenced by the source.</param>
        /// <returns>The analyzer diagnostics.</returns>
        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
            string source,
            string path,
            params MetadataReference[] references
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
                        path: "Assets/Scripts/Runtime/Runtime.cs"
                    ),
                    CSharpSyntaxTree.ParseText(
                        _gameplaySource,
                        path: @"C:\repo\Assets\Scripts\Gameplay\PlanetQueries.cs"
                    ),
                },
                new MetadataReference[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                }.Concat(references),
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
