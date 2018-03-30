# OpenCoverToCoberturaConverter

Converts [OpenCover](https://github.com/sawilde/OpenCover) reports to [Cobertura](http://cobertura.sourceforge.net) reports.

Also available as **NuGet** package: http://www.nuget.org/packages/OpenCoverToCoberturaConverter

Author: Daniel Palme  
Blog: [www.palmmedia.de](http://www.palmmedia.de)  
Twitter: [@danielpalme](http://twitter.com/danielpalme)


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