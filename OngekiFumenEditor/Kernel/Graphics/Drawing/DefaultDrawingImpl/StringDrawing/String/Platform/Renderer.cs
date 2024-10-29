using FontStashSharp.Interfaces;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String.Platform
{
	internal class Renderer : IFontStashRenderer2, IDisposable
	{
		private const string frag = @"
            #version 330
			#ifdef GL_ES
				#define LOWP lowp
				precision mediump float;
			#else
				#define LOWP
			#endif


			// Uniforms
			uniform sampler2D TextureSampler;

			// Varyings
			in vec4 v_color;
			in vec2 v_texCoords;

            out vec4 FragColor;

			void main()
			{
				FragColor = v_color * texture2D(TextureSampler, v_texCoords);
			}
		";
		private const string vert = @"
            #version 330
            // Attributes
            in vec3 a_position;
			in vec4 a_color;
			in vec2 a_texCoords0;

			// Uniforms
			uniform mat4 MVP;

			// Varyings
			out vec4 v_color;
			out vec2 v_texCoords;

			void main()
			{
				v_color = a_color;
				v_texCoords = a_texCoords0;
				gl_Position = MVP * vec4(a_position, 1.0);
			}
		";

		private const int MAX_SPRITES = 2048;
		private const int MAX_VERTICES = MAX_SPRITES * 4;
		private const int MAX_INDICES = MAX_SPRITES * 6;

		private readonly StringShader _shader;
		private readonly BufferObject<VertexPositionColorTexture> _vertexBuffer;
		private readonly BufferObject<short> _indexBuffer;
		private readonly VertexArrayObject _vao;
		private readonly VertexPositionColorTexture[] _vertexData = new VertexPositionColorTexture[MAX_VERTICES];
		private object _lastTexture;
		private int _vertexIndex = 0;
		private IPerfomenceMonitor performenceMonitor;
		private IDrawing refDrawing;
		private readonly Texture2DManager _textureManager;

		public ITexture2DManager TextureManager => _textureManager;

		private static readonly short[] indexData = GenerateIndexArray();

		public unsafe Renderer()
		{
			_textureManager = new Texture2DManager();

			_vertexBuffer = new BufferObject<VertexPositionColorTexture>(MAX_VERTICES, BufferTarget.ArrayBuffer, true);
			_indexBuffer = new BufferObject<short>(indexData.Length, BufferTarget.ElementArrayBuffer, false);
			_indexBuffer.SetData(indexData, 0, indexData.Length);

			_shader = new (vert, frag);
            _shader.Compile();
            _shader.Begin();

			_vao = new VertexArrayObject(sizeof(VertexPositionColorTexture));
			_vao.Bind();

			var location = _shader.GetAttribLocation("a_position");
			_vao.VertexAttribPointer(location, 3, VertexAttribPointerType.Float, false, 0);

			location = _shader.GetAttribLocation("a_color");
			_vao.VertexAttribPointer(location, 4, VertexAttribPointerType.UnsignedByte, true, 12);

			location = _shader.GetAttribLocation("a_texCoords0");
			_vao.VertexAttribPointer(location, 2, VertexAttribPointerType.Float, false, 16);
		}

		~Renderer() => Dispose(false);
		public void Dispose() => Dispose(true);

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing)
			{
				return;
			}

			_vao.Dispose();
			_vertexBuffer.Dispose();
			_indexBuffer.Dispose();
			_shader.Dispose();
		}

		public void Begin(Matrix4 mvp, IPerfomenceMonitor perfomenceMonitor, IDrawing refDrawing)
		{
			performenceMonitor = perfomenceMonitor;
			this.refDrawing = refDrawing;

			_shader.Begin();

			_shader.PassUniform("TextureSampler", 0);
			_shader.PassUniform("MVP", mvp);

			_vao.Bind();
			_indexBuffer.Bind();
			_vertexBuffer.Bind();
		}

		public void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight, ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
		{
			if (_lastTexture != texture)
			{
				FlushBuffer();
			}

			_vertexData[_vertexIndex++] = topLeft;
			_vertexData[_vertexIndex++] = topRight;
			_vertexData[_vertexIndex++] = bottomLeft;
			_vertexData[_vertexIndex++] = bottomRight;

			_lastTexture = texture;
		}

		public void End()
		{
			FlushBuffer();

			refDrawing = default;
			performenceMonitor = default;
		}

		private unsafe void FlushBuffer()
		{
			if (_vertexIndex == 0 || _lastTexture == null)
			{
				return;
			}

			_vertexBuffer.SetData(_vertexData, 0, _vertexIndex);

			var texture = (Texture)_lastTexture;
			texture.Bind();

			GL.DrawElements(PrimitiveType.Triangles, _vertexIndex * 6 / 4, DrawElementsType.UnsignedShort, IntPtr.Zero);
			performenceMonitor.CountDrawCall(refDrawing);

			_vertexIndex = 0;
		}

		private static short[] GenerateIndexArray()
		{
			short[] result = new short[MAX_INDICES];
			for (int i = 0, j = 0; i < MAX_INDICES; i += 6, j += 4)
			{
				result[i] = (short)j;
				result[i + 1] = (short)(j + 1);
				result[i + 2] = (short)(j + 2);
				result[i + 3] = (short)(j + 3);
				result[i + 4] = (short)(j + 2);
				result[i + 5] = (short)(j + 1);
			}
			return result;
		}
	}
}
