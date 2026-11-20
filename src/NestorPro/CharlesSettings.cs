using System.Text.Json;
namespace KeyboardToGamepad;

internal sealed record ButtonAction(int Code,string Name,int Primary,int Secondary=0);
internal sealed record InputChoice(int Code,string Name){public override string ToString()=>Name;}
internal static class ButtonCatalog
{
    internal static readonly ButtonAction[] Actions={
        new(0x11,"Mover para frente",0x11),new(0x1f,"Mover para trás",0x1f),new(0x1e,"Mover para esquerda",0x1e),new(0x20,"Mover para direita",0x20),
        new(0x200,"Atirar • RT",1001),new(0x201,"Mirar • LT",1002),
        new(0x39,"Pular • A",0x39),new(0x1d,"Postura / agachar • B",0x1d,1004),
        new(0x21,"Interagir / loja • X",0x21),new(0x13,"Recarregar / soltar item",0x13),new(0x02,"Trocar arma • Y",0x02,1005),
        new(0x2a,"Correr • LS",0x2a),new(0x2f,"Corpo a corpo • RS",0x2f),new(0x10,"Equipamento tático • LB",0x10),new(0x12,"Equipamento letal • RB",0x12),
        new(0x0f,"Mochila / inventário",0x0f),new(0x48,"Marcar / direcional cima",0x148,0x138),
        new(0x50,"Direcional baixo",0x150),new(0x4b,"Modo de disparo / esquerda",0x14b),new(0x4d,"Séries / direita",0x14d),new(0x1c,"Opções • Start",0x1c)
    };
    internal static readonly InputChoice[] Choices=BuildChoices();
    private static InputChoice[] BuildChoices()
    {
        var list=new List<InputChoice>{new(0,"Sem tecla"),new(1001,"Mouse esquerdo"),new(1002,"Mouse direito"),new(1004,"Mouse lateral 4"),new(1005,"Mouse lateral 5")};
        string letters="QWERTYUIOPASDFGHJKLZXCVBNM";int[] scans={16,17,18,19,20,21,22,23,24,25,30,31,32,33,34,35,36,37,38,44,45,46,47,48,49,50};
        for(int i=0;i<letters.Length;i++)list.Add(new(scans[i],letters[i].ToString()));
        for(int i=1;i<=9;i++)list.Add(new(i+1,i.ToString()));list.Add(new(0x0b,"0"));
        list.AddRange(new InputChoice[]{new(0x39,"Espaço"),new(0x0f,"TAB"),new(0x1c,"Enter"),new(0x1d,"Ctrl esquerdo"),new(0x11d,"Ctrl direito"),new(0x2a,"Shift esquerdo"),new(0x36,"Shift direito"),new(0x38,"Alt esquerdo"),new(0x138,"AltGr"),new(0x148,"Seta cima"),new(0x150,"Seta baixo"),new(0x14b,"Seta esquerda"),new(0x14d,"Seta direita"),new(0x0e,"Backspace"),new(0x3a,"Caps Lock"),new(0x152,"Insert"),new(0x153,"Delete"),new(0x149,"Page Up"),new(0x151,"Page Down"),new(0x14f,"End")});
        for(int i=1;i<=10;i++)list.Add(new(0x3a+i,"F"+i));list.Add(new(0x57,"F11"));list.Add(new(0x58,"F12"));return list.ToArray();
    }
    internal static string InputName(int code)=>Choices.FirstOrDefault(c=>c.Code==code)?.Name??"Desconhecida";
}

internal sealed class CharlesSettings
{
    public int Hip {get;set;}=394;
    // Older profiles have one gain; keep their Y response identical to X.
    public int? HipY {get;set;}
    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveHipY=>HipY??Hip;
    // Missing ADS fields inherit HIP, so existing profiles keep their response.
    public int? AdsX {get;set;}
    public int? AdsY {get;set;}
    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveAdsX=>AdsX??Hip;
    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveAdsY=>AdsY??EffectiveHipY;
    public int Deadzone {get;set;}=2500;
    public int RecoilLevel {get;set;}
    public int AimMotionLeft {get;set;}
    public int AimMotionRight {get;set;}
    internal EffectLevels Effects=>new(RecoilLevel,AimMotionLeft,AimMotionRight);
    public bool NativeInventory {get;set;}=true;
    public bool NativeShop {get;set;}
    public string DropOutput {get;set;}="A";
    public Dictionary<int,int[]> Bindings {get;set;}=ButtonCatalog.Actions.ToDictionary(a=>a.Code,a=>new[]{a.Primary,a.Secondary});
    internal InputBindings Validate()
    {
        Effects.Validate();
        if(Hip is <1 or >10000||EffectiveHipY is <1 or >10000||EffectiveAdsX is <1 or >10000||EffectiveAdsY is <1 or >10000||Deadzone is <0 or >12000||DropOutput is not ("A" or "RS" or "Native" or "Y"))throw new InvalidDataException("Ajustes fora dos limites permitidos.");
        return new InputBindings(Bindings);
    }
}

