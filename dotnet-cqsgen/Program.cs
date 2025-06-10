using System;
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
            var alwaysConflictCheckNameSpace = args.Any(a => a.ToLower() == "namespace-conflict-check");

            Console.WriteLine("Starting parsing cqs contracts!");
            var assemblyPath = $"{Directory.GetCurrentDirectory()}\\{args[0]}";
            var assemblyFolder = Path.GetDirectoryName(assemblyPath) ?? throw new NullReferenceException();
            var outputPath = args[1];

            var assembly = Assembly.LoadFile(assemblyPath);

            var loadedAssemblies = new List<Assembly>([assembly]);
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
            {
                var asm = Assembly.LoadFile($"{assemblyFolder}\\{new AssemblyName(e.Name).Name}.dll");
                loadedAssemblies.Add(asm);
                return asm;
            };

            var baseClasses = args[2].Split(";").Select(tn => GetBaseType(assembly, loadedAssemblies, tn)).ToList();

            var ext = Path.GetExtension(outputPath);

            var parser = GetGenerator(ext, assembly, loadedAssemblies, baseClasses, ignoreBaseClassProperties, noAssemblyInfo, alwaysConflictCheckNameSpace);
            var result = parser.Generate();
            
            Console.WriteLine($"Parsing complete, saving: {outputPath}");
            File.WriteAllText(outputPath, result);
        }

        private static Type GetBaseType(Assembly assembly, IEnumerable<Assembly> loadedAssemblies, string typeName)
        {
            _ = assembly.GetTypes();
            loadedAssemblies = loadedAssemblies.OrderByDescending(asm => asm == assembly);
            var type = loadedAssemblies.Select(asm =>
            {
                var type = asm.GetType(typeName);
                if (type is { IsInterface: false }) return type;

                var allWithName = asm.GetTypes().Where(t => t.FullName?.StartsWith(typeName, StringComparison.Ordinal) == true)
                    .ToList();

                return allWithName.SingleOrDefault(t => !allWithName.Any(other => other.GetInterfaces().Contains(t)));

            }).First(t => t != null);
            return type;
        }

        private static ScriptGenerator GetGenerator(string ext, Assembly assembly, IReadOnlyCollection<Assembly> loadedAssemblies, List<Type> baseClasses, bool ignoreBaseClassProperties, bool noAssemblyInfo, bool alwaysConflictCheckNameSpace)
        {
            switch (ext.ToLower())
            {
                case ".js":
                    return new JavaScriptGenerator(assembly, loadedAssemblies, baseClasses, ignoreBaseClassProperties, noAssemblyInfo);
                case ".ts":
                    return new TypeScriptGenerator(assembly, loadedAssemblies, baseClasses, ignoreBaseClassProperties, noAssemblyInfo, alwaysConflictCheckNameSpace);
                default:
                    throw new ArgumentException($"Extension {ext} not supported");
            }
        }



    }
}
