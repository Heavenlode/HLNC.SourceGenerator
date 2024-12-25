using Microsoft.CodeAnalysis;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Scriban;
using System.Reflection;
using System.Collections.Immutable;
using Scriban.Runtime.Accessors;

namespace HLNC.SourceGenerator
{
    // Enum representing various variant types
    // This is a direct copy of the VariantType enum in Godot
    public enum VariantType : long
    {
        Nil, Bool, Int, Float, String, Vector2, Vector2I, Rect2, Rect2I, Vector3, Vector3I, Transform2D, Vector4, Vector4I, Plane, Quaternion, Aabb, Basis, Transform3D, Projection, Color, StringName, NodePath, Rid, Object, Callable, Signal, Dictionary, Array, PackedByteArray, PackedInt32Array, PackedInt64Array, PackedFloat32Array, PackedFloat64Array, PackedStringArray, PackedVector2Array, PackedVector3Array, PackedColorArray, PackedVector4Array, Max
    }

    // Struct representing a collected network property
    internal struct CollectedNetworkProperty
    {
        public string NodePath;
        public string Name;
        public int Type;
        public int Subtype;
        public string InterestMask;
        public string NetworkSerializerClass;
        public string BsonSerializerClass;
    }

    // Struct representing a collected network function
    internal struct CollectedNetworkFunction
    {
        public string NodePath;
        public string Name;
        public ExtendedVariantType[] Arguments;
        public bool WithPeer;
    }

    // Enum representing various variant subtypes
    public enum VariantSubtype
    {
        None, Guid, Byte, Int, NetworkId, NetworkNode, Lazy
    }

    // Struct representing an extended variant type
    public struct ExtendedVariantType
    {
        public VariantType Type;
        public VariantSubtype Subtype;
    }

    [Generator]
    public class NetworkStaticRegistryGenerator : ISourceGenerator
    {
        // TODO: We should actually use these and raise an error if they are exceeded
        private const int MAX_NETWORK_PROPERTIES = 64;
        private const int MAX_NETWORK_FUNCTIONS = 64;

        // Maps for storing scene and network data
        internal static Dictionary<byte, string> ScenesMap = new Dictionary<byte, string>();
        internal static Dictionary<string, List<Tuple<int, string>>> StaticNetworkNodesMap = new Dictionary<string, List<Tuple<int, string>>>();
        internal static Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkProperty>>> PropertiesMap = new Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkProperty>>>();
        internal static Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkFunction>>> FunctionsMap = new Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkFunction>>>();

        // Cache for scene data
        private Dictionary<string, CollectedData> SceneDataCache = new Dictionary<string, CollectedData>();

        // Struct representing collected data
        struct CollectedData
        {
            public Dictionary<string, Dictionary<string, CollectedNetworkProperty>> Properties;
            public Dictionary<string, Dictionary<string, CollectedNetworkFunction>> Functions;
            public List<Tuple<int, string>> StaticNetworkNodes;
            public bool IsNetworkScene;
        }

