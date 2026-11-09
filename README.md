# UMK — Unity Modding Kit

<div align="center">

A CLI-only, shell-driven project configurator and task runner for Unity mod development.

[![NuGet Version](https://img.shields.io/nuget/v/KRWCLASSIC.UMK.svg)](https://www.nuget.org/packages/KRWCLASSIC.UMK)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

</div>

---

## Overview

UMK does not implement a custom build system, compiler, or mod templates. Instead, it acts as a lightweight task runner that executes raw system shell commands (`cmd` or `powershell`) defined by the user in `umk.json`.

It standardizes the repetitive Unity mod development loop (compile $\to$ copy DLL to `BepInEx\plugins` $\to$ kill process $\to$ launch game) into clean, composable scripts with recursive variable interpolation and fail-fast sequential execution.

---

## Installation

Install UMK globally as a .NET tool:

```cmd
dotnet tool install -g KRWCLASSIC.UMK
```

To update an existing installation:

```cmd
dotnet tool update -g KRWCLASSIC.UMK
```

---

## CLI Reference

```cmd
# Project initialization wizard (auto-detects Steam libraries, game paths, and outputs)
umk init

# Quick setup after git clone (reconfigures local game path & creates libs\ junctions)
umk setup

# Zero-copy game library linker (creates junctions in libs\ and references in .csproj)
umk linkgamelibs

# Run user-defined scripts from umk.json
umk build
umk run
umk <script>

# Preview resolved shell commands without executing them
umk run --dry-run
umk <script> --dry-run

# Inspect available scripts and resolved variables
umk list

# Help & documentation
umk help
umk help variables
umk help scripts
```

---

## Configuration: `umk.json`

The manifest defines project variables and executable scripts:

```json
{
  "umk": "0.1.0",
  "name": "MyMod",
  "shell": "cmd",
  "variables": {
    "configuration": "Release",
    "steamId": "123456",
    "game": "E:\\SteamLibrary\\steamapps\\common\\MyGame",
    "gameExe": "MyGame.exe",
    "plugins": "${game}\\BepInEx\\plugins",
    "output": "bin\\${configuration}\\netstandard2.1"
  },
  "scripts": {
    "kill": [
      "taskkill /F /IM \"${gameExe}\" >nul 2>&1 || ver >nul"
    ],
    "compile": [
      "dotnet build MyMod.csproj -c ${configuration}"
    ],
    "copy": [
      "copy /Y \"${output}\\MyMod.dll\" \"${plugins}\""
    ],
    "build": [
      "compile",
      "copy"
    ],
    "run": [
      "kill",
      "build",
      "start \"\" \"steam://rungameid/${steamId}\""
    ],
    "clean": [
      "dotnet clean"
    ]
  }
}
```

### Variables
- Syntax: `${variableName}`
- Variables can reference other variables (e.g. `${game}\BepInEx\plugins`).
- On Windows, forward slashes (`/`) in path variables are automatically normalized to backslashes (`\`) for shell compatibility.
- Circular variable references are detected and rejected before execution.

### Scripts
- Sequential arrays of ordinary shell commands or references to other script keys.
- Step references (e.g. `"build"` calling `"compile"` then `"copy"`) expand inline with cycle detection.
- Execution stops immediately on the first non-zero exit code (fail-fast).

---

## Zero-Copy Game Library Linking (`umk linkgamelibs`)

Instead of copying 100+ MB of game DLLs into every mod project folder:

1. `umk linkgamelibs` reads the `game` path from `umk.json`.
2. Creates two Windows directory junctions (`mklink /J`) inside `./libs/`:
   - `libs\bepinex` $\to$ `<game>\BepInEx\core`
   - `libs\managed` $\to$ `<game>\<Game>_Data\Managed`
3. Injects a compact wildcard block into your `.csproj`:

```xml
<ItemGroup Label="UMKGameLibs">
  <Reference Include="libs\bepinex\0Harmony.dll" Private="False" />
  <Reference Include="libs\bepinex\BepInEx*.dll" Private="False" />
  <Reference Include="libs\managed\*.dll" Private="False" />
  <Reference Remove="libs\managed\mscorlib.dll" />
  <Reference Remove="libs\managed\netstandard.dll" />
  <Reference Remove="libs\managed\WindowsBase.dll" />
  <Reference Remove="libs\managed\Accessibility.dll" />
  <Reference Remove="libs\managed\nunit.framework.dll" />
  <Reference Remove="libs\managed\System*.dll" />
  <Reference Remove="libs\managed\Mono*.dll" />
  <Reference Remove="libs\managed\Microsoft*.dll" />
  <Reference Remove="libs\managed\I18N*.dll" />
</ItemGroup>
```

- **0 MB disk duplicate:** Junctions point directly to the game files.
- **No local paths in `.csproj`:** References are purely relative (`libs\...`), safe for git commits and sharing.
- **No BCL conflicts:** Automatically excludes base class library assemblies (`mscorlib`, `netstandard`, `System.*`, `Mono.*`) while linking all game modules, `UnityEngine*.dll`, and `Unity.*.dll`.

---

## License

MIT
