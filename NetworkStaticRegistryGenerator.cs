using Microsoft.CodeAnalysis;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Scriban;
using System.Reflection;
using System.Collections.Immutable;

namespace HLNC.SourceGenerators
{

    public enum VariantType : long
    {
        //
        // Summary:
        //     Variable is null.
        Nil,
        //
        // Summary:
        //     Variable is of type System.Boolean.
        Bool,
        //
        // Summary:
        //     Variable is of type System.Int32.
        Int,
        //
        // Summary:
        //     Variable is of type System.Single.
        Float,
        //
        // Summary:
        //     Variable is of type System.String.
        String,
        //
        // Summary:
        //     Variable is of type Godot.Vector2.
        Vector2,
        //
        // Summary:
        //     Variable is of type Godot.Vector2I.
        Vector2I,
        //
        // Summary:
        //     Variable is of type Godot.Rect2.
        Rect2,
        //
        // Summary:
        //     Variable is of type Godot.Rect2I.
        Rect2I,
        //
        // Summary:
        //     Variable is of type Godot.Vector3.
        Vector3,
        //
        // Summary:
        //     Variable is of type Godot.Vector3I.
        Vector3I,
        //
        // Summary:
        //     Variable is of type Godot.Transform2D.
        Transform2D,
        //
        // Summary:
        //     Variable is of type Godot.Vector4.
        Vector4,
        //
        // Summary:
        //     Variable is of type Godot.Vector4I.
        Vector4I,
        //
        // Summary:
        //     Variable is of type Godot.Plane.
        Plane,
        //
        // Summary:
        //     Variable is of type Godot.Quaternion.
        Quaternion,
        //
        // Summary:
        //     Variable is of type Godot.Aabb.
        Aabb,
        //
        // Summary:
        //     Variable is of type Godot.Basis.
        Basis,
        //
        // Summary:
        //     Variable is of type Godot.Transform3D.
        Transform3D,
        //
        // Summary:
        //     Variable is of type Godot.Projection.
        Projection,
        //
        // Summary:
        //     Variable is of type Godot.Color.
        Color,
        //
        // Summary:
        //     Variable is of type Godot.StringName.
        StringName,
        //
        // Summary:
        //     Variable is of type Godot.NodePath.
        NodePath,
        //
        // Summary:
        //     Variable is of type Godot.Rid.
        Rid,
        //
        // Summary:
        //     Variable is of type Godot.GodotObject.
        Object,
        //
        // Summary:
        //     Variable is of type Godot.Callable.
        Callable,
        //
        // Summary:
        //     Variable is of type Godot.Signal.
        Signal,
        //
        // Summary:
        //     Variable is of type Godot.Collections.Dictionary.
        Dictionary,
        //
        // Summary:
        //     Variable is of type Godot.Collections.Array.
        Array,
        //
        // Summary:
        //     Variable is of type System.Byte[].
        PackedByteArray,
        //
        // Summary:
        //     Variable is of type System.Int32[].
        PackedInt32Array,
        //
        // Summary:
        //     Variable is of type System.Int64[].
        PackedInt64Array,
        //
        // Summary:
        //     Variable is of type System.Single[].
        PackedFloat32Array,
        //
        // Summary:
        //     Variable is of type System.Double[].
        PackedFloat64Array,
        //
        // Summary:
        //     Variable is of type System.String[].
        PackedStringArray,
        //
        // Summary:
        //     Variable is of type Godot.Vector2[].
        PackedVector2Array,
        //
        // Summary:
        //     Variable is of type Godot.Vector3[].
        PackedVector3Array,
        //
        // Summary:
        //     Variable is of type Godot.Color[].
        PackedColorArray,
        //
        // Summary:
        //     Variable is of type Godot.Vector4[].
        PackedVector4Array,
        //
        // Summary:
        //     Represents the size of the Godot.VariantType enum.
        Max
    }
    internal struct CollectedNetworkProperty
    {
        public string NodePath;
        public string Name;
        public int Type;
        public byte Index;
        public int Subtype;
        public long InterestMask;
        public string NetworkSerializerClass;
        public string BsonSerializerClass;
    }

    internal struct CollectedNetworkFunction
    {
        public string NodePath;
        public string Name;
        public byte Index;
        public ExtendedVariantType[] Arguments;
        public bool WithPeer;
    }
    public enum VariantSubtype
    {
        None,
        Guid,
        Byte,
        Int,
        NetworkId,
        NetworkNode,
        AsyncPeerValue

    }

    public struct ExtendedVariantType
    {
        public VariantType Type;
        public VariantSubtype Subtype;
    }