        // Method to get the variant type of a symbol
        public static ExtendedVariantType GetVariantType(ITypeSymbol t)
        {
            VariantType propType = VariantType.Nil;
            VariantSubtype subType = VariantSubtype.None;

            switch (t.SpecialType.ToString())
            {
                case "System_Int64":
                case "System_Int32":
                case "System_Byte":
                    propType = VariantType.Int;
                    subType = t.SpecialType.ToString() == "System_Byte" ? VariantSubtype.Byte : t.SpecialType.ToString() == "System_Int32" ? VariantSubtype.Int : VariantSubtype.None;
                    break;
                case "System_Single":
                    propType = VariantType.Float;
                    break;
                case "System_String":
                    propType = VariantType.String;
                    break;
                case "System_Boolean":
                    propType = VariantType.Bool;
                    break;
                default:
                    if (t.ToString() == "Godot.Vector3")
                        propType = VariantType.Vector3;
                    else if (t.ToString() == "Godot.Quaternion")
                        propType = VariantType.Quaternion;
                    else if (t.ToString() == "byte[]")
                        propType = VariantType.PackedByteArray;
                    else if (t.ToString().StartsWith("Godot.Collections.Dictionary"))
                        propType = VariantType.Dictionary;
                    else if (t.TypeKind == TypeKind.Enum) {
                        propType = VariantType.Int;
                        subType = VariantSubtype.Int;
                    }
                    else if (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Interface)
                    {
                        propType = VariantType.Object;
                        if (t.ToString() == "HLNC.LazyPeerState")
                            subType = VariantSubtype.Lazy;
                        else if (t.ToString() == "HLNC.NetworkNode3D")
                            subType = VariantSubtype.NetworkNode;
                    }
                    else
                        Debug.WriteLine($"Unknown type: {t} with kind {t.TypeKind} and special type {t.SpecialType}");
                    break;
            }

            return new ExtendedVariantType { Type = propType, Subtype = subType };
        }

        // Method to read a resource file
        public string ReadResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream($"HLNC.SourceGenerator.{name}"))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        // Method to get parent types of a class
        public static IEnumerable<INamedTypeSymbol> GetParentTypes(GeneratorExecutionContext context, ClassDeclarationSyntax type)
        {
            if (type == null) yield break;

            var currentBaseType = ModelExtensions.GetDeclaredSymbol(context.Compilation.GetSemanticModel(type.SyntaxTree), type);
            while (currentBaseType != null)
            {
                yield return currentBaseType as INamedTypeSymbol;
                currentBaseType = (currentBaseType as INamedTypeSymbol).BaseType;
            }
        }

        // Method to check if a type is NetworkNode3D
        public static bool IsNetworkNode3D(IEnumerable<INamedTypeSymbol> types)
        {
            return types.Any(t => t.ToString() == "HLNC.NetworkNode3D");
        }

        // Struct representing class data
        public struct ClassData
        {
            public INamedTypeSymbol ClassSymbol;
            public IEnumerable<IPropertySymbol> Properties;
            public IEnumerable<IMethodSymbol> Functions;
        }

