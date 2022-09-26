using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics
{
    public class Shader : IDisposable
    {
        private int vertexShader, fragmentShader, program = -1;

        private bool compiled = false;

        private string vert;

        private string frag;

        public string VertexProgram { get { return vert; } set { vert = value; } }

        public string FragmentProgram { get { return frag; } set { frag = value; } }

        private Dictionary<string, object> _uniforms;

        public Dictionary<string, object> Uniforms { get { return _uniforms; } internal set { _uniforms = value; } }

        private string vertError;
        private string fragError;
        public string Error => $"vertex shader compile error :\n{vertError}\n\nfragment shader compile error :\n{fragError}";

        public void Compile()
        {
            if (compiled == false)
            {
                Dispose();

                Uniforms = new Dictionary<string, object>();

                vertexShader = GL.CreateShader(ShaderType.VertexShader);
                fragmentShader = GL.CreateShader(ShaderType.FragmentShader);

                GL.ShaderSource(vertexShader, vert);
                GL.ShaderSource(fragmentShader, frag);

                GL.CompileShader(vertexShader);
                if (!GL.IsShader(vertexShader))
                    throw new Exception("Vertex shader compile failed.");
                GL.CompileShader(fragmentShader);
                if (!GL.IsShader(fragmentShader))
                    throw new Exception("Fragment shader compile failed.");

                vertError = GL.GetShaderInfoLog(vertexShader);
                if (!string.IsNullOrEmpty(vertError))
                    Log.LogDebug("[Vertex Shader]:" + vertError);

                fragError = GL.GetShaderInfoLog(fragmentShader);
                if (!string.IsNullOrEmpty(fragError))
                    Log.LogDebug("[Fragment Shader]:" + fragError);

                program = GL.CreateProgram();

                GL.AttachShader(program, vertexShader);
                GL.AttachShader(program, fragmentShader);

                GL.LinkProgram(program);

                if (!string.IsNullOrEmpty(GL.GetProgramInfoLog(program)))
                    Log.LogError(GL.GetProgramInfoLog(program));

                GL.GetProgram(program, GetProgramParameterName.ActiveUniforms, out var total);

                for (int i = 0; i < total; i++)
                    GL.GetActiveUniform(program, i, 16, out _, out _, out _, out var _);

                compiled = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Begin()
        {
            GL.UseProgram(program);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End()
        {
            GL.UseProgram(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, Texture tex)
        {
            if (tex == null)
            {
                PassNullTexUniform(name);
                return;
            }

            GL.BindTexture(TextureTarget.Texture2D, tex.ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassNullTexUniform(string name)
        {
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, float v) => GL.Uniform1(l, v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, float val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, int v) => GL.Uniform1(l, v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, int val)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(int l, Vector2 v) => GL.Uniform2(l, v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, Vector2 val)
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
        public void PassUniform(int l, Matrix4 v) => GL.UniformMatrix4(l, false, ref v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PassUniform(string name, Matrix4 matrix4)
        {
            int l = GetUniformLocation(name);
            PassUniform(l, matrix4);
        }

        private Dictionary<string, int> _uniformDictionary = new Dictionary<string, int>();
        private Dictionary<string, int> _attrbDictionary = new Dictionary<string, int>();

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
    }
}