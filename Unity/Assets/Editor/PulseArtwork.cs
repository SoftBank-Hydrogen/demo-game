using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Offline generation only. PNG sprites are committed scene assets, not rebuilt in play.
public static class PulseArtwork
{
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    static Color Alpha(Color c, float a) { c.a = a; return c; }
    static readonly Color mint = Hex("#5CF2C4"), line = Hex("#1E3D3F");
    const string Folder = "Assets/Art/Pulse";
    public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        Bake("Solid", 2, vh => Quad(vh,new Vector2(-1,-1),new Vector2(1,1),Color.white));
        Bake("Frame", 660, vh => {
            Disc(vh, Vector2.zero, 315, new Color(.03f,.23f,.19f,.14f), new Color(.03f,.18f,.17f,0));
            Ring(vh, Vector2.zero, 310, .8f, Alpha(line,.7f));
            Ring(vh, Vector2.zero, 269, .9f, Alpha(line,.7f));
            for(int i=0;i<4;i++) Disc(vh, Polar(310, Mathf.PI/4+Mathf.PI/2*i), 2, Alpha(mint,.7f), Alpha(mint,.7f));
        });
        Bake("Ticks", 660, vh => {
            for(int i=0;i<80;i++) {
                float a=i*Mathf.PI*2/80;
                Segment(vh,Polar(i%5==0?290:297,a),Polar(302,a),i%5==0?1.4f:.7f,Alpha(line,i%5==0?1:.65f));
            }
        });
        Bake("FineRing", 504, vh => Ring(vh,Vector2.zero,250,.9f,Color.white));
        Bake("Wave", 504, vh => Ring(vh,Vector2.zero,250,2,Color.white));
        Bake("Track", 376, vh => Ring(vh,Vector2.zero,186,2,Alpha(line,.9f)));
        Bake("Progress", 376, vh => Ring(vh,Vector2.zero,186,4,mint));
        Bake("Tip", 18, vh => {
            Disc(vh,Vector2.zero,8,Alpha(mint,.7f),Alpha(mint,0));
            Disc(vh,Vector2.zero,3,Hex("#E9FFF8"),mint);
        });
        Bake("Glow", 544, vh => Disc(vh,Vector2.zero,146*1.85f,Color.white,new Color(1,1,1,0)));
        Bake("Core", 296, vh => {
            Disc(vh,Vector2.zero,146,Hex("#D1FFF0"),Hex("#227C69"));
            Disc(vh,new Vector2(-146*.18f,146*.25f),146*.55f,new Color(.93f,1,.97f,.35f),new Color(.5f,1,.8f,0));
            Ring(vh,Vector2.zero,146,1.5f,Alpha(mint,.8f));
        });
        Bake("Rounded", 128, vh => Rounded(vh,new Rect(-64,-64,128,128),30,Color.white), new Vector4(32,32,32,32));
        AssetDatabase.SaveAssets();
    }
    // Rasterize the former vector artwork once, with 2x supersampling.
    static void Bake(string name, int size, Action<VertexHelper> draw, Vector4 border = default)
    {
        int n = size * 2;
        var pixels = new Color[n*n];
        using(var vh = new VertexHelper()) {
            draw(vh);
            var mesh = new Mesh(); vh.FillMesh(mesh);
            var vertices=mesh.vertices; var colors=mesh.colors; var triangles=mesh.triangles;
            for(int k=0;k<triangles.Length;k+=3) {
                int ia=triangles[k],ib=triangles[k+1],ic=triangles[k+2];
                Vector2 a=((Vector2)vertices[ia]+Vector2.one*size/2)*2;
                Vector2 b=((Vector2)vertices[ib]+Vector2.one*size/2)*2;
                Vector2 c=((Vector2)vertices[ic]+Vector2.one*size/2)*2;
                float area=Cross(b-a,c-a); if(Mathf.Abs(area)<.00001f) continue;
                int xmin=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))));
                int xmax=Mathf.Min(n-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
                int ymin=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));
                int ymax=Mathf.Min(n-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
                for(int y=ymin;y<=ymax;y++) for(int x=xmin;x<=xmax;x++) {
                    var p=new Vector2(x+.5f,y+.5f);
                    float u=Cross(b-p,c-p)/area, v=Cross(c-p,a-p)/area, w=1-u-v;
                    if(u<0 || v<0 || w<=0) continue;
                    Color src=colors[ia]*u+colors[ib]*v+colors[ic]*w;
                    Color dst=pixels[y*n+x];
                    // Premultiplied intermediates avoid dark fringes during downsampling.
                    pixels[y*n+x]=new Color(src.r*src.a+dst.r*(1-src.a),src.g*src.a+dst.g*(1-src.a),src.b*src.a+dst.b*(1-src.a),src.a+dst.a*(1-src.a));
                }
            }
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        var output=new Color[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
            int i=y*2*n+x*2; Color c=(pixels[i]+pixels[i+1]+pixels[i+n]+pixels[i+n+1])*.25f;
            if(c.a>0) { c.r/=c.a;c.g/=c.a;c.b/=c.a; }
            output[y*size+x]=c;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.SetPixels(output); texture.Apply();
        string path=Folder+"/"+name+".png"; File.WriteAllBytes(path,texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.spriteBorder=border;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None; importer.maxTextureSize=1024;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.isReadable=false; importer.wrapMode=TextureWrapMode.Clamp;
        var textureSettings=new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();
    }
    static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
    static Vector2 Polar(float radius, float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color c) => Quad(vh, min, max, c, c);
    static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color bottom, Color top)
    {
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(min.x, min.y), bottom, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), top, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), top, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), bottom, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
    static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        var dir = (b - a).normalized; var side = new Vector2(-dir.y, dir.x) * width * .5f;
        int n = vh.currentVertCount;
        vh.AddVert(a + side, c, Vector2.zero); vh.AddVert(b + side, c, Vector2.zero);
        vh.AddVert(b - side, c, Vector2.zero); vh.AddVert(a - side, c, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
    static void Disc(VertexHelper vh, Vector2 center, float radius, Color inside, Color outside)
    {
        const int steps = 96; int n = vh.currentVertCount;
        vh.AddVert(center, inside, Vector2.zero);
        for (int i = 0; i <= steps; i++) vh.AddVert(center + Polar(radius, i * Mathf.PI * 2 / steps), outside, Vector2.zero);
        for (int i = 0; i < steps; i++) vh.AddTriangle(n, n + i + 1, n + i + 2);
    }
    static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color c, float fill = 1)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(180 * fill)); int n = vh.currentVertCount;
        for (int i = 0; i <= count; i++)
        {
            float a = Mathf.PI / 2 - (float)i / count * Mathf.PI * 2 * fill;
            vh.AddVert(center + Polar(radius - width * .5f, a), c, Vector2.zero);
            vh.AddVert(center + Polar(radius + width * .5f, a), c, Vector2.zero);
            if (i > 0) { int k = n + i * 2; vh.AddTriangle(k - 2, k - 1, k); vh.AddTriangle(k, k - 1, k + 1); }
        }
    }
    static void Rounded(VertexHelper vh, Rect r, float radius, Color c)
    {
        int n = vh.currentVertCount; vh.AddVert(r.center, c, Vector2.zero);
        var centers = new[] { new Vector2(r.xMax - radius, r.yMax - radius), new Vector2(r.xMin + radius, r.yMax - radius), new Vector2(r.xMin + radius, r.yMin + radius), new Vector2(r.xMax - radius, r.yMin + radius) };
        for (int corner = 0; corner < 4; corner++)
            for (int i = 0; i <= 12; i++) vh.AddVert(centers[corner] + Polar(radius, (corner * 90 + i * 7.5f) * Mathf.Deg2Rad), c, Vector2.zero);
        int count = 52;
        for (int i = 0; i < count; i++) vh.AddTriangle(n, n + i + 1, n + (i + 1) % count + 1);
    }
}
