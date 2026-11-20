using System.Diagnostics;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets.Xbox360;
namespace KeyboardToGamepad;
internal static class DirectOutputProbe
{
    [StructLayout(LayoutKind.Sequential)]private struct Pad
    {public ushort Buttons;public byte LT,RT;public short LX,LY,RX,RY;}
    [StructLayout(LayoutKind.Sequential)]private struct State
    {public uint Packet;public Pad Pad;}
    [DllImport("xinput1_4.dll")]private static extern uint XInputGetState(uint index,out State state);
    internal static int Run()
    {
        using var client=new ViGEmClient();var pad=client.CreateXbox360Controller();pad.AutoSubmitReport=false;
        pad.Connect();
        try
        {
            pad.SetAxisValue(Xbox360Axis.LeftThumbX,-9876);pad.SetAxisValue(Xbox360Axis.LeftThumbY,13579);
            pad.SetAxisValue(Xbox360Axis.RightThumbX,12345);pad.SetAxisValue(Xbox360Axis.RightThumbY,-23456);
            pad.SetSliderValue(Xbox360Slider.LeftTrigger,64);pad.SetSliderValue(Xbox360Slider.RightTrigger,192);
            pad.SetButtonState(Xbox360Button.A,true);pad.SetButtonState(Xbox360Button.Start,true);pad.SubmitReport();
            uint? found=null;var timer=Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<2000&&found==null)
            {
                for(uint i=0;i<4;i++)if(XInputGetState(i,out var s)==0&&s.Pad.LX==-9876&&s.Pad.LY==13579&&
                    s.Pad.RX==12345&&s.Pad.RY==-23456&&s.Pad.LT==64&&s.Pad.RT==192&&s.Pad.Buttons==0x1010)found=i;
                if(found==null)Thread.Sleep(5);
            }
            if(found==null)throw new Exception("A saída XInput não devolveu os eixos, gatilhos e botões enviados.");
            pad.SetButtonState(Xbox360Button.A,false);pad.SetButtonState(Xbox360Button.Start,false);
            foreach(var axis in new[]{Xbox360Axis.LeftThumbX,Xbox360Axis.LeftThumbY,Xbox360Axis.RightThumbX,Xbox360Axis.RightThumbY})pad.SetAxisValue(axis,0);
            pad.SetSliderValue(Xbox360Slider.LeftTrigger,0);pad.SetSliderValue(Xbox360Slider.RightTrigger,0);pad.SubmitReport();
            Thread.Sleep(30);
            if(XInputGetState(found.Value,out var neutral)!=0||neutral.Pad.Buttons!=0||neutral.Pad.LX!=0||neutral.Pad.LY!=0||neutral.Pad.RX!=0||neutral.Pad.RY!=0||neutral.Pad.LT!=0||neutral.Pad.RT!=0)
                throw new Exception("A saída XInput não retornou ao estado neutro.");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"teste-saida-direta.txt"),$"PASS: XInput slot {found}; eixos 16 bits, botões, gatilhos e retorno ao neutro.\n");
            return 0;
        }
        finally{pad.Disconnect();}
    }
}
