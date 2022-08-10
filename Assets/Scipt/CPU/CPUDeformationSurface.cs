using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Linq;
using Assets.script;
using UnityEngine.Rendering;

public class CPUDeformationSurface : MonoBehaviour
{
    public enum MyModel
    {
        Tetrahedron,
        Cube33,
        Cube55,
        Cube99,
        Sphere,
        IcoSphere_low,
        IcoSphere,
        Torus,
        Bunny_Low_Poly,
        Bunny,
        Cow,
        Armadillo,
        Dragon_low,
        Dragon,
        AsainDragon,
        Dragon_refine

    };
    [Header("3D model")]
    public MyModel model;

    //todo here // make it selectable
    

    [Header("Obj Parameters")]
    public float nodeMass = 1.0f;
    public float dt = 0.005f; // have to devide by 20
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    public float velocityDamping = -0.1f;
    public int speed = 1;
    public float thresholdError = 0.00001f;

    [Header("Geometric Parameters")]
    public bool useInteriorSpring = false;

    [Header("Spring Parameters")]
    public float kStiffness = 100.0f;
    public float kDamping = 1.0f;

    [Header("Rendering")]
    public Shader renderingShader;
    public Color matColor;
    public bool renderVolume;

    [HideInInspector]
    private int nodeCount;
    private int springCount;
    private int triCount; // size of triangle
    private int tetCount;

    private Material material;

    //main  property
    //list position
    Vector3[] Positions;
    Vector3[] WorldPositions;
    Vector3[] Velocities;
    Vector3[] Forces;
    Vector3[] Normals;
    List<Spring> initSpring = new List<Spring>();
    List<Triangle> faces = new List<Triangle>();
    List<Tetrahedron> elements = new List<Tetrahedron>();
    private string modelName;

    List<int> initTrianglePtr = new List<int>();
    List<Triangle> initTriangle = new List<Triangle>();

    //for render
    ComputeBuffer vertsBuff = null;
    ComputeBuffer triBuffer = null;


    //for collision detection
    [Header("Object Collision")]
    public CPUDeformationSurface[] deformableObjList;
    public bool renderBoundingBox = false;

    struct vertData
    {
        public Vector3 pos;
        public Vector2 uvs;
        public Vector3 norms;
    };
    int[] triArray;
    vertData[] vDataArray;
    private static GameObject obj;

    private AABB boundingBox;

    public List<AABB> boundingBoxpair = new List<AABB>();


    void SelectModelName()
    {
        switch (model)
        {

            case MyModel.Tetrahedron:modelName = "test";break;
            case MyModel.Cube33: modelName = "33cube.1"; break;
            case MyModel.Cube55: modelName = "55cube.1"; break;
            case MyModel.Cube99: modelName = "99cube.1"; break;
            case MyModel.Sphere: modelName = "559sphere.1"; break;
            case MyModel.IcoSphere_low: modelName = "icosphere_low.1"; break;
            case MyModel.IcoSphere: modelName = "icosphere.1"; break;
            case MyModel.Torus: modelName = "torus.1"; break;
            case MyModel.Bunny_Low_Poly: modelName = "bunny_741"; break;
            case MyModel.Bunny: modelName = "bunny.1"; break;
            case MyModel.Cow: modelName = "cow.1"; break;
            case MyModel.Armadillo: modelName = "Armadillo.1"; break;
            case MyModel.Dragon_low: modelName = "dragon_vrip_res4.1"; break;
            case MyModel.Dragon: modelName = "dragon_vrip_res3.1"; break;
            case MyModel.AsainDragon: modelName = "asian_dragon.1"; break;
            case MyModel.Dragon_refine: modelName = "dragon_refine_01.1"; break;
        }
    }


