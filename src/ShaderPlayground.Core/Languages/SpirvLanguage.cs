﻿namespace ShaderPlayground.Core.Languages
{
    internal sealed class SpirvLanguage : IShaderLanguage
    {
        public string Name { get; } = LanguageNames.SpirvAssembly;

    public string DefaultCode { get; } = DefaultSpirvCode;

    public string FileExtension { get; } = "spvasm";

    private static readonly string DefaultSpirvCode = @"; SPIR-V
; Version: 1.0
; Generator: Khronos Glslang Reference Front End; 7
; Bound: 14
; Schema: 0
               OpCapability Shader
          %1 = OpExtInstImport ""GLSL.std.450""
               OpMemoryModel Logical GLSL450
               OpEntryPoint Fragment %main ""main"" %gl_FragColor
               OpExecutionMode %main OriginUpperLeft
               OpSource GLSL 330
               OpName %main ""main""
               OpName %gl_FragColor ""gl_FragColor""
               OpDecorate %gl_FragColor Location 0
       %void = OpTypeVoid
          %3 = OpTypeFunction %void
      %float = OpTypeFloat 32
    %v4float = OpTypeVector %float 4
%_ptr_Output_v4float = OpTypePointer Output %v4float
%gl_FragColor = OpVariable %_ptr_Output_v4float Output
%float_0_400000006 = OpConstant %float 0.400000006
%float_0_800000012 = OpConstant %float 0.800000012
    %float_1 = OpConstant %float 1
         %13 = OpConstantComposite %v4float %float_0_400000006 %float_0_400000006 %float_0_800000012 %float_1
       %main = OpFunction %void None %3
          %5 = OpLabel
               OpStore %gl_FragColor %13
               OpReturn
               OpFunctionEnd";
}
}
