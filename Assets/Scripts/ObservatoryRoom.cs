using UnityEngine;

[ExecuteAlways]
public class ObservatoryRoom : MonoBehaviour
{
    [Header("Circular Room")]
    public float roomRadius = 5f;
    public float wallHeight = 3.2f;
    public float wallThickness = 0.2f;

    [Header("Wall Segments")]
    [Range(8, 64)]
    public int wallSegments = 24;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;
    public Material ceilingMaterial;

    [Header("Generation")]
    public bool generateRoom = false;

    private Transform generatedRoom;

    void Update()
    {
        if (generateRoom)
        {
            generateRoom = false;
            GenerateRoom();
        }
    }

    [ContextMenu("Generate Circular Room")]
    public void GenerateRoom()
    {
        ClearRoom();

        generatedRoom = new GameObject("Generated Circular Room").transform;
        generatedRoom.SetParent(transform);

        CreateFloor();
        CreateCeiling();
        CreateCircularWalls();

        Debug.Log("Circular observatory generated.");
    }

    void CreateFloor()
    {
        CreateCylinder(
            "Floor",
            new Vector3(0, -0.1f, 0),
            new Vector3(roomRadius * 2f, 0.2f, roomRadius * 2f),
            floorMaterial
        );
    }

    void CreateCeiling()
    {
        CreateCylinder(
            "Ceiling",
            new Vector3(0, wallHeight, 0),
            new Vector3(roomRadius * 2f, 0.2f, roomRadius * 2f),
            ceilingMaterial
        );
    }

    void CreateCircularWalls()
    {
        float circumference = 2f * Mathf.PI * roomRadius;

        float segmentWidth = circumference / wallSegments;

        for (int i = 0; i < wallSegments; i++)
        {
            float angle = i * 360f / wallSegments;

            float radians = angle * Mathf.Deg2Rad;

            Vector3 position = new Vector3(
                Mathf.Sin(radians) * roomRadius,
                wallHeight / 2f,
                Mathf.Cos(radians) * roomRadius
            );

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);

            wall.name = "Wall_" + i;

            wall.transform.SetParent(generatedRoom);

            wall.transform.localPosition = position;

            // Slightly smaller than circumference section
            wall.transform.localScale = new Vector3(
                segmentWidth,
                wallHeight,
                wallThickness
            );

            // Rotate wall around the circle
            wall.transform.localRotation = Quaternion.Euler(
                0,
                angle,
                0
            );

            if (wallMaterial != null)
            {
                wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            }
        }
    }

    void ClearRoom()
    {
        Transform oldRoom = transform.Find("Generated Circular Room");

        if (oldRoom != null)
        {
            DestroyImmediate(oldRoom.gameObject);
        }
    }

    GameObject CreateCylinder(
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material
    )
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Cylinder
        );

        obj.name = objectName;

        obj.transform.SetParent(generatedRoom);

        obj.transform.localPosition = position;

        obj.transform.localScale = scale;

        if (material != null)
        {
            obj.GetComponent<Renderer>().sharedMaterial = material;
        }

        return obj;
    }
}