    float totalVolume;
    private void setupMeshData()
    {
        LoadModel.LoadData(modelName, obj,useInteriorSpring);

        Positions = LoadModel.positions.ToArray();
        faces = LoadModel.faces;
        if(useInteriorSpring)
            initSpring = LoadModel.springs;
        else
            initSpring = LoadModel.springsSurface;
        //initSpring = LoadModel.springs;
        triArray = LoadModel.triangles.ToArray();
        elements = LoadModel.element;

        nodeCount = Positions.Length;
        springCount = initSpring.Count;
        triCount = faces.Count; //
        tetCount = elements.Count;

        WorldPositions = new Vector3[nodeCount];
        Velocities = new Vector3[nodeCount];
        Forces = new Vector3[nodeCount];
        WorldPositions.Initialize();
        Velocities.Initialize();
        Forces.Initialize();

        vDataArray = new vertData[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            vDataArray[i] = new vertData();
            vDataArray[i].pos = Positions[i];
            vDataArray[i].norms = Vector3.zero;
            vDataArray[i].uvs = Vector3.zero;
        }

        int triBuffStride = sizeof(int);
        triBuffer = new ComputeBuffer(triArray.Length,
            triBuffStride, ComputeBufferType.Default);


        int vertsBuffstride = 8 * sizeof(float);
        vertsBuff = new ComputeBuffer(vDataArray.Length,
            vertsBuffstride, ComputeBufferType.Default);

        initTrianglePtr.Add(0);
        for (int i = 0; i < nodeCount; i++)
        {
            foreach (Triangle tri in faces)
            {
                if (tri.v0 == i || tri.v1 == i || tri.v2 == i)
                    initTriangle.Add(tri);
            }
            initTrianglePtr.Add(initTriangle.Count);
        }

        LoadModel.ClearData();

    }
    private void setupShader()
    {
        material.SetBuffer(Shader.PropertyToID("vertsBuff"), vertsBuff);
        material.SetBuffer(Shader.PropertyToID("triBuff"), triBuffer);
    }
    private void setBuffData()
    {
        vertsBuff.SetData(vDataArray);
        triBuffer.SetData(triArray);

        Vector3 translation = transform.position;
        Vector3 scale = this.transform.localScale;
        Quaternion rotationeuler = transform.rotation;
        Matrix4x4 trs = Matrix4x4.TRS(translation, rotationeuler, scale);
        material.SetMatrix("TRSMatrix", trs);
        material.SetMatrix("invTRSMatrix", trs.inverse);
    }

    private void initAABB()
    {
        Vector3 min = Positions[0];
        Vector3 max = Positions[0];
        for (int i = 0; i < nodeCount; i++)
        {

            max.x = Mathf.Max(max.x, Positions[i].x);
            max.y = Mathf.Max(max.y, Positions[i].y);
            max.z = Mathf.Max(max.z, Positions[i].z);

            min.x = Mathf.Min(min.x, Positions[i].x);
            min.y = Mathf.Min(min.y, Positions[i].y);
            min.z = Mathf.Min(min.z, Positions[i].z);
            
        }

        boundingBox.Max = max;
        boundingBox.Min = min;
        boundingBox.Center = (min+max)/2;


        //print(min);
        //print(max);
        //print((min + max) / 2);


    }

    private void updateAABB()
    {
        Vector3 min = Positions[0];
        Vector3 max = Positions[0];
        for (int i = 0; i < nodeCount; i++)
        {

            max.x = Mathf.Max(max.x, Positions[i].x);
            max.y = Mathf.Max(max.y, Positions[i].y);
            max.z = Mathf.Max(max.z, Positions[i].z);

            min.x = Mathf.Min(min.x, Positions[i].x);
            min.y = Mathf.Min(min.y, Positions[i].y);
            min.z = Mathf.Min(min.z, Positions[i].z);

        }

        boundingBox.Max = max;
        boundingBox.Min = min;
        boundingBox.Center = (min + max) / 2;
    }

    

    void setup()
    {
        obj = gameObject;

        material = new Material(renderingShader); // new material for difference object
        material.color = matColor; //set color to material

        SelectModelName();
        setupMeshData();
        setupShader();
        setBuffData();
        initAABB();

        //broad phrase (assign AABB pair)
        foreach (CPUDeformationSurface obj in deformableObjList)
        {
            boundingBoxpair.Add(obj.boundingBox);
        }


        print("nodes :: " + nodeCount);
        print("tris :: " + triCount);
        print("tet :: " + tetCount);
        print("springs :: " + springCount);

    }

    void Start()
    {
        setup();

        //compute volume based on divergence theorism
        totalVolume = computeSurfaceVolume();
    }

