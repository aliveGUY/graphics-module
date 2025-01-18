using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

class MeshWindow : GameWindow
{
  private readonly Object3D _mesh;
  private int _vao, _vbo, _ebo;
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

    _vao = GL.GenVertexArray();
    _vbo = GL.GenBuffer();
    _ebo = GL.GenBuffer();

    GL.BindVertexArray(_vao);

    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
    GL.BufferData(BufferTarget.ArrayBuffer, _mesh.Meshes[0].Vertices.Count * sizeof(float), _mesh.Meshes[0].Vertices.ToArray(), BufferUsageHint.StaticDraw);

    GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
    GL.BufferData(BufferTarget.ElementArrayBuffer, _mesh.Meshes[0].Indices.Count * sizeof(int), _mesh.Meshes[0].Indices.ToArray(), BufferUsageHint.StaticDraw);

    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
    GL.EnableVertexAttribArray(0);

    // Upload static animation data
    GL.UseProgram(_shaderProgram);

    GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "keyframeTimes"), _mesh.Meshes[0].KeyframeTimes.Length, _mesh.Meshes[0].KeyframeTimes);
    GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "translations"), _mesh.Meshes[0].TranslationLength, _mesh.Meshes[0].TranslationBuffer);
    GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "rotations"), _mesh.Meshes[0].RotationLength, _mesh.Meshes[0].RotationBuffer);
    GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "keyframeCount"), _mesh.Meshes[0].KeyframeTimes.Length);
  }

  protected override void OnUpdateFrame(FrameEventArgs args)
  {
    base.OnUpdateFrame(args);

    _deltaTime = (_deltaTime + (float)args.Time) % _mesh.Meshes[0].Duration;

    UpdateProjection();
  }

  protected override void OnRenderFrame(FrameEventArgs args)
  {
    base.OnRenderFrame(args);

    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

    GL.UseProgram(_shaderProgram);

    GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "animationTime"), _deltaTime);
    GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "animationDuration"), _mesh.Meshes[0].Duration);

    GL.BindVertexArray(_vao);
    GL.DrawElements(PrimitiveType.Triangles, _mesh.Meshes[0].Indices.Count, DrawElementsType.UnsignedInt, 0);

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

    // Use Object3D's model matrix
    var model = _mesh.Meshes[0].ModelMatrix *
                Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_rotationX)) *
                Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_rotationY));

    GL.UseProgram(_shaderProgram);
    GL.UniformMatrix4(_projectionLocation, false, ref projection);
    GL.UniformMatrix4(_viewLocation, false, ref view);
    GL.UniformMatrix4(_modelLocation, false, ref model);
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

    GL.DeleteBuffer(_vbo);
    GL.DeleteBuffer(_ebo);
    GL.DeleteVertexArray(_vao);
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

