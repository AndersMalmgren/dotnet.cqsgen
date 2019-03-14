using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace dotnet_cqsgen
{
    public class Parser
    {
        private readonly IEnumerable<string> _baseclassTypes;
        private readonly string _assemblyPath;
        private readonly bool _ignoreBaseClassProperties;


        public Parser(IEnumerable<string> baseclassTypes, string assemblyPath, bool ignoreBaseClassProperties)
        {
            _baseclassTypes = baseclassTypes;
            _assemblyPath = assemblyPath;
            _ignoreBaseClassProperties = ignoreBaseClassProperties;
        }

        public string Parse()
        {
            var assembly = Assembly.LoadFile(_assemblyPath);
            var baseClasses = _baseclassTypes.Select(tn => assembly.GetType(tn)).ToList();
            
            var allTypes = assembly.GetTypes();
            var concreteTypes = allTypes
                .Where(t => !t.IsAbstract && baseClasses.Any(bc => bc.IsAssignableFrom(t)))
                .SelectMany(t => new[] { t }.Union(t.GetProperties().Where(p => p.PropertyType.IsClass && t.Assembly == assembly).Select(p => p.PropertyType)))
                .ToList();

            var enumTypes = concreteTypes
                .SelectMany(c => c.GetProperties().Where(p => p.PropertyType.IsEnum).Select(p => p.PropertyType))
                .Distinct()
                .ToList();


            return $@"(function() {{
{string.Join(Environment.NewLine, BuildNamespace(concreteTypes.Union(enumTypes)).Select(ns => $"   {ns}"))}

{string.Join(Environment.NewLine, BuildEnums(enumTypes))}
{string.Join(Environment.NewLine, BuildContracts(concreteTypes, baseClasses))}
}})();
";
        }
        public IEnumerable<string> BuildNamespace(IEnumerable<Type> contracts)
        {
            var window = new Namespace();
            foreach (var contract in contracts)
            {
                window.Add(contract.Namespace.Split('.'));
            }

            return window.Render();
        }

        public IEnumerable<string> BuildEnums(IEnumerable<Type> enums)
        {
            foreach (var @enum in enums)
            {
                var values = Enum.GetValues(@enum).Cast<int>();

                yield return $@"   {@enum.FullName} = {{
{string.Join($", {Environment.NewLine}", values.Select(v => $"      {Enum.GetName(@enum, v)}: {v}"))}
   }};";
            }
        }

        public IEnumerable<string> BuildContracts(IEnumerable<Type> contracts, IEnumerable<Type> baseTypes)
        {
            foreach (var contract in contracts)
            {
                var cameled = contract.GetProperties().Where(p => !_ignoreBaseClassProperties || baseTypes.All(bc => bc != p.DeclaringType)).Select(p => CamelCased(p.Name)).ToList();
                var properties = string.Join(", ", cameled);

                yield return $@"   {contract.FullName}=function({properties}) {{
{string.Join(Environment.NewLine, cameled.Select(p => $"      this.{p} = {p};"))}
   }};
   {contract.FullName}.constructor.type=""{contract.FullName}"";";
            }
        }

        public string CamelCased(string pascalCased)
        {
            return pascalCased.Substring(0, 1).ToLower() + pascalCased.Substring(1);
        }
    }

}
