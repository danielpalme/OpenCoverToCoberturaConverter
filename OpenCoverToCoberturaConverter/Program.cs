using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Palmmedia.OpenCoverToCoberturaConverter
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            var namedArguments = new Dictionary<string, string>();

            foreach (var arg in args)
            {
                var match = Regex.Match(arg, "-(?<key>\\w{2,}):(?<value>.+)");

                if (match.Success)
                {
                    namedArguments[match.Groups["key"].Value.ToUpperInvariant()] = match.Groups["value"].Value;
                }
            }

            string inputFile = null;

            if (!namedArguments.TryGetValue("INPUT", out inputFile))
            {
                ShowHelp();
                return 1;
            }

            string targetFile = null;

            if (!namedArguments.TryGetValue("OUTPUT", out targetFile))
            {
                ShowHelp();
                return 1;
            }

            string sourcesDirectory = null;

            if (!namedArguments.TryGetValue("SOURCES", out sourcesDirectory))
            {
                Console.WriteLine("Sources directory not set, will try to guess. This might not work properly when merging results from multiple test assemblies.");
            }

            string includeGettersSettersString = null;
            bool includeGetSet = false;
            if (namedArguments.TryGetValue("INCLUDEGETTERSSETTERS", out includeGettersSettersString))
            {
                if (!bool.TryParse(includeGettersSettersString, out includeGetSet))
                {
                    Console.WriteLine("Unable to determine if getters/setters should be included or not. Ignoring for now.");
                }
            }

            if (!File.Exists(inputFile))
            {
                Console.WriteLine("Report does not exist: " + inputFile);
                return 1;
            }

            XDocument inputReport = null;

            try
            {
                inputReport = XDocument.Load(inputFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to parse report: " + inputFile + " (" + ex.Message + ")");
                return 1;
            }

            XDocument targetReport = Converter.ConvertToCobertura(inputReport, sourcesDirectory, includeGetSet);

            try
            {
                targetReport.Save(targetFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save report: " + targetFile + " (" + ex.Message + ")");
                return 1;
            }

            return 0;
        }

        private static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("OpenCoverToCoberturaConverter " + typeof(Program).Assembly.GetName().Version);

            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine("    [\"]-input:<OpenCover Report>[\"]");
            Console.WriteLine("    [\"]-output:<Cobertura Report>[\"]");
            Console.WriteLine("    [\"]-sources:<Solution Base Directory>[\"]");
            Console.WriteLine("    [\"]-includeGettersSetters:<true|false>[\"]");

            Console.WriteLine();
            Console.WriteLine("Default values::");
            Console.WriteLine("    -includeGettersSetters:false");

            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  \"-input:OpenCover.xml\" \"-output:Cobertura.xml\"");
        }
    }
}
