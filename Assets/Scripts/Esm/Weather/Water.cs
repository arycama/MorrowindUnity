using UnityEngine;
using UnityEngine.Rendering;

public class Water : Singleton<Water>
{
    [SerializeField, Tooltip("Number of vertices used to create the water grid")]
    private int resolution;

    private Projection projection = new();
	private float surfaceFps;
    private int surfaceFrameCount;
    private float worldAlpha;
    private Color32 underwaterColor;
    private float underwaterColorWeight;
    private float tileTextureDivisor;

	private Texture2D[] textures;
    private int lastTextureIndex;
    private double lastUpdateTime;
    private Matrix4x4 interpolation;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private float waterHeight;
    private bool hasWater;

    private void Start()
    {
        surfaceFrameCount = IniManager.GetInt("Water", "SurfaceFrameCount");
		surfaceFps = IniManager.GetInt("Water", "SurfaceFPS");
        worldAlpha = IniManager.GetFloat("Water", "World Alpha");
        underwaterColor = IniManager.GetColor("Water", "UnderwaterColor");
        underwaterColorWeight = IniManager.GetFloat("Water", "UnderwaterColorWeight");
        tileTextureDivisor = IniManager.GetFloat("Water", "TileTextureDivisor");

		var surfaceTexture = IniManager.GetString("Water", "SurfaceTexture");

		textures = new Texture2D[surfaceFrameCount];
        for (var i = 0; i < surfaceFrameCount; i++)
        {
            var path = $"textures/water/{surfaceTexture}{i:00}.dds";
            var texture = BsaFileReader.LoadTexture(path);
            textures[i] = texture as Texture2D;
        }

        CellManager.OnFinishedLoadingCells += SetupWater;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        vertices = new Vector3[resolution * resolution];
        uvs = new Vector2[resolution * resolution];
        var indices = new int[resolution * resolution * 6];

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                Vector2 uv = new Vector3(x / (resolution - 1.0f), y / (resolution - 1.0f));

                uvs[x + y * resolution] = uv;
                vertices[x + y * resolution] = new Vector3(uv.x, uv.y, 0.0f);
            }
        }

        int num = 0;
        for (int x = 0; x < resolution - 1; x++)
        {
            for (int y = 0; y < resolution - 1; y++)
            {
                indices[num++] = x + y * resolution;
                indices[num++] = x + (y + 1) * resolution;
                indices[num++] = (x + 1) + y * resolution;

                indices[num++] = x + (y + 1) * resolution;
                indices[num++] = (x + 1) + (y + 1) * resolution;
                indices[num++] = (x + 1) + y * resolution;
            }
        }

        var bigNumber = 1e6f;
        mesh = new Mesh();

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0);

        mesh.bounds = new Bounds(Vector3.zero, new Vector3(bigNumber, 20.0f, bigNumber));
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        RenderPipelineManager.beginCameraRendering += OnCameraPreCull;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnCameraPreCull;
        CellManager.OnFinishedLoadingCells -= SetupWater;
    }

    private void OnCameraPreCull(ScriptableRenderContext context, Camera camera)
    {
        projection.UpdateProjection(camera);
        interpolation = projection.Interpolation;

        // Update each vertex position
        for (var i = 0; i < vertices.Length; i++)
        {
            var uv = uvs[i];

            Vector4 p;
            p.x = Mathf.Lerp(Mathf.Lerp(interpolation[0, 0], interpolation[1, 0], uv.x), Mathf.Lerp(interpolation[3, 0], interpolation[2, 0], uv.x), uv.y);
            p.y = Mathf.Lerp(Mathf.Lerp(interpolation[0, 1], interpolation[1, 1], uv.x), Mathf.Lerp(interpolation[3, 1], interpolation[2, 1], uv.x), uv.y);
            p.z = Mathf.Lerp(Mathf.Lerp(interpolation[0, 2], interpolation[1, 2], uv.x), Mathf.Lerp(interpolation[3, 2], interpolation[2, 2], uv.x), uv.y);
            p.w = Mathf.Lerp(Mathf.Lerp(interpolation[0, 3], interpolation[1, 3], uv.x), Mathf.Lerp(interpolation[3, 3], interpolation[2, 3], uv.x), uv.y);

            vertices[i] = p / p.w;
        }

        mesh.SetVertices(vertices);

        var updateInterval = 1.0 / surfaceFps;
        var delta = (Time.time - lastUpdateTime) / updateInterval;

		if (Time.time > lastUpdateTime + updateInterval)
        {
			lastUpdateTime = Time.time;

			if (++lastTextureIndex == textures.Length)
                lastTextureIndex = 0;

            meshRenderer.sharedMaterial.mainTexture = textures[lastTextureIndex];
            meshRenderer.sharedMaterial.SetTexture("FadeTexture", textures[(lastTextureIndex + 1) % textures.Length]);
        }

        meshRenderer.sharedMaterial.SetFloat("Alpha", worldAlpha);
        meshRenderer.sharedMaterial.SetColor("Albedo", underwaterColor);
        meshRenderer.sharedMaterial.SetFloat("Scale", 1.0f / tileTextureDivisor);
        meshRenderer.sharedMaterial.SetFloat("Fade", (float)delta);

        meshRenderer.enabled = hasWater;
		Shader.SetGlobalFloat("WaterHeight", waterHeight);
        Shader.SetGlobalVector("UnderwaterColor", ((Color)underwaterColor).linear);
        Shader.SetGlobalFloat("UnderwaterColorWeight", underwaterColorWeight);
	}

    private void SetupWater(CellRecord cell)
    {
        if (cell.CellData.IsInterior)
        {
            hasWater = cell.CellData.HasWater;

			if (cell.CellData.HasWater)
            {
                waterHeight = cell.WaterHeight;
				projection.OceanLevel = cell.WaterHeight;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
			hasWater = true;
			gameObject.SetActive(true);
            waterHeight = 0;
        }
    }
}