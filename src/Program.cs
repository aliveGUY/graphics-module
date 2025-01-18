class Program
{
  static void Main()
  {
    Object3D mesh = new();
    mesh.LoadFromGLTF("src/static/meshes/chair/gltf/chair.gltf");

    using var window = new MeshWindow(mesh);
    window.Run();
  }
}

