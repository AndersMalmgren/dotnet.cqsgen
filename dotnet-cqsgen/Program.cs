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
            //args = new[] {"foo", "output.js", "Foobar.Command;Foobar.Query" };
            Console.WriteLine("Starting parsing cqs contracts!");
            var assemblyPath = $"{Directory.GetCurrentDirectory()}\\{args[0]}";
            var outputPath = args[1];

            var parser = new Parser();
            var result = parser.Parse(args[2].Split(";"), assemblyPath);


            Console.WriteLine($"Parsing complete, saving: {outputPath}");
            File.WriteAllText(outputPath, result);

        }
    }
}
