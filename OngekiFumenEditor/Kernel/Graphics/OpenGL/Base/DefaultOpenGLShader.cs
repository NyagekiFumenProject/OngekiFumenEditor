using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Base
{
    public class DefaultOpenGLShader : IDisposable
    {
        private int vertexShader, fragmentShader, program = -1;

        private bool compiled = false;

        private string vert;

        private string frag;

        public string VertexProgram { get { return vert; } set { vert = value; } }

        public string FragmentProgram { get { return frag; } set { frag = value; } }

        public string GeometryProgram { get { return geo; } set { geo = value; } }

        private Dictionary<string, object> _uniforms;

        public Dictionary<string, object> Uniforms { get { return _uniforms; } internal set { _uniforms = value; } }

        private readonly Dictionary<int, float> cachedFloatUniformValues = new();
        private readonly Dictionary<int, int> cachedIntUniformValues = new();
        private readonly Dictionary<int, Vector2> cachedVector2UniformValues = new();
        private readonly Dictionary<int, Vector4> cachedVector4UniformValues = new();
        private readonly Dictionary<int, Matrix4> cachedMatrix4UniformValues = new();

        private string vertError;
        private string fragError;
        private string geoError;
        public string Error => GenErrorString();

        private static int currentUsingProgram = int.MinValue;

        private string GenErrorString()
        {
            string gen(string name, string msg)
            {
                if (string.IsNullOrWhiteSpace(msg))
                    return string.Empty;

                return $"{msg} has compile error(s):{msg}\n";
            }

            return gen(nameof(VertexProgram), vertError) + gen(nameof(FragmentProgram), fragError) + gen(nameof(GeometryProgram), geoError);
        }

        public void Compile()
        {
            if (compiled == false)
            {
                Dispose();

                Uniforms = new Dictionary<string, object>();
                ClearUniformValueCache();

                var genShaders = new List<int>();

                void compileShader(string source, ShaderType shaderType, ref int shader, ref string msg)
                {
                    shader = GL.CreateShader(shaderType);
                    GL.ShaderSource(shader, source);

                    GL.CompileShader(shader);
                    if (!GL.IsShader(shader))
                        throw new Exception($"{shaderType} compile failed.");
                    msg = GL.GetShaderInfoLog(shader);
                    if (!string.IsNullOrEmpty(msg))
                        Log.LogDebug($"[{shaderType}]:{msg}");

                    genShaders.Add(shader);
                }

                compileShader(VertexProgram, ShaderType.VertexShader, ref vertexShader, ref vertError);
                compileShader(FragmentProgram, ShaderType.FragmentShader, ref fragmentShader, ref fragError);
                if (!string.IsNullOrWhiteSpace(GeometryProgram))
                    compileShader(GeometryProgram, ShaderType.GeometryShader, ref geometryShader, ref geoError);

                program = GL.CreateProgram();

                foreach (var shader in genShaders)
                    GL.AttachShader(program, shader);

                GL.LinkProgram(program);

                var buildShaderError = GL.GetProgramInfoLog(program);
                if (!string.IsNullOrEmpty(buildShaderError))
                    Log.LogError(buildShaderError);

                GL.GetProgram(program, GetProgramParameterName.ActiveUniforms, out var total);

                for (int i = 0; i < total; i++)
                    GL.GetActiveUniform(program, i, 16, out _, out _, out _, out var _);

                GLUtility.CheckError($"Create shader {this.GetType().Name} failed");
                compiled = true;
            }
        }
#if DEBUG
        private static string _currentUsingShaderName = null;
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Begin()
        {
            //假设本项目所有OGL操作都是被我们代码管控之中，因此glUseProgram可以不需要来回切换
#if DEBUG
            /*
                检查是否在我们渲染管控范围外有其他着色器绑定, 好在开发时能及时发现并处理
             */
            GL.GetInteger(GetPName.CurrentProgram, out int curProgram);
            if (curProgram != currentUsingProgram && currentUsingProgram != int.MinValue)
                throw new Exception($"There are another shader {_currentUsingShaderName} is using.");
#endif
            if (currentUsingProgram != program)
            {
                GL.UseProgram(program);
                currentUsingProgram = program;
#if DEBUG
                _currentUsingShaderName = GetType().Name;
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End()
        {
            //不需要解绑
            //GL.UseProgram(0);
            currentUsingProgram = int.MinValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, DefaultOpenGLTexture tex)
        {
            PassUniform(GetUniformLocation(name), tex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassNullTexUniform(string name)
        {
            PassUniform(GetUniformLocation(name), default(DefaultOpenGLTexture));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, DefaultOpenGLTexture tex, int textureUnitIndex = 0)
        {
            OpenGLTextureBindingCache.BindTexture2D(tex?.ID ?? 0, textureUnitIndex);
            PassUniform(l, textureUnitIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, float v)
        {
            if (UniformValueEquals(cachedFloatUniformValues, l, v))
                return;
            GL.Uniform1(l, v);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, float val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, int v)
        {
            if (UniformValueEquals(cachedIntUniformValues, l, v))
                return;
            GL.Uniform1(l, v);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, int val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, Vector2 v)
        {
            if (UniformValueEquals(cachedVector2UniformValues, l, v))
                return;
            GL.Uniform2(l, v);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, Vector2 val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, Vector4 v)
        {
            if (UniformValueEquals(cachedVector4UniformValues, l, v))
                return;
            GL.Uniform4(l, v);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, Vector4 val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, System.Numerics.Vector2 v) => PassUniform(l, new Vector2(v.X, v.Y));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, System.Numerics.Vector2 val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, Matrix4 v)
        {
            if (UniformValueEquals(cachedMatrix4UniformValues, l, v))
                return;
            GL.UniformMatrix4(l, false, ref v);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, Matrix4 matrix4)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, matrix4);
        }

        private Dictionary<string, int> _uniformDictionary = new Dictionary<string, int>();
        private Dictionary<string, int> _attrbDictionary = new Dictionary<string, int>();
        private string geo;
        private int geometryShader;

        public int GetUniformLocation(string name)
        {
            if (!_uniformDictionary.TryGetValue(name, out var l))
            {
                l = GL.GetUniformLocation(program, name);
                _uniformDictionary[name] = l;
            }

            return l;
        }

        public int GetAttribLocation(string name)
        {
            if (!_attrbDictionary.TryGetValue(name, out var l))
            {
                l = GL.GetAttribLocation(program, name);
                _attrbDictionary[name] = l;
            }

            return l;
        }

        public void Dispose()
        {
            if (program < 0)
                return;
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            GL.DeleteProgram(program);
            program = -1;
        }

        public int ShaderProgram => program;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool UniformValueEquals<T>(Dictionary<int, T> cache, int location, T value)
        {
            if (location < 0)
                return true;

            if (cache.TryGetValue(location, out var cachedValue) && EqualityComparer<T>.Default.Equals(cachedValue, value))
                return true;

            cache[location] = value;
            return false;
        }

        private void ClearUniformValueCache()
        {
            cachedFloatUniformValues.Clear();
            cachedIntUniformValues.Clear();
            cachedVector2UniformValues.Clear();
            cachedVector4UniformValues.Clear();
            cachedMatrix4UniformValues.Clear();
        }
    }
}
