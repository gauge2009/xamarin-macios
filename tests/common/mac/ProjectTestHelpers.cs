using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using System.Reflection;

namespace Xamarin.MMP.Tests
{
	// Hide the hacks and provide a nice interface for writting tests that build / run XM projects
	static class TI 
	{
		public class UnifiedTestConfig
		{
			public string TmpDir { get; set; }

			// Not necessarly required
			public bool FSharp { get; set; }
			public bool XM45 { get; set; }
			public string ProjectName { get; set; }
			public string TestCode { get; set; }
			public string CSProjConfig { get; set; }
			public string References { get; set; }
			public string AssemblyName { get; set; }
			public string ItemGroup { get; set; }
			public string SystemMonoVersion { get; set; }

			// Binding project specific
			public string APIDefinitionConfig { get; set; }
			public string StructsAndEnumsConfig { get; set; }

			public UnifiedTestConfig (string tmpDir)
			{
				TmpDir = tmpDir;
				ProjectName = "";
				TestCode = "";
				CSProjConfig = "";
				References = "";
				AssemblyName = "";
				APIDefinitionConfig = "";
				StructsAndEnumsConfig = "";
				ItemGroup = "";
				SystemMonoVersion = "";
			}
		}

		public static string AssemblyDirectory
		{
			get
			{
				string codeBase = Assembly.GetExecutingAssembly ().CodeBase;
				UriBuilder uri = new UriBuilder (codeBase);
				string path = Uri.UnescapeDataString (uri.Path);
				return Path.GetDirectoryName (path);
			}
		}

		public static Version FindMonoVersion ()
		{
			string output = RunAndAssert ("/Library/Frameworks/Mono.framework/Commands/mono", new StringBuilder ("--version"), "FindMonoVersion");

			Regex versionRegex = new Regex("compiler version \\d.\\d.\\d", RegexOptions.IgnoreCase);
			return new Version (versionRegex.Match (output).Value.Split (' ')[2]);
		}

		public static string RunAndAssert (string exe, StringBuilder args, string stepName, bool shouldFail = false, Func<string> getAdditionalFailInfo = null)
		{
			StringBuilder output = new StringBuilder ();
			Environment.SetEnvironmentVariable ("MONO_PATH", null);
			int compileResult = Xamarin.Bundler.Driver.RunCommand (exe, args != null ? args.ToString() : string.Empty, MonoDevelopLike, output, suppressPrintOnErrors: shouldFail);
			Func<string> getInfo = () => getAdditionalFailInfo != null ? getAdditionalFailInfo() : "";
			if (!shouldFail)
				Assert.AreEqual (0, compileResult, stepName + " failed: '" + output + "' " + exe + " " + args + getInfo ());
			else
				Assert.AreNotEqual (0, compileResult, stepName + " did not fail as expected: '" + output + "' " + exe + " " + args + getInfo ());

			return output.ToString ();
		}

		public static string BuildProject (string csprojTarget, bool isUnified, bool shouldFail = false)
		{
			string rootDirectory = FindRootDirectory ();

			// These are required to have xbuild use are local build instead of system install
			Environment.SetEnvironmentVariable ("XBUILD_FRAMEWORK_FOLDERS_PATH", rootDirectory + "/Library/Frameworks/Mono.framework/External/xbuild-frameworks");
			Environment.SetEnvironmentVariable ("MSBuildExtensionsPath", rootDirectory + "/Library/Frameworks/Mono.framework/External/xbuild");
			Environment.SetEnvironmentVariable ("XAMMAC_FRAMEWORK_PATH", rootDirectory + "/Library/Frameworks/Xamarin.Mac.framework/Versions/Current");

			// This is to force build to use our mmp and not system mmp
			StringBuilder buildArgs = new StringBuilder ();
			if (isUnified) {
				buildArgs.Append (" /verbosity:normal ");
				buildArgs.Append (" /property:XamarinMacFrameworkRoot=" + rootDirectory + "/Library/Frameworks/Xamarin.Mac.framework/Versions/Current ");
			} else
				buildArgs.Append (" build ");

			buildArgs.Append (csprojTarget);


			if (isUnified)
				return RunAndAssert ("/Library/Frameworks/Mono.framework/Commands/xbuild", buildArgs, "Compile", shouldFail, () => File.ReadAllText (csprojTarget));
			else
				return RunAndAssert ("/Applications/Xamarin Studio.app/Contents/MacOS/mdtool", buildArgs, "Compile", shouldFail, () => File.ReadAllText (csprojTarget));
		}

