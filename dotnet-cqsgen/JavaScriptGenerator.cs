using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace dotnet_cqsgen
{
    public class JavaScriptGenerator : ScriptGenerator
    {
        public JavaScriptGenerator(Assembly assembly, List<Type> baseClasses, bool ignoreBaseClassProperties, bool noAssemblyInfo) : base(assembly, baseClasses, ignoreBaseClassProperties, noAssemblyInfo)
        {
            InitTypes();
        }

        public override string Generate()
        {

            return $@"//{GetHeader()}
(function() {{
{string.Join(Environment.NewLine, BuildNamespace(materializedTypes.Union(enumTypes)).Select(ns => $"   {ns}"))}

{string.Join(Environment.NewLine, BuildEnums(enumTypes))}
{string.Join(Environment.NewLine, BuildContracts(materializedTypes))}
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
                var cameled = GetProperties(contract).Select(p => CamelCased(p.Name)).ToList();
                var properties = string.Join(", ", cameled);

                yield return $@"   {contract.FullName}=function({properties}) {{
{string.Join(Environment.NewLine, cameled.Select(p => $"      this.{p} = {p};"))}
   }};
   {contract.FullName}.constructor.type=""{contract.FullName}"";";
            }
        }
    }

}