    [Generator]
    public class NetworkStaticRegistryGenerator : ISourceGenerator
    {
        public static ExtendedVariantType GetVariantType(ITypeSymbol t)
        {
            VariantType propType = VariantType.Nil;
            VariantSubtype subType = VariantSubtype.None;

            if (t.SpecialType.ToString() == "System_Int64" || t.SpecialType.ToString() == "System_Int32" || t.SpecialType.ToString() == "System_Byte")
            {
                propType = VariantType.Int;
                if (t.SpecialType.ToString() == "System_Byte")
                {
                    subType = VariantSubtype.Byte;
                }
                else if (t.SpecialType.ToString() == "System_Int32")
                {
                    subType = VariantSubtype.Int;
                }
            }
            else if (t.SpecialType.ToString() == "System_Single")
            {
                propType = VariantType.Float;
            }
            else if (t.SpecialType.ToString() == "System_String")
            {
                propType = VariantType.String;
            }
            else if (t.ToString() == "Godot.Vector3")
            {
                propType = VariantType.Vector3;
            }
            else if (t.ToString() == "Godot.Quaternion")
            {
                propType = VariantType.Quaternion;
            }
            else if (t.SpecialType.ToString() == "System_Boolean")
            {
                propType = VariantType.Bool;
            }
            else if (t.SpecialType.ToString() == "System_Byte[]")
            {
                propType = VariantType.PackedByteArray;
            }
            // else if (t.IsGenericType && t.GetGenericTypeDefinition().ToString() == "System.Collections.Generic.Dictionary`2")
            // {
            //     propType = VariantType.Dictionary;
            // }
            // Now we identify if t is an enum
            else if (t.TypeKind == TypeKind.Enum)
            {
                propType = VariantType.Int;
                // var T = t.GetEnumUnderlyingType();
            }
            // Check to see if the property is an object
            else if (t.TypeKind == TypeKind.Class)
            {
                propType = VariantType.Object;
                if (t.ToString() == "HLNC.LazyPeerState")
                {
                    subType = VariantSubtype.AsyncPeerValue;
                }
                else if (t.ToString() == "HLNC.NetworkNode3D")
                {
                    subType = VariantSubtype.NetworkNode;
                }
            }
            // else if (t.GetInterfaces()
            //             .Where(i => i.IsGenericType)
            //             .Any(i => i.GetGenericTypeDefinition() == typeof(INetworkSerializable<>))
            //         )
            // {
            //     propType = VariantType.Object;
            //     if (t == typeof(LazyPeerState))
            //     {
            //         subType = VariantSubtype.AsyncPeerValue;
            //     }
            //     else if (t == typeof(NetworkNode3D))
            //     {
            //         subType = VariantSubtype.NetworkNode;
            //     }
            // }
            // else
            // {
            //     return new VariantType
            //     {
            //         Type = VariantType.Nil,
            //         Subtype = VariantSubtype.None
            //     };
            // }

            return new ExtendedVariantType
            {
                Type = propType,
                Subtype = subType
            };
        }

        public string ReadResource(string name)
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream($"HLNC.SourceGenerators.{name}"))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        private const int MAX_NETWORK_PROPERTIES = 64;
        private const int MAX_NETWORK_FUNCTIONS = byte.MaxValue;

        /// <summary>
        /// Map of scene IDs to scene paths
        /// </summary>
        internal static Dictionary<byte, string> ScenesMap = [];

        /// <summary>
        /// A map of every packed scene to a list of paths to its internal network nodes.
        /// </summary>
        internal static Dictionary<string, List<Tuple<int, string>>> StaticNetworkNodesMap = [];


        /// <summary>
        /// A Dictionary of ScenePath to NodePath to PropertyName to CollectedNetworkProperty.
        /// It includes all child Network Nodes within the Scene including itself, but not nested network scenes.
        /// </summary>
        internal static Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkProperty>>> PropertiesMap = [];
        internal static Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkFunction>>> FunctionsMap = [];
        public static IEnumerable<INamedTypeSymbol> GetParentTypes(GeneratorExecutionContext context, ClassDeclarationSyntax type)
        {
            // is there any base type?
            if (type == null)
            {
                yield break;
            }
            // (n as ClassDeclarationSyntax).BaseList?.Types.Any(t => t.Type.ToString() == "NetworkNode3D") == true;

            // return all inherited types
            var currentBaseType = ModelExtensions.GetDeclaredSymbol(context.Compilation.GetSemanticModel(type.SyntaxTree), type);
            while (currentBaseType != null)
            {
                yield return currentBaseType as INamedTypeSymbol;
                currentBaseType = (currentBaseType as INamedTypeSymbol).BaseType;
            }
        }

