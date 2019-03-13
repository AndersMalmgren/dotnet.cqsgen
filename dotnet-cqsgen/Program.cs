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
            //args = new[] {"foo", "output.js", "Contracts.Commands.Command;Contracts.Queries.Query", "ignore-properties-on-base" };
            var ignoreBaseClassProperties = args.Any(a => a.ToLower() == "ignore-properties-on-base");

            Console.WriteLine("Starting parsing cqs contracts!");
            var assemblyPath = $"{Directory.GetCurrentDirectory()}\\{args[0]}";
            var outputPath = args[1];

            var parser = new Parser(args[2].Split(";"), assemblyPath, ignoreBaseClassProperties);
            var result = parser.Parse();
            
            Console.WriteLine($"Parsing complete, saving: {outputPath}");
            File.WriteAllText(outputPath, result);

        }
    }
}