		static string ProjectTextReplacement (UnifiedTestConfig config, string text)
		{
			return text.Replace ("%CODE%", config.CSProjConfig).Replace ("%REFERENCES%", config.References).Replace ("%NAME%", config.AssemblyName ?? Path.GetFileNameWithoutExtension (config.ProjectName)).Replace ("%ITEMGROUP%", config.ItemGroup);
		}

		static void RunEXEAndVerifyGUID (string tmpDir, Guid guid, string path)
		{
			// Assert that the program actually runs and returns our guid
			Assert.IsTrue (File.Exists (path), string.Format ("{0} did not generate an exe?", path));
			string output = RunAndAssert (path, null, "Run");
			Assert.IsTrue(File.Exists (Path.Combine (tmpDir, guid.ToString ())), "Generated program did not create expected guid file: " + output);
		}

		public static string GenerateEXEProject (UnifiedTestConfig config)
		{
			WriteMainFile (config.TestCode, true, config.FSharp, Path.Combine (config.TmpDir, config.FSharp ? "Main.fs" : "Main.cs"));

			string sourceDir = FindSourceDirectory ();
			File.Copy (Path.Combine (sourceDir, "Info-Unified.plist"), Path.Combine (config.TmpDir, "Info.plist"), true);

			return CopyFileWithSubstitutions (Path.Combine (sourceDir, config.ProjectName), Path.Combine (config.TmpDir, config.ProjectName), text =>
				{
					return ProjectTextReplacement (config, text);
				});
		}

		public static string GenerateBindingLibraryProject (UnifiedTestConfig config)
		{
			string sourceDir = FindSourceDirectory ();
			CopyFileWithSubstitutions (Path.Combine (sourceDir, "ApiDefinition.cs"), Path.Combine (config.TmpDir, "ApiDefinition.cs"), text => text.Replace ("%CODE%", config.APIDefinitionConfig));
			CopyFileWithSubstitutions (Path.Combine (sourceDir, "StructsAndEnums.cs"), Path.Combine (config.TmpDir, "StructsAndEnums.cs"), text => text.Replace ("%CODE%", config.StructsAndEnumsConfig));

			return CopyFileWithSubstitutions (Path.Combine (sourceDir, config.ProjectName), Path.Combine (config.TmpDir, config.ProjectName), text => {
					return ProjectTextReplacement (config, text);
				});
		}

		public static string GenerateUnifiedLibraryProject (UnifiedTestConfig config)
		{
			string sourceDir = FindSourceDirectory ();
			string sourceFileName = config.FSharp ? "Component1.fs" : "MyClass.cs";
			string projectSuffix = config.FSharp ? ".fsproj" : ".csproj";
			File.Copy (Path.Combine (sourceDir, sourceFileName), Path.Combine (config.TmpDir, sourceFileName), true);

			return CopyFileWithSubstitutions (Path.Combine (sourceDir, config.ProjectName + projectSuffix), Path.Combine (config.TmpDir, config.ProjectName + projectSuffix), text => {
					return ProjectTextReplacement (config, text);
				});
		}

