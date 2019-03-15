using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace dotnet_cqsgen
{
    class Program
    {
        static void Main(string[] args)
        {
            var ignoreBaseClassProperties = args.Any(a => a.ToLower() == "ignore-properties-on-base");

            Console.WriteLine("Starting parsing cqs contracts!");
            var assemblyPath = $"{Directory.GetCurrentDirectory()}\\{args[0]}";
            var outputPath = args[1];

            var assembly = Assembly.LoadFile(assemblyPath);
            var baseClasses = args[2].Split(";").Select(tn => assembly.GetType(tn)).ToList();

            var allTypes = assembly.GetTypes();
            var concreteTypes = allTypes
                .Where(t => !t.IsAbstract && baseClasses.Any(bc => bc.IsAssignableFrom(t)))
                .ToList();

            var containtedTypes = concreteTypes
                .SelectMany(t => GetProperties(t, assembly))
                .Distinct()
                .ToList();

            concreteTypes = containtedTypes
                .Union(concreteTypes)
                .ToList();

            var enumTypes = concreteTypes
                .SelectMany(c => c.GetProperties().Where(p => p.PropertyType.IsEnum).Select(p => p.PropertyType))
                .Distinct()
                .ToList();

            var ext = Path.GetExtension(outputPath);

            var parser = GetGenerator(ext, assembly, baseClasses, concreteTypes, enumTypes,ignoreBaseClassProperties);
            var result = parser.Generate();
            
            Console.WriteLine($"Parsing complete, saving: {outputPath}");
            File.WriteAllText(outputPath, result);
        }

        private static ScriptGenerator GetGenerator(string ext, Assembly assembly, List<Type> baseClasses, IEnumerable<Type> concreteTypes, IEnumerable<Type> enumTypes, bool ignoreBaseClassProperties)
        {
            switch (ext.ToLower())
            {
                case ".js":
                    return new JavaScriptGenerator(assembly, baseClasses, concreteTypes, enumTypes, ignoreBaseClassProperties);
                case ".ts":
                    return new TypeScriptGenerator(assembly, baseClasses, concreteTypes, enumTypes, ignoreBaseClassProperties);
                default:
                    throw new ArgumentException($"Extension {ext} not supported");
            }
        }

        private static IEnumerable<Type> GetProperties(Type t, Assembly assembly)
        {
            var types = t.GetProperties().Select(p => ExtractElementFromArray(p.PropertyType)).Where(pt => !pt.IsValueType && pt.Assembly == assembly).ToList();
            foreach (var type in types)
            {
                if (type.Name == "InvoiceStatus")
                {

                }

                yield return type;
                foreach (var inner in GetProperties(type, assembly).ToList())
                    yield return inner;
            }
        }

        private static Type ExtractElementFromArray(Type propType)
        {
            if (typeof(IEnumerable).IsAssignableFrom(propType))
            {
                var args = propType.GetGenericArguments();
                if (args.Length > 0) return args[0];
            }

            return propType;
        }

    }
}
