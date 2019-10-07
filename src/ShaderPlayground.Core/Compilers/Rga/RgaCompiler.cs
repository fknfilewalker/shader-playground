﻿using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShaderPlayground.Core.Util;

namespace ShaderPlayground.Core.Compilers.Rga
{
    internal sealed class RgaCompiler : IShaderCompiler
    {
        static RgaCompiler()
        {
            ProcessHelper.Run(
                Path.Combine(AppContext.BaseDirectory, "Binaries", "rga", "2.2", "rga.exe"),
                "-s dx11 --list-asics",
                out var stdOutput,
                out var _);

            // Extract ASICs from output.
            var coreRegex = new Regex(@"\n([a-zA-Z0-9 ]+) \(");
            var matches = coreRegex.Matches(stdOutput);

            AsicOptions = matches
                .Cast<Match>()
                .Select(x => x.Groups[1].Value)
                .ToArray();
        }

        public string Name { get; } = CompilerNames.Rga;
        public string DisplayName { get; } = "Radeon GPU Analyzer";
        public string Url { get; } = "https://github.com/GPUOpen-Tools/RGA";
        public string Description { get; } = "The Radeon GPU Analyzer (RGA) is an offline compiler and code analysis tool for DirectX shaders, OpenGL shaders, Vulkan shaders and OpenCL kernels.";

        public string[] InputLanguages { get; } = { LanguageNames.Hlsl, LanguageNames.Glsl, LanguageNames.SpirvAssembly };

        public ShaderCompilerParameter[] Parameters { get; } = new[]
        {
            CommonParameters.CreateVersionParameter("rga"),
            new ShaderCompilerParameter("Asic", "ASIC", ShaderCompilerParameterType.ComboBox, AsicOptions, "gfx900"),

            // HLSL
            CommonParameters.HlslEntryPoint.WithFilter(CommonParameters.InputLanguageParameterName, LanguageNames.Hlsl),
            new ShaderCompilerParameter("TargetProfile", "Target profile", ShaderCompilerParameterType.ComboBox, TargetProfileOptions, "ps_5_0", filter: new ParameterFilter(CommonParameters.InputLanguageParameterName, LanguageNames.Hlsl)),

            // GLSL
            new ShaderCompilerParameter("GlslTarget", "Target", ShaderCompilerParameterType.ComboBox, TargetOptions, TargetVulkan, filter: new ParameterFilter(CommonParameters.InputLanguageParameterName, LanguageNames.Glsl)),
            CommonParameters.GlslShaderStage.WithFilter(CommonParameters.InputLanguageParameterName, LanguageNames.Glsl)
        };

        private static readonly string[] AsicOptions;

        private static readonly string[] TargetProfileOptions =
        {
            "cs_4_0",
            "cs_4_1",
            "cs_5_0",
            "ds_5_0",
            "gs_4_0",
            "gs_4_1",
            "gs_5_0",
            "hs_5_0",
            "ps_4_0",
            "ps_4_1",
            "ps_5_0",
            "vs_4_0",
            "vs_4_1",
            "vs_5_0",
        };

        private const string TargetOpenGL = "OpenGL";
        private const string TargetVulkan = "Vulkan";

        private static readonly string[] TargetOptions =
        {
            TargetOpenGL,
            TargetVulkan
        };

