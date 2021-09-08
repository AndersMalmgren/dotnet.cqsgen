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
            var noAssemblyInfo = args.Any(a => a.ToLower() == "no-assembly-info");

            Console.WriteLine("Starting parsing cqs contracts!");
            var assemblyPath = $"{Directory.GetCurrentDirectory()}\\{args[0]}";
            var outputPath = args[1];

            var assembly = Assembly.LoadFile(assemblyPath);
            var baseClasses = args[2].Split(";").Select(tn => assembly.GetType(tn)).ToList();

            var ext = Path.GetExtension(outputPath);

            var parser = GetGenerator(ext, assembly, baseClasses, ignoreBaseClassProperties, noAssemblyInfo);
            var result = parser.Generate();
            
            Console.WriteLine($"Parsing complete, saving: {outputPath}");
            File.WriteAllText(outputPath, result);
        }

        private static ScriptGenerator GetGenerator(string ext, Assembly assembly, List<Type> baseClasses, bool ignoreBaseClassProperties, bool noAssemblyInfo)
        {
            switch (ext.ToLower())
            {
                case ".js":
                    return new JavaScriptGenerator(assembly, baseClasses, ignoreBaseClassProperties, noAssemblyInfo);
                case ".ts":
                    return new TypeScriptGenerator(assembly, baseClasses, ignoreBaseClassProperties, noAssemblyInfo);
                default:
                    throw new ArgumentException($"Extension {ext} not supported");
            }
        }



    }
}
