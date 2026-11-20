namespace KeyboardToGamepad;

// These are output-only experiments; they do not detect targets or change
// the game's aim-assist strength. Units match the former DS4 sliders.
internal sealed record EffectLevels(int Recoil,int Left,int Right)
{
    internal void Validate()
    {
        if(Recoil is <0 or >50||Left is <0 or >40||Right is <0 or >8)
            throw new InvalidDataException("Efeitos fora dos limites permitidos.");
    }
}

internal static class DirectEffects
{
    private const int Scale=256;
    internal static void Apply(EffectLevels levels,bool gameplay,bool aiming,bool firing,long timeMs,
        ref short lx,ref short ly,ref short rx,ref short ry)
    {
        if(!gameplay||!aiming)return;
        bool rightIdle=Math.Abs((int)rx)<=4*Scale&&Math.Abs((int)ry)<=4*Scale;
        if(levels.Left>0&&Math.Abs((int)lx)<=8*Scale&&Math.Abs((int)ly)<=8*Scale)
            lx=(short)((timeMs/180%2==0?1:-1)*levels.Left*Scale);
        if(levels.Right>0&&rightIdle)
            rx=(short)((timeMs/400%2==0?1:-1)*levels.Right*Scale);
        // XInput positive Y is up; subtract to compensate downwards.
        if(levels.Recoil>0&&firing)
            ry=(short)Math.Clamp((int)ry-levels.Recoil*Scale,short.MinValue,short.MaxValue);
    }
}
