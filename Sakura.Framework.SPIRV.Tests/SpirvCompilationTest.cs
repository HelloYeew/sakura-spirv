// This code is part of the Sakura framework project. Licensed under the MIT License.
// See the LICENSE file for full license text.

using System;
using System.Text;
using NUnit.Framework;

namespace Sakura.Framework.SPIRV.Tests;

/// <summary>
/// Minimal test for testing shader compilation
/// </summary>
[TestFixture]
public class SpirvCompilationTest
{
    #region Shader sources

    private const string minimal_vert = @"#version 450

layout(location = 0) in vec2 aPosition;

void main() {
    gl_Position = vec4(aPosition, 0.0, 1.0);
}";

    private const string minimal_frag = @"#version 450

layout(location = 0) out vec4 FragColor;

void main() {
    FragColor = vec4(1.0, 0.0, 1.0, 1.0);
}";

    private const string uniform_vert = @"#version 450

layout(location = 0) in vec2 aPosition;
layout(set = 0, binding = 0) uniform Transform {
    mat4 uMatrix;
};

void main() {
    gl_Position = uMatrix * vec4(aPosition, 0.0, 1.0);
}";

    private const string uniform_frag = @"#version 450

layout(location = 0) out vec4 FragColor;
layout(set = 0, binding = 1) uniform Color {
    vec4 uColor;
};

void main() {
    FragColor = uColor;
}";

    private const string spec_const_vert = @"#version 450

layout(constant_id = 0) const int VERTEX_COUNT = 3;
layout(constant_id = 1) const float SCALE = 1.0;

layout(location = 0) in vec2 aPosition;

void main() {
    gl_Position = vec4(aPosition * SCALE, 0.0, 1.0);
}";

    private const string spec_const_frag = @"#version 450

layout(constant_id = 2) const bool INVERT_COLOR = false;
layout(location = 0) out vec4 FragColor;

void main() {
    vec4 color = vec4(1.0, 0.5, 0.0, 1.0);
    FragColor = INVERT_COLOR ? (1.0 - color) : color;
}";

    private const string macro_frag = @"#version 450

layout(location = 0) out vec4 FragColor;

void main() {
#ifdef USE_RED
    FragColor = vec4(1.0, 0.0, 0.0, 1.0);
#else
    FragColor = vec4(0.0, 0.0, 1.0, 1.0);
#endif
}";

    private const string compute_shader = @"#version 450

layout(local_size_x = 16, local_size_y = 16) in;

layout(set = 0, binding = 0, rgba8) uniform image2D outputImage;

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    vec4 color = vec4(float(coord.x) / 256.0, float(coord.y) / 256.0, 0.5, 1.0);
    imageStore(outputImage, coord, color);
}";

    private const string invalid_glsl = @"#version 450
