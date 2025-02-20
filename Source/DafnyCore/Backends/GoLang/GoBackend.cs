using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Dafny.Compilers;

public class GoBackend : ExecutableBackend {

  public override IReadOnlySet<string> SupportedExtensions => new HashSet<string> { ".go" };

  public override string TargetName => "Go";
  public override bool IsStable => true;
  public override string TargetExtension => "go";
  public override string TargetBaseDir(string dafnyProgramName) =>
    $"{Path.GetFileNameWithoutExtension(dafnyProgramName)}-go/src";

  public override bool SupportsInMemoryCompilation => false;
  public override bool TextualTargetIsExecutable => false;
  protected override SinglePassCodeGenerator CreateCodeGenerator() {
    return new GoCodeGenerator(Options, Reporter);
  }

  public override async Task<bool> OnPostGenerate(string dafnyProgramName, string targetDirectory, TextWriter outputWriter) {
    return await base.OnPostGenerate(dafnyProgramName, targetDirectory, outputWriter) && await OptimizeImports(targetDirectory, outputWriter);
  }

  private async Task<bool> OptimizeImports(string targetFilename, TextWriter outputWriter) {
    var goArgs = new List<string> {
      "-w",
      targetFilename
    };

    var writer = new StringWriter();

    var psi = PrepareProcessStartInfo("goimports", goArgs);

    var result = await RunProcess(psi, writer, writer);
    if (result != 0) {
      await outputWriter.WriteLineAsync("Error occurred while invoking goimports:");
      await outputWriter.WriteAsync(writer.ToString());
    }
    return 0 == result;
  }

  public override async Task<(bool Success, object CompilationResult)> CompileTargetProgram(string dafnyProgramName,
    string targetProgramText,
    string callToMain, /*?*/
    string targetFilename /*?*/, ReadOnlyCollection<string> otherFileNames,
    bool runAfterCompile, TextWriter outputWriter) {
    if (runAfterCompile) {
      Contract.Assert(callToMain != null);  // this is part of the contract of CompileTargetProgram
      // Since the program is to be run soon, nothing further is done here. Any compilation errors (that is, any errors
      // in the emitted program--this should never happen if the compiler itself is correct) will be reported as 'go run'
      // will run the program.
      return (true, null);
    } else {
      // compile now
      return (await SendToNewGoProcess(dafnyProgramName, targetFilename, otherFileNames,
        outputWriter, outputWriter, callToMain != null, run: false), null);
    }
  }

  public override Task<bool> RunTargetProgram(string dafnyProgramName, string targetProgramText,
    string callToMain, /*?*/
    string targetFilename, ReadOnlyCollection<string> otherFileNames,
    object compilationResult, TextWriter outputWriter, TextWriter errorWriter) {

    return SendToNewGoProcess(dafnyProgramName, targetFilename, otherFileNames, outputWriter, errorWriter, hasMain: true, run: true);
  }

  private async Task<bool> SendToNewGoProcess(string dafnyProgramName, string targetFilename,
    ReadOnlyCollection<string> otherFileNames,
    TextWriter outputWriter, TextWriter errorWriter, bool hasMain, bool run) {
    Contract.Requires(targetFilename != null);

    foreach (var otherFileName in otherFileNames) {
      if (Path.GetExtension(otherFileName) != ".go") {
        outputWriter.WriteLine("Unrecognized file as extra input for Go compilation: {0}", otherFileName);
        return false;
      }

      if (!CopyExternLibraryIntoPlace(mainProgram: targetFilename, externFilename: otherFileName, outputWriter: outputWriter)) {
        return false;
      }
    }

    List<string> goArgs = new();
    if (run) {
      goArgs.Add("run");
    } else {
      string output;
      var outputToFile = !Options.RunAfterCompile;

      if (outputToFile) {
        string extension;
        if (hasMain) {
          switch (Environment.OSVersion.Platform) {
            case PlatformID.Unix:
            case PlatformID.MacOSX:
            case (PlatformID)128: // early Mono
              extension = null;
              break;
            default:
              extension = "exe";
              break;
          }
        } else {
          extension = "a";
        }
        output = Path.ChangeExtension(dafnyProgramName, extension);
      } else {
        switch (Environment.OSVersion.Platform) {
          case PlatformID.Unix:
          case PlatformID.MacOSX:
          case (PlatformID)128: // early Mono
            output = "/dev/null";
            break;
          default:
            output = "NUL";
            break;
        }
      }

      goArgs.Add("build");
      goArgs.Add("-o");
      goArgs.Add(output);
    }

    goArgs.Add(targetFilename);
    goArgs.AddRange(Options.MainArgs);

    var psi = PrepareProcessStartInfo("go", goArgs);

    psi.EnvironmentVariables["GOPATH"] = GoPath(targetFilename);
    // Dafny compiles to the old Go package system, whereas Go has moved on to a module
    // system. Until Dafny's Go compiler catches up, the GO111MODULE variable has to be set.
    psi.EnvironmentVariables["GO111MODULE"] = "auto";

    return 0 == await RunProcess(psi, outputWriter, errorWriter);
  }

  static string GoPath(string filename) {
    var dirName = Path.GetDirectoryName(Path.GetDirectoryName(filename));

    Contract.Assert(dirName != null);

    // Filename is Foo-go/src/Foo.go, so go two directories up
    return Path.GetFullPath(dirName);
  }

  bool CopyExternLibraryIntoPlace(string externFilename, string mainProgram, TextWriter outputWriter) {
    // Grossly, we need to look in the file to figure out where to put it
    var pkgName = FindPackageName(externFilename);
    if (pkgName == null) {
      outputWriter.WriteLine("Unable to determine package name: {0}", externFilename);
      return false;
    }
    if (pkgName.StartsWith("_")) {
      // Check this here because otherwise Go acts like the package simply doesn't exist, which is confusing
      outputWriter.WriteLine("Go packages can't start with underscores: {0}", pkgName);
      return false;
    }

    var mainDir = Path.GetDirectoryName(mainProgram);

    Contract.Assert(mainDir != null);

    var tgtDir = Path.Combine(mainDir, pkgName);
    var tgtFilename = Path.Combine(tgtDir, pkgName + ".go");

    Directory.CreateDirectory(tgtDir);
    File.Copy(externFilename, tgtFilename, overwrite: true);

    string relTgtFilename;
    var cwd = Directory.GetCurrentDirectory();
    if (tgtFilename.StartsWith(cwd)) {
      relTgtFilename = tgtFilename.Substring(cwd.Length + 1); // chop off relative path and '/'
    } else {
      relTgtFilename = tgtFilename;
    }
    if (Options.Verbose) {
      outputWriter.WriteLine("Additional input {0} copied to {1}", externFilename, relTgtFilename);
    }
    return true;
  }

  private static string FindPackageName(string externFilename) {
    using var rd = new StreamReader(new FileStream(externFilename, FileMode.Open, FileAccess.Read));
    while (rd.ReadLine() is string line) {
      var match = PackageLine.Match(line);
      if (match.Success) {
        return match.Groups[1].Value;
      }
    }
    return null;
  }

  private static readonly Regex PackageLine = new Regex(@"^\s*package\s+([a-zA-Z0-9_]+)\s*$");

  public GoBackend(DafnyOptions options) : base(options) {
  }
}