// Each published binding map is immutable. The input worker swaps it only between packets.
internal sealed class InputBindings
{
    private readonly Dictionary<int,int> map=new();
    internal InputBindings(Dictionary<int,int[]> bindings)
    {
        if(bindings.Count!=ButtonCatalog.Actions.Length||ButtonCatalog.Actions.Any(a=>!bindings.ContainsKey(a.Code)))throw new InvalidDataException("O perfil não contém todos os comandos.");
        foreach(var action in ButtonCatalog.Actions)
        {
            var keys=bindings[action.Code];if(keys==null||keys.Length!=2)throw new InvalidDataException("Cada comando precisa de duas opções de entrada.");
            foreach(int physical in keys)
            {
                if(physical==0)continue;
                if(!ButtonCatalog.Choices.Any(c=>c.Code==physical))throw new InvalidDataException("Tecla não suportada: "+physical);
                if(map.TryGetValue(physical,out int existing))throw new InvalidDataException($"{ButtonCatalog.InputName(physical)} já está em ‘{ButtonCatalog.Actions.First(a=>a.Code==existing).Name}’. Escolha outra tecla ou ‘Sem tecla’.");
                map.Add(physical,action.Code);
            }
        }
        if(!map.ContainsValue(0x200)||!map.ContainsValue(0x201)||!map.ContainsValue(0x0f))throw new InvalidDataException("Defina pelo menos uma entrada para atirar, mirar e abrir a mochila.");
    }
    internal int Resolve(int physical)=>map.GetValueOrDefault(physical,-1);
    internal static void Test()
    {
        var defaults=new CharlesSettings();var map=defaults.Validate();
        if(map.Resolve(0x21)!=0x21||map.Resolve(0x0f)!=0x0f||map.Resolve(1001)!=0x200||map.Resolve(0x148)!=0x48||map.Resolve(0x48)!=-1)throw new Exception("Padrão ou distinção de teclas estendidas inválido.");
        defaults.Bindings[0x21]=new[]{0x22,0};map=defaults.Validate();
        if(map.Resolve(0x22)!=0x21||map.Resolve(0x21)!=-1)throw new Exception("Remapeamento de interação inválido.");
        defaults.Bindings[0x13]=new[]{0x22,0};bool rejected=false;try{defaults.Validate();}catch(InvalidDataException){rejected=true;}
        if(!rejected)throw new Exception("Tecla duplicada aceita.");
    }
}

internal sealed class HeldActions
{
    private readonly Dictionary<(int,int),int> pressed=new();
    internal bool Contains(int action)=>pressed.ContainsValue(action);
    internal bool Fresh((int,int) identity)=>!pressed.ContainsKey(identity);
    internal void Set((int,int) identity,int action,bool up){if(up)pressed.Remove(identity);else pressed[identity]=action;}
    internal void Clear()=>pressed.Clear();
    internal static void Test()
    {
        var h=new HeldActions();h.Set((1,29),29,false);h.Set((-1,1004),29,false);h.Set((1,29),29,true);
        if(!h.Contains(29)||h.Fresh((-1,1004)))throw new Exception("Uma entrada soltou outra entrada da mesma ação.");
        h.Clear();h.Set((-1,1004),29,true);if(h.Contains(29))throw new Exception("Comando preso após reset.");
    }
}

internal static class CharlesSettingsStore
{
    internal static string DirectoryPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CharlesDS4SomenteDS4");
    internal static string FilePath=>Path.Combine(DirectoryPath,"ajustes.json");
    private static string LegacyPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CharlesDS4Teste","ajustes.json");
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    internal static CharlesSettings Load(string? path=null)
    {
        if(path==null&&!File.Exists(FilePath)&&File.Exists(LegacyPath))
        {
            string oldJson=File.ReadAllText(LegacyPath);
            var migrated=JsonSerializer.Deserialize<CharlesSettings>(oldJson,Options)??throw new InvalidDataException("Perfil anterior vazio.");
            // The old ADS converter was already close to linear. Carry its gain
            // into the single base converter so aiming does not start much slower.
            using(var doc=JsonDocument.Parse(oldJson))
                if(doc.RootElement.TryGetProperty("Ads",out var previousAds)&&previousAds.TryGetInt32(out int gain)&&gain is >=1 and <=10000)
                    migrated.Hip=gain;
            migrated.Validate();Save(migrated);return migrated;
        }
        path??=FilePath;if(!File.Exists(path))return new();
        var result=JsonSerializer.Deserialize<CharlesSettings>(File.ReadAllText(path),Options)??throw new InvalidDataException("Perfil vazio.");result.Validate();return result;
    }
    internal static void Save(CharlesSettings settings,string? path=null)
    {
        settings.Validate();path??=FilePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(settings,Options));if(File.Exists(path))File.Copy(path,path+".bak",true);File.Move(temp,path,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static void Test()
    {
        string dir=Path.Combine(Path.GetTempPath(),"Charles-settings-test-"+Guid.NewGuid().ToString("N"));string path=Path.Combine(dir,"ajustes.json");
        var legacy=JsonSerializer.Deserialize<CharlesSettings>("{\"Hip\":900,\"Ads\":4000,\"AdsCurve\":\"Custom\",\"AntiRecoil\":true}")!;
        if(legacy.Hip!=900||legacy.EffectiveHipY!=900||legacy.EffectiveAdsX!=900||legacy.EffectiveAdsY!=900||legacy.Validate().Resolve(0x21)!=0x21||JsonSerializer.Serialize(legacy).Contains("AdsCurve"))throw new Exception("Migração não ignorou ajustes de mira antigos.");
        try{var s=new CharlesSettings{Hip=900};s.Bindings[0x21]=new[]{0x22,0};Save(s,path);var loaded=Load(path);if(loaded.Hip!=900||loaded.Validate().Resolve(0x22)!=0x21)throw new Exception("Ajustes não persistiram.");Save(loaded,path);if(!File.Exists(path+".bak"))throw new Exception("Backup dos ajustes ausente.");}
        finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".bak"))File.Delete(path+".bak");if(System.IO.Directory.Exists(dir))System.IO.Directory.Delete(dir);}
    }
}