        public ShaderCompilerResult Compile(ShaderCode shaderCode, ShaderCompilerArguments arguments)
        {
            var version = Version.Parse(arguments.GetString(CommonParameters.VersionParameterName));

            var isVersion21OrLater = version >= new Version(2, 1);
            var isVersion22OrLater = version >= new Version(2, 2);

            var asic = arguments.GetString("Asic");
            var entryPoint = arguments.GetString("EntryPoint");
            var targetProfile = arguments.GetString("TargetProfile");
            var shaderStage = arguments.GetString(CommonParameters.GlslShaderStage.Name);

            using (var tempFile = TempFile.FromShaderCode(shaderCode))
            {
                var outputAnalysisPath = $"{tempFile.FilePath}.analysis";
                var ilPath = $"{tempFile.FilePath}.il";
                var isaPath = $"{tempFile.FilePath}.isa";
                var liveRegPath = $"{tempFile.FilePath}.livereg";
                var cfgPath = $"{tempFile.FilePath}.cfg";

                var args = $"--asic \"{asic}\" --il \"{ilPath}\" --line-numbers --isa \"{isaPath}\" --livereg \"{liveRegPath}\" --cfg \"{cfgPath}\"";

                switch (shaderCode.Language)
                {
                    case LanguageNames.Hlsl:
                        args += isVersion22OrLater ? $" -s dx11" : " -s hlsl";
                        args += $" --profile {targetProfile} --function {entryPoint}";
                        args += $" \"{tempFile.FilePath}\"";
                        break;

                    case LanguageNames.Glsl:
                        switch (arguments.GetString("GlslTarget"))
                        {
                            case TargetOpenGL:
                                args += $" -s opengl --{shaderStage}";
                                break;

                            case TargetVulkan:
                                args += $" -s {(isVersion21OrLater ? "vk-offline" : "vulkan")} --{shaderStage}";
                                break;
                        }
                        args += $" \"{tempFile.FilePath}\"";
                        break;

                    case LanguageNames.SpirvAssembly:
                        args += $" -s {(isVersion21OrLater ? "vk-spv-txt-offline" : "vulkan-spv-text")} --{shaderStage}";
                        args += $" \"{tempFile.FilePath}\"";
                        break;
                }

                var rgaPath = CommonParameters.GetBinaryPath("rga", arguments, "rga.exe");
                var result = ProcessHelper.Run(
                    rgaPath,
                    args,
                    out var stdOutput,
                    out _);

                var actualOutputPathPrefix = Path.GetDirectoryName(tempFile.FilePath);

                string GetActualOutputPath(string extension)
                {
                    if (extension == "analysis" && shaderCode.Language == LanguageNames.Hlsl)
                    {
                        return Path.Combine(
                            Path.GetDirectoryName(tempFile.FilePath),
                            $"{entryPoint}_{Path.GetFileName(tempFile.FilePath)}.analysis");
                    }

                    switch (shaderCode.Language)
                    {
                        case LanguageNames.Hlsl:
                            return Path.Combine(
                                actualOutputPathPrefix,
                                $"{asic}_{entryPoint}_{Path.GetFileName(tempFile.FilePath)}.{extension}");

                        default:
                            if (isVersion21OrLater)
                            {
                                return Path.Combine(
                                    actualOutputPathPrefix,
                                    $"{asic}_{Path.GetFileName(tempFile.FilePath)}_{shaderStage}.{extension}");
                            }
                            else
                            {
                                return Path.Combine(
                                    actualOutputPathPrefix,
                                    $"{asic}_{shaderStage}_{Path.GetFileName(tempFile.FilePath)}.{extension}");
                            }
                    }
                }

                outputAnalysisPath = GetActualOutputPath("analysis");
                ilPath = GetActualOutputPath("il");
                isaPath = GetActualOutputPath("isa");
                liveRegPath = GetActualOutputPath("livereg");
                cfgPath = GetActualOutputPath("cfg");

                var outputAnalysis = FileHelper.ReadAllTextIfExists(outputAnalysisPath);
                var il = FileHelper.ReadAllTextIfExists(ilPath);
                var isa = FileHelper.ReadAllTextIfExists(isaPath);
                var liveReg = FileHelper.ReadAllTextIfExists(liveRegPath);
                var cfg = FileHelper.ReadAllTextIfExists(cfgPath);

                FileHelper.DeleteIfExists(outputAnalysisPath);
                FileHelper.DeleteIfExists(ilPath);
                FileHelper.DeleteIfExists(isaPath);
                FileHelper.DeleteIfExists(liveRegPath);
                FileHelper.DeleteIfExists(cfgPath);

                var selectedOutputIndex = !result || stdOutput.Contains("\nError: ") || stdOutput.Contains("... failed.")
                    ? 4
                    : (int?) null;

                return new ShaderCompilerResult(
                    selectedOutputIndex == null,
                    null,
                    selectedOutputIndex,
                    new ShaderCompilerOutput("ISA Disassembly", null, isa),
                    new ShaderCompilerOutput("IL Disassembly", null, il),
                    //new ShaderCompilerOutput("Analysis", null, outputAnalysis),
                    new ShaderCompilerOutput("Live register analysis", null, liveReg),
                    new ShaderCompilerOutput("Control flow graph", "graphviz", cfg),
                    new ShaderCompilerOutput("Build output", null, stdOutput));
            }
        }
    }
}
