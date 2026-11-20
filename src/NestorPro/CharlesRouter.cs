using System.Diagnostics;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace KeyboardToGamepad;

// Independent physical-input router, using the upstream Interception wrapper.
// Only this worker owns the virtual report. Physical input is never mirrored
// for an action handled by this layer. No game memory is read.
internal sealed class CharlesRouter : IDisposable
{
    private readonly ViGEmClient client;
    private readonly IXbox360Controller pad;
    private EffectLevels effects=new(0,0,0);
    private IntPtr context;
    private Thread? thread;
    private volatile bool running;
    internal volatile bool Active;
    internal volatile bool AutomaticEnabled=true;
    internal volatile bool PreviewEnabled,PreviewActive;
    internal volatile string Mode="Jogo";
    internal volatile string Error="";
    // One base gain is required to convert mouse counts into stick units.
    // Direct output keeps mouse gain separate from optional effects.
    internal float MouseGainX=394,MouseGainY=394,AdsGainX=394,AdsGainY=394;
    internal int MouseDeadzone=2500;
    internal short RightX,RightY;
    internal volatile bool NativeInventory=true, NativeShop=false;
    internal volatile string InventoryDropOutput="A";
    internal volatile string LastRoute="Nenhum comando";
    private InputBindings bindings;
    private InputBindings? pendingBindings;
    private readonly Dictionary<(int,int),int> keyOwners=new();
    private readonly NativeKeyPairs nativeKeys=new();
    private readonly MousePairs mousePairs=new();
    private readonly MouseStick mouseStick=new();
    private readonly HeldActions held=new();
    private readonly HashSet<(int,int)> swallowed=new();
    private readonly HashSet<(int,int)> shiftKeys=new();
    private readonly Interception.Predicate predicate=d=>d>=1&&d<=20?1:0;
    private readonly InteractionInput interaction=new();
    private string pulse="";
    private readonly MenuPulses pulses=new();
    private bool homeHeld;
    private IntPtr gameWindow;
    private IntPtr inspectedWindow;
    private bool inspectedGame;
    private long inspectAt;
    internal long Physical,Consumed,Reports;
    private long diagnosticAt;
    internal static void Diagnostic(string message)
    {
        try {Directory.CreateDirectory(CharlesSettingsStore.DirectoryPath);File.AppendAllText(Path.Combine(CharlesSettingsStore.DirectoryPath,"diagnostico-ds4.log"),DateTime.Now.ToString("O")+" "+message+Environment.NewLine);}catch{}
    }
    internal CharlesRouter(CharlesSettings? settings=null)
    {
        settings??=new CharlesSettings();bindings=settings.Validate();
        MouseGainX=settings.Hip;MouseGainY=settings.EffectiveHipY;AdsGainX=settings.EffectiveAdsX;AdsGainY=settings.EffectiveAdsY;MouseDeadzone=settings.Deadzone;NativeInventory=settings.NativeInventory;NativeShop=settings.NativeShop;
        InventoryDropOutput=settings.DropOutput;
        Diagnostic("Inicializando ViGEm");client=new ViGEmClient();
        effects=settings.Effects;
        pad=client.CreateXbox360Controller();pad.AutoSubmitReport=false;
        pad.Connect();pad.SubmitReport();Diagnostic("XInput direto conectado; iniciando captura");
        try
        {
            context=Interception.interception_create_context();
            if(context==IntPtr.Zero)throw new InvalidOperationException("Interception indisponível.");
            Interception.interception_set_filter(context,predicate,0xffff);
            running=true;thread=new Thread(Loop){IsBackground=true,Name="Charles-input-router"};thread.Start();Diagnostic("Captura iniciada");
        }
        catch {pad.Disconnect();client.Dispose();throw;}
    }
    private static long Now=>Environment.TickCount64;
    private void Clear(){held.Clear();shiftKeys.Clear();pulses.Reset();mouseStick.Reset();interaction.Reset();mousePairs.ResetPressed();pulse="";Mode="Jogo";}
    internal void ApplySettings(CharlesSettings settings)
    {
        var validated=settings.Validate();
        MouseGainX=settings.Hip;MouseGainY=settings.EffectiveHipY;AdsGainX=settings.EffectiveAdsX;AdsGainY=settings.EffectiveAdsY;MouseDeadzone=settings.Deadzone;NativeInventory=settings.NativeInventory;NativeShop=settings.NativeShop;
        InventoryDropOutput=settings.DropOutput;
        SetEffects(settings.Effects);
        Interlocked.Exchange(ref pendingBindings,validated);
    }
    internal void SetEffects(EffectLevels levels){levels.Validate();Volatile.Write(ref effects,levels);}
    internal void Toggle(){AutomaticEnabled=!AutomaticEnabled;Active=false;}
    private (float X,float Y) CameraGains(bool aiming)=>aiming?(AdsGainX,AdsGainY):(MouseGainX,MouseGainY);
    // Readings in DS4 are independent of game capture: the cursor stays native.
    internal static bool ShouldPreview(bool enabled,bool ds4Focus)=>enabled&&ds4Focus;
    internal static bool IsWarzoneProcess(string name)=>name.Equals("cod",StringComparison.OrdinalIgnoreCase)||name.Equals("cod25-cod",StringComparison.OrdinalIgnoreCase)||name.Equals("ModernWarfare",StringComparison.OrdinalIgnoreCase);
    private void RefreshAutomaticFocus()
    {
        if(Now<inspectAt)return;
        inspectAt=Now+50;
        var window=GetForegroundWindow();
        if(window!=inspectedWindow)
        {
            inspectedWindow=window;inspectedGame=false;
            GetWindowThreadProcessId(window,out uint process);
            if(window!=IntPtr.Zero&&process!=(uint)Environment.ProcessId)
            {
                try {using var foreground=Process.GetProcessById((int)process);inspectedGame=IsWarzoneProcess(foreground.ProcessName);}
                catch(ArgumentException){} catch(System.ComponentModel.Win32Exception){} catch(InvalidOperationException){}
            }
        }
        gameWindow=inspectedGame?window:IntPtr.Zero;
        Active=AutomaticEnabled&&inspectedGame;
        PreviewActive=ShouldPreview(PreviewEnabled,window!=IntPtr.Zero&&ProcessIsPanel(window));
    }
    private static bool ProcessIsPanel(IntPtr window)
    {
        GetWindowThreadProcessId(window,out uint id);return id==(uint)Environment.ProcessId;
    }
    internal bool ActivateForeground()
    {
        var window=GetForegroundWindow();
        GetWindowThreadProcessId(window,out uint process);
        if(window==IntPtr.Zero||process==(uint)Environment.ProcessId)return false;
        gameWindow=window;Active=true;return true;
    }
    private void Loop()
    {
        bool previous=false,previousPreview=false;
        try
        {
            while(running)
            {
                var replacement=Interlocked.Exchange(ref pendingBindings,null);
                if(replacement!=null){Clear();bindings=replacement;}
                RefreshAutomaticFocus();
                if(previousPreview!=PreviewActive){Clear();previousPreview=PreviewActive;}
                if(previous!=Active){Clear();previous=Active;}
                if(Active&&gameWindow!=IntPtr.Zero&&GetForegroundWindow()!=gameWindow){Active=false;Clear();previous=false;}
                int device=Interception.interception_wait_with_timeout(context,4);
                if(device>0)
                {
                    Interception.Stroke stroke=default;
                    if(Interception.interception_receive(context,device,ref stroke,1)>0)
                    {
                        Physical++;
                        bool consume=device<=10?Key(device,stroke.Key):Mouse(ref stroke.Mouse);
                        if(consume)Consumed++;else Interception.interception_send(context,device,ref stroke,1);
                    }
                }
                if(Active&&Mode=="Jogo"&&interaction.MenuCandidate(Now)&&CursorVisible())
                {EnterMode("Loja");}
                Submit();
                if(Now>=diagnosticAt){diagnosticAt=Now+10000;Diagnostic($"ativo={Active} automatico={AutomaticEnabled} preview={PreviewActive} modo={Mode} entradas={Physical} relatorios={Reports} X={RightX} Y={RightY}");}
            }
        }
        catch(Exception ex){Error=ex.Message;Active=false;Diagnostic("Falha: "+ex);}
        finally
        {
            // Removing the filter restores native input even when output fails.
            if(context!=IntPtr.Zero)Interception.interception_set_filter(context,predicate,0);
        }
    }
    private bool Key(int device,Interception.KeyStroke stroke)
    {
        bool up=(stroke.State&1)!=0;
        int code=stroke.Code|((stroke.State&2)!=0?0x100:0);
        if(code is 0x2a or 0x36){if(up)shiftKeys.Remove((device,code));else shiftKeys.Add((device,code));}
        if(stroke.Code==0x47)Diagnostic($"HOME codigo={code:X} soltou={up}");
        if(nativeKeys.Continue(device,code,up))return false;
        bool consumed=MappedKey(device,code,up);
        if(!consumed)nativeKeys.Begin(device,code,up);
        return consumed;
    }
    private bool MappedKey(int device,int physical,bool up)
    {
        var identity=(device,physical);
        int logical=keyOwners.TryGetValue(identity,out int owned)?owned:physical is 0x147 or 0x01?physical:VirtualShop&&physical==1001?0x39:Mode!="Jogo"?ResolveMenu(physical):bindings.Resolve(physical);
        if(logical<0)return false;
        bool consumed=RouteKey(logical,identity,up);
        if(up)keyOwners.Remove(identity);else if(consumed)keyOwners.TryAdd(identity,logical);
        return consumed;
    }
    private static int ResolveMenu(int physical)=>physical switch
    {
        0x11 or 0x48 or 0x148=>0x11, 0x1f or 0x50 or 0x150=>0x1f,
        0x1e or 0x4b or 0x14b=>0x1e, 0x20 or 0x4d or 0x14d=>0x20,
        0x39=>0x39, 0x21=>0x21, 0x13=>0x13, 0x0f=>0x0f, 0x01=>0x01, _=>-1
    };
    private void EnterMode(string mode)
    {
        Mode=mode;held.Clear();pulses.Reset();interaction.Reset();mouseStick.Reset();
        mousePairs.ResetPressed();
    }
    private bool NativeMenu=>Mode=="Inventário"?NativeInventory:Mode=="Loja"&&NativeShop;
    private bool VirtualShop=>Active&&Mode=="Loja"&&!NativeShop;
    private bool RouteKey(int scan,(int,int) identity,bool up)
    {
        if(scan==0x147) // HOME remains reserved for pause/resume.
        {if(!up&&!homeHeld){Toggle();inspectAt=0;RefreshAutomaticFocus();}homeHeld=!up;return true;}
        if(up&&swallowed.Remove(identity)){held.Set(identity,scan,true);if(scan==0x21)interaction.Key(held.Contains(0x21),Now);return true;}
        if(!Active)return false;
        if(!up&&swallowed.Contains(identity)&&held.Fresh(identity))return true;
        // TAB uses the backpack command of the virtual controller, without a
        // second native TAB or repeated presses while the key remains held.
        if(scan==0x0f)
        {
            bool freshTab=held.Fresh(identity);
            if(up)held.Set(identity,scan,true);else swallowed.Add(identity);
            if(!up&&freshTab)
            {
                EnterMode(Mode=="Inventário"?"Jogo":"Inventário");
                held.Set(identity,scan,false);
                Pulse("Down",95);
                LastRoute=ButtonCatalog.InputName(identity.Item2)+" → mochila: pulso único de 95 ms";
            }
            return true;
        }
        // ESC remains native, without a duplicate controller B command.
        if(scan==0x01)
        {
            bool freshMenu=held.Fresh(identity);
            held.Set(identity,scan,up);
            if(!up&&freshMenu)
            {
                EnterMode("Jogo");
                held.Set(identity,scan,false);
            }
            return false;
        }
        // Drop is independent of navigation policy: do not send R and RS together.
        // Outside inventory R continues to reload; F is unaffected.
        if(Mode=="Inventário"&&scan==0x13)
        {
            if(InventoryDropOutput=="Native")
            {
                if(!up)LastRoute="Inventário: R → teclado nativo (sem controle)";
                return false;
            }
            bool first=held.Fresh(identity);
            held.Set(identity,scan,up);if(!up)swallowed.Add(identity);
            string output=InventoryDropOutput=="A"&&shiftKeys.Count>0?"RS":InventoryDropOutput;
            if(!up&&first)Pulse(output,90);
            if(!up)LastRoute=$"Inventário: R → {output}, pulso único de 90 ms";
            return true;
        }
        // The inventory is a controller menu with a second row for ammunition.
        // Keep its native mouse and other keys, but navigate both axes with
        // the virtual D-pad; native WASD do not select controller item slots.
        if(Mode=="Inventário"&&NativeInventory&&scan is 0x11 or 0x1f or 0x1e or 0x20)
        {
            bool first=held.Fresh(identity);
            held.Set(identity,scan,up);if(!up)swallowed.Add(identity);
            string direction=scan switch {0x11=>"Up",0x1f=>"Down",0x1e=>"Left",_=>"Right"};
            if(!up&&first)Pulse(direction,70);
            if(!up)LastRoute=$"Inventário: {ButtonCatalog.InputName(identity.Item2)} → direcional {direction}";
            return true;
        }
        if(Mode!="Jogo"&&NativeMenu)
        {
            if(!up)LastRoute=$"{Mode}: tecla 0x{scan:X2} → teclado nativo (sem controle)";
            return false;
        }
        bool fresh=held.Fresh(identity);
        held.Set(identity,scan,up);if(!up)swallowed.Add(identity);
        if(scan==0x21&&Mode=="Jogo")interaction.Key(!up,Now);
        if(!up&&fresh)
        {
            if(Mode!="Jogo")
            {
                if(scan is 0x11 or 0x48)Pulse("Up",45);
                if(scan is 0x1f or 0x50)Pulse("Down",45);
                // In the shop D-pad Left is also the game's fire-mode button.
                // Navigate its columns with the left stick so buying cannot
                // switch the weapon from automatic to single-shot.
                if(scan is 0x1e or 0x4b)Pulse(Mode=="Loja"?"ShopLeft":"Left",45);
                if(scan is 0x20 or 0x4d)Pulse(Mode=="Loja"?"ShopRight":"Right",45);
                if(scan is 0x39 or 0x21 or 0x200)Pulse("A",90);
            }
        }
        if(!up)LastRoute=$"{Mode}: {ButtonCatalog.InputName(identity.Item2)} → {ButtonCatalog.Actions.First(a=>a.Code==scan).Name}";
        return true;
    }
    private bool Mouse(ref Interception.MouseStroke m)
    {
        // Absolute pointing devices are passed through rather than treated as deltas.
        if((m.Flags&1)!=0)return false;
        // Preview only while DS4Windows has focus. Keep the native cursor and
        // clicks available for editing; no game actions or recoil are generated.
        if(PreviewActive){mouseStick.Add(m.X,m.Y);return false;}
        bool play=Active&&Mode=="Jogo";
        bool shop=VirtualShop;
        ushort original=m.State;
        int[] inputs={1001,1002,1004,1005};int[] downs={1,4,0x40,0x100};
        int mask=0;for(int i=0;i<inputs.Length;i++)if(bindings.Resolve(inputs[i])>=0)mask|=1<<i;
        // Keep click ownership until release, including when crossing a menu.
        // In controller shop mode primary click confirms A; cursor deltas must
        // not select a different item through native mouse hover.
        m.State=mousePairs.Route(m.State,play||shop,shop?1:mask);
        for(int i=0;i<inputs.Length;i++)
        {
            int down=downs[i],up=down<<1;
            if((original&down)!=0&&(m.State&down)==0)MappedKey(-1,inputs[i],false);
            if((original&up)!=0&&(m.State&up)==0)MappedKey(-1,inputs[i],true);
        }
        if(play){mouseStick.Add(m.X,m.Y);m.X=m.Y=0;return m.State==0;}
        if(shop){m.X=m.Y=0;m.State=(ushort)(m.State&~0xc00);m.Rolling=0;return m.State==0;}
        return false;
    }
    private void Pulse(string action,int duration){pulses.Push(action,duration);}
    private static short ShopHorizontalAxis(string mode,string action)=>mode=="Loja"?(short)(action switch
    {
        "ShopLeft"=>-32767,"ShopRight"=>32767,_=>0
    }):(short)0;
    private void Submit()
    {
        pulse=pulses.At(Now);
        bool play=Active&&Mode=="Jogo";
        bool H(int c)=>play&&held.Contains(c);
        bool P(string p)=>Active&&pulse==p;
        foreach(var b in new[]{Xbox360Button.A,Xbox360Button.B,Xbox360Button.X,Xbox360Button.Y,Xbox360Button.Up,Xbox360Button.Down,Xbox360Button.Left,Xbox360Button.Right,Xbox360Button.Back,Xbox360Button.Start,Xbox360Button.LeftThumb,Xbox360Button.RightThumb,Xbox360Button.LeftShoulder,Xbox360Button.RightShoulder})pad.SetButtonState(b,false);
        pad.SetButtonState(Xbox360Button.A,H(0x39)||P("A"));pad.SetButtonState(Xbox360Button.B,H(0x1d)||P("B"));
        pad.SetButtonState(Xbox360Button.X,H(0x13)||interaction.Output(Active));pad.SetButtonState(Xbox360Button.Y,H(0x02)||P("Y"));
        pad.SetButtonState(Xbox360Button.LeftThumb,H(0x2a));pad.SetButtonState(Xbox360Button.RightThumb,H(0x2f)||P("RS"));
        pad.SetButtonState(Xbox360Button.LeftShoulder,H(0x10));pad.SetButtonState(Xbox360Button.RightShoulder,H(0x12));
        pad.SetButtonState(Xbox360Button.Back,P("Back"));
        pad.SetButtonState(Xbox360Button.Start,H(0x1c));
        pad.SetButtonState(Xbox360Button.Up,H(0x48)||H(0x38)||P("Up"));pad.SetButtonState(Xbox360Button.Down,H(0x50)||P("Down"));pad.SetButtonState(Xbox360Button.Left,H(0x4b)||P("Left"));pad.SetButtonState(Xbox360Button.Right,H(0x4d)||P("Right"));
        short leftX=play?(short)(((H(0x20)?1:0)-(H(0x1e)?1:0))*32767):ShopHorizontalAxis(Mode,pulse);
        short leftY=(short)(((H(0x11)?1:0)-(H(0x1f)?1:0))*32767);
        bool aiming=H(0x201),firing=H(0x200);
        bool camera=play||PreviewActive;
        var gains=CameraGains(aiming);
        if(camera)mouseStick.Update(Stopwatch.GetTimestamp(),gains.X,gains.Y);else mouseStick.Reset();
        RightX=camera?mouseStick.X:(short)0;RightY=camera?mouseStick.Y:(short)0;
        DirectEffects.Apply(Volatile.Read(ref effects),play,aiming,firing,Now,
            ref leftX,ref leftY,ref RightX,ref RightY);
        pad.SetAxisValue(Xbox360Axis.LeftThumbX,leftX);
        pad.SetAxisValue(Xbox360Axis.LeftThumbY,leftY);
        pad.SetAxisValue(Xbox360Axis.RightThumbX,RightX);
        pad.SetAxisValue(Xbox360Axis.RightThumbY,RightY);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger,aiming?(byte)255:(byte)0);
        pad.SetSliderValue(Xbox360Slider.RightTrigger,firing?(byte)255:(byte)0);
        pad.SubmitReport();Reports++;
    }
    public void Dispose()
    {
        running=false;
        if(thread!=null&&!thread.Join(2000))return;
        if(context!=IntPtr.Zero){Interception.interception_destroy_context(context);context=IntPtr.Zero;}
        pad.Disconnect();client.Dispose();
    }
    // Isolate routing checks from the physical driver and virtual device while testing.
    private CharlesRouter(bool testOnly)
    {client=null!;pad=null!;bindings=new CharlesSettings().Validate();Active=true;}
    internal static void TestRoutes()
    {
        if(!IsWarzoneProcess("cod")||!IsWarzoneProcess("cod25-cod")||IsWarzoneProcess("codex"))throw new Exception("O jogo atualizado não foi reconhecido corretamente.");
        var preview=new CharlesRouter(testOnly:true){Active=false,PreviewEnabled=true,PreviewActive=true};
        var previewMouse=new Interception.MouseStroke{X=31,Y=-17,State=1};
        if(preview.Mouse(ref previewMouse)||previewMouse.X!=31||previewMouse.Y!=-17||previewMouse.State!=1||preview.held.Contains(0x200))throw new Exception("Prévia DS4 prendeu cursor ou acionou disparo.");
        preview.Toggle();if(!preview.PreviewActive||!preview.PreviewEnabled||preview.AutomaticEnabled)throw new Exception("HOME alterou leitura DS4 ou não pausou o jogo.");
        if(ShouldPreview(true,false)||ShouldPreview(false,true)||!ShouldPreview(true,true))throw new Exception("Prévia DS4 ignorou foco ou configuração.");
        preview.Toggle();if(!preview.AutomaticEnabled||!ShouldPreview(preview.PreviewEnabled,true))throw new Exception("HOME não retomou jogo preservando prévia DS4.");
        TestShopMouse();
        var r=new CharlesRouter(testOnly:true);
        r.MouseGainX=2000;r.MouseGainY=2100;r.AdsGainX=2500;r.AdsGainY=2600;
        if(r.CameraGains(false)!=(2000f,2100f)||r.CameraGains(true)!=(2500f,2600f))throw new Exception("ADS alterou o giro sem mirar ou ignorou o ganho próprio.");
        bool Key(int scan,bool up=false)=>r.Key(1,new Interception.KeyStroke{Code=(ushort)(scan&255),State=(ushort)((up?1:0)|((scan&0x100)!=0?2:0))});
        if(!Key(0x21)||!r.interaction.Output(true)||!Key(0x21)||!r.interaction.Output(true))throw new Exception("Interação segurada interrompida por repetição.");
        if(!Key(0x21,true)||r.interaction.Output(true))throw new Exception("Interação não terminou na soltura.");
        r.Clear();Key(0x0f);Key(0x0f);if(r.Mode!="Inventário"||r.pulses.At(0)!="Down")throw new Exception("Mochila não abriu ou repetiu ao segurar.");
        Key(0x0f,true);r.pulses.Reset();Key(0x13);Key(0x13);
        if(r.pulses.At(0)!="A"||r.pulses.At(90)!=""||r.pulses.At(130)!="")throw new Exception("Descarte não enviou pulso único em A.");
        Key(0x13,true);r.pulses.Reset();
        if(!Key(0x1e)||!Key(0x1e)||r.pulses.At(0)!="Left"||r.pulses.At(70)!=""||r.pulses.At(110)!="")throw new Exception("A repetiu ou não navegou à esquerda no inventário.");
        Key(0x1e,true);r.pulses.Reset();
        if(!Key(0x20)||r.pulses.At(0)!="Right"||r.pulses.At(70)!="")throw new Exception("D não navegou à direita no inventário.");
        Key(0x20,true);r.pulses.Reset();
        if(!Key(0x14b)||r.pulses.At(0)!="Left")throw new Exception("Seta esquerda não navegou no inventário.");
        Key(0x14b,true);r.pulses.Reset();
        if(!Key(0x14d)||r.pulses.At(0)!="Right")throw new Exception("Seta direita não navegou no inventário.");
        Key(0x14d,true);r.pulses.Reset();
        if(!Key(0x1f)||!Key(0x1f)||r.pulses.At(0)!="Down"||r.pulses.At(70)!="")throw new Exception("S repetiu ou não desceu até a linha de munição.");
        Key(0x1f,true);r.pulses.Reset();
        if(!Key(0x11)||r.pulses.At(0)!="Up")throw new Exception("W não retornou à linha superior do inventário.");
        Key(0x11,true);r.pulses.Reset();
        Key(0x2a);Key(0x13);if(r.pulses.At(0)!="RS"||r.pulses.At(90)!="")throw new Exception("Shift+R não acionou soltar tudo.");
        Key(0x13,true);Key(0x2a,true);r.pulses.Reset();
        Key(0x01);Key(0x01,true);if(r.Mode!="Jogo")throw new Exception("ESC não retornou ao jogo.");
        r.EnterMode("Loja");Key(0x1f);Key(0x1f,true);Key(0x39);
        if(r.pulses.At(0)!="Down"||r.pulses.At(45)!=""||r.pulses.At(85)!="A"||r.pulses.At(130)!="A"||r.pulses.At(170)!="A")throw new Exception("Loja ou confirmação por Espaço mudou o comportamento aprovado.");
        Key(0x39,true);r.pulses.Reset();
        Key(0x1f);Key(0x1f);
        if(r.pulses.At(0)!="Down")throw new Exception("Loja repetiu a direção na mesma pressão.");
        Key(0x1f,true);r.pulses.Reset();Key(0x1f);if(r.pulses.At(0)!="Down")throw new Exception("Loja não aceitou a próxima pressão.");Key(0x1f,true);
        r.pulses.Reset();Key(0x1e);if(r.pulses.At(0)!="ShopLeft"||ShopHorizontalAxis(r.Mode,r.pulses.At(0))!=-32767)throw new Exception("A na loja deve usar o analógico esquerdo, sem mudar o modo de disparo.");
        Key(0x1e,true);r.pulses.Reset();Key(0x20);if(r.pulses.At(0)!="ShopRight"||ShopHorizontalAxis(r.Mode,r.pulses.At(0))!=32767)throw new Exception("D na loja deve usar o analógico esquerdo.");
        Key(0x20,true);r.pulses.Reset();Key(0x14b);if(r.pulses.At(0)!="ShopLeft")throw new Exception("Seta esquerda na loja não foi isolada do modo de disparo.");Key(0x14b,true);
        r.EnterMode("Jogo");if(ShopHorizontalAxis(r.Mode,"ShopLeft")!=0||ShopHorizontalAxis("Inventário","ShopLeft")!=0)throw new Exception("Navegação da loja vazou para jogo ou inventário.");
        r.Clear();Key(0x1d);r.MappedKey(-1,1004,false);Key(0x1d,true);
        if(!r.held.Contains(0x1d))throw new Exception("Alias do mouse perdeu botão segurado.");r.MappedKey(-1,1004,true);
        if(r.held.Contains(0x1d))throw new Exception("Alias do mouse ficou preso.");
        var settings=new CharlesSettings();settings.Bindings[0x21]=new[]{0x22,0};
        Key(0x21);r.Clear();r.bindings=settings.Validate();Key(0x21,true);
        if(r.interaction.Output(true)||Key(0x21))throw new Exception("Remapeamento deixou interação presa ou manteve a tecla antiga.");
        Key(0x21,true);if(!Key(0x22)||!r.interaction.Output(true))throw new Exception("Tecla de interação personalizada não foi aplicada.");Key(0x22,true);
        r.Clear();settings.Bindings[0x200]=new[]{0x2c,0};settings.Bindings[0x201]=new[]{0x2d,0};r.bindings=settings.Validate();
        Key(0x2c);Key(0x2d);if(!r.held.Contains(0x200)||!r.held.Contains(0x201))throw new Exception("Mirar e atirar não aceitaram teclado.");Key(0x2c,true);Key(0x2d,true);
        r.Clear();r.bindings=new CharlesSettings().Validate();
        var m=new Interception.MouseStroke{State=1};if(!r.Mouse(ref m)||!r.held.Contains(0x200))throw new Exception("Clique de disparo não foi mapeado.");
        r.EnterMode("Inventário");m=new Interception.MouseStroke{State=2};r.Mouse(ref m);if(m.State!=0||r.held.Contains(0x200))throw new Exception("Soltura do disparo vazou para inventário.");
        m=new Interception.MouseStroke{State=4};if(r.Mouse(ref m)||m.State!=4)throw new Exception("Clique nativo foi consumido no inventário.");
        r.EnterMode("Jogo");m=new Interception.MouseStroke{State=8};if(r.Mouse(ref m)||m.State!=8||r.held.Contains(0x201))throw new Exception("Clique nativo virou mira após trocar camada.");
        r.EnterMode("Inventário");r.InventoryDropOutput="Native";
        Key(0x20);Key(0x20,true);r.pulses.Reset();
        if(Key(0x13)||Key(0x13,true)||r.pulses.At(0)!="")throw new Exception("R nativo foi consumido ou duplicado depois da navegação lateral.");
        r.Clear();r.Active=false;if(Key(0x21))throw new Exception("Mapeamento consumiu entrada fora do jogo.");Key(0x21,true);
    }
    private static void TestShopMouse()
    {
        var r=new CharlesRouter(testOnly:true);
        r.EnterMode("Loja");
        r.Key(1,new Interception.KeyStroke{Code=0x1f});
        r.Key(1,new Interception.KeyStroke{Code=0x1f,State=1});
        var m=new Interception.MouseStroke{X=3,Y=-2};
        if(!r.Mouse(ref m)||m.X!=0||m.Y!=0)throw new Exception("Cursor nativo pode trocar a seleção da loja.");
        r.Key(1,new Interception.KeyStroke{Code=0x39});
        r.Key(1,new Interception.KeyStroke{Code=0x39});
        r.Key(1,new Interception.KeyStroke{Code=0x39,State=1});
        if(r.pulses.At(0)!="Down"||r.pulses.At(45)!=""||r.pulses.At(85)!="A"||r.pulses.At(175)!=""||r.pulses.At(215)!="")throw new Exception("Compra não confirmou exatamente uma vez após navegar.");
        r.pulses.Reset();m=new Interception.MouseStroke{State=1,X=7,Y=2};
        if(!r.Mouse(ref m)||m.State!=0||m.X!=0||m.Y!=0)throw new Exception("Clique da compra vazou nativo.");
        m=new Interception.MouseStroke{State=1};r.Mouse(ref m); // duplicate down cannot repeat purchase
        m=new Interception.MouseStroke{State=2};if(!r.Mouse(ref m))throw new Exception("Soltura da compra vazou.");
        if(r.pulses.At(0)!="A"||r.pulses.At(90)!=""||r.pulses.At(130)!="")throw new Exception("Clique repetiu compra.");
        m=new Interception.MouseStroke{State=0x400,Rolling=120};
        if(!r.Mouse(ref m)||m.State!=0||m.Rolling!=0)throw new Exception("Rolagem trocou seleção da loja virtual.");
        // Native inventory, native shop and inactive mouse must retain their deltas and clicks.
        foreach(string mode in new[]{"Inventário","Loja","Jogo"})
        {
            r=new CharlesRouter(testOnly:true);r.NativeShop=true;r.EnterMode(mode);if(mode=="Jogo")r.Active=false;
            m=new Interception.MouseStroke{State=1,X=9,Y=-4};
            if(r.Mouse(ref m)||m.State!=1||m.X!=9||m.Y!=-4)throw new Exception("Correção da loja alterou mouse nativo fora dela.");
            // A native press still receives its native release after entering controller shop.
            r.Active=true;r.NativeShop=false;r.EnterMode("Loja");m=new Interception.MouseStroke{State=2};
            if(r.Mouse(ref m)||m.State!=2||r.pulses.At(0)!="")throw new Exception("Compra converteu um clique já iniciado nativo.");
        }
        r=new CharlesRouter(testOnly:true);m=new Interception.MouseStroke{State=1};r.Mouse(ref m);
        r.EnterMode("Loja");m=new Interception.MouseStroke{State=2};
        if(!r.Mouse(ref m)||r.pulses.At(0)!=""||r.held.Contains(0x200))throw new Exception("Soltar disparo gerou compra indevida.");
    }
    [StructLayout(LayoutKind.Sequential)]private struct Cursor {public int Size,Flags;public IntPtr Handle;public int X,Y;}
    [DllImport("user32.dll")]private static extern bool GetCursorInfo(ref Cursor c);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    private static bool CursorVisible(){var c=new Cursor{Size=Marshal.SizeOf<Cursor>()};return GetCursorInfo(ref c)&&(c.Flags&1)!=0;}
}
