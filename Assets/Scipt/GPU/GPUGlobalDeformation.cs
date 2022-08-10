using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Linq;
using Assets.script;
using UnityEngine.Rendering;
using System.Reflection;

public class GPUGlobalDeformation : MonoBehaviour
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


    [HideInInspector]
    string modelName;

    [Header("Obj Parameters")]
    public float nodeMass = 1.0f;
    public float dt = 0.005f; //
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    public float velocityDamping = -0.1f;
    public float thresholdError = 0.000001f;

    [Header("Simulation Parameter")]
    public ComputeShader computeShaderObject;
    public int speed = 1;

    [Header("Geometric Parameters")]
    public bool useInteriorSpring = false;

    [Header("Spring Parameters")]
    public float kStiffness = 100.0f;
    public float kDamping = 1.0f;
   

    [Header("Debugging Setting")]
    public bool renderVolumeText = false;

    public enum LabelPosition
    {
        Top_Left,
        Top_Center,
        Top_Right,   
    }
    [Header("Label Data")]
    public LabelPosition position;
    public int xOffset;
    public int fontSize;
    private Rect rectPos;
    private Color color;

    [HideInInspector]
    private int nodeCount;
    private int springCount;
    private int triCount; // size of triangle
    private int tetCount;

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

    //for render
    private ComputeBuffer vertsBuff = null;
    private ComputeBuffer triBuffer = null;
    //for compute shader
    private ComputeBuffer positionsBuffer = null;
    private ComputeBuffer velocitiesBuffer = null;
    private ComputeBuffer forcesAsIntBuffer = null;

    private ComputeBuffer facesBuffer = null;
    private ComputeBuffer jacobianAsUIntBuffer = null;
    private ComputeBuffer jacobianBuffer = null;
    private ComputeBuffer surfacevolumeUIntBuffer = null;
    private ComputeBuffer rhsBuffer = null;
    private ComputeBuffer tempRhsBuffer = null;
    private ComputeBuffer systemBuffer = null;
    //private ComputeBuffer lambdaBuffer = null;  // not use currently, might use for debugging

    private ComputeBuffer springsBuffer = null;
    private ComputeBuffer triangleBuffer = null;
    private ComputeBuffer triPtrBuffer = null;

    private int updatePosKernel;
    private int mass_springKernel;
    private int computenormalKernel;
    // for volume preservation
    private int jacobianKernel;
    private int computeVolumeKernel;
    private int calculateLambdaKernel;
    private int calculateForceKernel;

    [Header("Rendering")]
    public Shader renderingShader;
    public Color matColor;

    private Material material;
    private ComputeShader computeShader;

    struct vertData
    {
        public Vector3 pos;
        public Vector2 uvs;
        public Vector3 norms;
    };
    int[] triArray;
    vertData[] vDataArray;
    private static GameObject obj;
    float totalVolume;
    int frame = 0;

    void setPositionofLabel()
    {
        int w = Screen.width, h = Screen.height;
        switch (position)
        {
            case LabelPosition.Top_Left:
                {
                    rectPos = new Rect(0 + xOffset, 0, w, h * 2 / 100);
                    color = Color.white;
                }
                break;
            case LabelPosition.Top_Center:
                {
                    rectPos = new Rect((w / 2) + xOffset, 0, w, h * 2 / 100);
                    color = Color.white;
                }
                break;
            case LabelPosition.Top_Right:
                {
                    rectPos = new Rect((w) + xOffset, 0, w, h * 2 / 100);
                    color = Color.white;
                }
                break;
        }
        fontSize = h * 2 / 50;
    }

    void SelectModelName()
    {
        switch (model)
        {

            case MyModel.Tetrahedron: modelName = "test"; break;
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
    private void setupMeshData()
    {

        LoadModel.LoadData(modelName, obj, useInteriorSpring);


        Positions = LoadModel.positions.ToArray();
        faces = LoadModel.faces;
        if (useInteriorSpring)
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
    private void setupComputeBuffer()
    {
        positionsBuffer = new ComputeBuffer(nodeCount, sizeof(float) * 3);
        positionsBuffer.SetData(Positions);

        velocitiesBuffer = new ComputeBuffer(nodeCount, sizeof(float) * 3);
        velocitiesBuffer.SetData(Velocities);

        UInt3Struct[] forceUintArray = new UInt3Struct[nodeCount];
        forceUintArray.Initialize();

        forcesAsIntBuffer = new ComputeBuffer(nodeCount, sizeof(uint) * 3);
        forcesAsIntBuffer.SetData(forceUintArray);

        springsBuffer = new ComputeBuffer(springCount,
            sizeof(float) + (sizeof(int) * 2));

        Spring[] springArr = initSpring.ToArray();
        springsBuffer.SetData(springArr);

        List<Triangle> initTriangle = new List<Triangle>();
        List<int> initTrianglePtr = new List<int>();
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

        triangleBuffer = new ComputeBuffer(initTriangle.Count, (sizeof(int) * 3));
        triangleBuffer.SetData(initTriangle.ToArray());

        triPtrBuffer = new ComputeBuffer(initTrianglePtr.Count, sizeof(int));
        triPtrBuffer.SetData(initTrianglePtr.ToArray());

        //for surface volume preservation
        facesBuffer = new ComputeBuffer(faces.Count, sizeof(int) * 3);
        facesBuffer.SetData(faces);

        UInt3Struct[] uInt3Structs = new UInt3Struct[nodeCount];
        Vector3[] initJacobian = new Vector3[nodeCount];
        uint[] initUint= new uint[1];
        float[] initFloat= new float[1];
        UInt3Struct[] tempRHS = new UInt3Struct[1];
        //uint[] initSVolume = new uint[1];

        jacobianAsUIntBuffer = new ComputeBuffer(nodeCount, sizeof(uint) * 3);
        jacobianAsUIntBuffer.SetData(uInt3Structs);
        jacobianBuffer = new ComputeBuffer(nodeCount, sizeof(float) * 3);
        jacobianBuffer.SetData(initJacobian);

        surfacevolumeUIntBuffer = new ComputeBuffer(1, sizeof(uint));
        surfacevolumeUIntBuffer.SetData(initUint);
        rhsBuffer = new ComputeBuffer(1, sizeof(uint));
        rhsBuffer.SetData(initUint);
        tempRhsBuffer = new ComputeBuffer(1, sizeof(uint)*3);
        tempRhsBuffer.SetData(tempRHS);
        systemBuffer = new ComputeBuffer(1, sizeof(uint));
        systemBuffer.SetData(initUint);
        //not initial data
    }
    private float computeSurfaceVolume()
    {
        float vol = 0.0f;
        for (int i = 0; i < triCount; i++)
        {
            int i1 = triArray[i * 3 + 0];
            int i2 = triArray[i * 3 + 1];
            int i3 = triArray[i * 3 + 2];

            //pos1 = Positions[]
            Vector3 pos1 = Positions[i1];
            Vector3 pos2 = Positions[i2];
            Vector3 pos3 = Positions[i3];

            //float area = 0.5f * length(cross(pos2 - pos1, pos3 - pos1));
            //float3 tmp = area * (pos1 + pos2 + pos3);
            //float3 norm = normalize(cross(pos2 - pos1, pos3 - pos1));

            float area = 0.5f * (Vector3.Cross(pos2 - pos1, pos3 - pos1)).magnitude;
            Vector3 tmp = area * (pos1 + pos2 + pos3);
            Vector3 norm = Vector3.Cross(pos2 - pos1, pos3 - pos1).normalized;

            vol += Vector3.Dot(tmp, norm);

        }
        return vol / 9.0f;
    }
    private void setupComputeShader()
    {
        updatePosKernel = computeShader.FindKernel("updatePosKernel");
        mass_springKernel = computeShader.FindKernel("MSSKernel");
        computenormalKernel = computeShader.FindKernel("computenormalKernel");


        computeVolumeKernel = computeShader.FindKernel("computeVolumeKernel");
        jacobianKernel = computeShader.FindKernel("jacobianKernel");
        calculateLambdaKernel = computeShader.FindKernel("calculateLambdaKernel");
        calculateForceKernel = computeShader.FindKernel("calculateForceKernel");

        computeShader.SetInt("nodeCount", nodeCount);
        computeShader.SetInt("springCount", springCount);
        computeShader.SetInt("triCount", triCount);
        computeShader.SetInt("tetCount", tetCount);

        computeShader.SetFloat("dt", dt);
        float restVolume = computeSurfaceVolume();
        totalVolume = restVolume;
        //print(restVolume);
        computeShader.SetFloat("restVolume", restVolume);
        computeShader.SetFloat("nodeMass", nodeMass);

        computeShader.SetFloat("kS", kStiffness);
        computeShader.SetFloat("kD", kDamping);
        computeShader.SetFloat("thresholdError", thresholdError);

        computeShader.SetBuffer(updatePosKernel, "Positions", positionsBuffer);
        computeShader.SetBuffer(updatePosKernel, "Velocities", velocitiesBuffer);
        computeShader.SetBuffer(updatePosKernel, "ForcesAsInt", forcesAsIntBuffer);
        computeShader.SetBuffer(updatePosKernel, "vertsBuff", vertsBuff); //passing to rendering
        computeShader.SetBuffer(updatePosKernel, "SurfacevolumeUInt", surfacevolumeUIntBuffer);
        computeShader.SetBuffer(updatePosKernel, "JacobianVectorUInt", jacobianAsUIntBuffer);
        computeShader.SetBuffer(updatePosKernel, "Jacobian", jacobianBuffer);


        computeShader.SetBuffer(mass_springKernel, "Positions", positionsBuffer);
        computeShader.SetBuffer(mass_springKernel, "Velocities", velocitiesBuffer);
        computeShader.SetBuffer(mass_springKernel, "ForcesAsInt", forcesAsIntBuffer);
        computeShader.SetBuffer(mass_springKernel, "Springs", springsBuffer);


        computeShader.SetBuffer(computeVolumeKernel, "Positions", positionsBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "Jacobian", jacobianBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "JacobianVectorUInt", jacobianAsUIntBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "SurfacevolumeUInt", surfacevolumeUIntBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "Faces", facesBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "Rhs", rhsBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "temprhs", tempRhsBuffer);
        computeShader.SetBuffer(computeVolumeKernel, "System", systemBuffer);

        computeShader.SetBuffer(jacobianKernel, "Positions", positionsBuffer);
        computeShader.SetBuffer(jacobianKernel, "JacobianVectorUInt", jacobianAsUIntBuffer);
        computeShader.SetBuffer(jacobianKernel, "Jacobian", jacobianBuffer);

        computeShader.SetBuffer(jacobianKernel, "Triangles", triangleBuffer);
        computeShader.SetBuffer(jacobianKernel, "TrianglePtr", triPtrBuffer);

        computeShader.SetBuffer(calculateLambdaKernel, "Velocities", velocitiesBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "ForcesAsInt", forcesAsIntBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "JacobianVectorUInt", jacobianAsUIntBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "Jacobian", jacobianBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "SurfacevolumeUInt", surfacevolumeUIntBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "Rhs", rhsBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "temprhs", tempRhsBuffer);
        computeShader.SetBuffer(calculateLambdaKernel, "System", systemBuffer);


        computeShader.SetBuffer(calculateForceKernel, "ForcesAsInt", forcesAsIntBuffer);
        computeShader.SetBuffer(calculateForceKernel, "JacobianVectorUInt", jacobianAsUIntBuffer);
        computeShader.SetBuffer(calculateForceKernel, "Jacobian", jacobianBuffer);
        computeShader.SetBuffer(calculateForceKernel, "Rhs", rhsBuffer);
        computeShader.SetBuffer(calculateForceKernel, "System", systemBuffer);
        computeShader.SetBuffer(calculateForceKernel, "temprhs", tempRhsBuffer);

        computeShader.SetBuffer(computenormalKernel, "Positions", positionsBuffer);
        computeShader.SetBuffer(computenormalKernel, "Triangles", triangleBuffer);
        computeShader.SetBuffer(computenormalKernel, "TrianglePtr", triPtrBuffer);
        computeShader.SetBuffer(computenormalKernel, "vertsBuff", vertsBuff); //passing to rendering

    }

    void setup()
    {
        obj = gameObject;

        material = new Material(renderingShader); // new material for difference object
        material.color = matColor; //set color to material
        computeShader = Instantiate(computeShaderObject); // to instantiate the compute shader to be use with multiple object

        SelectModelName();
        setupMeshData();
        setupShader();
        setBuffData();
        setupComputeBuffer();
        setupComputeShader();
        setPositionofLabel();
    }

    void Start()
    {
        setup();
        print("nodes :: " + nodeCount);
        print("tris :: " + triCount);
        print("tet :: " + tetCount);
        print("springs :: " + springCount);

        //Application.targetFrameRate = 1000;
    }
    float[] volumeDataGPU = new float[1];
    void dispatchComputeShader()
    {
        for (int i = 0; i < speed; i++)
        {

            computeShader.Dispatch(mass_springKernel,
                (int)Mathf.Ceil(springCount / 1024.0f), 1, 1);
            computeShader.Dispatch(computeVolumeKernel,
                (int)Mathf.Ceil(triCount / 1024.0f), 1, 1);
            computeShader.Dispatch(jacobianKernel,
                (int)Mathf.Ceil(nodeCount / 1024.0f), 1, 1);
            computeShader.Dispatch(calculateLambdaKernel,
                (int)Mathf.Ceil(nodeCount / 1024.0f), 1, 1);
            computeShader.Dispatch(calculateForceKernel,
                (int)Mathf.Ceil(nodeCount / 1024.0f), 1, 1);
                    
            
            if ((i == speed-1)&& renderVolumeText == true)
            {
                surfacevolumeUIntBuffer.GetData(volumeDataGPU);
                //print(volumeDataGPU[0]);
            }
           

            computeShader.Dispatch(updatePosKernel,
                (int)Mathf.Ceil(nodeCount / 1024.0f), 1, 1);
        }
        computeShader.Dispatch(computenormalKernel, (int)Mathf.Ceil(nodeCount / 1024.0f), 1, 1);
    }



    

    private void OnGUI()
    {
        if (renderVolumeText)
        {
            int w = Screen.width, h = Screen.height;
            GUIStyle style = new GUIStyle();
            Rect rect = rectPos;
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = h * 2 / 50;
            style.normal.textColor = color;

            //get volume data;

            float currVolume = volumeDataGPU[0];
            //currVolume = computeSurfaceVolume();

            float vLost = (currVolume / totalVolume) * 100.0f;
            string text = string.Format("Volume: {0:0.00} %", vLost);
            GUI.Label(rect, text, style);
        }
    }

    void Update()
    {
        dispatchComputeShader();
        //setData
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
        material.SetPass(0);
        Graphics.DrawProcedural(material, bounds, MeshTopology.Triangles, triArray.Length,
            1, null, null, ShadowCastingMode.On, true, gameObject.layer);
        frame++;
    }

    void writeImageData(int f, int maxFrame, string imgPath)
    {
        if (f < maxFrame)
            ScreenCapture.CaptureScreenshot(imgPath + "frame" + f.ToString().PadLeft(3, '0') + ".png");
    }

    private void OnDestroy()
    {
        if (this.enabled)
        {
            vertsBuff.Dispose();
            triBuffer.Dispose();
            positionsBuffer.Dispose();
            velocitiesBuffer.Dispose();
            forcesAsIntBuffer.Dispose();
            facesBuffer.Dispose();
            jacobianAsUIntBuffer.Dispose();
            jacobianBuffer.Dispose();
            surfacevolumeUIntBuffer.Dispose();
            rhsBuffer.Dispose();
            tempRhsBuffer.Dispose();
            systemBuffer.Dispose();
            springsBuffer.Dispose();
            triangleBuffer.Dispose();
            triPtrBuffer.Dispose();
        }
    }

}