    void computeSpringForce()
    {
        for (int i = 0; i < springCount; i++)
        {
            int i1 = initSpring[i].i1;
            int i2 = initSpring[i].i2;
            float rL = initSpring[i].RestLength;

            Vector3 p1 = Positions[i1];
            Vector3 p2 = Positions[i2];
            Vector3 v1 = Velocities[i1];
            Vector3 v2 = Velocities[i2];

            Vector3 forceDirection = p2 - p1;
            Vector3 velocityDirection = v2 - v2;

            float leng = Vector3.Distance(p2, p1);
            float springForce = ((leng - rL) * kStiffness);
            float damp = (Vector3.Dot(velocityDirection, forceDirection) / leng) * kDamping;
            Vector3 SpringForce = (springForce + damp) * forceDirection / leng;

            Forces[i1] += SpringForce;
            Forces[i2] -= SpringForce;

        }
    }
    
    void globalVolumePreservation()
    {
        float system = 0.0f;
        float rhs = 0.0f;
        float lambda = 0.0f;

        float[] uStar = new float[nodeCount];
        float[] vStar = new float[nodeCount];
        float[] wStar = new float[nodeCount];
        Vector3[] jacobianVector = new Vector3[nodeCount];
        uStar.Initialize();
        vStar.Initialize();
        wStar.Initialize();
        jacobianVector.Initialize();

        // Calculate temporary velocity using current status
        for (int i = 0; i < nodeCount; i++)
        {
            uStar[i] = (Velocities[i].x / dt) + gravity.x + Forces[i].x;
            vStar[i] = (Velocities[i].y / dt) + gravity.y + Forces[i].y;
            wStar[i] = (Velocities[i].z / dt) + gravity.z + Forces[i].z;
        }
        // Calculate Jacobian vector 
        for (int i = 0; i < triCount; i++)
        {
            int i1 = triArray[i * 3 + 0];
            int i2 = triArray[i * 3 + 1];
            int i3 = triArray[i * 3 + 2];

            //pos1 = Positions[]
            Vector3 pos1 = Positions[i1];
            Vector3 pos2 = Positions[i2];
            Vector3 pos3 = Positions[i3];

            jacobianVector[i1].x += 0.5f * (pos3.y * pos2.z - pos2.y * pos3.z);
            jacobianVector[i1].y += 0.5f * (-pos3.x * pos2.z + pos2.x * pos3.z);
            jacobianVector[i1].z += 0.5f * (pos3.x * pos2.y - pos2.x * pos3.y);
            jacobianVector[i2].x += 0.5f * (-pos3.y * pos1.z + pos1.y * pos3.z);
            jacobianVector[i2].y += 0.5f * (pos3.x * pos1.z - pos1.x * pos3.z);
            jacobianVector[i2].z += 0.5f * (-pos3.x * pos1.y + pos1.x * pos3.y);
            jacobianVector[i3].x += 0.5f * (pos2.y * pos1.z - pos1.y * pos2.z);
            jacobianVector[i3].y += 0.5f * (-pos2.x * pos1.z + pos1.x * pos2.z);
            jacobianVector[i3].z += 0.5f * (pos2.x * pos1.y - pos1.x * pos2.y);
        }

        //for (int i = 0; i < nodeCount; i++)
        //{
        //    int s = initTrianglePtr[i];
        //    int e = initTrianglePtr[i+1];

        //    for (int j = s; j < e; j++)
        //    {
        //        Triangle t = initTriangle[j];

        //        Vector3 pos1 = Positions[t.v0];
        //        Vector3 pos2 = Positions[t.v1];
        //        Vector3 pos3 = Positions[t.v2];

        //        if (i == t.v0) {
        //            jacobianVector[i].x += 0.5f * (pos3.y * pos2.z - pos2.y * pos3.z);
        //            jacobianVector[i].y += 0.5f * (-pos3.x * pos2.z + pos2.x * pos3.z);
        //            jacobianVector[i].z += 0.5f * (pos3.x * pos2.y - pos2.x * pos3.y);
        //        }
        //        else if (i == t.v1) {
        //            jacobianVector[i].x += 0.5f * (-pos3.y * pos1.z + pos1.y * pos3.z);
        //            jacobianVector[i].y += 0.5f * (pos3.x * pos1.z - pos1.x * pos3.z);
        //            jacobianVector[i].z += 0.5f * (-pos3.x * pos1.y + pos1.x * pos3.y);
        //        }
        //        else if (i == t.v2) {
        //            jacobianVector[i].x += 0.5f * (pos2.y * pos1.z - pos1.y * pos2.z);
        //            jacobianVector[i].y += 0.5f * (-pos2.x * pos1.z + pos1.x * pos2.z);
        //            jacobianVector[i].z += 0.5f * (pos2.x * pos1.y - pos1.x * pos2.y);
        //        }
        //    }
        //}

        // Build system to solve for Lagrange Multipliers	
        // Create system: sys = phiq * phiq'
        system = 0.0f;
        for (int i = 0; i < nodeCount; i++)
        {
            system += (jacobianVector[i].x * jacobianVector[i].x)
                + (jacobianVector[i].y * jacobianVector[i].y)
                + (jacobianVector[i].z * jacobianVector[i].z);
        }

        // Calculate current error 
        //print(system);
        //print(computeSurfaceVolume());
        float phi = (totalVolume- computeSurfaceVolume());

        rhs = phi / (dt * dt);
        float tempX = 0.0f;
        float tempY = 0.0f;
        float tempZ = 0.0f;
        //Vector3 tmpRhs = Vector3.zero;
        for (int i = 0; i < nodeCount; i++)
        {
            tempX += jacobianVector[i].x * uStar[i];
            tempY += jacobianVector[i].y * vStar[i];
            tempZ += jacobianVector[i].z * wStar[i];

        }
        //print(tempX + tempY + tempZ);
        rhs = rhs + tempX + tempY + tempZ;
        //print(rhs);
        //rhs = rhs + tmpRhs.x + tmpRhs.x + tmpRhs.x;
        lambda = rhs / system;


        for (int i = 0; i < nodeCount; i++)
        {
            Forces[i] += -jacobianVector[i] * lambda;
        }
    }
    void UpdateNodes()
    {
        //Euler method
        for (int i = 0; i < nodeCount; i++)
        {
            Vector3 pos = Positions[i];
            Vector3 vel = Velocities[i];
            Vector3 force = gravity + (Forces[i]);
            force += velocityDamping * vel; // reduce the velocity

            vel = vel + force * dt;
            pos = pos + vel * dt;

            Positions[i] = pos;
            Velocities[i] = vel;
            Forces[i] = Vector3.zero;

            if (Positions[i].y < 0.0f)  // TODO : fetch the position of floor object
            {
                Positions[i].y = 0.0f;
                Velocities[i] *= -0.1f;
            }
            vDataArray[i].pos = Positions[i];
        }
      
    }
    void computeVertexNormal()
    {
        for (int i = 0; i < triCount; i++)
        {
            Vector3 v1 = Positions[triArray[i * 3 + 0]];
            Vector3 v2 = Positions[triArray[i * 3 + 1]];
            Vector3 v3 = Positions[triArray[i * 3 + 2]];

            Vector3 N = (Vector3.Cross(v2 - v1, v3 - v1));

            vDataArray[triArray[i * 3 + 0]].norms += N;
            vDataArray[triArray[i * 3 + 1]].norms += N;
            vDataArray[triArray[i * 3 + 2]].norms += N;
        }
        for (int i = 0; i < nodeCount; i++)
        {
            vDataArray[i].norms = vDataArray[i].norms.normalized;
        }
    }
    int frame = 0;

