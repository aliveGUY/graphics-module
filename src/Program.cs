class Program
{
  static void Main()
  {
    Object3D mesh = new();
    mesh.LoadFromGLTF("src/static/meshes/example_morph.gltf");

    using var window = new MeshWindow(mesh);
    window.Run();
  }
}

