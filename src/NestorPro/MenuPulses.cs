namespace KeyboardToGamepad;

internal sealed class MenuPulses
{
    private readonly Queue<(string Action,int Duration)> queue=new();
    private string current="";
    private long end,ready;
    internal void Push(string action,int duration){if(queue.Count>=32)throw new InvalidOperationException("Fila de menu cheia; desative e tente novamente.");queue.Enqueue((action,duration));}
    internal string At(long now)
    {
        if(current!=""&&now>=end){current="";ready=now+40;}
        if(current==""&&now>=ready&&queue.Count>0){var p=queue.Dequeue();current=p.Action;end=now+p.Duration;}
        return current;
    }
    internal void Reset(){queue.Clear();current="";end=ready=0;}
    internal static void Test()
    {
        var p=new MenuPulses();p.Push("Down",45);p.Push("Down",45);p.Push("A",90);
        if(p.At(0)!="Down"||p.At(44)!="Down"||p.At(45)!=""||p.At(84)!=""||p.At(85)!="Down"||p.At(130)!=""||p.At(170)!="A"||p.At(260)!="")throw new Exception("Fila, duração ou intervalo de menu inválido.");
        p.Push("RS",90);p.Reset();if(p.At(500)!="")throw new Exception("Reset deixou descarte pendente.");
        p.Push("RS",90);if(p.At(1000)!="RS"||p.At(1089)!="RS"||p.At(1090)!="")throw new Exception("Pulso de descarte inválido.");
    }
}
