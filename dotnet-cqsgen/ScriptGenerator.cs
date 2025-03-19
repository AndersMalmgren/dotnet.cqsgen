using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace dotnet_cqsgen
{
    public abstract class ScriptGenerator 
    {
        protected readonly Assembly Assembly;
        protected readonly IEnumerable<Assembly> LoadedAssemblies;
        protected readonly List<Type> BaseClasses;

        protected IEnumerable<Type> MaterializedTypes;
        protected IEnumerable<Type> EnumTypes;

        protected readonly bool IgnoreBaseClassProperties;
        private readonly bool noAssemblyInfo;

        public ScriptGenerator(Assembly assembly, IReadOnlyCollection<Assembly> loadedAssemblies, List<Type> baseClasses, bool ignoreBaseClassProperties, bool noAssemblyInfo)
        {
            Assembly = assembly;
            LoadedAssemblies = loadedAssemblies;
            BaseClasses = baseClasses;

            IgnoreBaseClassProperties = ignoreBaseClassProperties;
            this.noAssemblyInfo = noAssemblyInfo;
        }

        protected void InitTypes(bool includeAbstract = false, Func<IEnumerable<Type>, IEnumerable<Type>> extractAdditonalTypes = null)
        {
            var predicate = new Func<Type, Type, bool>((t, t2) =>
            {
                if (t2.IsAssignableFrom(t)) return true;
                return t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == t2);
            });

            var allTypes = LoadedAssemblies.SelectMany(asm => asm.GetTypes());
            var concreteTypes = allTypes
                .Where(t => (includeAbstract || !t.IsAbstract) && BaseClasses.Any(bc => predicate(t, bc)))
                .ToList();

            var extraTypes = (extractAdditonalTypes?.Invoke(concreteTypes) ?? Enumerable.Empty<Type>())
                .ToList();

            concreteTypes = concreteTypes
                .Union(extraTypes)
                .ToList();

            var contained = concreteTypes
                .SelectMany(FindProperties)
                .Distinct()
                .Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t)
                .ToList();

            MaterializedTypes = contained
                .Union(concreteTypes)
                .Where(mt => !mt.IsEnum)
                .ToList();

            EnumTypes = MaterializedTypes
                .SelectMany(c => c.GetProperties().Select(p => GetUnderlyingType(p.PropertyType)).Where(pt => pt.IsEnum))
                .Union(concreteTypes.Where(mt => mt.IsEnum))
                .Distinct()
                .ToList();

        }

        private IEnumerable<Type> FindProperties(Type t)
        {
            var types = t.GetProperties().Select(p => ExtractElementFromArray(p.PropertyType)).Where(pt => !pt.IsValueType && pt.Assembly == Assembly).ToList();
            foreach (var type in types)
            {
                yield return type;
                foreach (var inner in FindProperties(type).ToList())
                    yield return inner;
            }
        }

        private Type GetUnderlyingType(Type type)
        {
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null) return nullableType;

            return ExtractElementFromArray(type);
        }

        protected Type ExtractElementFromArray(Type propType)
        {
            if (typeof(IEnumerable).IsAssignableFrom(propType))
            {
                var args = propType.GetGenericArguments();
                if (args.Length > 0) return args[0];
                if (propType.IsArray) return propType.GetElementType();
            }

            return propType;
        }

        protected IEnumerable<PropertyInfo> GetProperties(Type contract)
        {
            return contract
                .GetProperties()
                .Where(p => !IgnoreBaseClassProperties || BaseClasses.All(bc => bc != p.DeclaringType));
        }

        protected string CamelCased(string pascalCased)
        {
            return pascalCased.Substring(0, 1).ToLower() + pascalCased.Substring(1);
        }

        protected string GetHeader()
        {
            var info = noAssemblyInfo ? "a tool" : Assembly.FullName;
            return $"Generated from {info}, do not modify https://github.com/AndersMalmgren/dotnet.cqsgen";
        }

        public abstract string Generate();
    }
}