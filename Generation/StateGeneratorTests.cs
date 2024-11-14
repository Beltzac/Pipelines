using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Reflection;
using Xunit;

namespace Generation
{
    public class StateGeneratorTests
    {
        [Fact]
        public void GeneratesStateService_ForSimpleState()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public class TestState
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }
}";
            
            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            Assert.Equal(2, outputs.Length);
            
            var serviceOutput = outputs.First(o => o.HintName == "TestStateService.g.cs");
            Assert.Contains("public partial class TestStateService", serviceOutput.SourceText.ToString());
            Assert.Contains("public string Name", serviceOutput.SourceText.ToString());
            Assert.Contains("public int Count", serviceOutput.SourceText.ToString());
            
            var registrationOutput = outputs.First(o => o.HintName == "StateServiceRegistration.g.cs");
            Assert.Contains("services.AddScoped<TestNamespace.TestStateService>();", registrationOutput.SourceText.ToString());
        }

        [Fact]
        public void GeneratesCollectionMethods_ForListProperties()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public class ListState
    {
        public List<string> Items { get; set; }
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName == "ListStateService.g.cs");
            var sourceText = serviceOutput.SourceText.ToString();
            
            Assert.Contains("public void AddItem(string item)", sourceText);
            Assert.Contains("public void RemoveItem(string item)", sourceText);
        }

        [Fact]
        public void GeneratesDictionaryMethods_ForDictionaryProperties()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public class DictionaryState
    {
        public Dictionary<string, int> Mappings { get; set; }
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName == "DictionaryStateService.g.cs");
            var sourceText = serviceOutput.SourceText.ToString();
            
            Assert.Contains("public void SetMappings(Dictionary<string, int> values)", sourceText);
        }

        [Fact]
        public void DoesNotGenerate_ForNonStateClasses()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public class RegularClass
    {
        public string Name { get; set; }
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            Assert.Empty(outputs);
        }

        private static ImmutableArray<(string HintName, SourceText SourceText)> GetGeneratedOutput(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create(
                "test",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new StateGenerator();
            
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(compilation);
            
            return driver
                .GetRunResult()
                .GeneratedTrees
                .Select(t => (t.FilePath, SourceText.From(t.GetText())))
                .ToImmutableArray();
        }
    }
}
