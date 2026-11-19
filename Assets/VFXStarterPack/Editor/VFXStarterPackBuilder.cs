#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;

public static class VFXStarterPackBuilder
{
    const string R="Assets/VFXStarterPack";
    const string M=R+"/Materials";
    const string P=R+"/Prefabs";
    const string T=R+"/Textures";
    const string S=R+"/Showcase";
    const string PACK="Assets/UnityTechnologies/ParticlePack/EffectExamples";

    [MenuItem("Tools/VFX Starter Pack/Build Animated VFX Pack")]
    public static void Build()
    {
        EnsureFolder(M);
        EnsureFolder(P+"/Transformation");
        EnsureFolder(P+"/Elements");
        EnsureFolder(P+"/FinalBoss");
        EnsureFolder(T+"/Enhanced");
        EnsureFolder(T+"/Portraits");
        EnsureFolder(S);
        ConfigureParticleTextures();

        Material glow=Mat("Glow","Enhanced/VFX_SoftBloom.png"), spark=Mat("Spark","Enhanced/VFX_Starburst.png"),
            ring=Mat("Ring","Enhanced/VFX_RuneRing.png"), flame=Mat("Flame","Enhanced/VFX_Flame.png"),
            water=Mat("Water","Enhanced/VFX_WaterDrop.png"), earth=Mat("Earth","Enhanced/VFX_EarthShard.png"),
            wind=Mat("Wind","Enhanced/VFX_WindRibbon.png"), bolt=Mat("Bolt","Enhanced/VFX_LightningBranch.png"),
            cylinder=CylinderMat();

        GameObject tr=Transformation(glow,spark,ring,cylinder,PortraitMat("Transformation"));
        GameObject el=ElementalBending(flame,water,earth,wind,spark,glow,cylinder,PortraitMat("Elemental"));
        GameObject bo=Boss(glow,spark,ring,bolt,cylinder,PortraitMat("FinalBoss"));

        Save(tr,P+"/Transformation/VFX_Transformation_Celestial.prefab");
        Save(el,P+"/Elements/VFX_Elemental_Bending.prefab");
        Save(bo,P+"/FinalBoss/VFX_FinalBoss_Apocalypse.prefab");
        Object.DestroyImmediate(tr); Object.DestroyImmediate(el); Object.DestroyImmediate(bo);
        ShowcaseScene();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("VFX Pack Built","Three animated Particle System prefabs have been created. The elemental prefab combines fire, water, earth, and air around one cylinder. Open Assets/VFXStarterPack/Showcase/VFX_Showcase.unity and press Play.","Great!");
    }