		public static string BuildUnifiedExecutable (UnifiedTestConfig config, bool shouldFail = false)
		{
			string projectName;
			if (config.FSharp)
				projectName = config.XM45 ? "FSharpXM45Example" : "FSharpUnifiedExample";
			else
				projectName = config.XM45 ? "XM45Example" : "UnifiedExample";
			string projectExtension = config.FSharp ? ".fsproj" : ".csproj";

			config.ProjectName = projectName + projectExtension;
			string csprojTarget = GenerateEXEProject (config);

			return BuildProject (csprojTarget, isUnified: true, shouldFail: shouldFail);
		}

		public static string TestUnifiedExecutable (UnifiedTestConfig config, bool shouldFail = false)
		{
			string projectName;
			if (config.FSharp)
				projectName = config.XM45 ? "FSharpXM45Example" : "FSharpUnifiedExample";
			else
				projectName = config.XM45 ? "XM45Example" : "UnifiedExample";
			string projectExtension = config.FSharp ? ".fsproj" : ".csproj";

			Guid guid = Guid.NewGuid ();
			config.TestCode += GenerateOuputCommand (config.TmpDir, guid);
			config.ProjectName = projectName + projectExtension;
			string csprojTarget = GenerateEXEProject (config);

			string buildOutput = BuildProject (csprojTarget, isUnified : true, shouldFail : shouldFail);
			if (shouldFail)
				return buildOutput;

			string bundleName = config.AssemblyName != "" ? config.AssemblyName : projectName;
			string exePath = Path.Combine (config.TmpDir, "bin/Debug/" + bundleName + ".app/Contents/MacOS/" +  bundleName);
			RunEXEAndVerifyGUID (config.TmpDir, guid, exePath);
			return buildOutput;
		}

		public static string TestClassicExecutable (string tmpDir, string testCode = "", string csprojConfig = "", bool shouldFail = false)
		{
			Guid guid = Guid.NewGuid ();
			string csprojTarget = GenerateClassicEXEProject (tmpDir, "ClassicExample.csproj", testCode + GenerateOuputCommand (tmpDir,guid), csprojConfig, "");
			string buildOutput = BuildProject (csprojTarget, isUnified : false, shouldFail : shouldFail);
			if (shouldFail)
				return buildOutput;

			string exePath = Path.Combine (tmpDir, "bin/Debug/ClassicExample.app/Contents/MacOS/ClassicExample");
			RunEXEAndVerifyGUID (tmpDir, guid, exePath);
			return buildOutput;
		}

		public static string TestSystemMonoExecutable (UnifiedTestConfig config, bool shouldFail = false)
		{
			Guid guid = Guid.NewGuid ();
			var projectName = "SystemMonoExample";
			config.TestCode += GenerateOuputCommand (config.TmpDir, guid);
			config.ProjectName = $"{projectName}.csproj";
			string csprojTarget = GenerateSystemMonoEXEProject (config);

			string buildOutput = BuildProject (csprojTarget, isUnified : true, shouldFail : shouldFail);
			if (shouldFail)
				return buildOutput;

			string exePath = Path.Combine (config.TmpDir, "bin/Debug/" + projectName + ".app/Contents/MacOS/" + projectName);
			RunEXEAndVerifyGUID (config.TmpDir, guid, exePath);
			return buildOutput;
		}

		public static string GenerateClassicEXEProject (string tmpDir, string projectName, string testCode, string csprojConfig = "", string references = "", string assemblyName = null)
		{
			WriteMainFile (testCode, false, false, Path.Combine (tmpDir, "Main.cs"));

			string sourceDir = FindSourceDirectory ();
			File.Copy (Path.Combine (sourceDir, "Info-Classic.plist"), Path.Combine (tmpDir, "Info.plist"), true);

			return CopyFileWithSubstitutions (Path.Combine (sourceDir, projectName), Path.Combine (tmpDir, projectName), text =>
				{
					return text.Replace ("%CODE%", csprojConfig).Replace ("%REFERENCES%", references).Replace ("%NAME%", assemblyName ?? Path.GetFileNameWithoutExtension (projectName));
				});
		}

