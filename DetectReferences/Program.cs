using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DetectReferences
{
    class Program
    {
        private static readonly HashSet<string> _editorAssemblies = new HashSet<string>
        {
            "Microsoft.WebTools.Languages.Css",
            "Microsoft.WebTools.Languages.Css.Editor",
            "Microsoft.WebTools.Languages.Extensions",
            "Microsoft.WebTools.Languages.Html",
            "Microsoft.WebTools.Languages.Html.Editor",
            "Microsoft.WebTools.Languages.Html.VS",
            "Microsoft.WebTools.Languages.Json.Arm",
            "Microsoft.WebTools.Languages.Json",
            "Microsoft.WebTools.Languages.Json.Editor",
            "Microsoft.WebTools.Languages.Json.VS",
            "Microsoft.WebTools.Languages.LanguageServer.Delegation",
            "Microsoft.WebTools.Languages.LanguageServer.Server",
            "Microsoft.WebTools.Languages.Razor.2",
            "Microsoft.WebTools.Languages.Razor.3",
            "Microsoft.WebTools.Languages.Razor.Core",
            "Microsoft.WebTools.Languages.Shared",
            "Microsoft.WebTools.Languages.Shared.Editor",
            "Microsoft.WebTools.Languages.Shared.VS",
        };

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveAssembly;

            if (args.Length < 0)
            {
                Console.WriteLine("Please specify an install path");
                return;
            }

            string rootPath = args[0];

            foreach (string file in Directory.EnumerateFiles(rootPath, "*.dll", SearchOption.AllDirectories))
            {
                if (_editorAssemblies.Contains(Path.GetFileNameWithoutExtension(file)))
                {
                    continue;
                }

                IEnumerable<string> editorDependencies = FindEditorDependencies(file);

                if (editorDependencies.Any())
                {
                    Console.WriteLine(file);

                    foreach (string editorDependency in editorDependencies)
                    {
                        Console.WriteLine($"  {editorDependency}");
                    }
                }
            }
        }

        private static readonly IEnumerable<string> AssemblyPaths = new string[]
        {
            @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Enterprise\Common7\IDE",
            @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Enterprise\Common7\IDE\PublicAssemblies",
            @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Enterprise\Common7\IDE\PrivateAssemblies",
        };

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            foreach (string path in AssemblyPaths)
            {
                try
                {
                    AssemblyName assemblyName = new AssemblyName(args.Name);

                    string pathToTest = Path.Combine(path, assemblyName.Name + ".dll");

                    if (File.Exists(pathToTest))
                    {
                        return Assembly.ReflectionOnlyLoadFrom(pathToTest);
                    }
                }
                catch
                {
                    // Try the next one
                }
            }

            return null;
        }

        private static IEnumerable<string> FindEditorDependencies(string file)
        {
            List<string> editorReferences = new List<string>();

            try
            {
                Assembly assembly = Assembly.LoadFrom(file);

                AssemblyName[] references = assembly.GetReferencedAssemblies();

                foreach (AssemblyName reference in references)
                {
                    if (_editorAssemblies.Contains(reference.Name))
                    {
                        editorReferences.Add(reference.Name);
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // Not a managed DLL
            }
            catch (FileLoadException)
            {
                // Probably a duplicate DLL
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType().Name} processing {file}");
                Console.WriteLine($"  {ex.Message}");
            }

            return editorReferences;
        }
    }
}