    private void AABBCollisionDetectionResponse()
    {
        foreach (CPUDeformationSurface obj in deformableObjList)
        {
            if (IsCollided(boundingBox, obj.boundingBox))
            {
                //response
                for (int i = 0; i < nodeCount; i++)
                {
                    Velocities[i] *= -1.0f;
                }
            }
        }
    }
    void Update()
    {
        for (int i = 0; i < speed; i++)
        {
            computeSpringForce();
            globalVolumePreservation();
            UpdateNodes();

            updateAABB();
            AABBCollisionDetectionResponse();
        }

        frame++;
        //print("totale volume :"+totalVolume);
        computeVertexNormal();
        vertsBuff.SetData(vDataArray);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
        material.SetPass(0);
        Graphics.DrawProcedural(material,bounds, MeshTopology.Triangles,triArray.Length,
            1,null,null, ShadowCastingMode.On,true,gameObject.layer);
        if (Input.GetKey(KeyCode.Escape))
            UnityEditor.EditorApplication.isPlaying = false;
    }

    private float calculateTriArea(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float area = 0.0f;
        float term1, term2, term3;

        term1 = (p2.y - p1.y) * (p3.z - p1.z) - (p2.z - p1.z) * (p3.y - p1.y);
        term2 = (p2.x - p1.x) * (p3.z - p1.z) - (p2.z - p1.z) * (p3.x - p1.x);
        term3 = (p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x);

        area = 0.5f * Mathf.Sqrt(term1 * term1 + term2 * term2 + term3 * term3);
        return area;
    }
    private float computeSurfaceVolume()
    {
        Vector3[] coefficientVector = new Vector3[nodeCount];
        coefficientVector.Initialize();
        float area = 0.0f;
        Vector3 pos1, pos2, pos3;
        float currentVolume = 0.0f;

        for (int i = 0; i < triCount; i++)
        {
            int i1 = triArray[i * 3 + 0];
            int i2 = triArray[i * 3 + 1];
            int i3 = triArray[i * 3 + 2];

            //pos1 = Positions[]
            pos1 = Positions[i1];
            pos2 = Positions[i2];
            pos3 = Positions[i3];

            area = calculateTriArea(pos1,pos2,pos3);

            Vector3 norm = Vector3.Cross(pos2 - pos1, pos3 - pos1);
            norm = norm.normalized;

            coefficientVector[i1] += (norm * area / 3.0f);
            coefficientVector[i2] += (norm * area / 3.0f);
            coefficientVector[i3] += (norm * area / 3.0f);
        }

        for (int i = 0; i < nodeCount; i++)
        {
            //currentVolume += (coefficientVector[i].x * Positions[i].x)
            //    + (coefficientVector[i].y * Positions[i].y) 
            //    + (coefficientVector[i].z * Positions[i].z);

            currentVolume += (Vector3.Dot(coefficientVector[i],Positions[i]));
        }

        return currentVolume/3.0f;
    }