		static string GetTargetFrameworkValue (UnifiedTestConfig config)
		{
			string version = config.SystemMonoVersion == "" ? "4.5" : config.SystemMonoVersion;
			return string.Format ("<TargetFrameworkVersion>v{0}</TargetFrameworkVersion>", version);
		}

		public static string GenerateSystemMonoEXEProject (UnifiedTestConfig config)
		{
			WriteMainFile (config.TestCode, true, false, Path.Combine (config.TmpDir, "Main.cs"));

			string sourceDir = FindSourceDirectory ();
			File.Copy (Path.Combine (sourceDir, "Info-Unified.plist"), Path.Combine (config.TmpDir, "Info.plist"), true);

			return CopyFileWithSubstitutions (Path.Combine (sourceDir, config.ProjectName), Path.Combine (config.TmpDir, config.ProjectName), text =>
				{
					return ProjectTextReplacement (config, text.Replace ("%TARGETFRAMEWORKVERSION%", GetTargetFrameworkValue (config)));
				});
		}

		public static string TestDirectory => "../../../../../../../";

		public static string FindSourceDirectory ()
		{
			string codeBase = System.Reflection.Assembly.GetExecutingAssembly ().CodeBase;
			UriBuilder uri = new UriBuilder (codeBase);
			string path = Uri.UnescapeDataString (uri.Path);
			string assemblyDirectory = Path.GetDirectoryName (path);
			return Path.Combine(assemblyDirectory, TestDirectory + "common/mac");
		}

		static string CopyFileWithSubstitutions (string src, string target, Func<string, string > replacementAction)
		{
			string text = replacementAction (System.IO.File.ReadAllText (src));
			System.IO.File.WriteAllText (target, text);
			return target;
		}

		// Configuration.MonoDevelopLike is a Dictionary<string, string> but RunCommand wants string [], convert!
		static string [] MonoDevelopLike {
			get {
				List<string> keys = Xamarin.Tests.Configuration.MonoDevelopLike.Keys.ToList ();
				int numberOfKeys = keys.Count ();
				var retValue = new string [numberOfKeys  * 2 + 2];
				for (int i = 0 ; i < numberOfKeys ; i++) {
					retValue[i * 2] = keys[i];
					if (keys[i] == "PATH") // For some reason MonoDevelopLike is blapping PATH so we can't find xcrun
						retValue[1 + (i * 2)] = Xamarin.Tests.Configuration.MonoDevelopLike[keys[i]] + ":/usr/bin";
					else
						retValue[1 + (i * 2)] = Xamarin.Tests.Configuration.MonoDevelopLike[keys[i]];
				}
				retValue [numberOfKeys * 2] = "MD_APPLE_SDK_ROOT";
				retValue [numberOfKeys * 2 + 1] = System.Environment.GetEnvironmentVariable ("MD_APPLE_SDK_ROOT");
				return retValue;
			}
		}

		static void WriteMainFile (string content, bool isUnified, bool fsharp, string location)
		{
			const string FSharpMainTemplate = @"
namespace FSharpUnifiedExample
open System
open AppKit

module main =
    [<EntryPoint>]
    let main args =
        NSApplication.Init ()
        %CODE%
        0";

			const string MainTemplate = @"
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace TestCase
{
	class MainClass
	{
		static void Main (string[] args)
		{
			NSApplication.Init ();
			%CODE%
		}
	}
}";
			string currentTemplate = fsharp ? FSharpMainTemplate : MainTemplate;
			string testCase = currentTemplate.Replace("%CODE%", content);
			if (isUnified)
				testCase = testCase.Replace ("MonoMac.", string.Empty);
			using (StreamWriter s = new StreamWriter (location))
				s.Write(testCase);
		}

