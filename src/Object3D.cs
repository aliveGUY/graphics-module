using SharpGLTF.Schema2;
using OpenTK.Mathematics;

public struct MeshData
{
  // Mesh
  public List<float> Vertices { get; set; }
  public List<int> Indices { get; set; }
  public Matrix4 ModelMatrix { get; set; }
  // Animation
  public float Duration { get; set; }
  public float[] KeyframeTimes { get; set; }
  public float[] TranslationBuffer { get; set; }
  public int TranslationLength { get; set; }
  public float[] RotationBuffer { get; set; }
  public int RotationLength { get; set; }

  public MeshData()
  {
    Vertices = [];
    Indices = [];
    Duration = 0f;
    ModelMatrix = Matrix4.Identity;
    KeyframeTimes = [];
    TranslationBuffer = [];
    TranslationLength = 0;
    RotationBuffer = [];
    RotationLength = 0;
  }
}

public class Object3D
{
  public List<MeshData> Meshes { get; private set; } = new List<MeshData>();

  /// <summary>
  /// Loads a GLTF file and extracts the buffer data from the default scene.
  /// </summary>
  public void LoadFromGLTF(string filePath)
  {
    ModelRoot model = ModelRoot.Load(filePath);
    Scene scene = model.DefaultScene;
    IReadOnlyList<Animation> animations = model.LogicalAnimations;

    foreach (Node node in scene.VisualChildren)
    {
      TraverseNode(node);
    }
    ExtractAnimationData(animations);
  }

  private void TraverseNode(Node node)
  {
    if (node == null) return;

    if (node.Mesh != null)
    {
      ExtractMeshData(node);
    }

    foreach (Node child in node.VisualChildren)
    {
      TraverseNode(child);
    }
  }

  private void ExtractMeshData(Node node)
  {
    foreach (MeshPrimitive primitive in node.Mesh.Primitives)
    {
      var meshData = new MeshData();

      if (primitive.VertexAccessors.TryGetValue("POSITION", out Accessor? positionAccessor))
      {
        meshData.Vertices.AddRange(positionAccessor.AsVector3Array().SelectMany(v => new[] { v.X, v.Y, v.Z }));
      }

      if (primitive.IndexAccessor != null)
      {
        meshData.Indices.AddRange(primitive.IndexAccessor.AsIndicesArray().Select(i => (int)i));
      }

      meshData.ModelMatrix = ConvertToMatrix4(node.LocalMatrix); ;

      Meshes.Add(meshData);
    }
  }

  private Matrix4 ConvertToMatrix4(System.Numerics.Matrix4x4 matrix)
  {
    return new Matrix4(
      matrix.M11, matrix.M12, matrix.M13, matrix.M14,
      matrix.M21, matrix.M22, matrix.M23, matrix.M24,
      matrix.M31, matrix.M32, matrix.M33, matrix.M34,
      matrix.M41, matrix.M42, matrix.M43, matrix.M44
    );
  }

  private void ExtractAnimationData(IReadOnlyList<Animation> animations)
  {
    if (animations == null || animations.Count == 0) return;

    foreach (var animation in animations)
    {
      foreach (var channel in animation.Channels)
      {
        var targetNode = channel.TargetNode;
        if (targetNode == null || targetNode.Mesh == null) continue;

        int meshIndex = Meshes.FindIndex(m => m.ModelMatrix == ConvertToMatrix4(targetNode.WorldMatrix));
        if (meshIndex == -1) continue;

        var mesh = Meshes[meshIndex];

        var translationKeyframes = channel.GetTranslationSampler()?.GetLinearKeys();
        var rotationKeyframes = channel.GetRotationSampler()?.GetLinearKeys();

        if (translationKeyframes?.Any() == true)
        {
          if (mesh.KeyframeTimes.Length == 0)
          {
            mesh.KeyframeTimes = translationKeyframes.Select(kf => kf.Key).ToArray();
          }

          mesh.TranslationBuffer = translationKeyframes
              .SelectMany(kf => new[] { kf.Value.X, kf.Value.Y, kf.Value.Z })
              .ToArray();

          mesh.TranslationLength = translationKeyframes.Count();
        }

        if (rotationKeyframes?.Any() == true)
        {
          if (mesh.KeyframeTimes.Length == 0)
          {
            mesh.KeyframeTimes = rotationKeyframes.Select(kf => kf.Key).ToArray();
          }

          mesh.RotationBuffer = rotationKeyframes
              .SelectMany(kf => new[] { kf.Value.X, kf.Value.Y, kf.Value.Z, kf.Value.W })
              .ToArray();

          mesh.RotationLength = rotationKeyframes.Count();
        }

        mesh.Duration = mesh.KeyframeTimes?.Max() ?? 0f;

        Meshes[meshIndex] = mesh;
      }
    }
  }

}
