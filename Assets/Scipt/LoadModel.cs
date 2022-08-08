using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Linq;
using Assets.script;



public class LoadModel : MonoBehaviour
{
    private static GameObject obj;
    
    public static List<Vector3> positions = new List<Vector3>();
    public static List<Triangle> faces = new List<Triangle>(); //for compute
    public static List<Tetrahedron> element = new List<Tetrahedron>();
    public static List<Spring> springs = new List<Spring>();
    public static List<Spring> springsSurface = new List<Spring>();
    public static List<int> triangles = new List<int>();//for render
    //public static bool useInteriorSpring = false;

    //for transfor to center
    static int nodeCount;
    static int triCount;
    static int tetCount;
    static int springCount;
    static int springSurfaceCount;
    static string modelPath = "/TetGen_Model/Modified model/";
    static string fileName = "559sphere.1";
    //static string fileName = "33cube.1";


    private static float computeTetraVolume(Vector3 i1, Vector3 i2, Vector3 i3, Vector3 i4)
    {
        float volume = 0.0f;

        volume = 1.0f / 6.0f
            * (i3.x * i2.y * i1.z - i4.x * i2.y * i1.z - i2.x * i3.y * i1.z
            + i4.x * i3.y * i1.z + i2.x * i4.y * i1.z - i3.x * i4.y * i1.z
            - i3.x * i1.y * i2.z + i4.x * i1.y * i2.z + i1.x * i3.y * i2.z
            - i4.x * i3.y * i2.z - i1.x * i4.y * i2.z + i3.x * i4.y * i2.z
            + i2.x * i1.y * i3.z - i4.x * i1.y * i3.z - i1.x * i2.y * i3.z
            + i4.x * i2.y * i3.z + i1.x * i4.y * i3.z - i2.x * i4.y * i3.z
            - i2.x * i1.y * i4.z + i3.x * i1.y * i4.z + i1.x * i2.y * i4.z
            - i3.x * i2.y * i4.z - i1.x * i3.y * i4.z + i2.x * i3.y * i4.z);

        return volume;
    }

    public LoadModel(string filename)
    {
        fileName = filename;
    }
    public LoadModel(string filename,GameObject gameobj)
    {
        fileName = filename;
        obj = gameobj;
    }

    public static void LoadData(string filename, GameObject gameobj,bool useInteriorSpring)
    {
        fileName = filename;
        obj = gameobj;
        print("start load data !");
        loadNodesPosition();
        loadFaces();
        loadTetrahedron();
        if (useInteriorSpring)
            loadInteriorSpring();
        else
            loadSurfaceSpring();

        //tet2spring();
        //tri2spring();

        //print(nodeCount);
        //print("tri :: "+triCount);
        //print(tetCount);
        //print(springCount);
        //print(springSurfaceCount);

    }

  

