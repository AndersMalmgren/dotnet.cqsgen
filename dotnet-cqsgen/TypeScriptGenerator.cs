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

        public TypeScriptGenerator(Assembly assembly, List<Type> baseClasses, IEnumerable<Type> concreteTypes, IEnumerable<Type> enumTypes, bool ignoreBaseClassProperties) : base(assembly, baseClasses, concreteTypes, enumTypes, ignoreBaseClassProperties)
        {
            typeMapping = new Dictionary<Type, string>
            {
                { typeof(string), "string" },
                { typeof(Guid), "string" },
                { typeof(int), "number" },
                { typeof(decimal), "number" },
                { typeof(float), "number" },
                { typeof(long), "number" },
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
            var namespaces = concreteTypes
                .Union(baseClasses)
                .GroupBy(c => c.Namespace)
                .OrderBy(ns => !ns.Any(c => baseClasses.Any(bc => bc == c)));

            foreach (var ns in namespaces)
            {
                yield return $"namespace {ns.Key} {{";

                foreach (var @enum in enumTypes.Where(e => e.Namespace == ns.Key))
                {
                    var values = Enum.GetValues(@enum).Cast<int>();

                    yield return $"    export enum {@enum.Name} {{";
                    foreach (var value in values)
                        yield return $"        {Enum.GetName(@enum, value)} = {value},";
                    yield return "    }";
                }

                foreach (var contract in ns)
                {
                    var baseContract = baseClasses.FirstOrDefault(bc => bc != contract && bc.IsAssignableFrom(contract));
                    var hasBaseContract = baseContract != null;

                    var properties = GetProperties(contract)
                        .Select(p => new { IsBaseProperty = p.DeclaringType == baseContract, CamelCased = CamelCased(p.Name), TypeName = GetPropertyTypeName(p.PropertyType, ns.Key) })
                        .OrderBy(p => !p.IsBaseProperty)
                        .ToList();

                    var extends = hasBaseContract ? $" extends {GetPropertyTypeName(baseContract, ns.Key)}" : string.Empty;

                    yield return $"    export class {contract.Name}{extends} {{";
                    foreach (var p in properties.Where(p => !p.IsBaseProperty)) yield return $"        {CamelCased(p.CamelCased)}: {p.TypeName};";
                    yield return $"        constructor({string.Join(", ", properties.Select(p => $"{p.CamelCased}:{p.TypeName}"))}) {{";
                    if (hasBaseContract) yield return $"                super({string.Join(", ", properties.Where(p => p.IsBaseProperty).Select(p => p.CamelCased))})";
                    foreach (var p in properties) yield return $"                this.{p.CamelCased}={p.CamelCased};";
                    yield return "        }";
                    yield return "    }";
                    yield return $@"    {contract.Name}[""type""]=""{contract.FullName}"";";
                }
    
                yield return "}";
            }
        }

        private string GetPropertyTypeName(Type type, string ns)
        {
            if (typeMapping.ContainsKey(type)) return typeMapping[type];
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var args = type.GetGenericArguments();
                if (args.Length > 0) return $"{GetPropertyTypeName(args[0], ns)}[]";
            }

            if (type.Assembly == assembly)
            {
                if (ns == type.Namespace) return type.Name;

                var stripIndex = 0;
                for (int i = 0; i < type.Namespace.Length; i++)
                {
                    if (i < ns.Length && ns[i] == type.Namespace[i])
                        stripIndex = i;
                }

                if (stripIndex > 0 && stripIndex + 1 < type.Namespace.Length)
                    return $"{type.Namespace.Substring(stripIndex + 1)}.{type.Name}";
            }

            return type.Name;
        }
    }
}