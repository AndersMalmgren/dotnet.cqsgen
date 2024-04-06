using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace dotnet_cqsgen
{
    public abstract class ScriptGenerator 
    {
        protected readonly Assembly assembly;
        protected readonly List<Type> baseClasses;

        protected IEnumerable<Type> materializedTypes;
        protected IEnumerable<Type> enumTypes;

        protected readonly bool ignoreBaseClassProperties;
        private readonly bool noAssemblyInfo;

        public ScriptGenerator(Assembly assembly, List<Type> baseClasses, bool ignoreBaseClassProperties, bool noAssemblyInfo)
        {
            this.assembly = assembly;
            this.baseClasses = baseClasses;

            this.ignoreBaseClassProperties = ignoreBaseClassProperties;
            this.noAssemblyInfo = noAssemblyInfo;
        }

        protected void InitTypes(bool includeAbstract = false, Func<IEnumerable<Type>, IEnumerable<Type>> extractAdditonalTypes = null)
        {
            var predicate = new Func<Type, Type, bool>((t, t2) =>
            {
                if (t2.IsAssignableFrom(t)) return true;
                return t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == t2);
            });

            var allTypes = assembly.GetTypes();
            var concreteTypes = allTypes
                .Where(t => (includeAbstract || !t.IsAbstract) && baseClasses.Any(bc => predicate(t, bc)))
                .ToList();

            var extraTypes = (extractAdditonalTypes?.Invoke(concreteTypes) ?? Enumerable.Empty<Type>())
                .ToList();

            concreteTypes = concreteTypes
                .Union(extraTypes)
                .ToList();

            var containtedTypes = concreteTypes
                .SelectMany(FindProperties)
                .Distinct()
                .Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t)
                .ToList();

            materializedTypes = containtedTypes
                .Union(concreteTypes)
                .Where(mt => !mt.IsEnum)
                .ToList();

            enumTypes = materializedTypes
                .SelectMany(c => c.GetProperties().Select(p => GetUnderlyingType(p.PropertyType)).Where(pt => pt.IsEnum))
                .Union(concreteTypes.Where(mt => mt.IsEnum))
                .Distinct()
                .ToList();

        }

        private IEnumerable<Type> FindProperties(Type t)
        {
            var types = t.GetProperties().Select(p => ExtractElementFromArray(p.PropertyType)).Where(pt => !pt.IsValueType && pt.Assembly == assembly).ToList();
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

            return type;
        }

        protected Type ExtractElementFromArray(Type propType)
        {
            if (typeof(IEnumerable).IsAssignableFrom(propType))
            {
                var args = propType.GetGenericArguments();
                if (args.Length > 0) return args[0];
            }

            return propType;
        }

        protected IEnumerable<PropertyInfo> GetProperties(Type contract)
        {
            return contract
                .GetProperties()
                .Where(p => !ignoreBaseClassProperties || baseClasses.All(bc => bc != p.DeclaringType));
        }

        protected string CamelCased(string pascalCased)
        {
            return pascalCased.Substring(0, 1).ToLower() + pascalCased.Substring(1);
        }

        protected string GetHeader()
        {
            var info = noAssemblyInfo ? "a tool" : assembly.FullName;
            return $"Generated from {info}, do not modify https://github.com/AndersMalmgren/dotnet.cqsgen";
        }

        public abstract string Generate();
    }
}