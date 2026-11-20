namespace KeyboardToGamepad;

// F is a held interaction, not a timed menu pulse. Cursor recognition is armed
// on release so it cannot interrupt a long interaction such as a loadout.
internal sealed class InteractionInput
{
    private bool down;
    private long candidateUntil;
    internal void Key(bool pressed,long now)
    {
        if(pressed){down=true;candidateUntil=0;}
        else if(down){down=false;candidateUntil=now+1500;}
    }
    internal bool Output(bool active)=>active&&down;
    internal bool MenuCandidate(long now)=>!down&&now<candidateUntil;
    internal void CancelCandidate()=>candidateUntil=0;
    internal void Reset(){down=false;candidateUntil=0;}
    internal static void Test()
    {
        var f=new InteractionInput();f.Key(true,0);
        for(long time=0;time<=5000;time+=10)
        {
            if(time%100==0)f.Key(true,time); // OS repeat must not release X
            if(!f.Output(true)||f.MenuCandidate(time))throw new Exception("F soltou ou entrou em menu durante interação longa.");
        }
        f.Key(false,5001);
        if(f.Output(true)||!f.MenuCandidate(5001)||f.MenuCandidate(6501))throw new Exception("Soltura de F ou reconhecimento de menu inválido.");
        f.Key(true,7000);if(f.Output(false))throw new Exception("F ativo com mapeamento desligado.");
        f.Reset();if(f.Output(true)||f.MenuCandidate(7001))throw new Exception("Reset não liberou F.");
        f.Key(true,8000);f.Key(false,8010);
        if(f.Output(true)||!f.MenuCandidate(8010))throw new Exception("Toque rápido de F inválido.");
    }
}
