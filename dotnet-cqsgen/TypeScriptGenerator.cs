using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace dotnet_cqsgen
{
    public class TypeScriptGenerator : ScriptGenerator
    {
        private readonly Dictionary<Type, string> typeMapping;
        private readonly List<IGrouping<string, Type>> namespaces;

        public TypeScriptGenerator(Assembly assembly, IReadOnlyCollection<Assembly> loadedAssemblies, List<Type> baseClasses, bool ignoreBaseClassProperties, bool noAssemblyInfo) : base(assembly, loadedAssemblies, baseClasses, ignoreBaseClassProperties, noAssemblyInfo)
        {
            InitTypes(true, types => types
                .SelectMany(t => (t.BaseType?.GenericTypeArguments ?? Enumerable.Empty<Type>()).Union(t.GetInterfaces().Where(i => i.IsGenericType).SelectMany(i => i.GetGenericArguments())))
                .Select(ExtractElementFromArray)
                .Where(t => t.Assembly == assembly));

            typeMapping = new Dictionary<Type, string>
            {
                { typeof(string), "string" },
                { typeof(Guid), "string" },
                { typeof(int), "number" },
                { typeof(decimal), "number" },
                { typeof(float), "number" },
                { typeof(long), "number" },
                { typeof(byte), "number" },
                { typeof(DateTime), "Date" },
                { typeof(bool), "boolean" },
                { typeof(byte[]), "string" }
            };

            namespaces = MaterializedTypes.Union(EnumTypes)
                .GroupBy(c => c.Namespace)
                .OrderBy(ns => !ns.Any(c => BaseClasses.Any(bc => bc == c)))
                .ToList();
        }

        public override string Generate()
        {
            return string.Join(Environment.NewLine, GenerateInternal());
        }

        private IEnumerable<string> GenerateInternal()
        {
            yield return "//" + GetHeader();


            foreach (var ns in namespaces)
            {
                yield return $"export namespace {ns.Key} {{";

                foreach (var @enum in ns.Where(contract => contract.IsEnum))
                {
                    var values = Enum.GetValues(@enum).Cast<int>();

                    yield return $"    export enum {@enum.Name} {{";
                    foreach (var value in values)
                        yield return $"        {Enum.GetName(@enum, value)} = {value},";
                    yield return "    }";
                }

                foreach (var grp in ns.Where(contract => !contract.IsEnum).OrderBy(contract => !BaseClasses.Contains(contract)).GroupBy(contract => StripGenericsFromName(contract.Name)))
                {
                    var grpList = grp.ToList();
                    var contract = grpList.Count == 1 ? grpList[0] : grpList.First(c => c.IsGenericType);
                    var hasDefaultGenericArguments = grpList.Count > 1;

                    var contractName = grp.Key;

                    var baseType = GetBaseType(contract);
                    var baseContract = LoadedAssemblies.Contains(baseType?.Assembly)  && (!hasDefaultGenericArguments || grpList.All(bc => bc != baseType)) ? baseType : null;
                    var hasBaseContract = baseContract != null;
                    
                    var properties = GetProperties(contract)
                        .Select(p => new { p.PropertyType, IsBaseProperty = p.DeclaringType != contract && grpList.All(bc => p.DeclaringType != bc), CamelCased = CamelCased(p.Name), TypeName = GetPropertyTypeName(p.PropertyType, ns.Key), IsNullable = Nullable.GetUnderlyingType(p.PropertyType) != null, NullablePostfix = Nullable.GetUnderlyingType(p.PropertyType) != null ? "?":"" })
                        .OrderBy(p => !p.IsBaseProperty)
                        .ToList();

                    var extends = hasBaseContract ? $" extends {StripGenericsFromName(GetPropertyTypeName(baseContract, ns.Key, true))}" : string.Empty;
                    var typeOverride = hasBaseContract ? "override " : string.Empty;

                    yield return $"    export class {contractName}{GetGenerics(contract, ns.Key, hasDefaultGenericArguments)}{extends} {{";
                    yield return $"        static {typeOverride}type='{StripGenericsFromName(contract.FullName)}';";
                    foreach (var p in properties.Where(p => !p.IsBaseProperty)) yield return $"        {CamelCased(p.CamelCased)}{p.NullablePostfix}: {p.TypeName};";
                    foreach (var p in ((contract as TypeInfo)?.GenericTypeParameters ?? Enumerable.Empty<Type>()).Where(gt => properties.All(p => p.PropertyType != gt))) yield return $"        private _dummy{p.Name}:{p.Name};";

                    if(hasBaseContract || properties.Any())
                    { 
                        yield return $"        constructor({string.Join(", ", properties.OrderBy(p => p.IsNullable).Select(p => $"{p.CamelCased}{p.NullablePostfix}:{p.TypeName}"))}) {{";
                        if (hasBaseContract) yield return $"            super({string.Join(", ", properties.Where(p => p.IsBaseProperty).OrderBy(p => p.IsNullable).Select(p => p.CamelCased))});";
                        foreach (var p in properties.Where(p => !p.IsBaseProperty)) yield return $"            this.{p.CamelCased}={p.CamelCased};";
                        yield return "        }";
                    }
                    yield return "    }";
                }
    
                yield return "}";
            }
        }

        private Type GetBaseType(Type contract)
        {
            if (contract.BaseType != typeof(object)) return contract.BaseType;
            var directlyImplementedInterfaces = contract.GetInterfaces().Where(i => !contract.GetInterfaces().Any(baseInterface => baseInterface.GetInterfaces().Contains(i)));
            var @interface = directlyImplementedInterfaces.Select(i => (i.IsGenericType ? i.GetGenericTypeDefinition() : i, i)).SingleOrDefault(i => BaseClasses.Contains(i.Item1));

            return @interface.i;
        }

        private string GetGenerics(Type contract, string ns, bool hasDefaultGenericArguments = false)
        {
            if (!contract.IsGenericType) return string.Empty;
            var info = (TypeInfo)contract;
            var types = info.GenericTypeParameters.Length > 0 ? info.GenericTypeParameters : info.GenericTypeArguments;
            var defaultArg = hasDefaultGenericArguments ? " = void" : string.Empty;

            return $"<{string.Join(", ", types.Select(t => GetPropertyTypeName(t, ns)))}{defaultArg}>";
        }

        private string StripGenericsFromName(string name)
        {
            var index = name.IndexOf("`", StringComparison.Ordinal);
            if (index < 0) return name;

            return name.Substring(0, index);
        }

        private string GetPropertyTypeName(Type type, string ns, bool extending = false)
        {
            string GetName()
            {
                if (!type.IsGenericType) return type.Name;

                var info = (TypeInfo)type;

                return $"{StripGenericsFromName(type.Name)}<{string.Join(", ", (info.GenericTypeParameters.Length > 0 ? info.GenericTypeParameters : info.GenericTypeArguments).Select(gta => GetPropertyTypeName(gta, ns)))}>";
            }

            if (typeMapping.ContainsKey(type)) return typeMapping[type];
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var elementType = type.GetElementType();
                if (elementType == null)
                {
                    var args = type.GetGenericArguments();
                    if (args.Length > 0)
                        elementType = args[0];
                }

                if (elementType != null) return $"{GetPropertyTypeName(elementType, ns)}[]";
            }

            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                return GetPropertyTypeName(nullableType, ns);
            }

            if (LoadedAssemblies.Contains(type.Assembly))
            {
                if (ns == type.Namespace || type.Namespace == null) return GetName();
                var stripped = GetSharedNameSpace(ns, type.Namespace, namespaces, extending);
                if(string.IsNullOrEmpty(stripped)) return GetName();
                return $"{stripped}.{GetName()}";
            }

            return GetName();
        }

        private string GetSharedNameSpace(string ns, string dependencyNs, IEnumerable<IGrouping<string, Type>> allNamespaces, bool conflictCheck)
        {
            var closure = ns.Split(".");
            var dependency = dependencyNs.Split(".");

            var start = GetNamespaceStart(closure, dependency);
            if (start == dependency.Length) return string.Empty;

            var stripped = string.Join(".", dependency.Skip(start));
            if (!conflictCheck) return stripped; 

            foreach (var existingNs in allNamespaces)
            {
                if(dependencyNs == existingNs.Key) continue;
                
                var existing = existingNs.Key.Split(".");
                var sharedEnd = GetNamespaceStart(closure, existing);

                if (sharedEnd >= existing.Length) continue;

                var existingËndShared = existing[sharedEnd];
                var depStart = dependency[start];

                if(depStart == existingËndShared) return dependencyNs;
            }

            return stripped;
        }

        private int GetNamespaceStart(string[] closure, string[] dependency)
        {
            for (int i = 0; i < dependency.Length; i++)
            {
                if (i >= closure.Length || closure[i] != dependency[i]) return i;
            }

            return dependency.Length;
        }

    }
}