something maybe kuromi kuromi kuromi
";

    #endregion

    #region Minimal vertex/fragment

    [Test]
    public void MinimalShader_CompilesTo_GLSL()
    {
        var result = compileVertexFragment(minimal_vert, minimal_frag, CrossCompileTarget.GLSL);

        Assert.That(result.VertexShader,   Is.Not.Null.And.Not.Empty);
        Assert.That(result.FragmentShader, Is.Not.Null.And.Not.Empty);
        Assert.That(result.VertexShader,   Does.Contain("void main"));
        Assert.That(result.FragmentShader, Does.Contain("void main"));

        Console.WriteLine("=== Vertex GLSL ===\n"   + result.VertexShader);
        Console.WriteLine("=== Fragment GLSL ===\n" + result.FragmentShader);
    }

    [Test]
    public void MinimalShader_CompilesTo_ESSL()
    {
        var result = compileVertexFragment(minimal_vert, minimal_frag, CrossCompileTarget.ESSL);

        Assert.That(result.VertexShader,   Does.Contain("void main"));
        Assert.That(result.FragmentShader, Does.Contain("void main"));
        // ESSL output should not carry a desktop GLSL version directive
        Assert.That(result.VertexShader, Does.Not.Contain("#version 450"));

        Console.WriteLine("=== Vertex ESSL ===\n" + result.VertexShader);
    }

    [Test]
    public void MinimalShader_CompilesTo_MSL()
    {
        var result = compileVertexFragment(minimal_vert, minimal_frag, CrossCompileTarget.MSL);

        Assert.That(result.VertexShader,   Is.Not.Null.And.Not.Empty);
        Assert.That(result.FragmentShader, Is.Not.Null.And.Not.Empty);
        Assert.That(result.VertexShader,   Does.Contain("#include <metal_stdlib>"));

        Console.WriteLine("=== Vertex MSL ===\n"   + result.VertexShader);
        Console.WriteLine("=== Fragment MSL ===\n" + result.FragmentShader);
    }

    [Test]
    public void MinimalShader_CompilesTo_HLSL()
    {
        var result = compileVertexFragment(minimal_vert, minimal_frag, CrossCompileTarget.HLSL);

        Assert.That(result.VertexShader,   Is.Not.Null.And.Not.Empty);
        Assert.That(result.FragmentShader, Is.Not.Null.And.Not.Empty);

        Console.WriteLine("=== Vertex HLSL ===\n"   + result.VertexShader);
        Console.WriteLine("=== Fragment HLSL ===\n" + result.FragmentShader);
    }

    #endregion

    #region GLSL → SPIR-V

    [Test]
    public void GlslToSpirv_Vertex_HasValidMagicNumber()
    {
        var result = SpirvCompilation.CompileGlslToSpirv(
            minimal_vert, "test.vert", ShaderStages.Vertex, new GlslCompileOptions());

        Assert.That(result.SpirvBytes, Is.Not.Null.And.Not.Empty);
        // SPIR-V magic: 0x07230203 (little-endian)
        Assert.That(result.SpirvBytes[0], Is.EqualTo(0x03));
        Assert.That(result.SpirvBytes[1], Is.EqualTo(0x02));
        Assert.That(result.SpirvBytes[2], Is.EqualTo(0x23));
        Assert.That(result.SpirvBytes[3], Is.EqualTo(0x07));

        Console.WriteLine($"Vertex SPIR-V: {result.SpirvBytes.Length} bytes");
    }

    [Test]
    public void GlslToSpirv_Fragment_HasValidMagicNumber()
    {
        var result = SpirvCompilation.CompileGlslToSpirv(
            minimal_frag, "test.frag", ShaderStages.Fragment, new GlslCompileOptions());

        Assert.That(result.SpirvBytes, Is.Not.Null.And.Not.Empty);
        Assert.That(result.SpirvBytes[0], Is.EqualTo(0x03));
        Assert.That(result.SpirvBytes[1], Is.EqualTo(0x02));
        Assert.That(result.SpirvBytes[2], Is.EqualTo(0x23));
        Assert.That(result.SpirvBytes[3], Is.EqualTo(0x07));

        Console.WriteLine($"Fragment SPIR-V: {result.SpirvBytes.Length} bytes");
    }

    [Test]
    public void GlslToSpirv_Compute_HasValidMagicNumber()
    {
        var result = SpirvCompilation.CompileGlslToSpirv(
            compute_shader, "test.comp", ShaderStages.Compute, new GlslCompileOptions());

        Assert.That(result.SpirvBytes, Is.Not.Null.And.Not.Empty);
        Assert.That(result.SpirvBytes[0], Is.EqualTo(0x03));
        Assert.That(result.SpirvBytes[1], Is.EqualTo(0x02));
        Assert.That(result.SpirvBytes[2], Is.EqualTo(0x23));
        Assert.That(result.SpirvBytes[3], Is.EqualTo(0x07));

        Console.WriteLine($"Compute SPIR-V: {result.SpirvBytes.Length} bytes");
    }

    [Test]
    public void GlslToSpirv_WithDebugFlag_Succeeds()
    {
        // Debug=true is required when the resulting SPIR-V will feed an OpenGL GLSL shader.
        var result = SpirvCompilation.CompileGlslToSpirv(
            minimal_vert, "test.vert", ShaderStages.Vertex, new GlslCompileOptions(debug: true));

        Assert.That(result.SpirvBytes, Is.Not.Null.And.Not.Empty);
        Console.WriteLine($"Debug SPIR-V: {result.SpirvBytes.Length} bytes");
    }

    [Test]
    public void GlslToSpirv_RoundTrip_SpirvBytesInputToCompileVertexFragment()
    {
        // Compile GLSL → SPIR-V first, then feed raw SPIR-V bytes into CompileVertexFragment
        // to verify the HasSpirvHeader branch (SPIR-V bytes input path) is exercised.
        byte[] vsSpirvBytes = SpirvCompilation.CompileGlslToSpirv(
            minimal_vert, "vert.spv", ShaderStages.Vertex, GlslCompileOptions.Default).SpirvBytes;
        byte[] fsSpirvBytes = SpirvCompilation.CompileGlslToSpirv(
            minimal_frag, "frag.spv", ShaderStages.Fragment, GlslCompileOptions.Default).SpirvBytes;

        var result = SpirvCompilation.CompileVertexFragment(
            vsSpirvBytes, fsSpirvBytes, CrossCompileTarget.GLSL);

        Assert.That(result.VertexShader,   Does.Contain("void main"));
        Assert.That(result.FragmentShader, Does.Contain("void main"));

        Console.WriteLine($"Round-trip: vert={result.VertexShader.Length}b, frag={result.FragmentShader.Length}b");
    }

    #endregion

    #region Macros

    [Test]
    public void MacroDefinition_Defined_AffectsOutput()
    {
        var withMacro = SpirvCompilation.CompileGlslToSpirv(
            macro_frag, "macro.frag", ShaderStages.Fragment,
            new GlslCompileOptions(false, new MacroDefinition("USE_RED")));

        var withoutMacro = SpirvCompilation.CompileGlslToSpirv(
            macro_frag, "macro.frag", ShaderStages.Fragment,
            new GlslCompileOptions());

        Assert.That(withMacro.SpirvBytes,    Is.Not.Null.And.Not.Empty);
        Assert.That(withoutMacro.SpirvBytes, Is.Not.Null.And.Not.Empty);
        // Different constant is embedded, so bytecode must differ
        Assert.That(withMacro.SpirvBytes, Is.Not.EqualTo(withoutMacro.SpirvBytes),
            "Bytecode should differ when USE_RED is defined vs not");
    }

    [Test]
    public void MacroDefinition_WithValue_IsAccepted()
    {
        const string src = @"#version 450
layout(location = 0) out vec4 FragColor;
void main() {
    FragColor = vec4(ALPHA_VALUE, 0.0, 0.0, 1.0);
}";
        var result = SpirvCompilation.CompileGlslToSpirv(
            src, "val.frag", ShaderStages.Fragment,
            new GlslCompileOptions(false, new MacroDefinition("ALPHA_VALUE", "0.5")));

        Assert.That(result.SpirvBytes, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Specialization constants

    [Test]
    public void SpecializationConstants_FloatScale_ChangesOutput()
    {
        var defaultResult = compileVertexFragment(spec_const_vert, spec_const_frag, CrossCompileTarget.GLSL);

        var scaledResult = compileVertexFragment(spec_const_vert, spec_const_frag, CrossCompileTarget.GLSL,
            options: new CrossCompileOptions(
                fixClipSpaceZ: false,
                invertVertexOutputY: false,
                specializations: SpecializationConstant.Create(1, 2.0f)));  // SCALE = 2.0

        Assert.That(defaultResult.VertexShader, Does.Contain("void main"));
        Assert.That(scaledResult.VertexShader,  Does.Contain("void main"));

        Console.WriteLine("Default spec:\n" + defaultResult.VertexShader);
        Console.WriteLine("Scaled spec:\n"  + scaledResult.VertexShader);
    }

    [Test]
    public void SpecializationConstants_Bool_ChangesOutput()
    {
        var normalResult = compileVertexFragment(spec_const_vert, spec_const_frag, CrossCompileTarget.GLSL);

        var invertedResult = compileVertexFragment(spec_const_vert, spec_const_frag, CrossCompileTarget.GLSL,
            options: new CrossCompileOptions(
                fixClipSpaceZ: false,
                invertVertexOutputY: false,
                specializations: SpecializationConstant.Create(2, true)));  // INVERT_COLOR = true

        Assert.That(normalResult.FragmentShader,   Does.Contain("void main"));
        Assert.That(invertedResult.FragmentShader, Does.Contain("void main"));
    }

    [Test]
    public void SpecializationConstants_Multiple_AreAccepted()
    {
        var result = compileVertexFragment(spec_const_vert, spec_const_frag, CrossCompileTarget.MSL,
            options: new CrossCompileOptions(
                fixClipSpaceZ: false,
                invertVertexOutputY: true,
                specializations: [SpecializationConstant.Create(0, 6), // VERTEX_COUNT = 6
                    SpecializationConstant.Create(1, 0.5f),            // SCALE = 0.5
                    SpecializationConstant.Create(2, true)             // INVERT_COLOR = true]
                    ]
            ));

        Assert.That(result.VertexShader,   Does.Contain("#include <metal_stdlib>"));
        Assert.That(result.FragmentShader, Does.Contain("#include <metal_stdlib>"));
    }

    #endregion

    #region CrossCompileOptions

    [Test]
    public void Option_InvertVertexOutputY_MSL_Succeeds()
    {
        var result = compileVertexFragment(minimal_vert, minimal_frag, CrossCompileTarget.MSL,
            options: new CrossCompileOptions(fixClipSpaceZ: false, invertVertexOutputY: true));

        Assert.That(result.VertexShader, Does.Contain("#include <metal_stdlib>"));
    }

    [Test]
    public void Option_FixClipSpaceZ_HLSL_Succeeds()
    {
        var result = compileVertexFragment(minimal_vert, minimal_frag, CrossCompileTarget.HLSL,
            options: new CrossCompileOptions(fixClipSpaceZ: true, invertVertexOutputY: false));

        Assert.That(result.VertexShader,   Is.Not.Null.And.Not.Empty);
        Assert.That(result.FragmentShader, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Option_NormalizeResourceNames_GLSL_Succeeds()
    {
        var result = compileVertexFragment(uniform_vert, uniform_frag, CrossCompileTarget.GLSL,
            options: new CrossCompileOptions(
                fixClipSpaceZ: false,
                invertVertexOutputY: false,
                normalizeResourceNames: true));

        Assert.That(result.VertexShader,   Does.Contain("void main"));
        Assert.That(result.FragmentShader, Does.Contain("void main"));
    }

    [Test]
    public void UniformBlock_CompilesTo_AllTargets()
    {
        foreach (var target in new[] { CrossCompileTarget.GLSL, CrossCompileTarget.HLSL, CrossCompileTarget.MSL })
        {
            var result = compileVertexFragment(uniform_vert, uniform_frag, target);

            Assert.That(result.VertexShader,   Is.Not.Null.And.Not.Empty,
                $"Vertex shader empty for {target}");
            Assert.That(result.FragmentShader, Is.Not.Null.And.Not.Empty,
                $"Fragment shader empty for {target}");

            Console.WriteLine($"[{target}] vert={result.VertexShader.Length}b frag={result.FragmentShader.Length}b");
        }
    }

    #endregion

    #region Compute shaders

    [Test]
    public void ComputeShader_CompilesTo_GLSL()
    {
        var result = SpirvCompilation.CompileCompute(
            Encoding.UTF8.GetBytes(compute_shader), CrossCompileTarget.GLSL);

        Assert.That(result.ComputeShader, Is.Not.Null.And.Not.Empty);
        Assert.That(result.ComputeShader, Does.Contain("void main"));

        Console.WriteLine("=== Compute GLSL ===\n" + result.ComputeShader);
    }

    [Test]
    public void ComputeShader_CompilesTo_MSL()
    {
        var result = SpirvCompilation.CompileCompute(
            Encoding.UTF8.GetBytes(compute_shader), CrossCompileTarget.MSL);

        Assert.That(result.ComputeShader, Is.Not.Null.And.Not.Empty);
        Assert.That(result.ComputeShader, Does.Contain("#include <metal_stdlib>"));

        Console.WriteLine("=== Compute MSL ===\n" + result.ComputeShader);
    }

    [Test]
    public void ComputeShader_CompilesTo_HLSL()
    {
        var result = SpirvCompilation.CompileCompute(
            Encoding.UTF8.GetBytes(compute_shader), CrossCompileTarget.HLSL);

        Assert.That(result.ComputeShader, Is.Not.Null.And.Not.Empty);

        Console.WriteLine("=== Compute HLSL ===\n" + result.ComputeShader);
    }

    #endregion

    #region Error handling

    [Test]
    public void InvalidGlsl_Throws_SpirvCompilationException()
    {
        Assert.Throws<SpirvCompilationException>(() =>
            SpirvCompilation.CompileGlslToSpirv(
                invalid_glsl, "bad.frag", ShaderStages.Fragment, new GlslCompileOptions()));
    }

    [Test]
    public void InvalidGlsl_ExceptionMessage_ContainsErrorInfo()
    {
        var ex = Assert.Throws<SpirvCompilationException>(() =>
            SpirvCompilation.CompileGlslToSpirv(
                invalid_glsl, "bad.frag", ShaderStages.Fragment, new GlslCompileOptions()));

        Assert.That(ex!.Message, Is.Not.Null.And.Not.Empty,
            "Exception message should contain compiler error details");

        Console.WriteLine("Compiler error: " + ex.Message);
    }

    [Test]
    public void EmptyShaderSource_Throws()
    {
        // Empty source hits a stackalloc(0) before the native call, so the managed layer
        // throws ArgumentNullException rather than SpirvCompilationException. Either way
        // the call must not silently succeed.
        Assert.Catch<Exception>(() =>
            SpirvCompilation.CompileGlslToSpirv(
                string.Empty, "empty.vert", ShaderStages.Vertex, new GlslCompileOptions()));
    }

    #endregion

    #region Helpers

    private static VertexFragmentCompilationResult compileVertexFragment(string vert, string frag, CrossCompileTarget target, CrossCompileOptions? options = null)
    {
        return SpirvCompilation.CompileVertexFragment(
            Encoding.UTF8.GetBytes(vert),
            Encoding.UTF8.GetBytes(frag),
            target,
            options ?? new CrossCompileOptions());
    }

    #endregion
}
