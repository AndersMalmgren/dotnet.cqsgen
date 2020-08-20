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

        public TypeScriptGenerator(Assembly assembly, List<Type> baseClasses, bool ignoreBaseClassProperties) : base(assembly, baseClasses, ignoreBaseClassProperties)
        {
            InitTypes(true, types => types.Where(t => t.BaseType.IsGenericType && t.BaseType.GenericTypeArguments.Length > 0).SelectMany(t => t.BaseType.GenericTypeArguments.Select(ExtractElementFromArray).Where(arg => arg.Assembly == assembly)));

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
                { typeof(bool), "boolean" }
            };
        }

        public override string Generate()
        {
            return string.Join(Environment.NewLine, GenerateInternal());
        }

        private IEnumerable<string> GenerateInternal()
        {
            var namespaces = materializedTypes
                .GroupBy(c => c.Namespace)
                .OrderBy(ns => !ns.Any(c => baseClasses.Any(bc => bc == c)));

            foreach (var ns in namespaces)
            {
                yield return $"export namespace {ns.Key} {{";

                foreach (var @enum in enumTypes.Where(e => e.Namespace == ns.Key))
                {
                    var values = Enum.GetValues(@enum).Cast<int>();

                    yield return $"    export enum {@enum.Name} {{";
                    foreach (var value in values)
                        yield return $"        {Enum.GetName(@enum, value)} = {value},";
                    yield return "    }";
                }

                foreach (var grp in ns.GroupBy(contract => StripGenericsFromName(contract.Name)))
                {
                    var grpList = grp.ToList();
                    var contract = grpList.Count == 1 ? grpList[0] : grpList.FirstOrDefault(c => c.IsGenericType);
                    var hasDefaultGenericArguments = grpList.Count > 1;

                    var contractName = grp.Key;

                    var baseContract = contract.BaseType?.Assembly == assembly && (!hasDefaultGenericArguments || grpList.All(bc => bc != contract.BaseType)) ? contract.BaseType : null;
                    var hasBaseContract = baseContract != null;
                    
                    var properties = GetProperties(contract)
                        .Select(p => new { IsBaseProperty = p.DeclaringType != contract && grpList.All(bc => p.DeclaringType != bc), CamelCased = CamelCased(p.Name), TypeName = GetPropertyTypeName(p.PropertyType, ns.Key), IsNullable = Nullable.GetUnderlyingType(p.PropertyType) != null, NullablePostfix = Nullable.GetUnderlyingType(p.PropertyType) != null ? "?":"" })
                        .OrderBy(p => !p.IsBaseProperty)
                        .ToList();

                    var extends = hasBaseContract ? $" extends {StripGenericsFromName(GetPropertyTypeName(baseContract, ns.Key))}{GetGenerics(baseContract, ns.Key)}" : string.Empty;

                    yield return $"    export class {contractName}{GetGenerics(contract, ns.Key, hasDefaultGenericArguments)}{extends} {{";
                    foreach (var p in properties.Where(p => !p.IsBaseProperty)) yield return $"        {CamelCased(p.CamelCased)}{p.NullablePostfix}: {p.TypeName};";
                    foreach (var p in (contract as TypeInfo)?.GenericTypeParameters ?? Enumerable.Empty<Type>()) yield return $"        private _dummy{p.Name}:{p.Name};";

                    if(hasBaseContract || properties.Any())
                    { 
                        yield return $"        constructor({string.Join(", ", properties.OrderBy(p => p.IsNullable).Select(p => $"{p.CamelCased}{p.NullablePostfix}:{p.TypeName}"))}) {{";
                        if (hasBaseContract) yield return $"            super({string.Join(", ", properties.Where(p => p.IsBaseProperty).Select(p => p.CamelCased))});";
                        foreach (var p in properties.Where(p => !p.IsBaseProperty)) yield return $"            this.{p.CamelCased}={p.CamelCased};";
                        yield return $"            this.constructor['type']='{contract.FullName}';";
                        yield return "        }";
                    }
                    yield return "    }";
                }
    
                yield return "}";
            }
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

        private string GetPropertyTypeName(Type type, string ns)
        {
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

            if (type.Assembly == assembly)
            {
                if (ns == type.Namespace || type.Namespace == null) return type.Name;

                var closure = ns.Split(".");
                var dependency = type.Namespace.Split(".");

                var start = GetNamespaceStart(closure, dependency);
                var stripped = string.Join(".", dependency.Skip(start));
                return $"{stripped}.{type.Name}";
            }

            return type.Name;
        }

        private int GetNamespaceStart(string[] closure, string[] dependency)
        {
            for (int i = 0; i < dependency.Length; i++)
            {
                if (i >= closure.Length || closure[i] != dependency[i]) return i;
            }

            return dependency.Length-1;
        }

    }
}