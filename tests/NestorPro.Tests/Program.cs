using KeyboardToGamepad;
using System.Text.Json;
using System.Diagnostics;
static (short,short,short,short) Apply(EffectLevels l,bool game,bool ads,bool fire,long time,short lx=0,short ly=0,short rx=0,short ry=0)
{DirectEffects.Apply(l,game,ads,fire,time,ref lx,ref ly,ref rx,ref ry);return(lx,ly,rx,ry);}
static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
var all=new EffectLevels(19,8,5);
foreach(bool ads in new[]{false,true})foreach(bool fire in new[]{false,true})
    Check(Apply(all,false,ads,fire,0,100,-50,200,-90)==(100,-50,200,-90),"Efeito vazou para menu/cursor livre.");
Check(Apply(all,true,false,true,0)==(0,0,0,0),"Efeito sem ADS.");
Check(Apply(new(0,0,0),true,true,true,0,100,200,300,400)==(100,200,300,400),"Zero não desliga.");
Check(Apply(new(19,0,0),true,true,false,0)==(0,0,0,0),"Recoil sem tiro.");
Check(Apply(new(19,0,0),true,true,true,0)==(0,0,0,-4864),"Recoil: sinal, escala ou eixo incorreto.");
Check(Apply(new(19,0,0),true,true,true,0,1000,2000,3000,-32000)==(1000,2000,3000,short.MinValue),"Recoil alterou movimento ou excedeu limite.");
Check(Apply(new(0,8,0),true,true,false,0)==(2048,0,0,0),"Pulso LS ausente.");
Check(Apply(new(0,8,0),true,true,false,180)==(-2048,0,0,0),"LS não alternou.");
Check(Apply(new(0,8,0),true,true,false,0,32767,0)==(32767,0,0,0),"LS substituiu A/D.");
Check(Apply(new(0,8,0),true,true,false,0,0,32767)==(0,32767,0,0),"LS substituiu W/S.");
Check(Apply(new(0,0,5),true,true,false,0)==(0,0,1280,0),"RS pulso/sinal errado.");
Check(Apply(new(0,0,5),true,true,false,400)==(0,0,-1280,0),"RS não alternou.");
Check(Apply(new(0,0,5),true,true,false,0,0,0,4000,100)==(0,0,4000,100),"RS substituiu mouse horizontal.");
Check(Apply(new(0,0,5),true,true,false,0,0,0,100,4000)==(0,0,100,4000),"RS substituiu mouse vertical.");
Check(Apply(all,true,true,true,0)==(2048,0,1280,-4864),"As três barras interferem entre si.");
Check(Apply(all,true,true,true,400)==(2048,0,-1280,-4864),"Ciclos independentes incorretos.");
var random=new Random(9);
for(int i=0;i<10000;i++)
{
    short lx=(short)random.Next(-32768,32768),ly=(short)random.Next(-32768,32768),rx=(short)random.Next(-32768,32768),ry=(short)random.Next(-32768,32768);
    Check(Apply(new(0,0,0),true,true,true,i,lx,ly,rx,ry)==(lx,ly,rx,ry),"Modo desligado alterou eixo.");
    Check(Apply(all,false,true,true,i,lx,ly,rx,ry)==(lx,ly,rx,ry),"Menu com efeitos.");
}
var s=JsonSerializer.Deserialize<CharlesSettings>("{\"Hip\":2000,\"HipY\":2100,\"AdsX\":2200,\"AdsY\":2300,\"DropOutput\":\"RS\"}")!;
s.Validate();Check(s.Effects==new EffectLevels(0,0,0),"Migração ligou efeito por conta própria.");
s.RecoilLevel=19;s.AimMotionLeft=8;s.AimMotionRight=5;
var saved=JsonSerializer.Deserialize<CharlesSettings>(JsonSerializer.Serialize(s))!;
Check(saved.Effects==all&&saved.Hip==2000&&saved.EffectiveAdsY==2300&&saved.DropOutput=="RS","Salvar efeitos perdeu ajustes.");
saved.AimMotionRight=9;bool invalid=false;try{saved.Validate();}catch(InvalidDataException){invalid=true;}
Check(invalid,"Barra fora do intervalo aceita.");
MouseStick.Test();
long T(int ms)=>(long)(ms*Stopwatch.Frequency/1000d);
foreach(int interval in new[]{1,2,4,8,10,16})
{
    var m=new MouseStick();m.Update(T(1000),1000,1000);
    for(int t=1;t<=600;t++)
    {if(t%interval==0)m.Add(interval,0);m.Update(T(1000+t),1000,1000);if(t>100)Check(m.X==1000,"Regressão de cadência do mouse.");}
    for(int t=601;t<640;t++)m.Update(T(1000+t),1000,1000);
    Check(m.X==0&&m.Y==0,"Mouse não parou.");
}
Console.WriteLine("PASS: efeitos, 20000 estados sem interferência, eixos independentes, gatilhos, persistência e 6 cadências do mouse.");
