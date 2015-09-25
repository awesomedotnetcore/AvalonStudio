﻿using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using AvalonStudio.Models.Solutions;
namespace AvalonStudio.Models.Tools.Compiler
{
    public class PicXC32ToolChain : StandardToolChain
    {
        public override void Compile (IConsole console, Project superProject, Project project, ProjectFile file, string outputFile, CompileResult result)
        {
            var startInfo = new ProcessStartInfo();

            string binDirectory = Path.Combine(Settings.ToolChainLocation, "bin");

            startInfo.FileName = Path.Combine(binDirectory, "xc32-gcc.exe");

            startInfo.WorkingDirectory = project.Solution.CurrentDirectory;

            if (!File.Exists(startInfo.FileName))
            {
                result.ExitCode = -1;
                console.WriteLine("Unable to find compiler (" + startInfo.FileName + ") Please check project compiler settings.");
                return;
            }

            // Hide console window
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            string fileArguments = string.Empty;

            if (file.FileType == FileType.CPlusPlus)
            {
                startInfo.FileName = Path.Combine(binDirectory, "xc32-g++.exe");
                fileArguments = "-x c++ -frtti -fno-exceptions -fno-check-new -fenforce-eh-specs -std=c++0x -fno-use-cxa-atexit";
            }

            startInfo.Arguments = string.Format("{0} {1} {2} -g -o{3} -MMD -MP", GetCompilerArguments(superProject, file.FileType), fileArguments, file.Location, outputFile);

            // Hide console window
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            //console.WriteLine("[CC] " + Path.GetFileName(file.Location) + startInfo.Arguments);

            using (var process = Process.Start(startInfo))
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    console.WriteLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    console.WriteLine(e.Data);
                };

                process.BeginOutputReadLine();

                process.BeginErrorReadLine();

                process.WaitForExit();