        // Method to get attribute argument
        private T GetAttributeArgument<T>(ISymbol sym, string attributeName, string argumentName, T defaultValue)
        {
            var attribute = sym.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == attributeName);
            if (attribute != null)
            {
                var interestMaskArgument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
                if (interestMaskArgument.Value.Value != null)
                {
                    return (T)interestMaskArgument.Value.Value;
                }
            }
            return defaultValue;
        }

        // Method to get attribute field value
        private string GetAttributeFieldValue(ISymbol sym, string attributeName, string argumentName)
        {
            var attribute = sym.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == attributeName);
            if (attribute != null)
            {
                var field = attribute.AttributeClass.GetMembers().FirstOrDefault(m => m.Name == argumentName);
                if (field != null)
                {
                    var equalsSyntax = field.DeclaringSyntaxReferences[0].GetSyntax() switch
                    {
                        PropertyDeclarationSyntax property => property.Initializer,
                        VariableDeclaratorSyntax variable => variable.Initializer,
                        _ => throw new Exception("Unknown declaration syntax")
                    };
                    if (equalsSyntax is not null)
                    {
                        return equalsSyntax.Value.ToString();
                    }
                }
            }
            return "";
        }

        Dictionary<string, ClassData[]> networkNodeClasses;

        // Method to execute the source generator
        public void Execute(GeneratorExecutionContext context)
        {
            var projectDir = "";
            context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out projectDir);
            projectDir = projectDir.Replace("\\", "/");
            var scenes = context.AdditionalFiles.Where(f => f.Path.EndsWith(".tscn"));
            var sceneTextMap = scenes.Select((f, i) => new { Path = f.Path.Replace("\\", "/").Replace(projectDir, ""), Value = f.GetText().ToString() }).ToDictionary(x => x.Path, x => x.Value);
            networkNodeClasses = GetNetworkNodeClasses(context);

            foreach (var sceneFile in scenes)
            {
                var sceneResourcePath = sceneFile.Path.Replace("\\", "/").Replace(projectDir, "res://");
                var result = CollectSceneData(context, sceneResourcePath, sceneFile.GetText()?.ToString(), sceneTextMap);
                if (!result.IsNetworkScene) continue;
                if (result.StaticNetworkNodes.Count > 0)
                {
                    StaticNetworkNodesMap[sceneResourcePath] = result.StaticNetworkNodes;
                }
                if (result.Properties.Count > 0)
                {
                    PropertiesMap[sceneResourcePath] = result.Properties;
                }
                if (result.Functions.Count > 0)
                {
                    FunctionsMap[sceneResourcePath] = result.Functions;
                }
            }

            context.AddSource($"NetworkScenesRegister.g.cs", Template.Parse(ReadResource("StaticSourceTemplate.sbncs")).Render(new { ScenesMap = ScenesMap.ToArray(), StaticNetworkNodesMap = StaticNetworkNodesMap.ToArray(), PropertiesMap, FunctionsMap }, member => member.Name));
        }

        // Method to get network node classes
        private Dictionary<string, ClassData[]> GetNetworkNodeClasses(GeneratorExecutionContext context)
        {
            return context.Compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot()
                    .DescendantNodes()
                    .Where(n => n is ClassDeclarationSyntax && IsNetworkNode3D(GetParentTypes(context, n as ClassDeclarationSyntax)))
                    .Select(n =>
                    {
                        var types = GetParentTypes(context, n as ClassDeclarationSyntax);
                        var classes = types.Select(t =>
                        {
                            var props = t.GetMembers().Where(m => m.GetAttributes().Any(a => a.AttributeClass != null && a.AttributeClass.Name == "NetworkProperty")).OfType<IPropertySymbol>();
                            var functions = t.GetMembers().Where(m => m.GetAttributes().Any(a => a.AttributeClass != null && a.AttributeClass.Name == "NetworkFunction")).OfType<IMethodSymbol>();
                            return new ClassData { ClassSymbol = t, Properties = props, Functions = functions };
                        }).ToArray();
                        return new { FilePath = st.FilePath, Data = classes };
                    }))
                .ToDictionary(x => x.FilePath.Replace("\\", "/"), x => x.Data);
        }

        // Method to collect scene data
        private CollectedData CollectSceneData(GeneratorExecutionContext context, string sceneResourcePath, string sceneFileContent, Dictionary<string, string> sceneTextMap)
        {
            if (SceneDataCache.TryGetValue(sceneResourcePath, out var cachedData))
            {
                return cachedData;
            }

            CollectedData result = new CollectedData
            {
                Properties = new Dictionary<string, Dictionary<string, CollectedNetworkProperty>>(),
                Functions = new Dictionary<string, Dictionary<string, CollectedNetworkFunction>>(),
                StaticNetworkNodes = new List<Tuple<int, string>>(),
                IsNetworkScene = false
            };


            // Parse the scene file into its respective nodes and resources
            var parser = new ConfigParser();
            var parsedTscn = parser.ParseTscnFile(sceneFileContent ?? "");
            if (parsedTscn.RootNode == null || !parsedTscn.RootNode.Properties.TryGetValue("script", out var rootScript)) return result;

            var networkNodeClass = networkNodeClasses.Keys.FirstOrDefault(k => k.Contains(rootScript));
            if (!string.IsNullOrEmpty(networkNodeClass))
            {
                Debug.WriteLine($"NetworkScene: {sceneResourcePath} with root node {parsedTscn.RootNode.Name} and script {rootScript} of class {networkNodeClass}");
                result.IsNetworkScene = true;
                byte sceneId = (byte)ScenesMap.Count;
                ScenesMap.Add(sceneId, sceneResourcePath);
            }

            var nodePathId = 0;

            // Iterate over each node in the scene
            foreach (var node in parsedTscn.Nodes)
            {
                IEnumerable<IPropertySymbol> nodeProperties;
                IEnumerable<IMethodSymbol> nodeFunctions;
                ClassData[] classDatas;
                var nodePath = node.Parent == null ? "." : node.Parent == "." ? node.Name : $"{node.Parent}/{node.Name}";

                // If the node has a script attached, first we check if the class is a NetworkNode3D
                // TODO: In the future, this should be expanded to include other types of network nodes, e.g. in GDScript.
                if (node.Properties.TryGetValue("script", out var script))
                {
                    var classPath = networkNodeClasses.Keys.FirstOrDefault(p => p.Contains(script));
                    if (string.IsNullOrEmpty(classPath)) continue;

                    if (networkNodeClasses.TryGetValue(classPath, out classDatas))
                    {
                        nodeProperties = classDatas.SelectMany(c => c.Properties);
                        nodeFunctions = classDatas.SelectMany(c => c.Functions);
                    }
                    else continue;
                }
                // If there is no script attached, then we check if there is an "Instance" attached
                // This means the node is a scene instance, and we need to recurse into the scene to collect the data
                else if (node.Instance != null)
                {
                    var recurseData = CollectSceneData(context, $"res://{node.Instance}", sceneTextMap[node.Instance], sceneTextMap);

                    // NetworkScene nodes exist within their own "root network context" so we do not flatten them into this current scene's data
                    if (recurseData.IsNetworkScene) continue;

                    // Flatten the static network nodes, properties, and functions from the instance to the current scene
                    foreach (var nodePathTuple in recurseData.StaticNetworkNodes)
                    {
                        result.StaticNetworkNodes.Add(new Tuple<int, string>(nodePathId++, nodePath + "/" + nodePathTuple.Item2));
                    }
                    foreach (var kvp in recurseData.Properties)
                    {
                        result.Properties[nodePath + "/" + kvp.Key] = kvp.Value;
                    }
                    foreach (var kvp in recurseData.Functions)
                    {
                        result.Functions[nodePath + "/" + kvp.Key] = kvp.Value;
                    }
                    continue;
                }
                // The node is neither a script nor an instance, so we skip it
                // This won't miss its child nodes, because non-scene nodes are flattened out in the tscn files and thus will be picked up.
                else continue;

                // Collect the node path and add it to the static network nodes
                // This allows us to reference the node by a compact ID across the network
                result.StaticNetworkNodes.Add(new Tuple<int, string>(nodePathId++, nodePath));

                // Collecting the fields marked as [NetworkProperty]
                foreach (var property in nodeProperties)
                {
                    // Only the root node of a NetworkNode scene get a NetworkId.
                    // TODO: Is there a better way to do this? Seems a bit weird to put the logic here.
                    if (node != parsedTscn.RootNode && property.Name == "NetworkId") continue;

                    var networkSerializerName = "";
                    var bsonSerializerName = "";
                    var propType = GetVariantType(property.Type);
                    propType.Subtype = GetAttributeArgument(property, "NetworkProperty", "Subtype", propType.Subtype);

                    if (propType.Type == VariantType.Object)
                    {
                        // If the property is an object, we check if they define custom serializers, and if so then we find the class name which implements them
                        // We get the ClassSymbol of the property, and determine if it implements the INetworkSerializable or IBsonSerializable interfaces
                        var propertyParentTypes = property.Type.DeclaringSyntaxReferences
                            .Select(syntaxRef => syntaxRef.GetSyntax() as ClassDeclarationSyntax)
                            .Where(syntax => syntax != null)
                            .SelectMany(syntax => GetParentTypes(context, syntax));

                        var networkSerializerSymbol = propertyParentTypes.FirstOrDefault(t => t.Interfaces.Any(i => i.Name == "INetworkSerializable"));
                        if (!string.IsNullOrEmpty(networkSerializerSymbol?.ContainingNamespace?.ToString()) && !string.IsNullOrEmpty(networkSerializerSymbol?.Name)) {
                            networkSerializerName = $"{networkSerializerSymbol?.ContainingNamespace}.{networkSerializerSymbol?.Name}" ?? "";
                        }
                        var bsonSerializerSymbol = propertyParentTypes.FirstOrDefault(t => t.Interfaces.Any(i => i.Name == "IBsonSerializable"));
                        if (!string.IsNullOrEmpty(bsonSerializerSymbol?.ContainingNamespace?.ToString()) && !string.IsNullOrEmpty(bsonSerializerSymbol?.Name)) {
                            bsonSerializerName = $"{bsonSerializerSymbol?.ContainingNamespace}.{bsonSerializerSymbol?.Name}" ?? "";
                        }
                    }


                    // Check if the property has an interest mask defined, and if so, use that, otherwise use the field value
                    // TODO: Maybe a bit of a hack to say -1L is the default value (i.e. unset by user?)
                    // I wonder if there's a good way to merge these two methods.
                    var interestMask = GetAttributeArgument(property, "NetworkProperty", "InterestMask", -1L);
                    var interestMaskField = GetAttributeFieldValue(property, "NetworkProperty", "InterestMask");


                    Debug.Print($"Property {sceneResourcePath} => {nodePath}.{property.Name} of type {propType.Type} with subtype {propType.Subtype} and interest mask {interestMask} and field value {interestMaskField}");
                    var propertyCollected = new CollectedNetworkProperty
                    {
                        BsonSerializerClass = bsonSerializerName,
                        NetworkSerializerClass = networkSerializerName,
                        NodePath = nodePath,
                        Name = property.Name,
                        Type = (int)propType.Type,
                        Subtype = (int)propType.Subtype,
                        InterestMask = interestMask == -1 ? interestMaskField : interestMask.ToString()
                    };
                    if (!result.Properties.ContainsKey(nodePath))
                    {
                        result.Properties[nodePath] = new Dictionary<string, CollectedNetworkProperty>();
                    }
                    result.Properties[nodePath].Add(property.Name, propertyCollected);
                }

                // Collect the [NetworkFunction] RPC methods
                foreach (var function in nodeFunctions)
                {
                    var withPeer = GetAttributeArgument(function, "NetworkFunction", "WithPeer", false);
                    var functionCollected = new CollectedNetworkFunction
                    {
                        NodePath = nodePath,
                        Name = function.Name,
                        Arguments = function.Parameters.Select(p => GetVariantType(p.Type)).Skip(withPeer ? 1 : 0).ToArray(),
                        WithPeer = withPeer
                    };
                    if (!result.Functions.ContainsKey(nodePath))
                    {
                        result.Functions[nodePath] = new Dictionary<string, CollectedNetworkFunction>();
                    }
                    result.Functions[nodePath].Add(function.Name, functionCollected);
                }
            }

            // Cache the collected data for the scene so that we don't have to re-parse it if it is used in other scenes
            SceneDataCache[sceneResourcePath] = result;
            return result;
        }

        // Method to initialize the source generator
        public void Initialize(GeneratorInitializationContext context)
        {
            // Apparently when you run the build multiple times, these values might not be cleared automatically?
            // I don't fully understand this (it is probably documented somewhere). I guess the Generator might still exist
            // Between builds somehow.
            PropertiesMap.Clear();
            FunctionsMap.Clear();
            StaticNetworkNodesMap.Clear();
            ScenesMap.Clear();
            SceneDataCache.Clear();
#if DEBUG
            // Uncomment the following lines to attach the debugger
            // if (!Debugger.IsAttached)
            // {
            //   Debugger.Launch();
            // }
#endif
        }
    }
}