using SharpGLTF.Schema2;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public struct MeshData
{
  // Mesh data
  public List<float> Vertices { get; set; }
  public List<int> Indices { get; set; }
  public List<float> TextureCoordinates { get; set; }
  public Matrix4 ModelMatrix { get; set; }

  // Animation data
  public float Duration { get; set; }
  public float[] KeyframeTimes { get; set; }
  public float[] TranslationBuffer { get; set; }
  public int TranslationLength { get; set; }
  public float[] RotationBuffer { get; set; }
  public int RotationLength { get; set; }

  // Texture info
  public List<TextureInfo> Textures { get; set; }

  public MeshData()
  {
    Vertices = new List<float>();
    Indices = new List<int>();
    TextureCoordinates = new List<float>();
    Duration = 0f;
    ModelMatrix = Matrix4.Identity;
    KeyframeTimes = Array.Empty<float>();
    TranslationBuffer = Array.Empty<float>();
    TranslationLength = 0;
    RotationBuffer = Array.Empty<float>();
    RotationLength = 0;
    Textures = new List<TextureInfo>();
  }
}

public class Object3D
{
  public List<MeshData> Meshes { get; private set; } = new List<MeshData>();

  public void LoadFromGLTF(string filePath)
  {
    var model = ModelRoot.Load(filePath);
    var scene = model.DefaultScene;
    var animations = model.LogicalAnimations;

    foreach (var node in scene.VisualChildren)
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

    foreach (var child in node.VisualChildren)
    {
      TraverseNode(child);
    }
  }

  private void ExtractMeshData(Node node)
  {
    foreach (var primitive in node.Mesh.Primitives)
    {
      var meshData = new MeshData();

      // Extract vertex positions
      var positionAccessor = primitive.GetVertexAccessor("POSITION");
      if (positionAccessor != null)
      {
        meshData.Vertices.AddRange(positionAccessor.AsVector3Array().SelectMany(v => new[] { v.X, v.Y, v.Z }));
      }

      // Extract texture coordinates
      var texCoordAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
      if (texCoordAccessor != null)
      {
        meshData.TextureCoordinates.AddRange(texCoordAccessor.AsVector2Array().SelectMany(v => new[] { v.X, v.Y }));
      }

      // Extract indices
      var indexAccessor = primitive.IndexAccessor;
      if (indexAccessor != null)
      {
        meshData.Indices.AddRange(indexAccessor.AsIndicesArray().Select(i => (int)i));
      }

      // Assign model matrix
      meshData.ModelMatrix = ConvertToMatrix4(node.LocalMatrix);

      // Extract textures
      var baseColorTextureInfo = primitive.Material?.FindChannel("BaseColor")?.Texture;
      if (baseColorTextureInfo != null)
      {
        var binaryData = LoadTextureData(baseColorTextureInfo.PrimaryImage);

        meshData.Textures.Add(new TextureInfo
        {
          TextureCoordinate = 0,
          ImageIndex = baseColorTextureInfo.PrimaryImage.LogicalIndex,
          BinaryData = binaryData
        });
      }

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

  private byte[] LoadTextureData(SharpGLTF.Schema2.Image gltfImage)
  {
    // Open the image content stream provided by SharpGLTF
    using var contentStream = gltfImage.Content.Open();
    using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(contentStream);

    using var outputStream = new MemoryStream();
    image.SaveAsPng(outputStream);
    return outputStream.ToArray();
  }

}

public class TextureInfo
{
  public int TextureCoordinate { get; set; }
  public int ImageIndex { get; set; }
  public required byte[] BinaryData { get; set; }
}
