using SharpGLTF.Schema2;
using OpenTK.Mathematics;

public class Object3D
{
  public List<float> Vertices { get; private set; } = [];
  public List<int> Indices { get; private set; } = [];
  public float Duration { get; private set; } = 0f;
  private Dictionary<string, List<(float Time, Vector3 Translation, Quaternion Rotation)>> _animationData = [];
  public Matrix4 ModelMatrix { get; private set; } = Matrix4.Identity;

  public float[] KeyframeTimes { get; private set; } = [];
  public Vector3[] Translations { get; private set; } = [];
  public Quaternion[] Rotations { get; private set; } = [];

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
        Vertices.AddRange(positionAccessor.AsVector3Array().SelectMany(v => new[] { v.X, v.Y, v.Z }));

      if (primitive.IndexAccessor != null)
        Indices.AddRange(primitive.IndexAccessor.AsIndicesArray().Select(i => (int)i));
    }
  }

  private void ExtractAnimationData(IReadOnlyList<Animation> animations)
  {
    if (animations == null || animations.Count == 0) return;

    Animation? firstAnimation = animations.FirstOrDefault();
    if (firstAnimation == null || firstAnimation.Channels.Count == 0) return;

    AnimationChannel? channel = firstAnimation.Channels.FirstOrDefault();
    if (channel == null || channel.TargetNode == null) return;

    string nodeName = channel.TargetNode.Name ?? "UnnamedNode";

    IAnimationSampler<System.Numerics.Vector3>? translationSampler = channel.GetTranslationSampler();
    IAnimationSampler<System.Numerics.Quaternion>? rotationSampler = channel.GetRotationSampler();

    if (translationSampler != null)
    {
      var translationKeyframes = translationSampler.GetLinearKeys();
      KeyframeTimes = translationKeyframes.Select(kf => kf.Key).ToArray();
      Translations = translationKeyframes.Select(kf => new Vector3(kf.Value.X, kf.Value.Y, kf.Value.Z)).ToArray();
    }

    if (rotationSampler != null)
    {
      var rotationKeyframes = rotationSampler.GetLinearKeys();
      if (KeyframeTimes.Length == 0)
      {
        KeyframeTimes = rotationKeyframes.Select(kf => kf.Key).ToArray();
      }

      Rotations = rotationKeyframes.Select(kf => new Quaternion(kf.Value.X, kf.Value.Y, kf.Value.Z, kf.Value.W)).ToArray();
    }

    Duration = KeyframeTimes.Length > 0 ? KeyframeTimes.Max() : 0f;
  }
}
