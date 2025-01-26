using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class MeshWindow : GameWindow
{
  private readonly Object3D _mesh;
  private readonly List<int> _vaos = [];
  private readonly List<int> _vbos = [];
  private readonly List<int> _ebos = [];
  private readonly List<int> _textureIds = [];

  private int _shaderProgram;
  private int _projectionLocation, _viewLocation, _modelLocation;

  private float _deltaTime = 0.0f;
  private float _zoom = 1.0f;
  private float _rotationX = 0.0f;
  private float _rotationY = 0.0f;
  private Vector2 _lastMousePosition;

  public MeshWindow(Object3D mesh)
      : base(GameWindowSettings.Default, new NativeWindowSettings
      {
        ClientSize = new Vector2i(1000, 800),
        Title = "GLTF Mesh Renderer with Morph Animation"
      })
  {
    _mesh = mesh;
  }

  protected override void OnLoad()
  {
    base.OnLoad();

    GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
    GL.Enable(EnableCap.DepthTest);

    // Load and compile shaders
    string vertexShaderSource = File.ReadAllText("src/static/shaders/vert.glsl");
    string fragmentShaderSource = File.ReadAllText("src/static/shaders/frag.glsl");

    int vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
    int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

    _shaderProgram = GL.CreateProgram();
    GL.AttachShader(_shaderProgram, vertexShader);
    GL.AttachShader(_shaderProgram, fragmentShader);
    GL.LinkProgram(_shaderProgram);

    GL.DeleteShader(vertexShader);
    GL.DeleteShader(fragmentShader);

    // Get uniform locations
    _projectionLocation = GL.GetUniformLocation(_shaderProgram, "projection");
    _viewLocation = GL.GetUniformLocation(_shaderProgram, "view");
    _modelLocation = GL.GetUniformLocation(_shaderProgram, "model");

    foreach (MeshData meshData in _mesh.Meshes)
    {
      int vao = GL.GenVertexArray();
      int vbo = GL.GenBuffer();
      int ebo = GL.GenBuffer();

      GL.BindVertexArray(vao);

      // Upload base vertices
      GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
      GL.BufferData(BufferTarget.ArrayBuffer, meshData.Vertices.Count * sizeof(float), meshData.Vertices.ToArray(), BufferUsageHint.StaticDraw);
      GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
      GL.EnableVertexAttribArray(0);

      // Upload morph targets
      for (int i = 0; i < meshData.MorphTargets.Length; i++)
      {
        int morphVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, morphVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, meshData.MorphTargets[i].Length * sizeof(float), meshData.MorphTargets[i], BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(2 + i, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(2 + i);
        _vbos.Add(morphVbo);
      }

      // Upload indices
      GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
      GL.BufferData(BufferTarget.ElementArrayBuffer, meshData.Indices.Count * sizeof(int), meshData.Indices.ToArray(), BufferUsageHint.StaticDraw);

      _vaos.Add(vao);
      _vbos.Add(vbo);
      _ebos.Add(ebo);
    }

  }

  private int LoadTexture(byte[] binaryData)
  {
    int textureId = GL.GenTexture();
    GL.BindTexture(TextureTarget.Texture2D, textureId);

    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

    using var memoryStream = new MemoryStream(binaryData);
    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(memoryStream);

    byte[] pixelData = new byte[image.Width * image.Height * 4];
    image.ProcessPixelRows(accessor =>
    {
      for (int y = 0; y < image.Height; y++)
      {
        var row = accessor.GetRowSpan(y);
        for (int x = 0; x < row.Length; x++)
        {
          var pixel = row[x];
          int index = (y * image.Width + x) * 4;
          pixelData[index] = pixel.R;
          pixelData[index + 1] = pixel.G;
          pixelData[index + 2] = pixel.B;
          pixelData[index + 3] = pixel.A;
        }
      }
    });

    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelData);

    return textureId;
  }

  protected override void OnUpdateFrame(FrameEventArgs args)
  {
    base.OnUpdateFrame(args);

    _deltaTime = (_deltaTime + (float)args.Time) % _mesh.Meshes.Max(m => m.Duration);
    UpdateProjection();
  }

  protected override void OnRenderFrame(FrameEventArgs args)
  {
    base.OnRenderFrame(args);

    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    GL.UseProgram(_shaderProgram);

    foreach (MeshData meshData in _mesh.Meshes)
    {
      float[] morphWeights = InterpolateMorphWeights(_deltaTime, meshData);

      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "animationTime"), _deltaTime);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "animationDuration"), meshData.Duration);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "keyframeTimes"), meshData.KeyframeTimes.Length, meshData.KeyframeTimes);
      GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "translations"), meshData.TranslationLength, meshData.TranslationBuffer);
      GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "rotations"), meshData.RotationLength, meshData.RotationBuffer);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "keyframeCount"), meshData.KeyframeTimes.Length);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "morphWeights"), morphWeights.Length, morphWeights);

      Matrix4 model = meshData.ModelMatrix *
                      Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_rotationX)) *
                      Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_rotationY));

      GL.UniformMatrix4(_modelLocation, false, ref model);

      GL.BindVertexArray(_vaos[_mesh.Meshes.IndexOf(meshData)]);

      if (meshData.Textures.Count > 0)
      {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _textureIds[_mesh.Meshes.IndexOf(meshData)]);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "textureSampler"), 0);
      }

      GL.DrawElements(PrimitiveType.Triangles, meshData.Indices.Count, DrawElementsType.UnsignedInt, 0);
    }

    SwapBuffers();
  }

  private float[] InterpolateMorphWeights(float elapsedTime, MeshData meshData)
  {
    float time = elapsedTime % meshData.Duration;
    float[] _morphWeights = new float[meshData.KeyframeWeights.GetLength(1)];

    for (int i = 0; i < meshData.KeyframeTimes.Length - 1; i++)
    {
      if (time >= meshData.KeyframeTimes[i] && time <= meshData.KeyframeTimes[i + 1])
      {
        float t = (time - meshData.KeyframeTimes[i]) / (meshData.KeyframeTimes[i + 1] - meshData.KeyframeTimes[i]);
        for (int j = 0; j < _morphWeights.Length; j++)
        {
          _morphWeights[j] = meshData.KeyframeWeights[i, j] * (1.0f - t) + meshData.KeyframeWeights[i + 1, j] * t;
        }
        break;
      }
    }

    return _morphWeights;
  }

  private void UpdateProjection()
  {
    float aspectRatio = Size.X / (float)Size.Y;
    float fov = MathHelper.DegreesToRadians(45.0f);
    float cameraDistance = 5.0f * _zoom;
    float farPlane = Math.Max(cameraDistance * 2.0f, 100.0f);

    Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(fov, aspectRatio, 0.1f, farPlane);
    Matrix4 view = Matrix4.LookAt(new Vector3(0.0f, 0.0f, cameraDistance), Vector3.Zero, Vector3.UnitY);

    GL.UseProgram(_shaderProgram);
    GL.UniformMatrix4(_projectionLocation, false, ref projection);
    GL.UniformMatrix4(_viewLocation, false, ref view);
  }

  protected override void OnMouseWheel(MouseWheelEventArgs e)
  {
    base.OnMouseWheel(e);

    _zoom *= (e.OffsetY > 0) ? 1.1f : 0.9f;
    UpdateProjection();
  }

  protected override void OnMouseMove(MouseMoveEventArgs e)
  {
    base.OnMouseMove(e);

    var mouseState = MouseState;
    if (mouseState.IsButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left))
    {
      var delta = e.Position - _lastMousePosition;
      _rotationX += delta.Y * 0.5f;
      _rotationY += delta.X * 0.5f;
      UpdateProjection();
    }

    _lastMousePosition = e.Position;
  }

  protected override void OnResize(ResizeEventArgs e)
  {
    base.OnResize(e);

    GL.Viewport(0, 0, Size.X, Size.Y);
    UpdateProjection();
  }

  protected override void OnUnload()
  {
    base.OnUnload();

    foreach (int vbo in _vbos) GL.DeleteBuffer(vbo);
    foreach (int ebo in _ebos) GL.DeleteBuffer(ebo);
    foreach (int vao in _vaos) GL.DeleteVertexArray(vao);
    foreach (int textureId in _textureIds) GL.DeleteTexture(textureId);

    GL.DeleteProgram(_shaderProgram);
  }

  private int CompileShader(ShaderType type, string source)
  {
    int shader = GL.CreateShader(type);
    GL.ShaderSource(shader, source);
    GL.CompileShader(shader);

    GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
    if (success == 0)
    {
      string infoLog = GL.GetShaderInfoLog(shader);
      throw new Exception($"Shader compilation failed: {infoLog}");
    }

    return shader;
  }
}
