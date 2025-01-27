using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Generation
{
    /// <summary>
    /// Source generator that creates state management services for classes ending with 'State'.
    /// </summary>
    [Generator]
    public class StateGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register syntax provider to find classes ending with 'State'
            var syntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                {
                    // Quick check if it's a class
                    if (node is not ClassDeclarationSyntax classDeclaration)
                        return false;

                    // Check if class name ends with "State" and is public
                    return classDeclaration.Identifier.Text.EndsWith("State", StringComparison.Ordinal) &&
                           classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
                           !classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));
                },
                transform: (generatorSyntaxContext, _) =>
                {
                    var classDeclaration = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
                    var model = generatorSyntaxContext.SemanticModel;

                    // Get semantic information about the class
                    var symbol = model.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                    if (symbol == null) return default;

                    return (classDeclaration, symbol);
                });

            // Collect all state classes to generate the registration extension
            var stateClassesProvider = syntaxProvider.Collect();

            // Register individual state services
            context.RegisterSourceOutput(syntaxProvider, (context, tuple) =>
            {
                if (tuple.symbol == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG001",
                            "Null Symbol",
                            "Skipping generation due to null symbol",
                            "StateGenerator",
                            DiagnosticSeverity.Warning,
                            true),
                        Location.None));
                    return;
                }

                try
                {
                    var namespaceName = tuple.symbol.ContainingNamespace.ToDisplayString();
                    var className = tuple.symbol.Name;
                    var serviceName = $"{className}Service";

                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG002",
                            "Processing Class",
                            "Processing class {0} in namespace {1}",
                            "StateGenerator",
                            DiagnosticSeverity.Warning,
                            true),
                        Location.None,
                        className,
                        namespaceName));

                    // Get all properties from the state class including inherited ones
                    var properties = new List<IPropertySymbol>();
                    var propertyType = tuple.symbol;

                    while (propertyType != null)
                    {
                        properties.AddRange(propertyType.GetMembers()
                            .OfType<IPropertySymbol>()
                            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic));

                        propertyType = propertyType.BaseType;
                    }

                    // Get all interfaces including those from base classes
                    var interfaces = new List<string>();
                    var interfaceNamespaces = new HashSet<string>();
                    var currentType = tuple.symbol;

                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG003",
                            "Base Type Info",
                            "Class {0} has base type: {1}",
                            "StateGenerator",
                            DiagnosticSeverity.Warning,
                            true),
                        Location.None,
                        className,
                        currentType.BaseType?.ToString() ?? "none"));

                    // Get all interfaces including inherited ones
                    var allInterfaces = currentType.AllInterfaces;
                    foreach (var iface in allInterfaces)
                    {
                        // Use ToDisplayString to capture any generic parameters (e.g., IFoo<T>)
                        var interfaceName = iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                        if (!interfaces.Contains(interfaceName))
                        {
                            interfaces.Add(interfaceName);
                            interfaceNamespaces.Add(iface.ContainingNamespace.ToDisplayString());

                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    new DiagnosticDescriptor(
                                        "SG004",
                                        "Interface Found",
                                        "Found interface {0} in namespace {1}",
                                        "StateGenerator",
                                        DiagnosticSeverity.Warning,
                                        isEnabledByDefault: true),
                                    Location.None,
                                    interfaceName,
                                    iface.ContainingNamespace.ToDisplayString()));
                        }
                    }

                    var source = GenerateStateServiceSource(
                        namespaceName,
                        className,
                        serviceName,
                        properties,
                        interfaces,
                        new HashSet<string>(interfaceNamespaces));
                    context.AddSource($"{serviceName}.g.cs", SourceText.From(source, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG002",
                            "Generation failed",
                            "Failed to generate state service: {0}",
                            "StateGenerator",
                            DiagnosticSeverity.Error,
                            true),
                        Location.None,
                        ex.Message));
                }
            });

            // Register the extension method for DI registration
            context.RegisterSourceOutput(stateClassesProvider, (context, stateClasses) =>
            {
                var registrationSource = GenerateRegistrationExtensionSource(stateClasses
                    .Where(tuple => tuple.symbol != null)
                    .Select(tuple => (
                        Namespace: tuple.symbol!.ContainingNamespace.ToDisplayString(),
                        ClassName: tuple.symbol.Name,
                        ServiceName: $"{tuple.symbol.Name}Service"
                    ))
                    .ToList());

                context.AddSource("StateServiceRegistration.g.cs", SourceText.From(registrationSource, Encoding.UTF8));
            });
        }

        private static string GenerateRegistrationExtensionSource(List<(string Namespace, string ClassName, string ServiceName)> services)
        {
            var registrations = new StringBuilder();
            foreach (var service in services)
            {
                registrations.AppendLine($"            services.AddScoped<{service.Namespace}.{service.ServiceName}>();");
            }

            return $@"// <auto-generated/>
using Microsoft.Extensions.DependencyInjection;

namespace Generation
{{
    /// <summary>
    /// Extension methods for registering state services in the DI container.
    /// </summary>
    public static class StateServiceRegistrationExtensions
    {{
        /// <summary>
        /// Adds all state services to the service collection as scoped services.
        /// </summary>
        /// <param name=""services"">The service collection to add the services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddStateServices(this IServiceCollection services)
        {{
{registrations}
            return services;
        }}
    }}
}}";
        }

        private static string GenerateStateServiceSource(
            string namespaceName,
            string className,
            string serviceName,
            List<IPropertySymbol> properties,
            List<string> interfaces,
            HashSet<string> interfaceNamespaces)
        {
            var propertyAccessors = new StringBuilder();
            var collectionMethods = new StringBuilder();
            var interfaceMethodImplementations = new StringBuilder();

            foreach (var property in properties)
            {
                // Generate property accessor
                var propertyContent = new StringBuilder();
                propertyContent.AppendLine($@"
        /// <summary>
        /// Gets {(property.IsReadOnly ? string.Empty : "or sets ")}the {property.Name}.
        /// </summary>");

                if (property.IsReadOnly)
                {
                    propertyContent.AppendLine($@"        public {property.Type} {property.Name} => _state.{property.Name};");
                }
                else
                {
                    propertyContent.AppendLine($@"        public {property.Type} {property.Name}
        {{
            get => _state.{property.Name};
            set
            {{
                _state.{property.Name} = value;
                NotifyStateChanged();
            }}
        }}");
                }

                propertyAccessors.Append(propertyContent);

                // Generate collection-specific methods if property is a collection
                if (property.Type.ToString().StartsWith("System.Collections.Generic.List<"))
                {
                    var itemType = property.Type.ToString().Replace("System.Collections.Generic.List<", "").TrimEnd('>');
                    collectionMethods.AppendLine($@"
        /// <summary>
        /// Adds an item to {property.Name}.
        /// </summary>
        public void Add{property.Name.TrimEnd('s')}({itemType} item)
        {{
            _state.{property.Name}.Add(item);
            NotifyStateChanged();
        }}

        /// <summary>
        /// Removes an item from {property.Name}.
        /// </summary>
        public void Remove{property.Name.TrimEnd('s')}({itemType} item)
        {{
            _state.{property.Name}.Remove(item);
            NotifyStateChanged();
        }}");
                }
                else if (property.Type.ToString().StartsWith("System.Collections.Generic.Dictionary<"))
                {
                    var types = property.Type.ToString()
                        .Replace("System.Collections.Generic.Dictionary<", "")
                        .TrimEnd('>')
                        .Split(',');
                    var keyType = types[0].Trim();
                    var valueType = types[1].Trim();

                    collectionMethods.AppendLine($@"
        /// <summary>
        /// Sets the {property.Name} dictionary.
        /// </summary>
        public void Set{property.Name}(Dictionary<{keyType}, {valueType}> values)
        {{
            _state.{property.Name} = values;
            NotifyStateChanged();
        }}");
                }
            }

            var usingsBuilder = new StringBuilder();
            usingsBuilder.AppendLine("using System;");
            usingsBuilder.AppendLine("using System.Collections.Generic;");
            usingsBuilder.AppendLine("using System.Text.Json;");
            usingsBuilder.AppendLine("using System.IO;");
            usingsBuilder.AppendLine("using Common.Models;");

            foreach (var ns in interfaceNamespaces)
            {
                usingsBuilder.AppendLine($"using {ns};");
            }

            var usings = usingsBuilder.ToString();

            return $@"// <auto-generated/>
{usings}

namespace {namespaceName}
{{
    /// <summary>
    /// Auto-generated state management service for {className}.
    /// </summary>
    public class {serviceName} {(interfaces.Count > 0 ? $": {string.Join(", ", interfaces)}" : "")}
    {{
        private {className} _state = new();
        private bool _isInitialized = false;

        /// <summary>
        /// Event that is raised when the state changes.
        /// </summary>
        public event Action? OnChange;

        /// <summary>
        /// Notifies listeners that the state has changed.
        /// </summary>
        protected void NotifyStateChanged()
        {{
            _isInitialized = true;
            OnChange?.Invoke();
        }}

        /// <summary>
        /// Gets the current state.
        /// </summary>
        /// <returns>The current state instance.</returns>
        public {className} GetState() => _state;

        /// <summary>
        /// Initializes the state if it hasn't been initialized yet with the provided state.
        /// </summary>
        /// <param name=""initializeAction"">Action that initializes the state.</param>
        public void InitializeState(Action<{className}> initializeAction, bool force = false)
        {{
            if (!_isInitialized || force)
            {{
                UpdateState(initializeAction);
            }}
        }}

        /// <summary>
        /// Updates the state using the provided action and notifies listeners.
        /// </summary>
        /// <param name=""updateAction"">Action that modifies the state.</param>
        public void UpdateState(Action<{className}> updateAction)
        {{
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            updateAction(_state);
            NotifyStateChanged();
        }}

        /// <summary>
        /// Saves the current state to disk as JSON.
        /// </summary>
        public void Save()
        {{
            try
            {{
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ""TugboatCaptainsPlayground""
                );

                Directory.CreateDirectory(folder);

                var path = Path.Combine(folder, $""{className}.json"");
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions {{ WriteIndented = true, Converters = {{ new Json.More.JsonArrayTupleConverter() }} }});
                File.WriteAllText(path, json);
            }}
            catch (Exception ex)
            {{
                Console.WriteLine($""Failed to save state: {className} - {{ex.Message}}"");
            }}
        }}

        /// <summary>
        /// Loads the state from disk if it exists.
        /// </summary>
        /// <returns>True if state was loaded, false if no saved state exists.</returns>
        public bool Load()
        {{
            try
            {{
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ""TugboatCaptainsPlayground"",
                    $""{className}.json""
                );

                if (!File.Exists(path))
                    return false;

                var json = File.ReadAllText(path);
                _state = JsonSerializer.Deserialize<{className}>(json, new JsonSerializerOptions {{ WriteIndented = true, Converters = {{ new Json.More.JsonArrayTupleConverter() }} }});
                NotifyStateChanged();
            }}
            catch (Exception ex)
            {{
                Console.WriteLine($""Failed to load state: {className} - {{ex.Message}}"");
                // Create a new default state
                _state = new {className}();
                NotifyStateChanged();
                return false;
            }}
            return true;
        }}{propertyAccessors}{collectionMethods}
    }}
}}";
        }
    }
}
