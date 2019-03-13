using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace dotnet_cqsgen
{
    public class Parser
    {
        public string Parse(IEnumerable<string> baseclassTypes, string assemblyPath)
        {
            var assembly = Assembly.LoadFile(assemblyPath);
            var baseClasses = baseclassTypes.Select(tn => assembly.GetType(tn)).ToList();

            var allTypes = assembly.GetTypes();
            var concreteTypes = allTypes
                .Where(t => !t.IsAbstract && baseClasses.Any(bc => bc.IsAssignableFrom(t)))
                .ToList();

            var enumTypes = concreteTypes
                .SelectMany(c => c.GetProperties().Where(p => p.PropertyType.IsEnum).Select(p => p.PropertyType))
                .Distinct()
                .ToList();


            return $@"(function() {{
{string.Join(Environment.NewLine, BuildNamespace(concreteTypes).Select(ns => $"   {ns}"))}

{string.Join(Environment.NewLine, BuildEnums(enumTypes))}
{string.Join(Environment.NewLine, BuildContracts(concreteTypes))}
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

        public IEnumerable<string> BuildContracts(IEnumerable<Type> contracts)
        {
            foreach (var contract in contracts)
            {
                var cameled = contract.GetProperties().Select(p => CamelCased(p.Name)).ToList();
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
