namespace KeyboardToGamepad;
internal sealed class MousePairs
{
    private readonly int[] owners=new int[4]; // 1 native, 2 virtual
    internal readonly bool[] Pressed=new bool[4];
    internal ushort Route(ushort state,bool play,int mappedMask=15)
    {
        ushort forwarded=(ushort)(state&~0x3cf);
        int[] downs={1,4,0x40,0x100};
        for(int i=0;i<4;i++)
        {
            int down=downs[i],up=down<<1;
            if((state&down)!=0)
            {
                if(owners[i]==0)owners[i]=play&&(mappedMask&(1<<i))!=0?2:1;
                if(owners[i]==1)forwarded|=(ushort)down;
                else Pressed[i]=play;
            }
            if((state&up)!=0)
            {
                if(owners[i]!=2)forwarded|=(ushort)up;
                owners[i]=0;Pressed[i]=false;
            }
            if(!play)Pressed[i]=false;
        }
        return forwarded;
    }
    internal void ResetPressed(){Array.Clear(Pressed);}
    internal static void Test()
    {
        var p=new MousePairs();
        if(p.Route(1,true)!=0||!p.Pressed[0]||p.Route(2,false)!=0)throw new Exception("Clique virtual vazou para menu.");
        if(p.Route(4,false)!=4||p.Route(8,true)!=8||p.Pressed[1])throw new Exception("Clique nativo convertido antes da soltura.");
        if(p.Route(0x10,true)!=0x10)throw new Exception("Botão não mapeado foi perdido.");
        if(p.Route(0x100,true)!=0||p.Route(0x200,false)!=0)throw new Exception("Mouse 5 virtual vazou para menu.");
        p.Route(1,true);p.ResetPressed();p.Route(0,true);if(p.Pressed[0])throw new Exception("Clique reativado após reset.");
    }
}
