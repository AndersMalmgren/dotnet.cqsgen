using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace dotnet_cqsgen
{
    public abstract class ScriptGenerator 
    {
        protected readonly Assembly assembly;
        protected readonly List<Type> baseClasses;
        protected readonly IEnumerable<Type> concreteTypes;
        protected readonly IEnumerable<Type> enumTypes;
        protected readonly bool ignoreBaseClassProperties;

        public ScriptGenerator(Assembly assembly, List<Type> baseClasses, IEnumerable<Type> concreteTypes, IEnumerable<Type> enumTypes, bool ignoreBaseClassProperties)
        {
            this.assembly = assembly;
            this.baseClasses = baseClasses;
            this.concreteTypes = concreteTypes;
            this.enumTypes = enumTypes;
            this.ignoreBaseClassProperties = ignoreBaseClassProperties;
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

        public abstract string Generate();
    }
}