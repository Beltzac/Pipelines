using FluentAssertions;
using Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Tests.Generation
{
    public class StateGeneratorTests
    {
        [Test]
        public void ImplementsBaseClassInterfaces()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public interface IBaseInterface
    {
        void BaseMethod();
    }

    public class BaseState : IBaseInterface
    {
        public void BaseMethod() {}
    }

    public class DerivedState : BaseState
    {
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName.Contains("DerivedStateService.g.cs"));
            var serviceText = serviceOutput.SourceText.ToString();
            serviceText.Should().Contain("public class DerivedStateService : IBaseInterface");
        }

        [Test]
        public void ImplementsMultipleInterfaces()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public interface IFirstInterface
    {
        void FirstMethod();
    }

    public interface ISecondInterface
    {
        void SecondMethod();
    }

    public class MultiInterfaceState : IFirstInterface, ISecondInterface
    {
        public void FirstMethod() {}
        public void SecondMethod() {}
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName.Contains("MultiInterfaceStateService.g.cs"));
            var serviceText = serviceOutput.SourceText.ToString();
            serviceText.Should().Contain("public class MultiInterfaceStateService : IFirstInterface, ISecondInterface");
        }

        [Test]
        public void HandlesDeepInheritance()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public interface IDeepInterface
    {
        void DeepMethod();
    }

    public class Level1 : IDeepInterface
    {
        public void DeepMethod() {}
    }

    public class Level2 : Level1 {}
    public class Level3 : Level2 {}
    public class DeepState : Level3 {}
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName.Contains("DeepStateService.g.cs"));
            var serviceText = serviceOutput.SourceText.ToString();
            serviceText.Should().Contain("public class DeepStateService : IDeepInterface");
        }

        [Test]
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
            outputs.Should().HaveCount(2);

            var serviceOutput = outputs.First(o => o.HintName.Contains("TestStateService.g.cs"));
            var serviceText = serviceOutput.SourceText.ToString();
            serviceText.Should().Contain("public class TestStateService")
                .And.Contain("public string Name")
                .And.Contain("public int Count")
                .And.Contain("public void Save()")
                .And.Contain("public bool Load()");

            var registrationOutput = outputs.First(o => o.HintName.Contains("StateServiceRegistration.g.cs"));
            registrationOutput.SourceText.ToString().Should().Contain("services.AddScoped<TestNamespace.TestStateService>();");
        }

        [Test]
        public void GeneratesCollectionMethods_ForListProperties()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public class ListState
    {
        public System.Collections.Generic.List<string> Items { get; set; }
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName.Contains("ListStateService.g.cs"));
            var sourceText = serviceOutput.SourceText.ToString();

            sourceText.Should().Contain("public void AddItem(string item)")
                .And.Contain("public void RemoveItem(string item)");
        }

        [Test]
        public void GeneratesDictionaryMethods_ForDictionaryProperties()
        {
            // Arrange
            var source = @"
namespace TestNamespace
{
    public class DictionaryState
    {
        public System.Collections.Generic.Dictionary<string, int> Mappings { get; set; }
    }
}";

            // Act
            var outputs = GetGeneratedOutput(source);

            // Assert
            var serviceOutput = outputs.First(o => o.HintName.Contains("DictionaryStateService.g.cs"));
            var sourceText = serviceOutput.SourceText.ToString();

            sourceText.Should().Contain("public void SetMappings(Dictionary<string, int> values)");
        }

        [Test]
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
            var outputs = GetGeneratedOutput(source)
                .Where(x => !x.HintName.Contains("StateServiceRegistration.g.cs"));

            // Assert
            outputs.Should().BeEmpty();
        }

        private static ImmutableArray<(string HintName, SourceText SourceText)> GetGeneratedOutput(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("netstandard").Location),
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
                .Select(t => (t.FilePath, t.GetText()))
                .ToImmutableArray();
        }
    }
}