        public static bool IsNetworkNode3D(IEnumerable<INamedTypeSymbol> types)
        {
            return types.Any(t => t.ToString() == "HLNC.NetworkNode3D");
        }

        public struct ClassData
        {
            public INamedTypeSymbol ClassSymbol;
            public IEnumerable<IPropertySymbol> Properties;
            public IEnumerable<IMethodSymbol> Functions;
        }

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

        Dictionary<string, CollectedData> SceneDataCache = new Dictionary<string, CollectedData>();

        public void Execute(GeneratorExecutionContext context)
        {
            var projectDir = "";
            context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out projectDir);
            projectDir = projectDir.Replace("\\", "/");
            var scenes = context.AdditionalFiles.Where(f => f.Path.EndsWith(".tscn"));
            var sceneTextMap = scenes.Select((f, i) => new { Path = f.Path.Replace("\\", "/").Replace(projectDir, ""), Value = f.GetText().ToString() }).ToDictionary(x => x.Path, x => x.Value);
            Dictionary<string, ClassData[]> networkNodeClasses = context.Compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot()
                        .DescendantNodes()
                        .Where(n =>
                        {
                            if (n is not ClassDeclarationSyntax) return false;
                            return IsNetworkNode3D(GetParentTypes(context, n as ClassDeclarationSyntax));
                        })
                        .Select(n =>
                        {
                            var types = GetParentTypes(context, n as ClassDeclarationSyntax);
                            var classes = types.Select(t =>
                            {
                                var props = t.GetMembers().Where(m => m.GetAttributes().Any(a => a.AttributeClass != null && a.AttributeClass.Name == "NetworkProperty"))
                                .OfType<IPropertySymbol>();
                                // Now get all the functions with the attribute NetworkFunction
                                var functions = t.GetMembers().Where(m => m.GetAttributes().Any(a => a.AttributeClass != null && a.AttributeClass.Name == "NetworkFunction"))
                                .OfType<IMethodSymbol>();
                                return new ClassData
                                {
                                    ClassSymbol = t,
                                    Properties = props,
                                    Functions = functions
                                };
                            }).ToArray();

                            return new { FilePath = st.FilePath, Data = classes };
                        }))
                        .ToDictionary(x => x.FilePath.Replace("\\", "/"), x => x.Data);

            var sceneClassPaths = networkNodeClasses.Keys.ToImmutableHashSet();
            // Pretty print the scene classes
            // foreach (var sceneClass in sceneClasses)
            // {
            //     Debug.WriteLine($"Scene class: {sceneClass.Key}, {sceneClass.Value}");
            // }

            Dictionary<string, HashSet<string>> networkNodeTree = new Dictionary<string, HashSet<string>>();

            // Now we iterate and parse every scene


            // Collect all "scenePaths" values from the NetworkScenes attribute
            foreach (var sceneFile in scenes)
            {
                var sceneResourcePath = sceneFile.Path.Replace("\\", "/").Replace(projectDir, "res://");
                var result = CollectSceneData(sceneResourcePath, sceneFile.GetText()?.ToString(), networkNodeClasses, sceneClassPaths, sceneTextMap);
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

            // Add the source code to the compilation
            context.AddSource($"NetworkScenesRegister.g.cs", Template.Parse(ReadResource("StaticSourceTemplate.sbncs")).Render(new { ScenesMap = ScenesMap.ToArray(), StaticNetworkNodesMap = StaticNetworkNodesMap.ToArray(), PropertiesMap, FunctionsMap }, member => member.Name));
        }


        struct CollectedData
        {
            public Dictionary<string, Dictionary<string, CollectedNetworkProperty>> Properties;
            public Dictionary<string, Dictionary<string, CollectedNetworkFunction>> Functions;
            public List<Tuple<int, string>> StaticNetworkNodes;
            public bool IsNetworkScene;
        }

        private CollectedData CollectSceneData(string sceneResourcePath, string sceneFileContent, Dictionary<string, ClassData[]> networkNodeClasses, ImmutableHashSet<string> sceneClassPaths, Dictionary<string, string> sceneTextMap)
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
            var parser = new ConfigParser();
            var parsedTscn = parser.ParseTscnFile(sceneFileContent ?? "");
            string rootScript = "";
            SceneDataCache[sceneResourcePath] = result;
            if (parsedTscn.RootNode == null || !parsedTscn.RootNode.Properties.TryGetValue("script", out rootScript)) return result;
            // Get the networkNodeClass of the parsed scene
            var networkNodeClass = networkNodeClasses.Keys.FirstOrDefault(k => k.Contains(rootScript));
            if (!string.IsNullOrEmpty(networkNodeClass))
            {
                result.IsNetworkScene = true;
                byte sceneId = (byte)ScenesMap.Count;
                ScenesMap.Add(sceneId, sceneResourcePath);
            }

            var nodePathId = 0;
            var functionId = 0;
            var propertyId = 0;

            foreach (var node in parsedTscn.Nodes)
            {
                IEnumerable<IPropertySymbol> nodeProperties;
                IEnumerable<IMethodSymbol> nodeFunctions;
                ClassData[] classDatas;
                var nodePath = node.Parent == null ? "." : node.Parent == "." ? node.Name : $"{node.Parent}/{node.Name}";

                // The node is a NetworkNode3D
                if (node.Properties.TryGetValue("script", out var script))
                {
                    var classPath = sceneClassPaths.FirstOrDefault(p => p.Contains(script));
                    Debug.WriteLine($"CLASS: {classPath}, script: {script}");
                    if (string.IsNullOrEmpty(classPath))
                    {
                        continue;
                    }
                    if (networkNodeClasses.TryGetValue(classPath, out classDatas))
                    {
                        // Get all of the properties of the class
                        nodeProperties = classDatas.SelectMany(c => c.Properties);
                        nodeFunctions = classDatas.SelectMany(c => c.Functions);
                    }
                    else
                    {
                        continue;
                    }
                }
                // The node is a scene that we want to recurse through
                else if (node.Instance != null)
                {
                    var recurseData = CollectSceneData($"res://{node.Instance}", sceneTextMap[node.Instance], networkNodeClasses, sceneClassPaths, sceneTextMap);
                    if (recurseData.IsNetworkScene)
                    {
                        continue;
                    }
                    foreach (var nodePathTuple in recurseData.StaticNetworkNodes)
                    {
                        result.StaticNetworkNodes.Add(new Tuple<int, string>(nodePathId++, nodePath + "/" + nodePathTuple.Item2));
                    }
                    foreach (var kvp in recurseData.Properties)
                    {
                        foreach (var prop in kvp.Value)
                        {
                            var val = prop.Value;
                            val.Index = (byte)propertyId++;
                            kvp.Value[prop.Key] = val;
                        }
                        result.Properties[kvp.Key] = kvp.Value;
                    }
                    foreach (var kvp in recurseData.Functions)
                    {
                        result.Functions[kvp.Key] = kvp.Value;
                    }
                    continue;
                }
                else
                {
                    // It's just a normal node, so we skip. Nested nodes are included as part of the parent scene, thus will be reached eventually in the loop.
                    continue;
                }
                result.StaticNetworkNodes.Add(new Tuple<int, string>(nodePathId++, nodePath));
                // Now we get all properties from classWithAttribute which have the attribute "NetworkProperty"
                foreach (var property in nodeProperties)
                {
                    var networkSerializerName = "";
                    var bsonSerializerName = "";
                    var propType = GetVariantType(property.Type);

                    if (propType.Type == VariantType.Object)
                    {
                        networkSerializerName = classDatas.FirstOrDefault(c => c.ClassSymbol.Interfaces.Any(i => i.Name == "INetworkSerializable")).ClassSymbol.Name;
                        bsonSerializerName = classDatas.FirstOrDefault(c => c.ClassSymbol.Interfaces.Any(i => i.Name == "IBsonSerializable")).ClassSymbol.Name;
                    }

                    var propertyCollected = new CollectedNetworkProperty
                    {
                        BsonSerializerClass = bsonSerializerName,
                        NetworkSerializerClass = networkSerializerName,
                        NodePath = nodePath,
                        Name = property.Name,
                        Type = (int)propType.Type,
                        Subtype = (int)propType.Subtype,
                        Index = (byte)propertyId++,
                        InterestMask = GetAttributeArgument(property, "NetworkProperty", "InterestMask", 0L)
                    };
                    if (!result.Properties.ContainsKey(nodePath))
                    {
                        result.Properties[nodePath] = new Dictionary<string, CollectedNetworkProperty>();
                    }
                    result.Properties[nodePath].Add(property.Name, propertyCollected);
                }

                foreach (var function in nodeFunctions)
                {
                    var withPeer = GetAttributeArgument(function, "NetworkFunction", "WithPeer", false);
                    var functionCollected = new CollectedNetworkFunction
                    {
                        NodePath = nodePath,
                        Name = function.Name,
                        Index = (byte)functionId++,
                        // We skip the first argument, because that is the peer which made the call to the function
                        // Thus, not a networked argument
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

            return result;
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            PropertiesMap.Clear();
            FunctionsMap.Clear();
            StaticNetworkNodesMap.Clear();
            ScenesMap.Clear();
            SceneDataCache.Clear();
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //   Debugger.Launch();
            //}
#endif
        }
    }
}