    private void OnGUI()
    {
        if (renderVolume)
        {
            int w = Screen.width, h = Screen.height;
            GUIStyle style = new GUIStyle();
            Rect rect = new Rect(0, 0, w, h * 2 / 100);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = h * 2 / 50;
            style.normal.textColor = new Color(1, 1, 1, 1.0f);

            float currVolume = 0;
            currVolume = computeSurfaceVolume();

            float vLost = (currVolume / totalVolume) * 100.0f;
            string text = string.Format("Volume: {0:0.00} %", vLost);
            GUI.Label(rect, text, style);
        }
    }

    public bool IsCollided(AABB boundingBoxA,AABB boundingBoxB)
    {
        return (boundingBoxA.Max.x > boundingBoxB.Min.x &&
            boundingBoxA.Min.x < boundingBoxB.Max.x &&
            boundingBoxA.Max.y > boundingBoxB.Min.y &&
            boundingBoxA.Min.y < boundingBoxB.Max.y &&
            boundingBoxA.Max.z > boundingBoxB.Min.z &&
            boundingBoxA.Min.z < boundingBoxB.Max.z);
    }

    private void OnDrawGizmos()
    {

        if (renderBoundingBox)
        {
            Gizmos.color = Color.yellow;
            foreach (CPUDeformationSurface obj in deformableObjList)
            {
                if (IsCollided(boundingBox, obj.boundingBox))
                {
                    Gizmos.color = Color.red;
                    print("collide");
                }
                        
            }

            Vector3 size = new Vector3(
                Mathf.Abs(boundingBox.Max.x - boundingBox.Min.x),
                Mathf.Abs(boundingBox.Max.y - boundingBox.Min.y),
                Mathf.Abs(boundingBox.Max.z - boundingBox.Min.z)
                );
            Gizmos.DrawWireCube(boundingBox.Center, size);
        }

    }


    private void OnDestroy()
    {
        if (this.enabled)
        {
            vertsBuff.Dispose();
            triBuffer.Dispose();
        }
    }
}
