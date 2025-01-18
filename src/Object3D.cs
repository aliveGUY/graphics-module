using SharpGLTF.Schema2;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

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
    Vertices = new List<float>();
    Indices = new List<int>();
    Duration = 0f;
    ModelMatrix = Matrix4.Identity;
    KeyframeTimes = Array.Empty<float>();
    TranslationBuffer = Array.Empty<float>();
    TranslationLength = 0;
    RotationBuffer = Array.Empty<float>();
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
      ExtractMeshData(node.Mesh);
    }

    foreach (Node child in node.VisualChildren)
    {
      TraverseNode(child);
    }
  }

  private void ExtractMeshData(Mesh mesh)
  {
    foreach (MeshPrimitive primitive in mesh.Primitives)
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

      Meshes.Add(meshData);
    }
  }

  private void ExtractAnimationData(IReadOnlyList<Animation> animations)
  {
    if (animations == null || animations.Count == 0) return;

    var channel = animations
        .SelectMany(animation => animation.Channels)
        .FirstOrDefault(ch => ch.GetTranslationSampler() != null || ch.GetRotationSampler() != null);

    if (channel?.TargetNode == null) return;

    var translationKeyframes = channel.GetTranslationSampler()?.GetLinearKeys();
    var rotationKeyframes = channel.GetRotationSampler()?.GetLinearKeys();

    for (int i = 0; i < Meshes.Count; i++)
    {
      var mesh = Meshes[i];

      mesh.KeyframeTimes ??= Array.Empty<float>();

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

      Meshes[i] = mesh;
    }
  }
}