    private static void loadNodesPosition() {
        string Nodepath = Application.dataPath + modelPath + fileName + ".node";
        StreamReader reader1 = new StreamReader(Nodepath);
        string line;
       
        //Matrix4x4 sMatrix = Matrix4x4.Scale(obj.transform.localScale);
        //Matrix4x4 tMatrix = Matrix4x4.Translate(obj.transform.position);
        //Quaternion rotation = Quaternion.Euler(obj.transform.localEulerAngles.x,
        //   obj.transform.localEulerAngles.y, obj.transform.localEulerAngles.z);
        //Matrix4x4 rMatrix = Matrix4x4.Rotate(rotation);

        Matrix4x4 m = Matrix4x4.TRS(obj.transform.position, obj.transform.rotation, obj.transform.localScale);


        //print(rMatrix);

        using (reader1)
        {
            line = reader1.ReadLine();
            do
            {
                line = reader1.ReadLine(); // first line
                if (line != null)
                {
                    string[] tmpPosPerRow = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmpPosPerRow.Length > 0 && tmpPosPerRow.Length < 5) // if data
                    {
                        Vector3 pos = new Vector3(0, 0, 0);
                        pos.x = float.Parse(tmpPosPerRow[1]);
                        pos.y = float.Parse(tmpPosPerRow[2]);
                        pos.z = float.Parse(tmpPosPerRow[3]);

                        
                        
                        //pos = tMatrix.MultiplyPoint(pos);
                        //pos = sMatrix.MultiplyPoint(pos);
                        //pos = rMatrix.MultiplyPoint(pos);

                        pos = m.MultiplyPoint(pos);
                        positions.Add(pos);
                    }
                }
            }
            while (line != null);
            reader1.Close();
        }
        nodeCount = positions.Count;
    }
    private static void loadFaces() {
        string Facepath = Application.dataPath + modelPath + fileName + ".face";
        StreamReader reader1 = new StreamReader(Facepath);
        string line;
        using (reader1)
        {
            line = reader1.ReadLine();
            do
            {
                line = reader1.ReadLine(); // first line
                if (line != null)
                {
                    string[] tmpPosPerRow = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmpPosPerRow.Length > 0 && tmpPosPerRow.Length < 6) // if data
                    {
                        //Vector3 face = new Vector3(0, 0, 0);
                        
                        triangles.Add(int.Parse(tmpPosPerRow[1]));
                        triangles.Add(int.Parse(tmpPosPerRow[3]));
                        triangles.Add(int.Parse(tmpPosPerRow[2]));
                        //1,3,2 for torus

                        //faces.Add(Triangle(tmpPosPerRow[1], tmpPosPerRow[3], tmpPosPerRow[2]))
                        Triangle t;
                        if (fileName == "cow.1" || fileName == "dragon_vrip_res3.1" || fileName == "dragon_vrip_res4.1")
                            t = new Triangle(int.Parse(tmpPosPerRow[1]), 
                                int.Parse(tmpPosPerRow[2]), int.Parse(tmpPosPerRow[3]));
                        else 
                            t = new Triangle(int.Parse(tmpPosPerRow[1]), 
                                int.Parse(tmpPosPerRow[3]), int.Parse(tmpPosPerRow[2]));

                        faces.Add(t);


                        //face.x = float.Parse(tmpPosPerRow[1]);
                        //face.y = float.Parse(tmpPosPerRow[3]);
                        //face.z = float.Parse(tmpPosPerRow[2]);
                        ////print(face);
                        //faces.Add(face);
                    }
                }
            }
            while (line != null);
            reader1.Close();
        }
        triCount = faces.Count;
    }
    private static void loadTetrahedron() {
        string ElePath = Application.dataPath + modelPath + fileName + ".ele";
        StreamReader reader1 = new StreamReader(ElePath);
        string line;
        using (reader1)
        {
            line = reader1.ReadLine();
            do
            {
                line = reader1.ReadLine(); // first line
                if (line != null)
                {
                    string[] tmpPosPerRow = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmpPosPerRow.Length > 0 && tmpPosPerRow.Length < 6) // if data
                    {
                        

                        int i0 = int.Parse(tmpPosPerRow[1]);
                        int i1 = int.Parse(tmpPosPerRow[2]);
                        int i2 = int.Parse(tmpPosPerRow[3]);
                        int i3 = int.Parse(tmpPosPerRow[4]);
                        float volume = computeTetraVolume(positions[i0], positions[i1], 
                            positions[i2], positions[i3]);
                        Tetrahedron tetrahedron = new Tetrahedron(i0, i1, i2, i3, volume);

                        //print(i0 + "," + i1 + "," + i2 + "," + i3);
                        element.Add(tetrahedron);
                    }
                }
            }
            while (line != null);
            reader1.Close();

        }
        tetCount = element.Count;
    }
    private static void loadSurfaceSpring() {
        string springpath = Application.dataPath + modelPath + fileName + ".springsurface";
        StreamReader reader1 = new StreamReader(springpath);
        string line;

        using (reader1)
        {
            line = reader1.ReadLine();
            do
            {
                line = reader1.ReadLine(); // first line
                if (line != null)
                {
                    string[] tmpPosPerRow = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmpPosPerRow.Length > 0 && tmpPosPerRow.Length < 3) // if data
                    {
                       
                        int i1 = int.Parse(tmpPosPerRow[0]);
                        int i2 = int.Parse(tmpPosPerRow[1]);
                        float restLength = Vector3.Distance(positions[i1], positions[i2]);
                        Spring s = new Spring(i1, i2, restLength);
                        springsSurface.Add(s);
                    }
                }
            }
            while (line != null);
            reader1.Close();
        }

        springSurfaceCount = springsSurface.Count;
    }
    private static void loadInteriorSpring()
    {
        string springpath = Application.dataPath + modelPath + fileName + ".springinterior";
        StreamReader reader1 = new StreamReader(springpath);
        string line;

        using (reader1)
        {
            line = reader1.ReadLine();
            do
            {
                line = reader1.ReadLine(); // first line
                if (line != null)
                {
                    string[] tmpPosPerRow = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmpPosPerRow.Length > 0 && tmpPosPerRow.Length < 3) // if data
                    {

                        int i1 = int.Parse(tmpPosPerRow[0]);
                        int i2 = int.Parse(tmpPosPerRow[1]);
                        float restLength = Vector3.Distance(positions[i1], positions[i2]);
                        Spring s = new Spring(i1, i2, restLength);
                        springs.Add(s);
                    }
                }
            }
            while (line != null);
            reader1.Close();
        }

        springCount = springs.Count;
    }

    static Spring initSpring(int i1, int i2)
    {
        float rl = Vector3.Distance(positions[i1], positions[i2]);
        Spring spring = new Spring(i1,i2,rl);
        //print(i1 + "," + i2);

        return spring;
    }
    static void InitSpringList(Spring newSpring)
    {
        bool isDuplicated = false;

        foreach (Spring sp in springs)
        {
            if ((newSpring.i1 == sp.i1 && newSpring.i2 == sp.i2) || (newSpring.i1 == sp.i2 && newSpring.i2 == sp.i1))
            {
                isDuplicated = true;
            }
        }
        if (!isDuplicated)
        {
           springs.Add(newSpring);
           //print(newSpring.i1 + "," + newSpring.i2);
        }



    }
    static void InitSpringSurfaceList(Spring newSpring)
    {
        bool isDuplicated = false;

        foreach (Spring sp in springs)
        {
            if ((newSpring.i1 == sp.i1 && newSpring.i2 == sp.i2) || (newSpring.i1 == sp.i2 && newSpring.i2 == sp.i1))
            {
                isDuplicated = true;
            }
        }
        if (!isDuplicated)
        {
            springsSurface.Add(newSpring);
            //print(newSpring.i1 + "," + newSpring.i2);
        }



    }
    static void tet2spring()
    {
        //foreach (Tetrahedron t in element)
        //{
        //    InitSpringList(initSpring(t.i1, t.i2));
        //    InitSpringList(initSpring(t.i1, t.i3));
        //    InitSpringList(initSpring(t.i1, t.i4));
        //    InitSpringList(initSpring(t.i2, t.i3));
        //    InitSpringList(initSpring(t.i2, t.i4));
        //    InitSpringList(initSpring(t.i3, t.i4));
        //}

        for(int i = 0; i < tetCount; i++)
        {
            Tetrahedron t = element[i];

            //print(t.i1 + "," + t.i2 + "," + t.i3 + "," + t.i4);

            InitSpringList(initSpring(t.i1, t.i2));
            InitSpringList(initSpring(t.i1, t.i3));
            InitSpringList(initSpring(t.i1, t.i4));
            InitSpringList(initSpring(t.i2, t.i3));
            InitSpringList(initSpring(t.i2, t.i4));
            InitSpringList(initSpring(t.i3, t.i4));
        }

        springCount = springs.Count;
    }

    static void tri2spring()
    {
        for (int i = 0; i < triCount; i++)
        {
            Triangle t = faces[i];
            InitSpringSurfaceList(initSpring(t.v0, t.v1));
            InitSpringSurfaceList(initSpring(t.v0, t.v2));
            InitSpringSurfaceList(initSpring(t.v1, t.v2));
        }
        springSurfaceCount = springsSurface.Count;
    }
    public static void ClearData()
    {
        positions = new List<Vector3>();
        faces = new List<Triangle>(); //for compute
        element = new List<Tetrahedron>();
        springs = new List<Spring>();
        springsSurface = new List<Spring>();
        triangles = new List<int>();//for render

        nodeCount = 0;
        springCount = 0;
        triCount = 0;
        tetCount = 0;
    }


}
