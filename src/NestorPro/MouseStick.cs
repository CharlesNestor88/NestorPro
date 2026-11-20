using System.Diagnostics;

namespace KeyboardToGamepad;

// Only converts relative mouse motion to one virtual right stick. Curves,
// ADS-specific limits and recoil belong to the downstream DS4Windows profile.
internal sealed class MouseStick
{
    // Experimental mouse conversion window; not end-to-end input latency.
    private const double SampleMs=3;
    private static readonly double TicksPerMs=Stopwatch.Frequency/1000.0;
    private long sampleStart;
    private long sumX,sumY;
    private int dx,dy;
    private long lastMotion;
    private readonly double[] intervals=new double[5];
    private int intervalCount,intervalIndex;
    private double packetMs;
    private bool paced;
    internal short X,Y;

    internal void Add(int x,int y){dx+=x;dy+=y;}

    internal void Update(long now,float gainX,float gainY)
    {
        if(sampleStart==0)sampleStart=now;
        int frameX=dx,frameY=dy;
        bool motion=frameX!=0||frameY!=0;
        sumX+=dx;sumY+=dy;dx=dy=0;
        if(motion)
        {
            double gap=lastMotion==0?0:(now-lastMotion)/TicksPerMs;
            if(gap>=0.25&&gap<=20)
            {
                intervals[intervalIndex]=gap;
                intervalIndex=(intervalIndex+1)%intervals.Length;
                intervalCount=Math.Min(intervalCount+1,intervals.Length);
                if(intervalCount>=3)
                {
                    Span<double> sorted=stackalloc double[5];
                    intervals.AsSpan(0,intervalCount).CopyTo(sorted);
                    sorted=sorted[..intervalCount];sorted.Sort();
                    packetMs=sorted[intervalCount/2];
                    bool nextPaced=packetMs>=1.5;
                    if(nextPaced&&!paced)
                    {
                        // Start from the previous packet, not an empty 3-ms window.
                        sampleStart=lastMotion;sumX=frameX;sumY=frameY;
                    }
                    paced=nextPaced;
                }
            }
            if(paced&&gap>StopAfterMs)
            {
                // Do not dilute the first movement after a genuine pause.
                sampleStart=now-(long)(packetMs*TicksPerMs);
                sumX=frameX;sumY=frameY;
            }
            lastMotion=now;
        }
        double elapsed=(now-sampleStart)/TicksPerMs;
        if(paced)
        {
            // Slow-reporting mice need to hold velocity between packets. Empty
            // output windows are not evidence that the hand stopped moving.
            if(!motion&&lastMotion!=0&&(now-lastMotion)/TicksPerMs>=StopAfterMs)
            {
                X=Y=0;sumX=sumY=0;sampleStart=now;
                return;
            }
            if(!motion)return;
        }
        if(elapsed<(paced?0.25:SampleMs))return;
        double x=sumX/elapsed*gainX;
        double y=-sumY/elapsed*gainY;
        X=(short)Math.Round(Math.Clamp(x,-32767d,32767d));
        Y=(short)Math.Round(Math.Clamp(y,-32767d,32767d));
        sumX=sumY=0;
        sampleStart=now;
    }

    private double StopAfterMs=>Math.Clamp(packetMs*1.75,3,35);
    internal void Reset()
    {
        dx=dy=0;sumX=sumY=0;sampleStart=lastMotion=0;X=Y=0;
        Array.Clear(intervals);intervalCount=intervalIndex=0;packetMs=0;
        paced=false;
    }

    internal static void Test()
    {
        long T(int ms)=>(long)(ms*TicksPerMs);
        if(SampleMs!=3)throw new Exception("A variante de teste não está em 3 ms.");
        var timing=new MouseStick();timing.Update(T(4000),1000,1000);timing.Add(3,0);
        timing.Update(T(4002),1000,1000);
        if(timing.X!=0)throw new Exception("Movimento saiu antes da janela de 3 ms.");
        timing.Update(T(4003),1000,1000);
        if(timing.X!=1000)throw new Exception("Movimento não saiu ao completar 3 ms.");
        var m=new MouseStick();m.Update(T(1000),1000,1000);
        for(int t=1;t<=8;t++){m.Add(2,-1);m.Update(T(1000+t),1000,1000);}
        if(m.X!=2000||m.Y!=1000)throw new Exception("Conversão linear do mouse para RS incorreta.");
        for(int t=9;t<=16;t++)m.Update(T(1000+t),1000,1000);
        if(m.X!=0||m.Y!=0)throw new Exception("RS não voltou ao centro ao parar o mouse.");
        var grouped=new MouseStick();grouped.Update(T(2000),1000,1000);grouped.Add(16,-8);grouped.Update(T(2008),1000,1000);
        if(grouped.X!=2000||grouped.Y!=1000)throw new Exception("Cadência dos pacotes alterou a conversão.");
        grouped.Reset();grouped.Update(T(2016),1000,1000);grouped.Update(T(2024),1000,1000);
        if(grouped.X!=0||grouped.Y!=0)throw new Exception("Reset deixou movimento residual.");
        grouped.Add(-1000,1000);grouped.Update(T(2032),10000,10000);
        if(grouped.X!=-32767||grouped.Y!=-32767)throw new Exception("Eixo ultrapassou a faixa do controle.");
        var separate=new MouseStick();separate.Update(T(3000),1000,1500);
        separate.Add(8,-8);separate.Update(T(3008),1000,1500);
        if(separate.X!=1000||separate.Y!=1500)throw new Exception("Ganhos X e Y não foram aplicados separadamente.");
    }
}
