using Microsoft.CodeAnalysis;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Scriban;
using System.Reflection;
using System.Collections.Immutable;

namespace HLNC.SourceGenerators
{
    public struct VariantType
    {
        public int Type;
        public int Subtype;
    }
    internal struct CollectedNetworkProperty
    {
        public string NodePath;
        public string Name;
        public int Type;
        public byte Index;
        public int Subtype;
        public long InterestMask;
    }

    // internal struct CollectedNetworkFunction
    // {
    //     public string NodePath;
    //     public string Name;
    //     public byte Index;
    //     public VariantType[] Arguments;
    //     public bool WithPeer;
    // }
    [Generator]
    public class NetworkStaticRegistryGenerator : ISourceGenerator
    {
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

        public struct ExtendedVariantType
        {
            public VariantType Type;
            public VariantSubtype Subtype;
        }

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
            // else if (t.IsEnum)
            // {
            //     propType = VariantType.Int;
            //     // var T = t.GetEnumUnderlyingType();
            // }
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
        internal static Dictionary<byte, List<Tuple<byte, string>>> StaticNetworkNodesMap = [];


        /// <summary>
        /// A Dictionary of ScenePath to NodePath to PropertyName to CollectedNetworkProperty.
        /// It includes all child Network Nodes within the Scene including itself, but not nested network scenes.
        /// </summary>
        internal static Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkProperty>>> PropertiesMap = [];
        // internal static Dictionary<string, Dictionary<string, Dictionary<string, CollectedNetworkFunction>>> FUNCTIONS_MAP = [];
        // internal static Dictionary<string, Dictionary<byte, CollectedNetworkFunction>> FUNCTION_LOOKUP = [];
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
        }

        private long GetInterestMask(IPropertySymbol property)
        {
            var attribute = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "NetworkProperty");
            if (attribute != null)
            {
                var interestMaskArgument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "InterestMask");
                if (interestMaskArgument.Value.Value != null)
                {
                    return (long)interestMaskArgument.Value.Value;
                }
            }
            return 0;
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var scenes = context.AdditionalFiles.Where(f => f.Path.EndsWith(".tscn"));
            Dictionary<string, ClassData[]> sceneClasses = context.Compilation.SyntaxTrees
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
                                return new ClassData
                                {
                                    ClassSymbol = t,
                                    Properties = props
                                };
                            }).ToArray();

                            return new { FilePath = st.FilePath, Data = classes };
                        }))
                        .ToDictionary(x => x.FilePath.Replace("\\", "/"), x => x.Data);

            var sceneClassPaths = sceneClasses.Keys.ToImmutableHashSet();
            // Pretty print the scene classes
            foreach (var sceneClass in sceneClasses)
            {
                Debug.WriteLine($"Scene class: {sceneClass.Key}, {sceneClass.Value}");
            }

            var classesWithAttribute = context.Compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot()
                        .DescendantNodes()
                        .Where(n => n is ClassDeclarationSyntax)
                        .Select(n => n as ClassDeclarationSyntax)
                        .Where(r => r != null && r.AttributeLists
                            .SelectMany(al => al.Attributes)
                            .Any(a => a.Name.GetText().ToString() == "NetworkScenes")));

            Dictionary<string, HashSet<string>> networkNodeTree = new Dictionary<string, HashSet<string>>();

            // Collect all "scenePaths" values from the NetworkScenes attribute
            foreach (var classWithAttribute in classesWithAttribute)
            {
                if (classWithAttribute == null)
                {
                    continue;
                }
                var targetType = ModelExtensions.GetDeclaredSymbol(context.Compilation.GetSemanticModel(classWithAttribute.SyntaxTree), classWithAttribute) as INamedTypeSymbol;
                if (targetType == null)
                {
                    continue;
                }
                var attribute = targetType.GetAttributes().First(a => a.AttributeClass?.Name == "NetworkScenes");
                var scenePaths = attribute.ConstructorArguments.First().Values.Select(v => v.Value?.ToString());
                foreach (var scenePath in scenePaths)
                {
                    if (scenePath == null)
                    {
                        continue;
                    }
                    var sceneFile = scenes.FirstOrDefault(f => f.Path.Contains(scenePath.Replace("res://", "").Replace("/", "\\")));
                    Debug.WriteLine(scenePath);
                    if (sceneFile == null)
                    {
                        throw new System.Exception($"Scene file not found: {scenePath}");
                    }
                    var parser = new ConfigParser();
                    var parsedTscn = parser.ParseTscnFile(sceneFile.GetText()?.ToString() ?? "");
                    byte sceneId = (byte)ScenesMap.Count;
                    ScenesMap.Add(sceneId, scenePath);

                    StaticNetworkNodesMap[sceneId] = new List<Tuple<byte, string>>();
                    PropertiesMap[scenePath] = new Dictionary<string, Dictionary<string, CollectedNetworkProperty>>();
                    // FUNCTIONS_MAP[scenePath] = new Dictionary<string, Dictionary<string, CollectedNetworkFunction>>();
                    // FUNCTION_LOOKUP[scenePath] = new Dictionary<byte, CollectedNetworkFunction>();

                    var propertyId = -1;
                    // var functionId = -1;
                    byte nodePathId = 0;
                    foreach (var node in parsedTscn.Nodes)
                    {
                        IEnumerable<IPropertySymbol> nodeProperties;
                        if (node.Properties.TryGetValue("script", out var script))
                        {
                            var classPath = sceneClassPaths.FirstOrDefault(p => p.Contains(script));
                            Debug.WriteLine($"CLASS: {classPath}, script: {script}");
                            if (string.IsNullOrEmpty(classPath))
                            {
                                continue;
                            }
                            if (sceneClasses.TryGetValue(classPath, out var classDatas))
                            {
                                // Get all of the properties of the class
                                nodeProperties = classDatas.SelectMany(c => c.Properties);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                        var nodePath = node.Parent == null ? "." : node.Parent == "." ? node.Name : $"{node.Parent}/{node.Name}";
                        StaticNetworkNodesMap[sceneId].Add(new Tuple<byte, string>(nodePathId, nodePath));
                        nodePathId++;
                        // Now we get all properties from classWithAttribute which have the attribute "NetworkProperty"
                        foreach (var property in nodeProperties)
                        {
                            var propType = GetVariantType(property.Type);
                            var propertyCollected = new CollectedNetworkProperty
                            {
                                NodePath = nodePath,
                                Name = property.Name,
                                Type = (int)propType.Type,
                                Subtype = (int)propType.Subtype,
                                Index = (byte)++propertyId,
                                InterestMask = GetInterestMask(property)
                            };
                            if (!PropertiesMap[scenePath].ContainsKey(nodePath))
                            {
                                PropertiesMap[scenePath][nodePath] = new Dictionary<string, CollectedNetworkProperty>();
                            }
                            PropertiesMap[scenePath][nodePath].Add(property.Name, propertyCollected);
                        }
                    }
                }
            }

            // Add the source code to the compilation
            context.AddSource($"NetworkScenesRegister.g.cs", Template.Parse(ReadResource("StaticSourceTemplate.sbncs")).Render(new { ScenesMap = ScenesMap.ToArray(), StaticNetworkNodesMap = StaticNetworkNodesMap.ToArray(), PropertiesMap = PropertiesMap }, member => member.Name));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization required for this one
#if DEBUG
            // if (!Debugger.IsAttached)
            // {
            //     Debugger.Launch();
            // }
#endif
        }
    }
}