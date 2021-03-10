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
            { "Microsoft.WebTools.Languages.Css", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Css.Editor", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Extensions", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Html", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Html.Editor", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Html.VS", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Json.Arm", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Json", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Json.Editor", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Json.VS", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.LanguageServer.Delegation", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.LanguageServer.Server", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Razor.2", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Razor.3", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Razor.Core", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Shared", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Shared.Editor", new List<TypeReference>()},
            { "Microsoft.WebTools.Languages.Shared.VS", new List<TypeReference>()},
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

            var collateTypeUsageByExtension = new Dictionary<string, List<string>>();
            var collateMessageUsageByExtension = new Dictionary<string, List<string>>();

            using (FileStream typeUsageStream = new FileStream("ExtensionUsageByType.txt", FileMode.Create))
            using (StreamWriter typeUsageWriter = new StreamWriter(typeUsageStream))
            {
                foreach (KeyValuePair<string, List<TypeReference>> assembly in AllReferences.OrderBy(x => x.Key))
                {
                    if (assembly.Value.Count > 0)
                    {
                        typeUsageWriter.WriteLine("Types from {0}", assembly.Key);

                        foreach (TypeReference reference in assembly.Value.OrderBy(x => x.Name))
                        {
                            typeUsageWriter.Write("    {0} (", reference.Name);
                            bool comma = false;
                            foreach (string source in reference.Sources.OrderBy(x => x))
                            {
                                if (comma)
                                {
                                    typeUsageWriter.Write(", ");
                                }
                                else
                                {
                                    comma = true;
                                }

                                typeUsageWriter.Write(source);

                                if (!collateTypeUsageByExtension.ContainsKey(source))
                                {
                                    collateTypeUsageByExtension[source] = new List<string>();
                                }
                                collateTypeUsageByExtension[source].Add(reference.Name);
                            }
                            typeUsageWriter.Write(")");
                            typeUsageWriter.WriteLine();

                            foreach (string member in reference.Members.OrderBy(x => x))
                            {
                                typeUsageWriter.WriteLine("        {0}", member);
                            }
                        }

                        typeUsageWriter.WriteLine();
                    }
                }
            }

            using (FileStream typeUsageStream = new FileStream("TypeUsageByExtension.txt", FileMode.Create))
            using (StreamWriter typeUsageWriter = new StreamWriter(typeUsageStream))
            {

                foreach (KeyValuePair<string, List<string>> extensionUsage in collateTypeUsageByExtension)
                {
                    typeUsageWriter.WriteLine("Types used by: " + extensionUsage.Key);
                    foreach (var type in extensionUsage.Value)
                    {
                        typeUsageWriter.WriteLine("    {0}", type);
                    }
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
                string thisExeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        Arguments = $"\"{line}\" -t -m \"{tempFile}\"",
                        CreateNoWindow = true,
                        FileName = Path.Combine(thisExeDirectory, @"RefDump.exe"),
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
