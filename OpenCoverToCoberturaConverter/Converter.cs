﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Palmmedia.OpenCoverToCoberturaConverter
{
  public static class Converter
  {
    public static XDocument ConvertToCobertura(XDocument openCoverReport, string sourcesDirectory)
    {
      if (openCoverReport == null)
      {
        throw new ArgumentNullException("openCoverReport");
      }

      XDocument result = new XDocument(new XDeclaration("1.0", null, null), CreateRootElement(openCoverReport, sourcesDirectory));
      result.AddFirst(new XDocumentType("coverage", null, "http://cobertura.sourceforge.net/xml/coverage-04.dtd", null));

      return result;
    }

    private static XElement CreateRootElement(XDocument openCoverReport, string sourcesDirectory)
    {
      long coveredLines = 0;
      long totalLines = 0;

      long coveredBranches = 0;
      long totalBranches = 0;

      string commonPrefix;

      var rootElement = new XElement("coverage");

      rootElement.Add(CreateSourcesElement(openCoverReport, sourcesDirectory, out commonPrefix));
      var rootPrefixRegex = new Regex("^" + Regex.Escape(commonPrefix), RegexOptions.IgnoreCase);
      rootElement.Add(CreatePackagesElement(openCoverReport, ref coveredLines, ref totalLines, ref coveredBranches, ref totalBranches, rootPrefixRegex));

      double lineRate = totalLines == 0 ? 1 : coveredLines/(double) totalLines;
      double branchRate = totalBranches == 0 ? 1 : coveredBranches/(double) totalBranches;

      rootElement.Add(new XAttribute("line-rate", lineRate.ToString(CultureInfo.InvariantCulture)));
      rootElement.Add(new XAttribute("branch-rate", branchRate.ToString(CultureInfo.InvariantCulture)));
      rootElement.Add(new XAttribute("lines-covered", coveredLines.ToString(CultureInfo.InvariantCulture)));
      rootElement.Add(new XAttribute("lines-valid", totalLines.ToString(CultureInfo.InvariantCulture)));
      rootElement.Add(new XAttribute("branches-covered", coveredBranches.ToString(CultureInfo.InvariantCulture)));
      rootElement.Add(new XAttribute("branches-valid", totalBranches.ToString(CultureInfo.InvariantCulture)));
      rootElement.Add(new XAttribute("complexity", 0)); // TODO
      rootElement.Add(new XAttribute("version", 0)); // TODO
      rootElement.Add(new XAttribute("timestamp", ((long) (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds).ToString(CultureInfo.InvariantCulture)));

      return rootElement;
    }

    private static XElement CreateSourcesElement(XDocument openCoverReport, string sourcesDirectory, out string commonPrefix)
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

      commonPrefix = sourcesDirectory ?? sourceDirectories.FirstOrDefault();

      if (commonPrefix != null)
      {
        if (sourcesDirectory == null)
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
        }

        sources.Add(new XElement("source", commonPrefix));
        commonPrefix += Path.DirectorySeparatorChar;
      }

      return sources;
    }

    private static XElement CreatePackagesElement(XDocument openCoverReport, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches, Regex commonPrefix)
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

    private static XElement CreatePackageElement(XElement module, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches, Regex commonPrefix)
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
        .Where(c => !c.Element("FullName").Value.Contains("__")
                    && !c.Element("FullName").Value.Contains("<")
                    && !c.Element("FullName").Value.Contains("/")
                    && c.Attribute("skippedDueTo") == null)
        .ToArray();

      foreach (var clazz in classes)
      {
        classesElement.Add(CreateClassElement(clazz, filesById, ref packageCoveredLines, ref packageTotalLines, ref packageCoveredBranches, ref packageTotalBranches, commonPrefix));
      }

      double lineRate = packageTotalLines == 0 ? 1 : packageCoveredLines/(double) packageTotalLines;
      double branchRate = packageTotalBranches == 0 ? 1 : packageCoveredBranches/(double) packageTotalBranches;

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

    private static XElement CreateClassElement(XElement clazz, Dictionary<string, string> filesById, ref long coveredLines, ref long totalLines, ref long coveredBranches, ref long totalBranches, Regex commonPrefix)
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
      fileName = commonPrefix.Replace(fileName, string.Empty);

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
        .Where(m => m.Attribute("skippedDueTo") == null
                    && !m.HasAttributeWithValue("isGetter", "true")
                    && !m.HasAttributeWithValue("isSetter", "true")
                    && !Regex.IsMatch(m.Element("Name").Value, "::<.+>.+__"))
        .ToArray();

      foreach (var method in methods)
      {
        methodsElement.Add(CreateMethodElement(method, linesElement, ref classCoveredLines, ref classTotalLines, ref classCoveredBranches, ref classTotalBranches));
      }

      double lineRate = classTotalLines == 0 ? 1 : classCoveredLines/(double) classTotalLines;
      double branchRate = classTotalBranches == 0 ? 1 : classCoveredBranches/(double) classTotalBranches;

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
        .ToList();

      long methodCoveredLines = seqPoints.Count(s => s.Attribute("vc").Value != "0");
      long methodTotalLines = seqPoints.LongLength;

      long methodCoveredBranches = branchPoints.Count(s => s.Attribute("vc").Value != "0");
      long methodTotalBranches = branchPoints.Count;

      double lineRate = methodTotalLines == 0 ? 1 : methodCoveredLines/(double) methodTotalLines;
      double branchRate = methodTotalBranches == 0 ? 1 : methodCoveredBranches/(double) methodTotalBranches;

      var methodElement = new XElement(
        "method",
        new XAttribute("name", methodName),
        new XAttribute("signature", signature),
        new XAttribute("line-rate", lineRate.ToString(CultureInfo.InvariantCulture)),
        new XAttribute("branch-rate", branchRate.ToString(CultureInfo.InvariantCulture)));

      XElement linesElement = new XElement("lines");
      methodElement.Add(linesElement);

      for (int i = 0; i < seqPoints.Count(); i++)
      {
        var seqPoint = seqPoints[i];
        var methodLineElement = CreateLineElement(seqPoint);
        linesElement.Add(methodLineElement);

        var classLineElement = CreateLineElement(seqPoint);
        classLinesElement.Add(classLineElement);
        
        var matchingBranchPoints = branchPoints.FindAll(bp => bp.Attribute("sl") != null && bp.Attribute("sl").Value == seqPoint.Attribute("sl").Value);

        if (matchingBranchPoints.Any())
        {
          var lineCoveredBranches = matchingBranchPoints.Count(s => s.Attribute("vc").Value != "0");
          var totalMatchingBranchPoints = matchingBranchPoints.Count;

          double matchBranchRate = totalMatchingBranchPoints == 0 ? 1 : lineCoveredBranches / totalMatchingBranchPoints;

          AddBranchCoverageToLineElement(methodLineElement, matchBranchRate, lineCoveredBranches, totalMatchingBranchPoints);
          AddBranchCoverageToLineElement(classLineElement, matchBranchRate, lineCoveredBranches, totalMatchingBranchPoints);
        }
      }

      var linesElementList = linesElement.Elements("line").ToList();

      var orphanedBranches = branchPoints.Where(bp => !linesElementList.Any(le => le.HasAttributeWithValue("number", bp.Attribute("sl").Value)));



      //foreach (var orphanedBranch in branchPoints)
      //{
      //  if (orphanedBranch != null)
      //  {
      //    var lineElement = new XElement(
      //      "line",
      //      new XAttribute("number", orphanedBranch.Attribute("sl") != null ? orphanedBranch.Attribute("sl").Value : string.Empty),
      //      new XAttribute("hits", orphanedBranch.Attribute("vc").Value),
      //      new XAttribute("branch", orphanedBranch.Attribute("vc").Value != "0"));

      //    linesElement.Add(lineElement);

      //    AddBranchCoverageToLineElement(lineElement, matchBranchRate, lineCoveredBranches, totalMatchingBranchPoints);
      //  }
      //}



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

    private static void AddBranchCoverageToLineElement(XElement firstMethodLineElement, double coverage, double visitedBranches, double totalBranches)
    {
      if (firstMethodLineElement != null)
      {
        firstMethodLineElement.SetAttributeValue("branch", "true");
        firstMethodLineElement.Add(new XAttribute("condition-coverage", string.Format("{0}% ({1}/{2})", Math.Round(coverage*100), visitedBranches, totalBranches)));
      }
    }

    private static void AddOrphanedBranchCoverageToLineElements(XElement linesElement, XElement[] branchPoints)
    {
      if (linesElement == null || branchPoints == null)
      {
        return;
      }

      var copyOfBranches = branchPoints.ToList(); // need to be able to remove the orphaned branches without affecting the reference object.

      var lines = linesElement.Elements("line").ToArray();

      foreach (var line in lines)
      {
        // get the matching branchpoints
        var matchingBranches = Array.FindAll(branchPoints, bp => bp.HasAttributeWithValue("sl", line.Attribute("number").Value));

        if (matchingBranches.Any())
        {
          var lineCoveredBranches = matchingBranches.Count(s => s.Attribute("vc").Value != "0");
          var totalMatchingBranchPoints = matchingBranches.Length;

          double branchRate = totalMatchingBranchPoints == 0 ? 1 : lineCoveredBranches / totalMatchingBranchPoints;

          line.SetAttributeValue("branch", lineCoveredBranches != 0);
          line.Add(new XAttribute("condition-coverage", string.Format("{0}% ({1}/{2})", Math.Round(branchRate * 100), lineCoveredBranches, totalMatchingBranchPoints)));
        }
      }

      foreach (var branchPoint in copyOfBranches)
      {
        if (branchPoint != null)
        {
          var matchingLine = Array.Find(lines, le => branchPoint.HasAttributeWithValue("sl", le.Attribute("number").Value));

          if (matchingLine != null)
          {
            var matchingBranchPoints = Array.FindAll(branchPoints, bp => bp.Attribute("sl") != null && bp.Attribute("sl").Value == matchingLine.Attribute("number").Value);

            var lineCoveredBranches = matchingBranchPoints.Count(s => s.Attribute("vc").Value != "0");
            var totalMatchingBranchPoints = matchingBranchPoints.LongLength;

            var branchRate = totalMatchingBranchPoints == 0 ? 1 : lineCoveredBranches / (double)totalMatchingBranchPoints;

            matchingLine.SetAttributeValue("branch", lineCoveredBranches != 0);
            matchingLine.Add(new XAttribute("condition-coverage", string.Format("{0}% ({1}/{2})", Math.Round(branchRate * 100), lineCoveredBranches, totalMatchingBranchPoints)));

            branchPoint.Remove();
          }
        }
      }

      foreach (var orphanedBranch in copyOfBranches)
      {
        if (orphanedBranch != null)
        {
          var lineElement = new XElement(
            "line",
            new XAttribute("number", orphanedBranch.Attribute("sl") != null ? orphanedBranch.Attribute("sl").Value : string.Empty ),
            new XAttribute("hits", orphanedBranch.Attribute("vc").Value),
            new XAttribute("branch", orphanedBranch.Attribute("vc").Value != "0"));

          linesElement.Add(lineElement);
        }
      }
    }
  }
}
