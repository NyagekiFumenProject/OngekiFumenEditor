using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ReOsuStoryboardPlayer.Core.PrimitiveValue;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public class Shader
    {
        private int vertexShader, fragmentShader, program;

        private bool compiled = false;

        private string vert;

        private string frag;

        public string VertexProgram { get { return vert; } set { vert = value; } }

        public string FragmentProgram { get { return frag; } set { frag = value; } }

        private Dictionary<string, object> _uniforms;

        public Dictionary<string, object> Uniforms { get { return _uniforms; } internal set { _uniforms = value; } }

        public void Compile()
        {
            if (compiled == false)
            {
                compiled = true;

                Uniforms = new Dictionary<string, object>();

                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);
                GL.DeleteProgram(program);

                vertexShader = GL.CreateShader(ShaderType.VertexShader);
                fragmentShader = GL.CreateShader(ShaderType.FragmentShader);

                GL.ShaderSource(vertexShader, vert);
                GL.ShaderSource(fragmentShader, frag);

                GL.CompileShader(vertexShader);
                GL.CompileShader(fragmentShader);

                if (!String.IsNullOrEmpty(GL.GetShaderInfoLog(vertexShader)))
                    Log.LogError(GL.GetShaderInfoLog(vertexShader));

                if (!String.IsNullOrEmpty(GL.GetShaderInfoLog(fragmentShader)))
                    Log.LogError(GL.GetShaderInfoLog(fragmentShader));

                program = GL.CreateProgram();

                GL.AttachShader(program, vertexShader);
                GL.AttachShader(program, fragmentShader);

                GL.LinkProgram(program);

                if (!String.IsNullOrEmpty(GL.GetProgramInfoLog(program)))
                    Log.LogError(GL.GetProgramInfoLog(program));

                int total = 0;

                GL.GetProgram(program, GetProgramParameterName.ActiveUniforms, out total);

                for (int i = 0; i < total; i++)
                    GL.GetActiveUniform(program, i, 16, out _, out _, out _, out var _);
            }
        }

        public void Begin()
        {
            GL.UseProgram(program);
        }

        public void End()
        {
            GL.UseProgram(0);
        }

        public void PassUniform(string name, Texture tex)
        {
            if (tex == null)
            {
                PassNullTexUniform(name);
                return;
            }

            GL.BindTexture(TextureTarget.Texture2D, tex.ID);
        }

        public void PassNullTexUniform(string name)
        {
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void PassUniform(string name, Vec4 vec)
        {
            int l = GetUniformLocation(name);
            GL.Uniform4(l, vec.x, vec.y, vec.z, vec.w);
        }

        public void PassUniform(string name, float val)
        {
            int l = GetUniformLocation(name);
            GL.Uniform1(l, val);
        }

        public void PassUniform(string name, int val)
        {
            int l = GetUniformLocation(name);
            GL.Uniform1(l, val);
        }

        public void PassUniform(string name, Vector2 val)
        {
            int l = GetUniformLocation(name);
            GL.Uniform2(l, val);
        }

        public void PassUniform(string name, Matrix4 matrix4)
        {
            int l = GetUniformLocation(name);
            GL.UniformMatrix4(l, false, ref matrix4);
        }

        private Dictionary<string, int> _uniformDictionary = new Dictionary<string, int>();

        private int GetUniformLocation(string name)
        {
            if (_uniformDictionary.ContainsKey(name))
            {
                return _uniformDictionary[name];
            }
            int l = GL.GetUniformLocation(program, name);
            _uniformDictionary.Add(name, l);

            return l;
        }

        public int ShaderProgram => program;
    }
}