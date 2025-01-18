using SharpGLTF.Schema2;
using OpenTK.Mathematics;

public struct MeshData
{
  public List<float> Vertices { get; set; }
  public List<int> Indices { get; set; }
  public float Duration { get; set; }
  public Matrix4 ModelMatrix { get; set; }
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
  public MeshData Mesh = new();

  /// <summary>
  /// Loads a GLTF file and extracts the buffer data from the default scene.
  /// </summary>
  public void LoadFromGLTF(string filePath)
  {
    ModelRoot model = ModelRoot.Load(filePath);
    Scene scene = model.DefaultScene;
    IReadOnlyList<Animation> animations = model.LogicalAnimations;

    ExtractAnimationData(animations);
    TraverseNode(scene.VisualChildren.First());
  }

  private void TraverseNode(Node node)
  {
    if (node.Mesh != null)
      ExtractMeshData(node.Mesh);

    foreach (Node child in node.VisualChildren)
      TraverseNode(child);
  }

  private void ExtractMeshData(Mesh mesh)
  {
    foreach (MeshPrimitive primitive in mesh.Primitives)
    {
      if (primitive.VertexAccessors.TryGetValue("POSITION", out Accessor? positionAccessor))
        Mesh.Vertices.AddRange(positionAccessor.AsVector3Array().SelectMany(v => new[] { v.X, v.Y, v.Z }));

      if (primitive.IndexAccessor != null)
        Mesh.Indices.AddRange(primitive.IndexAccessor.AsIndicesArray().Select(i => (int)i));
    }
  }

  private void ExtractAnimationData(IReadOnlyList<Animation> animations)
  {
    if (animations == null || animations.Count == 0) return;

    var channel = animations
        .SelectMany(animation => animation.Channels)
        .FirstOrDefault(ch => ch.GetTranslationSampler() != null || ch.GetRotationSampler() != null);

    if (channel == null || channel.TargetNode == null) return;

    var translationSampler = channel.GetTranslationSampler();
    if (translationSampler != null)
    {
      var translationKeyframes = translationSampler.GetLinearKeys();

      if (Mesh.KeyframeTimes == null || Mesh.KeyframeTimes.Length == 0)
      {
        Mesh.KeyframeTimes = translationKeyframes.Select(kf => kf.Key).ToArray();
      }

      Mesh.TranslationBuffer = translationKeyframes
          .SelectMany(kf => new[] { kf.Value.X, kf.Value.Y, kf.Value.Z })
          .ToArray();

      Mesh.TranslationLength = translationKeyframes.Count();
    }

    var rotationSampler = channel.GetRotationSampler();
    if (rotationSampler != null)
    {
      var rotationKeyframes = rotationSampler.GetLinearKeys();

      if (Mesh.KeyframeTimes == null || Mesh.KeyframeTimes.Length == 0)
      {
        Mesh.KeyframeTimes = rotationKeyframes.Select(kf => kf.Key).ToArray();
      }

      Mesh.RotationBuffer = rotationKeyframes
          .SelectMany(kf => new[] { kf.Value.X, kf.Value.Y, kf.Value.Z, kf.Value.W })
          .ToArray();

      Mesh.RotationLength = rotationKeyframes.Count();
    }

    Mesh.Duration = Mesh.KeyframeTimes?.Max() ?? 0f;
  }
}
