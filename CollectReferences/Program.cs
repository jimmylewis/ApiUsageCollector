using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace CollectReferences
{
    class Program
    {
        private class TypeReference
        {
            internal TypeReference(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public HashSet<string> Sources { get; } = new HashSet<string>();

            public HashSet<string> Members { get; } = new HashSet<string>();
        }

        private static Dictionary<string, List<TypeReference>> AllReferences = new Dictionary<string, List<TypeReference>>
        {
            { "Microsoft.CSS.Core", new List<TypeReference>() },
            { "Microsoft.CSS.Editor", new List<TypeReference>() },
            { "Microsoft.Html.Core", new List<TypeReference>() },
            { "Microsoft.Html.Editor", new List<TypeReference>() },
            { "Microsoft.VisualStudio.Html.Package", new List<TypeReference>() },
            { "Microsoft.JSON.Core", new List<TypeReference>() },
            { "Microsoft.JSON.Editor", new List<TypeReference>() },
            { "Microsoft.VisualStudio.JSON.Package", new List<TypeReference>() },
            { "Microsoft.Web.Languages", new List<TypeReference>() },
            { "Microsoft.VisualStudio.JavaScript.Web.Extensions", new List<TypeReference>() },
            { "Microsoft.VisualStudio.Web.HTML.Razor.Implementation.Shims.2_0", new List<TypeReference>() },
            { "Microsoft.VisualStudio.Web.HTML.Razor.Implementation.Shims.3_0", new List<TypeReference>() },
            { "Microsoft.VisualStudio.Web.Editors.Razor.4_0", new List<TypeReference>() },
            { "Microsoft.Web.Core", new List<TypeReference>() },
            { "Microsoft.Web.Editor", new List<TypeReference>() },
            { "Microsoft.Web.Editor.Application", new List<TypeReference>() },
            { "Microsoft.VisualStudio.Web.Editor.Package", new List<TypeReference>() },
            { "Microsoft.VisualStudio.Web.Extensions", new List<TypeReference>() },
        };

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Please provide an input file.");
                return;
            }

            TextReader inputReader = File.OpenText(args[0]);

            string line;
            while ((line = inputReader.ReadLine()) != null)
            {
                if (line.Length > 0 && !Char.IsWhiteSpace(line[0]))
                {
                    XmlDocument references = GetReferencesFrom(line);

                    CollectReferences(Path.GetFileNameWithoutExtension(line), references);
                }
            }

            foreach (KeyValuePair<string, List<TypeReference>> assembly in AllReferences.OrderBy(x => x.Key))
            {
                if (assembly.Value.Count > 0)
                {
                    Console.WriteLine("Types from {0}", assembly.Key);

                    foreach (TypeReference reference in assembly.Value.OrderBy(x => x.Name))
                    {
                        Console.Write("    {0} (", reference.Name);
                        bool comma = false;
                        foreach (string source in reference.Sources.OrderBy(x => x))
                        {
                            if (comma)
                            {
                                Console.Write(", ");
                            }
                            else
                            {
                                comma = true;
                            }

                            Console.Write(source);
                        }
                        Console.Write(")");
                        Console.WriteLine();

                        foreach (string member in reference.Members.OrderBy(x => x))
                        {
                            Console.WriteLine("        {0}", member);
                        }
                    }

                    Console.WriteLine();
                }
            }
        }

        private static void CollectReferences(string source, XmlDocument references)
        {
            foreach (XmlElement reference in references.SelectNodes("/Assembly/Reference"))
            {
                try
                {
                    string referenceName = reference.GetAttribute("Name");
                    if (referenceName == null)
                    {
                        continue;
                    }

                    AssemblyName referenceAssemblyName = new AssemblyName(referenceName);

                    List<TypeReference> recordedTypeReferences;
                    if (!AllReferences.TryGetValue(referenceAssemblyName.Name, out recordedTypeReferences))
                    {
                        continue;
                    }

                    foreach (XmlElement typeReference in reference.SelectNodes("./Type"))
                    {
                        string typeName = typeReference.GetAttribute("Name");
                        if (typeName == null)
                        {
                            continue;
                        }

                        TypeReference recordedTypeReference = recordedTypeReferences.Where(x => x.Name == typeName).SingleOrDefault();
                        if (recordedTypeReference == null)
                        {
                            recordedTypeReference = new TypeReference(typeName);
                            recordedTypeReferences.Add(recordedTypeReference);
                        }

                        recordedTypeReference.Sources.Add(source);

                        foreach (XmlElement memberReference in typeReference.SelectNodes("./Member"))
                        {
                            string memberFullName = memberReference.GetAttribute("FullName");
                            memberFullName = memberFullName.Replace(typeName + "::", String.Empty);

                            recordedTypeReference.Members.Add(memberFullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: {1}", ex.GetType().Name, ex.Message);
                }
            }
        }

        private static XmlDocument GetReferencesFrom(string line)
        {
            string tempFile = Path.GetTempFileName();
            File.Delete(tempFile);
            tempFile += ".xml";

            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        Arguments = $"\"{line}\" -t -m \"{tempFile}\"",
                        CreateNoWindow = true,
                        FileName = @"C:\MetadataTools\src\RefDump\bin\Debug\net461\RefDump.exe",
                        UseShellExecute = false,
                    },
                };

                process.Start();
                process.WaitForExit();

                XmlDocument document = new XmlDocument();
                if (File.Exists(tempFile))
                {
                    document.Load(tempFile);
                }
                return document;
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
