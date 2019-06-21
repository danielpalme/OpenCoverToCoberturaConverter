# OpenCoverToCoberturaConverter

Converts [OpenCover](https://github.com/sawilde/OpenCover) reports to [Cobertura](https://cobertura.github.io/cobertura) reports.

Also available as **NuGet** package: https://www.nuget.org/packages/OpenCoverToCoberturaConverter

Author: Daniel Palme  
Blog: [www.palmmedia.de](https://www.palmmedia.de)  
Twitter: [@danielpalme](https://twitter.com/danielpalme)


## Usage
*OpenCoverToCoberturaConverter* is a commandline tool which works with full .NET Framework and .NET Core.  
It requires the following parameters:

```
Parameters:
    ["]-input:<OpenCover Report>["]
    ["]-output:<Cobertura Report>["]
    ["]-sources:<Solution Base Directory>["]
    ["]-includeGettersSetters:<true|false>["]

Default values:
    -includeGettersSetters:false

Example:
  "-input:OpenCover.xml" "-output:Cobertura.xml"
```

**.NET Framework**
```
 OpenCoverToCoberturaConverter.exe "-input:OpenCover.xml" "-output:Cobertura.xml"
```

**.NET Core**
```
 dotnet OpenCoverToCoberturaConverter.dll "-input:OpenCover.xml" "-output:Cobertura.xml"
```

## Alternative
An alternative to *OpenCoverToCoberturaConverter* is [ReportGenerator](https://github.com/danielpalme/ReportGenerator). 
*ReportGenerator* has the following advantages:
* Merging of several coverage files
* Supports several input and output formats

![Comparision](https://raw.githubusercontent.com/danielpalme/OpenCoverToCoberturaConverter/master/docs/resources/comparision.png)