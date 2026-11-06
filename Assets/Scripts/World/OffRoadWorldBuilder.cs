using System.Collections.Generic;
using UnityEngine;

public sealed class OffRoadWorldBuilder : MonoBehaviour
{
    public Vector3[] RoutePoints { get; private set; }
    public OffRoadRaceCheckpoint[] Checkpoints { get; private set; }

    private Material terrainMaterial;
    private Material roadMaterial;
    private Material grassMaterial;
    private Material cactusMaterial;
    private int mapIndex;
    private float mapSeed;

    private static readonly Color[] GroundColors =
    {
        new Color(0.48f, 0.39f, 0.25f),
        new Color(0.64f, 0.43f, 0.22f),
        new Color(0.24f, 0.37f, 0.25f)
    };

    public void BuildWorld()
    {
        mapIndex = 0;
        mapSeed = 17.4f;
        CreateRoute();
        CreateTerrain();
        CreateRoad();
        CreateRiver();
        ScatterBiomeProps();
        CreateCheckpoints();
        CreateHorizonLandmarks();
        SetMap(mapIndex);
    }

    public void SetMap(int index)
    {
        mapIndex = 0;
        if (terrainMaterial == null)
            return;

        terrainMaterial.color = GroundColors[0];
        roadMaterial.color = new Color(0.30f, 0.27f, 0.21f);
        RenderSettings.fogColor = new Color(0.58f, 0.66f, 0.70f);
    }

    private void CreateRoute()
    {
        RoutePoints = new[]
        {
            new Vector3(0f, 0f, 53f),
            new Vector3(33f, 0f, 49f),
            new Vector3(59f, 0f, 29f),
            new Vector3(66f, 0f, -2f),
            new Vector3(51f, 0f, -34f),
            new Vector3(20f, 0f, -49f),
            new Vector3(-16f, 0f, -50f),
            new Vector3(-48f, 0f, -34f),
            new Vector3(-65f, 0f, -7f),
            new Vector3(-57f, 0f, 25f),
            new Vector3(-31f, 0f, 47f)
        };
        for (int i = 0; i < RoutePoints.Length; i++)
            RoutePoints[i].y = TerrainHeight(RoutePoints[i].x, RoutePoints[i].z);
    }

