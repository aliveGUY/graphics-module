using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.IO;

public class MeshWindow : GameWindow
{
  private readonly Object3D _mesh;
  private List<int> _vaos = new();
  private List<int> _vbos = new();
  private List<int> _ebos = new();
  private List<int> _textureIds = new();
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
        Title = "GLTF Mesh Renderer"
      })
  {
    _mesh = mesh;
  }

  protected override void OnLoad()
  {
    base.OnLoad();

    GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
    GL.Enable(EnableCap.DepthTest);

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

    _projectionLocation = GL.GetUniformLocation(_shaderProgram, "projection");
    _viewLocation = GL.GetUniformLocation(_shaderProgram, "view");
    _modelLocation = GL.GetUniformLocation(_shaderProgram, "model");

    foreach (var meshData in _mesh.Meshes)
    {
      int vao = GL.GenVertexArray();
      int vbo = GL.GenBuffer();
      int ebo = GL.GenBuffer();

      GL.BindVertexArray(vao);

      GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
      GL.BufferData(BufferTarget.ArrayBuffer, meshData.Vertices.Count * sizeof(float), meshData.Vertices.ToArray(), BufferUsageHint.StaticDraw);

      GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
      GL.BufferData(BufferTarget.ElementArrayBuffer, meshData.Indices.Count * sizeof(int), meshData.Indices.ToArray(), BufferUsageHint.StaticDraw);

      GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
      GL.EnableVertexAttribArray(0);

      if (meshData.TextureCoordinates.Count > 0)
      {
        int texVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, texVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, meshData.TextureCoordinates.Count * sizeof(float), meshData.TextureCoordinates.ToArray(), BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        _vbos.Add(texVbo);
      }

      _vaos.Add(vao);
      _vbos.Add(vbo);
      _ebos.Add(ebo);

      foreach (var texture in meshData.Textures)
      {
        int textureId = LoadTexture(texture.BinaryData);
        _textureIds.Add(textureId);
      }
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

    // Convert image to an array of raw RGBA pixel data
    var pixelData = new byte[image.Width * image.Height * 4];
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

    float animationTime = _deltaTime;

    for (int i = 0; i < _mesh.Meshes.Count; i++)
    {
      var mesh = _mesh.Meshes[i];

      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "animationTime"), animationTime);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "animationDuration"), mesh.Duration);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "keyframeTimes"), mesh.KeyframeTimes.Length, mesh.KeyframeTimes);
      GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "translations"), mesh.TranslationLength, mesh.TranslationBuffer);
      GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "rotations"), mesh.RotationLength, mesh.RotationBuffer);
      GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "keyframeCount"), mesh.KeyframeTimes.Length);

      var model = mesh.ModelMatrix *
                  Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_rotationX)) *
                  Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_rotationY));

      GL.UniformMatrix4(_modelLocation, false, ref model);

      GL.BindVertexArray(_vaos[i]);

      if (mesh.Textures.Count > 0)
      {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _textureIds[i]);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "textureSampler"), 0);
      }

      GL.DrawElements(PrimitiveType.Triangles, mesh.Indices.Count, DrawElementsType.UnsignedInt, 0);
    }

    SwapBuffers();
  }

  private void UpdateProjection()
  {
    float aspectRatio = Size.X / (float)Size.Y;
    float fov = MathHelper.DegreesToRadians(30.0f);
    float cameraDistance = 5.0f * _zoom;
    float farPlane = Math.Max(cameraDistance * 2.0f, 100.0f);

    var projection = Matrix4.CreatePerspectiveFieldOfView(fov, aspectRatio, 0.1f, farPlane);
    var view = Matrix4.LookAt(new Vector3(0.0f, 0.0f, cameraDistance), Vector3.Zero, Vector3.UnitY);

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

    foreach (var vbo in _vbos) GL.DeleteBuffer(vbo);
    foreach (var ebo in _ebos) GL.DeleteBuffer(ebo);
    foreach (var vao in _vaos) GL.DeleteVertexArray(vao);
    foreach (var textureId in _textureIds) GL.DeleteTexture(textureId);

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
