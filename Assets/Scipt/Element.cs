using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.script
{
    public struct Spring
    {
        public int i1;
        public int i2;
        public float RestLength;
        public Spring(int Index1, int Index2, float restLength)
        {
            i1 = Index1;
            i2 = Index2;
            RestLength = restLength;
        }
    }
    public struct Triangle
    {
        public int v0;
        public int v1;
        public int v2;

        public Triangle(int V0, int V1, int V2)
        {
            v0 = V0;
            v1 = V1;
            v2 = V2;
        }
    }
    public struct Tetrahedron
    {
        public int i1;
        public int i2;
        public int i3;
        public int i4;
        public float RestVolume;
        public Tetrahedron(int Index1, int Index2, int Index3, int Index4, float restVolume)
        {
            i1 = Index1;
            i2 = Index2;
            i3 = Index3;
            i4 = Index4;
            RestVolume = restVolume;
        }
    }
    public struct UInt3Struct
    {
        public uint deltaXInt;
        public uint deltaYInt;
        public uint deltaZInt;
    }
}