                result.ExitCode = process.ExitCode;
            }
        }

        public override LinkResult Link (IConsole console, Project superProject, Project project, CompileResult assemblies, string outputDirectory)
        {
            LinkResult result = new LinkResult();

            ProcessStartInfo startInfo = new ProcessStartInfo();

            string binDirectory = Path.Combine(Settings.ToolChainLocation, "bin");

            startInfo.FileName = Path.Combine(binDirectory, "xc32-g++.exe");

            if (project.SelectedConfiguration.IsLibrary)
            {
                startInfo.FileName = Path.Combine(binDirectory, "xc32-ar.exe");
            }

            startInfo.WorkingDirectory = project.Solution.CurrentDirectory;

            if (!File.Exists(startInfo.FileName))
            {
                result.ExitCode = -1;
                console.WriteLine("Unable to find linker executable (" + startInfo.FileName + ") Check project compiler settings.");
                return result;
            }

            string objectArguments = string.Empty;
            foreach (string obj in assemblies.ObjectLocations)
            {
                objectArguments += obj + " ";
            }

			string libs = string.Empty;
			foreach (string lib in assemblies.LibraryLocations)
			{
				libs += lib + " ";
			}

			if (!Directory.Exists (outputDirectory))
            {
				Directory.CreateDirectory (outputDirectory);
			}

			string outputName = Path.GetFileNameWithoutExtension(project.FileName) + ".elf";

            if (project.SelectedConfiguration.IsLibrary)
            {
                outputName = "lib" + Path.GetFileNameWithoutExtension(project.FileName) + ".a";
            }

            var executable = Path.Combine(outputDirectory, outputName);

            string linkedLibraries = string.Empty;

            foreach (var libraryPath in project.SelectedConfiguration.LinkedLibraries)
            {
                string relativePath = Path.GetDirectoryName(libraryPath);

                string libName = Path.GetFileNameWithoutExtension(libraryPath).Substring(3);

                linkedLibraries += string.Format(" -L\"{0}\" -l{1}", relativePath, libName);
            }

            linkedLibraries = " " + linkedLibraries.Trim();

            string stdLibraries = string.Empty;

            switch (project.SelectedConfiguration.Library)
            {
                case LibraryType.NanoCLib:
                    stdLibraries = " -lc_nano";
                    break;

                case LibraryType.BaseCLib:
                    stdLibraries = " -lm -lgcc -lc";
                    break;

                case LibraryType.SemiHosting:
                    stdLibraries = " -lm -lgcc -lc -lrdimon";
                    break;

                case LibraryType.Retarget:
                    stdLibraries = " -lm -lgcc -lc -lnosys";
                    break;
            }

            // Hide console window
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.CreateNoWindow = true;

            startInfo.Arguments = string.Format("{0} -o{1} {2} -Wl,--start-group {3} {4} -Wl,--end-group", GetLinkerArguments (project), executable, objectArguments, libs, linkedLibraries);

            if (project.SelectedConfiguration.IsLibrary)
            {
                startInfo.Arguments = string.Format("rvs {0} {1}", executable, objectArguments);
            }

            console.WriteLine("[LL] " + startInfo.Arguments);

            using (var process = Process.Start(startInfo))
            {
                if (console != null)
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        console.WriteLine(process.StandardError.ReadLine());
                    }

                    while (!process.StandardOutput.EndOfStream)
                    {
                        console.WriteLine(process.StandardOutput.ReadLine());
                    }                    
                }

                process.WaitForExit();

                result.ExitCode = process.ExitCode;

                if (result.ExitCode == 0)
                {
                    result.Executable = executable;
                }
            }

            return result;
        }

        public override ProcessResult Size (IConsole console, Project project, LinkResult linkResult)
        {
            ProcessResult result = new ProcessResult ();

            ProcessStartInfo startInfo = new ProcessStartInfo ();

            string binDirectory = Path.Combine (Settings.ToolChainLocation, "bin");
            startInfo.FileName = Path.Combine (binDirectory, "xc32-size.exe");

            if (!File.Exists (startInfo.FileName))
            {
                console.WriteLine ("Unable to find tool (" + startInfo.FileName + ") check project compiler settings.");
                result.ExitCode = -1;
                return result;
            }

            startInfo.Arguments = string.Format ("{0}", linkResult.Executable);

            // Hide console window
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.CreateNoWindow = true;


            using (var process = Process.Start (startInfo))
            {
                if (console != null)
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var task = process.StandardOutput.ReadToEndAsync ();
                        task.Wait ();
                        console.WriteLine (task.Result);
                    }
                }

                process.WaitForExit ();

                result.ExitCode = process.ExitCode;
            }

            return result;
        }

        public override string GetCompilerArguments(Project project, FileType language)
        {
            string result = string.Empty;
            var configuration = project.SelectedConfiguration;

            string targetOptions = string.Empty;

            if (project.SelectedConfiguration.SelectedDeviceName != null)
            {
               targetOptions = string.Format(" -mprocessor={0}", project.SelectedConfiguration.SelectedDeviceName.Replace("PIC", ""));
            }


            string fpu = string.Empty;

            /*switch (configuration.Fpu)
            {
                case FPUSupport.Soft:
                    fpu = " -mfpu=fpv4-sp-d16 -mfloat-abi=softfp ";
                    break;

                case FPUSupport.Hard:
                    fpu = " -mfpu=fpv4-sp-d16 -mfloat-abi=hard ";
                    break;
            }*/

            string standardOptions = " -Wall -g -c -Wno-unknown-pragmas";

            if (!project.SelectedConfiguration.IsLibrary)
            {
                standardOptions += " -ffunction-sections -fdata-sections";
            }

            string optimizationLevel = string.Empty;

            switch (configuration.Optimization)
            {
                case OptimizationLevel.None:
                    optimizationLevel = " -O0";
                    break;

                case OptimizationLevel.Level1:
                    optimizationLevel = " -fauto-inc-dec -fbranch-count-reg -fcprop-registers -fdce -fdefer-pop -fdelayed-branch -fdse -fforward-propagate -fguess-branch-probability -fif-conversion2 -fif-conversion -finline-functions-called-once -fipa-pure-const -fipa-reference -fmerge-constants -fmove-loop-invariants -fshrink-wrap -fsplit-wide-types -ftree-ccp -ftree-ch -ftree-copy-prop -ftree-copyrename -ftree-dce -ftree-dominator-opts -ftree-dse -ftree-forwprop -ftree-fre -ftree-phiprop -ftree-sink -ftree-sra -ftree-pta -ftree-ter -funit-at-a-time";
                    break;

                case OptimizationLevel.Level2:
                    optimizationLevel = " -fauto-inc-dec -fbranch-count-reg -fcprop-registers -fdce -fdefer-pop -fdelayed-branch -fdse -fforward-propagate -fguess-branch-probability -fif-conversion2 -fif-conversion -finline-functions-called-once -fipa-pure-const -fipa-reference -fmerge-constants -fmove-loop-invariants -fshrink-wrap -fsplit-wide-types -ftree-ccp -ftree-ch -ftree-copy-prop -ftree-copyrename -ftree-dce -ftree-dominator-opts -ftree-dse -ftree-forwprop -ftree-fre -ftree-phiprop -ftree-sink -ftree-sra -ftree-pta -ftree-ter -funit-at-a-time -fthread-jumps -falign-functions  -falign-jumps -falign-loops -falign-labels -fcaller-saves -fcrossjumping -fcse-follow-jumps -fcse-skip-blocks -fdelete-null-pointer-checks -fexpensive-optimizations -fgcse -fgcse-lm -finline-small-functions -findirect-inlining -fipa-cp -fipa-sra -foptimize-sibling-calls -fpeephole2 -freorder-blocks -freorder-blocks-and-partition -freorder-functions -frerun-cse-after-loop -fsched-interblock -fsched-spec -fschedule-insns -fschedule-insns2 -fstrict-aliasing -fstrict-overflow -ftree-builtin-call-dce -ftree-switch-conversion -ftree-pre -ftree-vrp";
                    break;

                case OptimizationLevel.Level3:
                    optimizationLevel = " -fauto-inc-dec -fbranch-count-reg -fcprop-registers -fdce -fdefer-pop -fdelayed-branch -fdse -fforward-propagate -fguess-branch-probability -fif-conversion2 -fif-conversion -finline-functions-called-once -fipa-pure-const -fipa-reference -fmerge-constants -fmove-loop-invariants -fshrink-wrap -fsplit-wide-types -ftree-ccp -ftree-ch -ftree-copy-prop -ftree-copyrename -ftree-dce -ftree-dominator-opts -ftree-dse -ftree-forwprop -ftree-fre -ftree-phiprop -ftree-sink -ftree-sra -ftree-pta -ftree-ter -funit-at-a-time -fthread-jumps -falign-functions  -falign-jumps -falign-loops -falign-labels -fcaller-saves -fcrossjumping -fcse-follow-jumps -fcse-skip-blocks -fdelete-null-pointer-checks -fexpensive-optimizations -fgcse -fgcse-lm -finline-small-functions -findirect-inlining -fipa-cp -fipa-sra -foptimize-sibling-calls -fpeephole2 -freorder-blocks -freorder-blocks-and-partition -freorder-functions -frerun-cse-after-loop -fsched-interblock -fsched-spec -fschedule-insns -fschedule-insns2 -fstrict-aliasing -fstrict-overflow -ftree-builtin-call-dce -ftree-switch-conversion -ftree-pre -ftree-vrp -ftree-slp-vectorize -fvect-cost-model";
                    break;
            }

            string optimizationPreference = string.Empty;

            switch (configuration.OptimizationPreference)
            {
                case OptimizationPreference.Size:
                    optimizationPreference = " -Os";
                    break;

                case OptimizationPreference.Speed:
                    optimizationPreference = " -Ofast";
                    break;
            }

            string miscOptions = " " + configuration.MiscCompilerArguments;

            string defines = string.Empty;

            foreach (var define in configuration.Defines)
            {
                defines += string.Format(" -D{0}", define);
            }

            string includes = " ";

            foreach (var include in project.IncludeArguments)
            {
                includes += string.Format(" {0} ", include);
            }

            result = string.Format("{0}{1}{2}{3}{4}{5}{6}{7}", targetOptions, fpu, standardOptions, optimizationLevel, optimizationPreference, miscOptions, defines, includes);

            return result;
        }

        public override string GetLinkerArguments(Project project)
        {
            string result = string.Empty;

            if (project.SelectedConfiguration.SelectedDeviceName != null)
            {
                result += string.Format("-mprocessor={0} ", project.SelectedConfiguration.SelectedDeviceName.Replace("PIC", ""));
            }

            switch (project.SelectedConfiguration.Fpu)
            {
                case FPUSupport.Soft:
                    result += " -mfpu=fpv4-sp-d16 -mfloat-abi=softfp ";
                    break;

                case FPUSupport.Hard:
                    result += " -mfpu=fpv4-sp-d16 -mfloat-abi=hard ";
                    break;
            }

            result += string.Format("-g -flto -Wl,-Map={0}.map ", Path.GetFileNameWithoutExtension(project.FileName));

            if (project.SelectedConfiguration.NotUseStandardStartupFiles)
            {
                result += "-nostartfiles ";
            }

            if (project.SelectedConfiguration.DiscardUnusedSections)
            {
                result += "-Wl,--gc-sections ";
            }

            string optimizationLevel = string.Empty;

            switch (project.SelectedConfiguration.Optimization)
            {
                case OptimizationLevel.None:
                    result += " -O0";
                    break;

                case OptimizationLevel.Level1:
                    result += " -O1";
                    break;

                case OptimizationLevel.Level2:
                    result += " -O2";
                    break;

                case OptimizationLevel.Level3:
                    result += " -O3";
                    break;
            }

            result += " " + project.SelectedConfiguration.MiscLinkerArguments;

           // result += string.Format(" -L{0} -Wl,-T\"{1}\"", Path.GetDirectoryName(GetLinkerScriptLocation(project)), Path.GetFileName(GetLinkerScriptLocation(project)));

            return result;
        }

        private string GetLinkerScriptLocation(Project project)
        {
            return Path.Combine(project.CurrentDirectory, "link.ld");
        }

        #region Settings
        public enum PIC32OptimizationLevel
        {
            [Description ("-O0")]
            Off,
            [Description ("-O1")]
            Level1,
            [Description ("-O2")]
            Level2,
            [Description ("-O3")]
            Level3
        }

        public enum Pic32OptimizationPreference
        {
            [Description ("")]
            None,
            [Description ("-Os")]
            Size,
            [Description ("-Ofast")]
            Speed
        }

        public PIC32OptimizationLevel Optimization { get; set; }
        public Pic32OptimizationPreference OptimizationPriority { get; set; }

        public string CompilerCustomArguments { get; set; }

        public string LinkerCustomArguments { get; set; }

        public string LinkerScript { get; set; }
        #endregion

        public override string GDBExecutable
        {
            get
            {
                string binDirectory = Path.Combine (Settings.ToolChainLocation, "mips-none-elf");
                return Path.Combine (binDirectory, "mips-none-elf-gdb.exe");
            }
        }
    }
}