    private void CreateTerrain()
    {
        const int resolution = 145;
        const float extent = 125f;
        Mesh mesh = new Mesh { name = "Procedural highland terrain" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] vertices = new Vector3[resolution * resolution];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        Vector2[] uv = new Vector2[vertices.Length];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = Mathf.Lerp(-extent, extent, x / (float)(resolution - 1));
                float worldZ = Mathf.Lerp(-extent, extent, z / (float)(resolution - 1));
                int index = z * resolution + x;
                vertices[index] = new Vector3(worldX, TerrainHeight(worldX, worldZ), worldZ);
                uv[index] = new Vector2(x / (float)(resolution - 1), z / (float)(resolution - 1)) * 18f;
            }
        }

        int t = 0;
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int i = z * resolution + x;
                triangles[t++] = i;
                triangles[t++] = i + resolution;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + resolution;
                triangles[t++] = i + resolution + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        terrainMaterial = MakeMaterial("Terrain // procedural earth", new Color(0.48f, 0.39f, 0.25f), 0.05f);
        GameObject terrain = new GameObject("Highland terrain // 250m");
        terrain.transform.SetParent(transform);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void CreateRoad()
    {
        List<Vector3> samples = new List<Vector3>();
        const int stepsPerSegment = 14;
        for (int i = 0; i < RoutePoints.Length; i++)
        {
            Vector3 p0 = RoutePoints[(i - 1 + RoutePoints.Length) % RoutePoints.Length];
            Vector3 p1 = RoutePoints[i];
            Vector3 p2 = RoutePoints[(i + 1) % RoutePoints.Length];
            Vector3 p3 = RoutePoints[(i + 2) % RoutePoints.Length];
            for (int step = 0; step < stepsPerSegment; step++)
            {
                float u = step / (float)stepsPerSegment;
                Vector3 point = 0.5f * ((2f * p1) + (-p0 + p2) * u
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
                point.y = TerrainHeight(point.x, point.z) + 0.24f;
                samples.Add(point);
            }
        }

        Vector3[] vertices = new Vector3[samples.Count * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[samples.Count * 6];
        float distance = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 forward = samples[(i + 1) % samples.Count] - samples[(i - 1 + samples.Count) % samples.Count];
            forward.y = 0f;
            Vector3 side = Vector3.Cross(forward.normalized, Vector3.up) * 5.2f;
            vertices[i * 2] = transform.InverseTransformPoint(samples[i] - side);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(samples[i] + side);
            if (i > 0)
                distance += Vector3.Distance(samples[i - 1], samples[i]);
            uv[i * 2] = new Vector2(0f, distance * 0.12f);
            uv[i * 2 + 1] = new Vector2(1f, distance * 0.12f);

            int next = (i + 1) % samples.Count;
            int index = i * 6;
            triangles[index] = i * 2;
            triangles[index + 1] = i * 2 + 1;
            triangles[index + 2] = next * 2;
            triangles[index + 3] = i * 2 + 1;
            triangles[index + 4] = next * 2 + 1;
            triangles[index + 5] = next * 2;
        }

        Mesh mesh = new Mesh { name = "Looping dirt rally road" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        roadMaterial = MakeMaterial("Road // compacted dirt", new Color(0.30f, 0.27f, 0.21f), 0f);
        GameObject road = new GameObject("Rally route // 10.4m wide");
        road.transform.SetParent(transform);
        road.AddComponent<MeshFilter>().sharedMesh = mesh;
        road.AddComponent<MeshRenderer>().sharedMaterial = roadMaterial;
        road.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void CreateRiver()
    {
        Material water = MakeMaterial("River // alpine blue", new Color(0.12f, 0.38f, 0.48f), 0.65f);
        SetFloatIfPresent(water, "_Smoothness", 0.86f);
        GameObject river = GameObject.CreatePrimitive(PrimitiveType.Cube);
        river.name = "Shallow river";
        river.transform.SetParent(transform);
        river.transform.position = new Vector3(-84f, TerrainHeight(-84f, 77f) + 0.08f, 77f);
        river.transform.rotation = Quaternion.Euler(0f, -24f, 0f);
        river.transform.localScale = new Vector3(82f, 0.14f, 6.5f);
        river.GetComponent<Renderer>().sharedMaterial = water;
        Destroy(river.GetComponent<Collider>());

        Material bank = MakeMaterial("River banks", new Color(0.31f, 0.29f, 0.22f), 0f);
        for (int i = 0; i < 10; i++)
        {
            float along = -1f + i * 0.22f;
            Vector3 p = new Vector3(-84f + along * 41f, TerrainHeight(-84f + along * 41f, 77f), 77f);
            CreateRock(p + new Vector3(0f, 0.3f, (i % 2 == 0 ? 1f : -1f) * 4f), bank, 1.3f);
        }
    }

    private void ScatterBiomeProps()
    {
        Random.State oldState = Random.state;
        Random.InitState(4102 + mapIndex * 97);
        Material trunk = MakeMaterial("Tree bark", new Color(0.26f, 0.19f, 0.12f), 0f);
        Material leaves = MakeMaterial("Pine needles", new Color(0.13f, 0.27f, 0.18f), 0f);
        Material rock = MakeMaterial("Granite", new Color(0.36f, 0.36f, 0.33f), 0f);
        Material sand = MakeMaterial("Desert sandstone", new Color(0.66f, 0.46f, 0.27f), 0f);
        grassMaterial = MakeMaterial("Grass blades", new Color(0.36f, 0.39f, 0.22f), 0f);
        cactusMaterial = MakeMaterial("Sage cactus", new Color(0.22f, 0.39f, 0.23f), 0f);

        int placed = 0;
        int attempts = 0;
        while (placed < 190 && attempts < 1200)
        {
            attempts++;
            float x = Random.Range(-110f, 110f);
            float z = Random.Range(-110f, 110f);
            if (IsNearRoad(x, z, 11f) || (x < -40f && z > 52f && z < 101f))
                continue;

            Vector3 basePosition = new Vector3(x, TerrainHeight(x, z), z);
            int biome = BiomeAt(x, z);
            if (biome == 0 || (biome == 1 && placed % 3 == 0))
                CreateTree(basePosition, trunk, leaves, Random.Range(0.75f, 1.4f));
            else if (biome == 1)
                CreateCactus(basePosition);
            else
                CreateRock(basePosition + Vector3.up * 0.15f, biome == 2 ? sand : rock, Random.Range(0.55f, 1.6f));
            placed++;
        }

        for (int i = 0; i < 260; i++)
        {
            float x = Random.Range(-115f, 115f);
            float z = Random.Range(-115f, 115f);
            if (!IsNearRoad(x, z, 6f) && Random.value < 0.58f)
                CreateGrassTuft(new Vector3(x, TerrainHeight(x, z), z));
        }
        Random.state = oldState;
    }

    private void CreateCheckpoints()
    {
        Checkpoints = new OffRoadRaceCheckpoint[RoutePoints.Length];
        int finishIndex = RoutePoints.Length - 1;
        Material gateMaterial = MakeMaterial("Checkpoint // signal amber", new Color(1f, 0.48f, 0.08f), 0.3f);
        Material inactive = MakeMaterial("Checkpoint // muted", new Color(0.28f, 0.31f, 0.29f), 0.15f);

        for (int i = 0; i < RoutePoints.Length; i++)
        {
            bool isFinish = i == finishIndex;
            Vector3 point = RoutePoints[i];
            point.y = TerrainHeight(point.x, point.z) + 0.3f;
            Vector3 next = RoutePoints[(i + 1) % RoutePoints.Length];
            Vector3 forward = next - RoutePoints[(i - 1 + RoutePoints.Length) % RoutePoints.Length];
            forward.y = 0f;
            Quaternion rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            GameObject root = new GameObject(isFinish
                ? "FINISH LINE"
                : i == 0 ? "START LINE" : "Checkpoint " + (i + 1).ToString("00"));
            root.transform.SetParent(transform);
            root.transform.SetPositionAndRotation(point, rotation);
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(13f, 5f, 3f);

            Material leftMaterial = isFinish ? MakeMaterial("Finish // white", Color.white, 0.1f) : gateMaterial;
            Material rightMaterial = isFinish
                ? MakeMaterial("Finish // black", new Color(0.025f, 0.03f, 0.035f), 0.1f)
                : gateMaterial;
            CreateGatePillar(root.transform, new Vector3(-5f, 2.5f, 0f), leftMaterial);
            CreateGatePillar(root.transform, new Vector3(5f, 2.5f, 0f), rightMaterial);
            if (isFinish)
            {
                for (int tile = 0; tile < 12; tile++)
                {
                    Material tileMaterial = tile % 2 == 0 ? leftMaterial : rightMaterial;
                    CreateGatePillar(root.transform, new Vector3(-5.5f + tile, 4.65f, 0f),
                        tileMaterial, new Vector3(1f, 0.35f, 0.5f));
                    CreateGatePillar(root.transform, new Vector3(-5.5f + tile, -0.02f, 0f),
                        tileMaterial, new Vector3(1f, 0.08f, 3f));
                }
                CreateFinishBanner(root.transform);
            }
            else
            {
                CreateGatePillar(root.transform, new Vector3(0f, 5f, 0f), inactive,
                    new Vector3(12f, 0.35f, 0.45f));
            }
            ParticleSystem[] fireworks = isFinish ? CreateFinishFireworks(root.transform) : null;
            Transform flag = isFinish ? root.transform.Find("Finish flag animation") : null;
            Checkpoints[i] = root.AddComponent<OffRoadRaceCheckpoint>();
            Checkpoints[i].Initialize(i, fireworks, flag);
            if (isFinish)
                root.AddComponent<FinishLine>();
        }
    }

    private static void CreateFinishBanner(Transform parent)
    {
        for (int side = 0; side < 2; side++)
        {
            GameObject bannerObject = new GameObject("FINISH banner");
            bannerObject.transform.SetParent(parent, false);
            bannerObject.transform.localPosition = new Vector3(0f, 6.15f, side == 0 ? 0f : -0.15f);
            bannerObject.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
            TextMesh banner = bannerObject.AddComponent<TextMesh>();
            banner.text = "FINISH";
            banner.anchor = TextAnchor.MiddleCenter;
            banner.alignment = TextAlignment.Center;
            banner.characterSize = 0.75f;
            banner.fontSize = 56;
            banner.fontStyle = FontStyle.Bold;
            banner.color = new Color(1f, 0.79f, 0.24f);
        }

        GameObject flag = new GameObject("Finish flag animation");
        flag.transform.SetParent(parent, false);
        flag.transform.localPosition = new Vector3(3.1f, 3.9f, -0.28f);
        Material white = MakeMaterial("Flag tile // white", Color.white, 0f);
        Material black = MakeMaterial("Flag tile // black", Color.black, 0f);
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                Material tile = (row + column) % 2 == 0 ? white : black;
                CreateGatePillar(flag.transform, new Vector3(column * 0.42f, -row * 0.42f, 0f),
                    tile, new Vector3(0.42f, 0.42f, 0.08f));
            }
        }
    }

    private static ParticleSystem[] CreateFinishFireworks(Transform parent)
    {
        ParticleSystem[] fireworks = new ParticleSystem[2];
        for (int i = 0; i < fireworks.Length; i++)
        {
            GameObject effectObject = new GameObject("Finish fireworks");
            effectObject.transform.SetParent(parent, false);
            effectObject.transform.localPosition = new Vector3(i == 0 ? -3.2f : 3.2f, 3.8f, 0f);
            ParticleSystem effect = effectObject.AddComponent<ParticleSystem>();
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.duration = 1.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.32f, 0.08f), new Color(1f, 0.83f, 0.2f));
            main.gravityModifier = 0.3f;
            main.maxParticles = 80;
            var emission = effect.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });
            var shape = effect.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fireworks[i] = effect;
        }

        return fireworks;
    }

    private void CreateHorizonLandmarks()
    {
        Material mountain = MakeMaterial("Distant mountain slate", new Color(0.35f, 0.39f, 0.39f), 0f);
        Random.State oldState = Random.state;
        Random.InitState(183);
        for (int i = 0; i < 28; i++)
        {
            float angle = i * Mathf.PI * 2f / 28f;
            float radius = Random.Range(118f, 145f);
            float height = Random.Range(16f, 46f);
            GameObject peak = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            peak.name = "Distant ridge";
            peak.transform.SetParent(transform);
            peak.transform.position = new Vector3(Mathf.Cos(angle) * radius, height * 0.43f - 3f, Mathf.Sin(angle) * radius);
            peak.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), Random.Range(-9f, 9f));
            peak.transform.localScale = new Vector3(Random.Range(15f, 30f), height, Random.Range(18f, 34f));
            peak.GetComponent<Renderer>().sharedMaterial = mountain;
            Destroy(peak.GetComponent<Collider>());
        }
        Random.state = oldState;
    }

    private void CreateTree(Vector3 position, Material bark, Material leaves, float scale)
    {
        GameObject tree = new GameObject("Pine tree");
        tree.transform.SetParent(transform);
        tree.transform.position = position;
        GameObject trunk = CreatePrimitive(tree.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.6f, 0f),
            new Vector3(0.24f, 1.6f, 0.24f), bark, true);
        trunk.transform.localScale *= scale;
        for (int i = 0; i < 3; i++)
        {
            float y = 2.0f + i * 0.75f;
            GameObject crown = CreatePrimitive(tree.transform, PrimitiveType.Sphere, "Canopy", new Vector3(0f, y, 0f),
                new Vector3(1.7f - i * 0.28f, 1.8f - i * 0.28f, 1.7f - i * 0.28f), leaves, false);
            crown.transform.localScale *= scale;
        }
    }

    private void CreateCactus(Vector3 position)
    {
        GameObject plant = new GameObject("Desert cactus");
        plant.transform.SetParent(transform);
        plant.transform.position = position;
        CreatePrimitive(plant.transform, PrimitiveType.Capsule, "Stem", Vector3.up * 1.05f,
            new Vector3(0.34f, 1.05f, 0.34f), cactusMaterial, false);
        if (Random.value > 0.45f)
            CreatePrimitive(plant.transform, PrimitiveType.Capsule, "Arm", new Vector3(0.38f, 1.15f, 0f),
                new Vector3(0.16f, 0.5f, 0.16f), cactusMaterial, false).transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
    }

    private void CreateRock(Vector3 position, Material material, float scale)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Field rock";
        rock.transform.SetParent(transform);
        rock.transform.position = position;
        rock.transform.rotation = Random.rotation;
        rock.transform.localScale = new Vector3(scale * 1.3f, scale * 0.7f, scale);
        rock.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void CreateGrassTuft(Vector3 position)
    {
        GameObject tuft = new GameObject("Grass tuft");
        tuft.transform.SetParent(transform);
        tuft.transform.position = position;
        for (int i = 0; i < 3; i++)
        {
            GameObject blade = CreatePrimitive(tuft.transform, PrimitiveType.Cube, "Blade",
                new Vector3(Random.Range(-0.2f, 0.2f), 0.28f, Random.Range(-0.2f, 0.2f)),
                new Vector3(0.08f, 0.55f, 0.08f), grassMaterial, false);
            blade.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), Random.Range(-22f, 22f));
        }
    }

    private static void CreateGatePillar(Transform parent, Vector3 localPosition, Material material,
        Vector3? size = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = "Checkpoint frame";
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size ?? new Vector3(0.45f, 5f, 0.45f);
        part.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(part.GetComponent<Collider>());
    }

    private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name,
        Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider)
            Destroy(part.GetComponent<Collider>());
        return part;
    }

    private float TerrainHeight(float x, float z)
    {
        float broad = (Mathf.PerlinNoise((x + mapSeed) * 0.017f, (z - mapSeed) * 0.017f) - 0.5f) * 11f;
        float detail = (Mathf.PerlinNoise((x + mapSeed * 2f) * 0.064f, (z + mapSeed) * 0.064f) - 0.5f) * 2.8f;
        float mountains = 0f;
        mountains += 22f * Mathf.Exp(-((x + 80f) * (x + 80f) + (z - 76f) * (z - 76f)) / 1550f);
        mountains += 18f * Mathf.Exp(-((x - 91f) * (x - 91f) + (z + 70f) * (z + 70f)) / 2100f);
        float central = Mathf.Clamp01((Mathf.Abs(x) + Mathf.Abs(z) - 36f) / 32f);
        return -0.5f + (broad + detail + mountains) * central;
    }

    private int BiomeAt(float x, float z)
    {
        if (x > 18f && z > 22f)
            return 0;
        if (x < -26f && z < 28f)
            return 1;
        if (x > 20f && z < 8f)
            return 2;
        return 3;
    }

    private bool IsNearRoad(float x, float z, float minimum)
    {
        Vector2 point = new Vector2(x, z);
        for (int i = 0; i < RoutePoints.Length; i++)
        {
            Vector2 a = new Vector2(RoutePoints[i].x, RoutePoints[i].z);
            Vector2 b = new Vector2(RoutePoints[(i + 1) % RoutePoints.Length].x,
                RoutePoints[(i + 1) % RoutePoints.Length].z);
            if (DistanceToSegment(point, a, b) < minimum)
                return true;
        }
        return false;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
        return Vector2.Distance(point, a + ab * t);
    }

    private static Material MakeMaterial(string name, Color color, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name, color = color };
        SetFloatIfPresent(material, "_Metallic", metallic);
        return material;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }
}
