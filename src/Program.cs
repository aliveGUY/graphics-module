class Program
{
  static void Main()
  {
    Object3D mesh = new();
    mesh.LoadFromGLTF("src/static/meshes/Old_wooden_table_and_chair/gltf/Old_wooden_table_and_chair.gltf");

    using var window = new MeshWindow(mesh);
    window.Run();
  }
}

