# Fingerprint.NET

[![CI](https://github.com/AmSmart/Fingerprint.NET/actions/workflows/dotnet.yml/badge.svg)](https://github.com/AmSmart/Fingerprint.NET/actions?query=workflow%3ACI)
[![NuGet](https://img.shields.io/nuget/v/SmartLabs.Fingerprint.NET)](https://www.nuget.org/packages/SmartLabs.Fingerprint.NET/)

Fingerprint.NET is a library that helps you interface ZhianTec fingerprint sensors via .NET on Windows and Linux computers and micro-computers like the Raspberry Pi. This library is a .NET port and extension of the [PyFingerprint Library](https://github.com/bastianraschke/pyfingerprint).

## Supported Sensors
This library supports all libraries supported by the PyFingerprint library mentioned earlier i.e. the ZhianTec family of sensors (ZFM-20, ZFM-60, ZFM-70 and ZFM-100) and some other models like R302, R303, R305, R306, R307, R551 and FPM10A.

## How to install

### Manual Installation
The project is fairly trivial to build hence you can simply download the source code and consume in your project 

### Nuget
You can download the [Nuget Package](https://www.nuget.org/packages/SmartLabs.Fingerprint.NET/) from here or install via the Nuget CLI with the command below:  
```
PM> Install-Package SmartLabs.Fingerprint.NET
```
If you're using Visual Studio, you can also search and install the package **SmartLabs.Fingerprint.NET** via the Nuget Package Manager.  

### .NET CLI
You can install with the .NET CLI with the command below
```
dotnet add package SmartLabs.Fingerprint.NET
```

## Documentation
While I work on setting up a proper documentation page, I'd like to note that for most basic Fingerprint tasks, the ``FingerprintController`` would suffice. It has a minimal API surface and is easy to use. Check out the sample Console project to see it in action.
For some more advanced functionality, you can take a look at the ``Fingerprint`` class
