namespace KeyboardToGamepad;
// Preserve the destination of a native press through release across layer changes.
internal sealed class NativeKeyPairs
{
    private readonly HashSet<(int,int)> keys=new();
    internal bool Continue(int device,int scan,bool up)
    {
        var id=(device,scan);if(!keys.Contains(id))return false;
        if(up)keys.Remove(id);return true;
    }
    internal void Begin(int device,int scan,bool up){if(!up)keys.Add((device,scan));}
    internal static void Test()
    {
        var p=new NativeKeyPairs();p.Begin(1,19,false);
        if(!p.Continue(1,19,false)||p.Continue(2,19,false)||!p.Continue(1,19,true)||p.Continue(1,19,true))throw new Exception("Destino da tecla mudou antes da soltura.");
        p.Begin(1,0x138,false);if(p.Continue(1,0x38,false)||!p.Continue(1,0x138,true))throw new Exception("Tecla estendida confundida.");
    }
}
