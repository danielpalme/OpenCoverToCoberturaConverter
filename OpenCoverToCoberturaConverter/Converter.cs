using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Palmmedia.OpenCoverToCoberturaConverter
{
    public static class Converter
    {
        public static XDocument ConvertToCobertura(XDocument openCoverReport)
        {
            if (openCoverReport == null)
            {
                throw new ArgumentNullException("openCoverReport");
            }

            XDocument result = new XDocument(new XDeclaration("1.0", null, null), CreateRootElement(openCoverReport));
            result.AddFirst(new XDocumentType("coverage", null, "http://cobertura.sourceforge.net/xml/coverage-04.dtd", null));

            return result;
        }

        private static XElement CreateRootElement(XDocument openCoverReport)
        {
            long coveredLines = 0;
            long totalLines = 0;

            long coveredBranches = 0;
            long totalBranches = 0;

            string commonPrefix;

            var rootElement = new XElement("coverage");

            rootElement.Add(CreateSourcesElement(openCoverReport, out commonPrefix));
            rootElement.Add(CreatePackagesElement(openCoverReport, ref coveredLines, ref totalLines, ref coveredBranches, ref totalBranches, commonPrefix));

            double lineRate = totalLines == 0 ? 1 : coveredLines / (double)totalLines;
            double branchRate = totalBranches == 0 ? 1 : coveredBranches / (double)totalBranches;

            rootElement.Add(new XAttribute("line-rate", lineRate.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("branch-rate", branchRate.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("lines-covered", coveredLines.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("lines-valid", totalLines.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("branches-covered", coveredBranches.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("branches-valid", totalBranches.ToString(CultureInfo.InvariantCulture)));
            rootElement.Add(new XAttribute("complexity", 0)); // TODO
            rootElement.Add(new XAttribute("version", 0)); // TODO
            rootElement.Add(new XAttribute("timestamp", ((long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture)));

            return rootElement;
        }

        private static XElement CreateSourcesElement(XDocument openCoverReport, out string commonPrefix)
        {
            var sources = new XElement("sources");
            var sourceDirectories = openCoverReport.Root
                .Element("Modules")
                .Elements("Module")
                .Where(m => m.Attribute("skippedDueTo") == null)
                .Elements("Files")
                .Elements("File")
                .Attributes("fullPath")
                .Select(a => Path.GetDirectoryName(a.Value))
                .Distinct();

            commonPrefix = sourceDirectories.FirstOrDefault();

            if (commonPrefix != null)
            {
                foreach (var sourceDirectory in sourceDirectories)
                {
                    for (int i = 0; i < Math.Min(commonPrefix.Length, sourceDirectory.Length); i++)
                    {
                        if (commonPrefix[i] == sourceDirectory[i])
                            continue;

                        int lastMatch = commonPrefix.LastIndexOf('\\', i - 1);
                        commonPrefix = commonPrefix.Substring(0, lastMatch);
                        break;
                    }
                }

                sources.Add(new XElement("source", commonPrefix));
            }

            return sources;
        }

        private static XElement CreatePackagesElement(XDocument openCoverReport, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches, string commonPrefix)
        {
            var packagesElement = new XElement("packages");

            var modules = openCoverReport.Root
                .Element("Modules")
                .Elements("Module")
                .Where(m => m.Attribute("skippedDueTo") == null)
                .ToArray();

            foreach (var module in modules)
            {
                packagesElement.Add(CreatePackageElement(module, ref coveredLines, ref totalLines, ref coveredBranches, ref totalBranches, commonPrefix));
            }

            return packagesElement;
        }

        private static XElement CreatePackageElement(XElement module, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches, string commonPrefix)
        {
            long packageCoveredLines = 0;
            long packageTotalLines = 0;

            long packageCoveredBranches = 0;
            long packageTotalBranches = 0;

            var packageElement = new XElement(
                "package",
                new XAttribute("name", module.Element("ModuleName").Value));

            var classesElement = new XElement("classes");
            packageElement.Add(classesElement);

            var filesById = module
                .Elements("Files")
                .Elements("File")
                .ToDictionary(f => f.Attribute("uid").Value, f => f.Attribute("fullPath").Value);

            var classes = module
                .Elements("Classes")
                .Elements("Class")
                .Where(m => m.Attribute("skippedDueTo") == null)
                /* .Where(c => !c.Element("FullName").Value.Contains("__")
                     && !c.Element("FullName").Value.Contains("<")
                     && !c.Element("FullName").Value.Contains("/")) */
                .ToArray();

            foreach (var clazz in classes)
            {
                classesElement.Add(CreateClassElement(clazz, filesById, ref packageCoveredLines, ref packageTotalLines, ref packageCoveredBranches, ref packageTotalBranches, commonPrefix));
            }

            double lineRate = packageTotalLines == 0 ? 1 : packageCoveredLines / (double)packageTotalLines;
            double branchRate = packageTotalBranches == 0 ? 1 : packageCoveredBranches / (double)packageTotalBranches;

            packageElement.Add(
                new XAttribute(
                    "line-rate",
                    lineRate.ToString(CultureInfo.InvariantCulture)));
            packageElement.Add(
                new XAttribute(
                    "branch-rate",
                    branchRate.ToString(CultureInfo.InvariantCulture)));
            packageElement.Add(new XAttribute("complexity", 0)); // TODO

            coveredLines += packageCoveredLines;
            totalLines += packageTotalLines;

            coveredBranches += packageCoveredBranches;
            totalBranches += packageTotalBranches;

            return packageElement;
        }

        private static XElement CreateClassElement(XElement clazz, Dictionary<string, string> filesById, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches, string commonPrefix)
        {
            long classCoveredLines = 0;
            long classTotalLines = 0;

            long classCoveredBranches = 0;
            long classTotalBranches = 0;

            var firstMethodWithFileRef = clazz
                .Elements("Methods")
                .Elements("Method")
                .FirstOrDefault(m => m.Elements("FileRef").Any());

            // First method is used to determine name of file (partial classes are not handled correctly)
            string fileName = firstMethodWithFileRef == null ? string.Empty : filesById[firstMethodWithFileRef.Element("FileRef").Attribute("uid").Value];
            fileName = fileName.Replace(commonPrefix + '\\', null);

            var classElement = new XElement(
                "class",
                new XAttribute("name", clazz.Element("FullName").Value),
                new XAttribute("filename", fileName)); // TOOO

            var methodsElement = new XElement("methods");
            classElement.Add(methodsElement);

            var linesElement = new XElement("lines");
            classElement.Add(linesElement);

            var methods = clazz
                .Elements("Methods")
                .Elements("Method")
                .Where(m => m.Attribute("skippedDueTo") == null)
                /* .Where(m => !m.HasAttributeWithValue("isGetter", "true")
                     && !m.HasAttributeWithValue("isSetter", "true")
                     && !Regex.IsMatch(m.Element("Name").Value, "::<.+>.+__")) */
                .ToArray();

            foreach (var method in methods)
            {
                methodsElement.Add(CreateMethodElement(method, linesElement, ref classCoveredLines, ref classTotalLines, ref classCoveredBranches, ref classTotalBranches));
            }

            double lineRate = classTotalLines == 0 ? 1 : classCoveredLines / (double)classTotalLines;
            double branchRate = classTotalBranches == 0 ? 1 : classCoveredBranches / (double)classTotalBranches;

            classElement.Add(
                new XAttribute(
                    "line-rate",
                    lineRate.ToString(CultureInfo.InvariantCulture)));
            classElement.Add(
                new XAttribute(
                    "branch-rate",
                    branchRate.ToString(CultureInfo.InvariantCulture)));
            classElement.Add(new XAttribute("complexity", 0)); // TODO

            coveredLines += classCoveredLines;
            totalLines += classTotalLines;

            coveredBranches += classCoveredBranches;
            totalBranches += classTotalBranches;

            return classElement;
        }

        private static XElement CreateMethodElement(XElement method, XElement classLinesElement, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches)
        {
            var match = Regex.Match(method.Element("Name").Value, @"^.*::(?<methodname>.*)(?<signature>\(.*\))$");

            string methodName = match.Success ? match.Groups["methodname"].Value : method.Element("Name").Value;
            string signature = match.Success ? match.Groups["signature"].Value : method.Element("Name").Value;

            var seqPoints = method
              .Elements("SequencePoints")
              .Elements("SequencePoint")
              .ToArray();

            var branchPoints = method
                .Elements("BranchPoints")
                .Elements("BranchPoint")
                .ToArray();

            long methodCoveredLines = seqPoints.Count(s => s.Attribute("vc").Value != "0");
            long methodTotalLines = seqPoints.LongLength;

            long methodCoveredBranches = branchPoints.Count(s => s.Attribute("vc").Value != "0");
            long methodTotalBranches = branchPoints.LongLength;

            double lineRate = methodTotalLines == 0 ? 1 : methodCoveredLines / (double)methodTotalLines;
            double branchRate = methodTotalBranches == 0 ? 1 : methodCoveredBranches / (double)methodTotalBranches;

            var methodElement = new XElement(
                "method",
                new XAttribute("name", methodName),
                new XAttribute("signature", signature),
                new XAttribute("line-rate", lineRate.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("branch-rate", branchRate.ToString(CultureInfo.InvariantCulture)));

            var linesElement = new XElement("lines");
            methodElement.Add(linesElement);

            foreach (var seqPoint in seqPoints)
            {
                linesElement.Add(CreateLineElement(seqPoint));
                classLinesElement.Add(CreateLineElement(seqPoint));
            }

            coveredLines += methodCoveredLines;
            totalLines += methodTotalLines;

            coveredBranches += methodCoveredBranches;
            totalBranches += methodTotalBranches;

            return methodElement;
        }

        private static XElement CreateLineElement(XElement seqPoint)
        {
            var lineElement = new XElement(
                "line",
                new XAttribute("number", seqPoint.Attribute("sl").Value),
                new XAttribute("hits", seqPoint.Attribute("vc").Value),
                new XAttribute("branch", "false"));

            return lineElement;
        }
    }
}