		public static string FindRootDirectory ()
		{
			var current = Environment.CurrentDirectory;
			while (!Directory.Exists (Path.Combine (current, "xamarin-macios", "_mac-build")) && current.Length > 1)
				current = Path.GetDirectoryName (current);
			if (current.Length <= 1)
				throw new DirectoryNotFoundException (string.Format ("Could not find the root directory starting from {0}", Environment.CurrentDirectory));
			return Path.GetFullPath (Path.Combine (current, "xamarin-macios", "_mac-build"));
		}

		static string GenerateOuputCommand (string tmpDir, Guid guid)
		{
			return string.Format ("System.IO.File.Create(\"{0}\").Dispose();",  Path.Combine (tmpDir, guid.ToString ()));
		}
	}

	static class PlatformHelpers
	{
		// Yes, this is a copy of the one in PlatformAvailability.cs. However, right now
		// we don't depend on Xamarin.Mac.dll, so moving to it was too painful. If we start
		// using XM, we can revisit.
		const int sys1 = 1937339185;
		const int sys2 = 1937339186;

		// Deprecated in OSX 10.8 - but no good alternative is (yet) available
		[System.Runtime.InteropServices.DllImport ("/System/Library/Frameworks/Carbon.framework/Versions/Current/Carbon")]
		static extern int Gestalt (int selector, out int result);

		static int osx_major, osx_minor;

		public static bool CheckSystemVersion (int major, int minor)
		{
			if (osx_major == 0) {
				Gestalt (sys1, out osx_major);
				Gestalt (sys2, out osx_minor);
			}
			return osx_major > major || (osx_major == major && osx_minor >= minor);
		}
	}
}

// A bit of a hack so we can reuse all of the RunCommand logic
namespace Xamarin.Bundler {
	public static partial class Driver
	{
		public static int verbose { get { return 0; } }
		public static int RunCommand (string path, string args, string[] env = null, StringBuilder output = null, bool suppressPrintOnErrors = false)
		{
			Exception stdin_exc = null;
			var info = new ProcessStartInfo (path, args);
			info.UseShellExecute = false;
			info.RedirectStandardInput = false;
			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			System.Threading.ManualResetEvent stdout_completed = new System.Threading.ManualResetEvent (false);
			System.Threading.ManualResetEvent stderr_completed = new System.Threading.ManualResetEvent (false);

			if (output == null)
				output = new StringBuilder ();

			if (env != null){
				if (env.Length % 2 != 0)
					throw new Exception ("You passed an environment key without a value");

				for (int i = 0; i < env.Length; i+= 2)
					info.EnvironmentVariables [env[i]] = env[i+1];
			}

			if (verbose > 0)
				Console.WriteLine ("{0} {1}", path, args);

			using (var p = Process.Start (info)) {

				p.OutputDataReceived += (s, e) => {
					if (e.Data != null) {
						lock (output)
							output.AppendLine (e.Data);
					} else {
						stdout_completed.Set ();
					}
				};

				p.ErrorDataReceived += (s, e) => {
					if (e.Data != null) {
						lock (output)
							output.AppendLine (e.Data);
					} else {
						stderr_completed.Set ();
					}
				};

				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();

				p.WaitForExit ();

				stderr_completed.WaitOne (TimeSpan.FromSeconds (1));
				stdout_completed.WaitOne (TimeSpan.FromSeconds (1));

				if (p.ExitCode != 0) {
					// note: this repeat the failing command line. However we can't avoid this since we're often
					// running commands in parallel (so the last one printed might not be the one failing)
					if (!suppressPrintOnErrors)
						Console.Error.WriteLine ("Process exited with code {0}, command:\n{1} {2}{3}", p.ExitCode, path, args, output.Length > 0 ? "\n" + output.ToString () : string.Empty);
					return p.ExitCode;
				} else if (verbose > 0 && output.Length > 0 && !suppressPrintOnErrors) {
					Console.WriteLine (output.ToString ());
				}

				if (stdin_exc != null)
					throw stdin_exc;
			}

			return 0;
		}
	}
}