    static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');
        string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))
            {
                if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(current,parts[i])))
                    throw new IOException("Failed to create asset folder: "+next);
            }
            current=next;
        }
    }

    static Material Mat(string n,string texName)
    {
        string path=M+"/VFX_"+n+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){
            Shader sh=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if(sh==null) sh=Shader.Find("Particles/Standard Unlit");
            if(sh==null) sh=Shader.Find("Unlit/Transparent");
            if(sh==null) sh=Shader.Find("Sprites/Default");
            if(sh==null)throw new System.InvalidOperationException("No compatible transparent particle shader was found.");
            m=new Material(sh); AssetDatabase.CreateAsset(m,path);
        }
        Texture2D tx=AssetDatabase.LoadAssetAtPath<Texture2D>(T+"/"+texName);
        if(tx){if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",tx);if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",tx);}
        ConfigureParticleMaterial(m);
        return m;
    }

    static void ConfigureParticleMaterial(Material m)
    {
        if(m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface",1f);
            m.SetFloat("_Blend",0f);
            m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite",0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
        }
        else if(m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode",2f);
            if(m.HasProperty("_SrcBlend"))m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            if(m.HasProperty("_DstBlend"))m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            if(m.HasProperty("_ZWrite"))m.SetFloat("_ZWrite",0f);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        m.SetOverrideTag("RenderType","Transparent");
        m.renderQueue=(int)RenderQueue.Transparent;
    }

    static void ConfigureParticleTextures()
    {
        string[] names={
            "VFX_SoftBloom.png","VFX_Starburst.png","VFX_RuneRing.png","VFX_Flame.png",
            "VFX_WaterDrop.png","VFX_EarthShard.png","VFX_WindRibbon.png","VFX_LightningBranch.png"
        };
        foreach(string name in names)
        {
            string path=T+"/Enhanced/"+name;
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new System.InvalidOperationException("Could not import VFX texture: "+path);
            bool changed=false;
            if(importer.textureType!=TextureImporterType.Default){importer.textureType=TextureImporterType.Default;changed=true;}
            if(!importer.alphaIsTransparency){importer.alphaIsTransparency=true;changed=true;}
            if(importer.wrapMode!=TextureWrapMode.Clamp){importer.wrapMode=TextureWrapMode.Clamp;changed=true;}
            if(importer.filterMode!=FilterMode.Bilinear){importer.filterMode=FilterMode.Bilinear;changed=true;}
            if(importer.mipmapEnabled){importer.mipmapEnabled=false;changed=true;}
            if(importer.textureCompression!=TextureImporterCompression.Uncompressed){importer.textureCompression=TextureImporterCompression.Uncompressed;changed=true;}
            if(changed)importer.SaveAndReimport();
        }
    }

    static Material CylinderMat()
    {
        const string path=M+"/VFX_Cylinder.mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null)
        {
            Shader sh=Shader.Find("Universal Render Pipeline/Lit");
            if(sh==null)sh=Shader.Find("Standard");
            if(sh==null)throw new System.InvalidOperationException("No compatible lit shader was found for the showcase cylinder.");
            m=new Material(sh);
            AssetDatabase.CreateAsset(m,path);
        }
        Color color=new Color(.07f,.08f,.12f);
        if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
        if(m.HasProperty("_Color"))m.color=color;
        return m;
    }

    static Material PortraitMat(string name)
    {
        string path=M+"/VFX_Portrait_"+name+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null)
        {
            Shader sh=Shader.Find("Universal Render Pipeline/Unlit");
            if(sh==null)sh=Shader.Find("Unlit/Texture");
            if(sh==null)sh=Shader.Find("Sprites/Default");
            if(sh==null)throw new System.InvalidOperationException("No compatible unlit shader was found for the portrait display.");
            m=new Material(sh);
            AssetDatabase.CreateAsset(m,path);
        }
        if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",Color.white);
        if(m.HasProperty("_Color"))m.color=Color.white;
        return m;
    }

    static GameObject Root(string name,string meshPath,Material cylinderMaterial,Material portraitMaterial)
    {
        GameObject root=new GameObject(name);
        GameObject meshAsset=AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
        if(meshAsset==null)throw new System.InvalidOperationException("Showcase mesh was not found: "+meshPath);
        GameObject mesh=(GameObject)PrefabUtility.InstantiatePrefab(meshAsset);
        if(mesh==null)throw new System.InvalidOperationException("Could not instantiate showcase mesh: "+meshPath);
        mesh.transform.SetParent(root.transform,false);

        Transform cylinder=FindDescendant(mesh.transform,"CenterCylinder");
        Renderer cylinderRenderer=cylinder!=null?cylinder.GetComponentInChildren<Renderer>(true):null;
        if(cylinderRenderer!=null)
        {
            cylinderRenderer.sharedMaterial=cylinderMaterial;
            cylinder.name="Showcase_Cylinder";
        }
        else
        {
            GameObject cylinderObject=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinderObject.name="Showcase_Cylinder";
            cylinderObject.transform.SetParent(root.transform,false);
            cylinderObject.transform.localScale=new Vector3(.98f,1.02f,.98f);
            cylinderObject.GetComponent<Renderer>().sharedMaterial=cylinderMaterial;
        }
        mesh.name="Starter_Showcase_Mesh";

        GameObject portrait=GameObject.CreatePrimitive(PrimitiveType.Quad);
        portrait.name="Character_Portrait_Display";
        Object.DestroyImmediate(portrait.GetComponent<Collider>());
        portrait.transform.SetParent(root.transform,false);
        portrait.transform.localPosition=new Vector3(0,0,-.505f);
        portrait.transform.localRotation=Quaternion.Euler(0,180,0);
        portrait.transform.localScale=new Vector3(.5f,.82f,1);
        portrait.GetComponent<Renderer>().sharedMaterial=portraitMaterial;
        return root;
    }

    static Transform FindDescendant(Transform parent,string name)
    {
        if(parent.name==name)return parent;
        for(int i=0;i<parent.childCount;i++)
        {
            Transform child=parent.GetChild(i);
            if(child.name==name)return child;
            Transform match=FindDescendant(child,name);
            if(match!=null)return match;
        }
        return null;
    }

    static GameObject AddPackEffect(Transform parent,string name,string path,Vector3 position,Vector3 rotation,float scale)
    {
        if(parent==null)throw new System.InvalidOperationException("Particle Pack effect parent was not found: "+name);
        GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset==null)throw new System.InvalidOperationException("Particle Pack prefab was not found: "+path);
        GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
        if(instance==null)throw new System.InvalidOperationException("Could not instantiate Particle Pack prefab: "+path);
        instance.name=name;
        instance.transform.SetParent(parent,false);
        instance.transform.localPosition=position;
        instance.transform.localRotation=Quaternion.Euler(rotation);
        instance.transform.localScale=Vector3.one*scale;
        return instance;
    }

    static ParticleSystem PS(Transform parent,string n,Material mat,Color c,float rate,float life,float speed,float size,
        ParticleSystemShapeType shape,float radius,bool loop=true,int burst=0)
    {
        GameObject g=new GameObject(n); g.transform.SetParent(parent,false);
        ParticleSystem ps=g.AddComponent<ParticleSystem>();
        var main=ps.main; main.duration=5; main.loop=loop; main.playOnAwake=true;
        main.simulationSpace=ParticleSystemSimulationSpace.Local; main.startLifetime=life;
        main.startSpeed=speed; main.startSize=size; main.startColor=c; main.maxParticles=1800;
        var em=ps.emission; em.rateOverTime=rate;
        if(burst>0){em.rateOverTime=0;em.SetBursts(new[]{new ParticleSystem.Burst(0,(short)burst)});}
        var sh=ps.shape; sh.shapeType=shape; sh.radius=radius; sh.arc=360;
        var cl=ps.colorOverLifetime; cl.enabled=true;
        Gradient gr=new Gradient();
        gr.SetKeys(new[]{new GradientColorKey(c,0),new GradientColorKey(Color.white,.45f),new GradientColorKey(c,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.7f,.7f),new GradientAlphaKey(0,1)});
        cl.color=gr;
        var sz=ps.sizeOverLifetime; sz.enabled=true;
        sz.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.1f),new Keyframe(.25f,1),new Keyframe(1,0)));
        var no=ps.noise; no.enabled=true; no.strength=.3f; no.frequency=.35f;
        var rr=g.GetComponent<ParticleSystemRenderer>(); rr.renderMode=ParticleSystemRenderMode.Billboard; rr.material=mat;
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play();
        return ps;
    }
    static void Trail(ParticleSystem p,Color c){var t=p.trails;t.enabled=true;t.ratio=.6f;t.lifetime=.35f;t.widthOverTrail=new ParticleSystem.MinMaxCurve(.3f);t.colorOverLifetime=new ParticleSystem.MinMaxGradient(c);}

    static GameObject Transformation(Material glow,Material spark,Material ring,Material cylinder,Material portrait)
    {
        GameObject r=Root("VFX_Transformation_Celestial",R+"/Meshes/VFX_Transformation_Celestial.obj",cylinder,portrait); Transform t=r.transform;
        var a=PS(t,"01_Ground_Magic_Circle",ring,new Color(1,.55f,.9f),1,5,0,2.2f,ParticleSystemShapeType.Circle,.9f);
        a.transform.localPosition=Vector3.down*.92f;a.transform.localRotation=Quaternion.Euler(90,0,0);
        var aRotation=a.rotationOverLifetime;
        aRotation.enabled=true;aRotation.z=new ParticleSystem.MinMaxCurve(40);
        var b=PS(t,"02_Star_Spiral",spark,new Color(1,.75f,.25f),42,1.8f,.45f,.14f,ParticleSystemShapeType.Sphere,.75f);
        var bVelocity=b.velocityOverLifetime;
        bVelocity.enabled=true;bVelocity.y=new ParticleSystem.MinMaxCurve(.4f);Trail(b,new Color(1,.3f,.8f));
        var c=PS(t,"03_Ribbon_Glow",glow,new Color(.7f,.2f,1,.8f),18,2.2f,.2f,.32f,ParticleSystemShapeType.Circle,.65f);
        var cRotation=c.rotationOverLifetime;
        cRotation.enabled=true;cRotation.y=new ParticleSystem.MinMaxCurve(65);Trail(c,new Color(.9f,.2f,.8f));
        var d=PS(t,"04_Ascension_Burst",spark,new Color(1,.85f,.45f),0,.8f,3,.2f,ParticleSystemShapeType.Sphere,.15f,false,90);
        d.transform.localPosition=Vector3.up*.1f;
        AddPackEffect(t,"05_Pack_FireFlies",PACK+"/Misc Effects/Prefabs/FireFlies.prefab",Vector3.zero,Vector3.zero,.24f);
        AddPackEffect(t,"06_Pack_EnergyExplosion",PACK+"/Fire & Explosion Effects/Prefabs/EnergyExplosion.prefab",
            Vector3.up*.1f,Vector3.zero,.18f);
        return r;
    }

    static GameObject ElementalBending(Material flame,Material water,Material earth,Material wind,Material spark,Material glow,Material cylinder,Material portrait)
    {
        GameObject r=Root("VFX_Elemental_Bending",R+"/Meshes/VFX_Elemental_Bending.obj",cylinder,portrait); Transform t=r.transform;
        AddElement(t,"fire",flame,spark,new Color(1f,.22f,.035f),-.45f);
        AddElement(t,"water",water,glow,new Color(.08f,.65f,1f),.15f);
        AddElement(t,"earth",earth,spark,new Color(.48f,.72f,.28f),-.8f);
        AddElement(t,"air",wind,glow,new Color(.75f,.94f,1f),.65f);
        AddPackEffect(t.Find("Element_fire"),"04_Pack_FlameStream",PACK+"/Fire & Explosion Effects/Prefabs/FlameStream.prefab",
            Vector3.zero,Vector3.zero,.18f);
        AddPackEffect(t.Find("Element_water"),"04_Pack_BigSplash",PACK+"/Water Effects/Prefabs/BigSplash.prefab",
            Vector3.zero,Vector3.zero,.14f);
        AddPackEffect(t.Find("Element_earth"),"04_Pack_EarthShatter",PACK+"/Magic Effects/Prefabs/EarthShatter.prefab",
            Vector3.zero,Vector3.zero,.14f);
        AddPackEffect(t.Find("Element_air"),"04_Pack_SandSwirls",PACK+"/Misc Effects/Prefabs/SandSwirlsEffect.prefab",
            Vector3.zero,Vector3.zero,.18f);
        return r;
    }

    static void AddElement(Transform parent,string type,Material primary,Material secondary,Color c,float height)
    {
        GameObject group=new GameObject("Element_"+type);
        group.transform.SetParent(parent,false);
        group.transform.localPosition=Vector3.up*height;
        Transform t=group.transform;
        float rate=40,life=1.2f,speed=1.4f,size=.2f,gravity=0;
        ParticleSystemShapeType shape=ParticleSystemShapeType.Cone;
        if(type=="fire"){rate=55;life=.9f;speed=1.8f;size=.23f;gravity=-.12f;}
        if(type=="water"){shape=ParticleSystemShapeType.Sphere;rate=40;life=1.5f;speed=1.1f;size=.18f;gravity=.12f;}
        if(type=="earth"){rate=26;life=1.2f;speed=2.3f;size=.26f;gravity=.8f;}
        if(type=="air"){shape=ParticleSystemShapeType.Circle;rate=45;life=1.4f;speed=1.2f;size=.16f;}
        var p=PS(t,"01_"+type+"_Main",primary,c,rate,life,speed,size,shape,.42f);
        var main=p.main;main.gravityModifier=gravity;
        var noise=p.noise;
        noise.strength=type=="air"?.8f:.35f;
        if(type=="water"||type=="air")Trail(p,c);
        var pRotation=p.rotationOverLifetime;
        if(type=="earth")pRotation.enabled=true;
        var pVelocity=p.velocityOverLifetime;
        pVelocity.enabled=true;pVelocity.y=new ParticleSystem.MinMaxCurve(type=="earth"?-1.2f:.35f);
        var o=PS(t,"02_"+type+"_Orbit",secondary,new Color(c.r,c.g,c.b,.7f),12,2.1f,.2f,.25f,ParticleSystemShapeType.Circle,.78f);
        var oRotation=o.rotationOverLifetime;
        oRotation.enabled=true;oRotation.y=new ParticleSystem.MinMaxCurve(70);
        var b=PS(t,"03_"+type+"_Burst",primary,c,0,.7f,2.8f,.2f,ParticleSystemShapeType.Sphere,.2f,false,55);
        b.transform.localPosition=Vector3.up*.15f;
    }

    static GameObject Boss(Material glow,Material spark,Material ring,Material bolt,Material cylinder,Material portrait)
    {
        GameObject r=Root("VFX_FinalBoss_Apocalypse",R+"/Meshes/VFX_FinalBoss_Apocalypse.obj",cylinder,portrait);Transform t=r.transform;
        var a=PS(t,"01_Void_Aura",glow,new Color(.35f,.04f,.8f,.8f),32,2.8f,.18f,.65f,ParticleSystemShapeType.Sphere,.88f);
        var aNoise=a.noise;
        aNoise.strength=.65f;Trail(a,new Color(.45f,.08f,1));
        var b=PS(t,"02_Crimson_Embers",spark,new Color(1,.12f,.18f),65,1.7f,.8f,.12f,ParticleSystemShapeType.Sphere,.75f);
        var bMain=b.main;
        bMain.gravityModifier=-.18f;
        PS(t,"03_Void_Lightning_Flashes",bolt,new Color(.75f,.35f,1),7,.28f,0,.55f,ParticleSystemShapeType.Sphere,.7f);
        var d=PS(t,"04_Pulsing_Rings",ring,new Color(.55f,.12f,1,.85f),1,2,0,2.1f,ParticleSystemShapeType.Circle,.9f);
        d.transform.localPosition=Vector3.down*.85f;d.transform.localRotation=Quaternion.Euler(90,0,0);
        var dRotation=d.rotationOverLifetime;
        dRotation.enabled=true;dRotation.z=new ParticleSystem.MinMaxCurve(45);
        var e=PS(t,"05_Apocalypse_Burst",glow,new Color(1,.12f,.25f),0,.8f,4,.35f,ParticleSystemShapeType.Sphere,.25f,false,120);
        e.transform.localPosition=Vector3.up*.1f;
        AddPackEffect(t,"06_Pack_PlasmaSurge",PACK+"/Legacy Particles/Prefabs/PlasmaExplosionEffect.prefab",
            Vector3.zero,Vector3.zero,.18f);
        AddPackEffect(t,"07_Pack_ElectricalSparks",PACK+"/Misc Effects/Prefabs/ElectricalSparks.prefab",
            Vector3.up*.35f,Vector3.zero,.22f);
        return r;
    }

    static void Save(GameObject g,string path){PrefabUtility.SaveAsPrefabAsset(g,path);}
    static void ShowcaseScene()
    {
        Scene sc=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        Camera cam=Camera.main;if(cam){cam.transform.position=new Vector3(0,4,-13);cam.transform.rotation=Quaternion.Euler(13,0,0);cam.fieldOfView=50;}
        string[] paths={P+"/Transformation/VFX_Transformation_Celestial.prefab",P+"/Elements/VFX_Elemental_Bending.prefab",P+"/FinalBoss/VFX_FinalBoss_Apocalypse.prefab"};
        float[] xs={-4.5f,0f,4.5f};
        for(int i=0;i<paths.Length;i++){var pf=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);if(pf){var go=(GameObject)PrefabUtility.InstantiatePrefab(pf);go.transform.position=new Vector3(xs[i],1,0);}}
        EditorSceneManager.SaveScene(sc,S+"/VFX_Showcase.unity");
    }
}
#endif
