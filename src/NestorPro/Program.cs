using System.Windows.Forms;

namespace KeyboardToGamepad;

internal static class Program
{
    private const string ShowPanelSignal="Local\\CharlesDS4SomenteDS4.ShowPanel";
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Contains("--mouse-stick-test")){MouseStick.Test();return 0;}
        if(args.Contains("--direct-output-test"))return DirectOutputProbe.Run();
        if(args.Contains("--viewer-test"))
        {
            Application.EnableVisualStyles();
            using var panel=new CharlesPanel(false);
            panel.Show();panel.VerifyButtonsLayout();Application.DoEvents();
            using var image=new System.Drawing.Bitmap(panel.Width,panel.Height);
            panel.DrawToBitmap(image,new System.Drawing.Rectangle(System.Drawing.Point.Empty,panel.Size));
            image.Save(Path.Combine(AppContext.BaseDirectory,"painel-botoes-verificado.png"));
            panel.Close();Console.WriteLine("BOTÕES PASS");return 0;
        }
        if(args.Contains("--self-test"))
        {
            MenuPulses.Test();
            MouseStick.Test();
            InteractionInput.Test();
            NativeKeyPairs.Test();
            MousePairs.Test();
            InputBindings.Test();HeldActions.Test();CharlesSettingsStore.Test();
            CharlesRouter.TestRoutes();
            Application.EnableVisualStyles();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            if(!CharlesRouter.IsWarzoneProcess("cod")||!CharlesRouter.IsWarzoneProcess("ModernWarfare")||CharlesRouter.IsWarzoneProcess("codex")||CharlesRouter.IsWarzoneProcess("CODBrokerService")||CharlesRouter.IsWarzoneProcess("explorer"))throw new Exception("Filtro de ativação do Warzone inválido.");
            using var panel=new CharlesPanel(false);panel.Show();Application.DoEvents();
            panel.VerifyEditor();
            panel.VerifyButtonsLayout();
            panel.VerifyTray();
            panel.ShowPage("Visão geral");
            using var bitmap=new System.Drawing.Bitmap(panel.Width,panel.Height);
            panel.DrawToBitmap(bitmap,new System.Drawing.Rectangle(System.Drawing.Point.Empty,panel.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory,"painel-verificado.png"));
            foreach(string name in new[]{"Mira","Botões","Loja","Inventário","Sistema"})
            {
                panel.ShowPage(name);Application.DoEvents();
                using var pageImage=new System.Drawing.Bitmap(panel.Width,panel.Height);
                panel.DrawToBitmap(pageImage,new System.Drawing.Rectangle(System.Drawing.Point.Empty,panel.Size));
                pageImage.Save(Path.Combine(AppContext.BaseDirectory,"painel-"+name+".png"));
            }
            panel.Size=panel.MinimumSize;panel.ShowPage("Visão geral");Application.DoEvents();
            using var smallImage=new System.Drawing.Bitmap(panel.Width,panel.Height);
            panel.DrawToBitmap(smallImage,new System.Drawing.Rectangle(System.Drawing.Point.Empty,panel.Size));
            smallImage.Save(Path.Combine(AppContext.BaseDirectory,"painel-compacto.png"));
            panel.Close();
            Console.WriteLine("PASS: fila de direções, intervalo neutro, confirmação, descarte, reset e abertura do painel.");return 0;
        }
        if (!args.Contains("--console"))
        {
            using var instance=new Mutex(true,"Local\\CharlesDS4SomenteDS4.ControlPanel",out bool firstInstance);
            if(!firstInstance)
            {
                try
                {
                    using var signal=EventWaitHandle.OpenExisting(ShowPanelSignal);
                    signal.Set();
                    return 0;
                }
                catch(WaitHandleCannotBeOpenedException){}
                foreach(var process in System.Diagnostics.Process.GetProcessesByName("CharlesDS4-SomenteDS4"))
                {
                    using(process)
                    {
                        if(process.Id==Environment.ProcessId||process.MainWindowHandle==IntPtr.Zero)continue;
                        ShowWindow(process.MainWindowHandle,9);SetForegroundWindow(process.MainWindowHandle);break;
                    }
                }
                return 0;
            }
            using(var previous=System.Diagnostics.Process.GetProcessesByName("CharlesDS4").FirstOrDefault())
            {
                if(previous!=null)
                {
                    MessageBox.Show("Feche o Charles DS4 anterior antes de abrir esta versão de teste. Os dois programas não podem capturar o mesmo mouse ao mesmo tempo.","Charles • Somente DS4Windows");
                    return 0;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Interception.RegisterNativeLibraryResolver();
            using var showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,ShowPanelSignal);
            using var panel=new CharlesPanel();
            _=panel.Handle;
            var showThread=new Thread(()=>
            {
                while(!panel.IsDisposed)
                {
                    try {if(!showSignal.WaitOne(500))continue;}
                    catch(ObjectDisposedException){break;}
                    try {if(panel.IsHandleCreated)panel.BeginInvoke(new Action(panel.RestoreFromTray));}
                    catch(InvalidOperationException){}
                }
            }){IsBackground=true,Name="NestorPro-show-panel"};
            showThread.Start();
            Application.Run(panel);
            return 0;
        }
        Console.Title = "Charles Teste 0.1.0";
        Console.WriteLine("Charles Teste 0.1.0 - base experimental KeyboardToGamepad (MIT)");
        Console.WriteLine("--------------------------------------------------------------------");

        // Allow a single self-contained exe: load interception.dll from the copy embedded in this
        // build (falls back to a loose interception.dll next to the exe when not embedded).
        Interception.RegisterNativeLibraryResolver();

        // 1) Load the JSON config (path can be overridden as the first CLI arg).
        string configPath = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "config.json");

        // Seed a default config next to the exe on first run, so a lone exe just works.
        Config.EnsureFileExists(configPath);

        Config config;
        try
        {
            config = Config.Load(configPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load config '{configPath}': {ex.Message}");
            return 1;
        }
        Console.WriteLine($"Loaded {config.Mappings.Count} key mapping(s) from {configPath}");

        // 2) Translate "key name -> target name" into the lookup maps.
        InputMap map;
        try
        {
            map = InputMap.Build(config.Mappings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Invalid mapping: {ex.Message}");
            return 1;
        }

        PrintOccupiedKeys(config.Mappings);

        // 2.5) Make sure the required drivers are present; offer to install any missing one from the
        //      installers embedded in this exe. Returns false when we must stop (e.g. Interception was
        //      just installed and needs a reboot before it can capture keys).
        if (!DriverSetup.EnsureDrivers(config))
            return 0;

        // 3) Create the chosen virtual controller (requires the ViGEmBus driver).
        IVirtualPad pad;
        try
        {
            pad = VirtualPad.Create(config.ControllerType);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("[ERROR] Could not create the virtual controller (is ViGEmBus installed?).");
            Console.WriteLine("        https://github.com/nefarius/ViGEmBus/releases");
            Console.WriteLine($"        (details: {ex.Message})");
            return 2;
        }
        Console.WriteLine($"Virtual {pad.Kind} controller connected - it will act as Player 2.");

        // 4) Run the chosen input backend.
        return config.Backend.Trim().ToLowerInvariant() switch
        {
            "interception" => RunInterception(map, pad, config),
            "hook" => RunHook(map, pad, config),
            _ => UnknownBackend(config.Backend, pad),
        };
    }

    private static void PrintOccupiedKeys(Dictionary<string, string> mappings)
    {
        Console.WriteLine();
        Console.WriteLine($"Keys captured for Player 2 ({mappings.Count}):");
        foreach (var (key, target) in mappings)
            Console.WriteLine($"    {key,-12} -> {target}");
        Console.WriteLine("    (these keys are taken over for P2; the rest of the keyboard stays Player 1)");
        Console.WriteLine();
    }

    private static int UnknownBackend(string backend, IVirtualPad pad)
    {
        Console.WriteLine($"[ERROR] Unknown backend '{backend}' (use 'interception' or 'hook').");
        pad.Dispose();
        return 1;
    }

    private static int RunInterception(InputMap map, IVirtualPad pad, Config config)
    {
        if (!Interception.IsDriverInstalled())
        {
            Console.WriteLine();
            Console.WriteLine("[WARN] The Interception driver does not appear to be installed.");
            Console.WriteLine("       Capturing/blocking keys will NOT work until you install it and reboot:");
            Console.WriteLine("       1) in an admin console:  install-interception.exe /install");
            Console.WriteLine("       2) reboot your PC");
            Console.WriteLine("       Download: https://github.com/oblitum/Interception/releases");
            Console.WriteLine();
        }

        InterceptionEngine engine;
        try
        {
            engine = new InterceptionEngine(map, pad, config.BlockMappedKeys);
            engine.Start();
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine();
            Console.WriteLine("[ERROR] interception.dll not found next to KeyboardToGamepad.exe.");
            Console.WriteLine("        Get the x64 interception.dll from:");
            Console.WriteLine("        https://github.com/oblitum/Interception/releases");
            pad.Dispose();
            return 4;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"[ERROR] {ex.Message}");
            pad.Dispose();
            return 4;
        }

        if (ShouldUseDashboard(config))
        {
            try
            {
                new Dashboard(config, map, engine, pad.Kind).Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[dashboard error] {ex.Message} - falling back to plain mode.");
                RunPlain();
            }
        }
        else
        {
            RunPlain();
        }

        engine.Dispose();
        pad.Dispose();
        Console.WriteLine("Stopped.");
        return 0;
    }

    // The dashboard needs a real console; fall back to plain logging if output is redirected.
    private static bool ShouldUseDashboard(Config config)
    {
        if (config.Ui.Trim().ToLowerInvariant() != "dashboard")
            return false;
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine("[note] Output is redirected (no real console) - using plain mode.");
            return false;
        }
        return true;
    }

    private static void RunPlain()
    {
        Console.WriteLine("Backend: Interception (P2 keys are captured below Raw Input - hidden from the game).");
        Console.WriteLine("Running. Keep this window open. Press Ctrl+C to quit.");
        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();
    }

    private static int RunHook(InputMap map, IVirtualPad pad, Config config)
    {
        using var hook = new KeyboardHook(map, pad, config.BlockMappedKeys);
        try
        {
            hook.Install();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            pad.Dispose();
            return 3;
        }

        Console.WriteLine("Backend: low-level hook (WARNING: does NOT block Raw Input games like Cuphead).");
        Console.WriteLine("(the hook backend has no dashboard TUI -- it just logs.)");
        Console.WriteLine("Running. Keep this window open. Press Ctrl+C to quit.");

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; Application.Exit(); };
        Application.Run();

        pad.Dispose();
        Console.WriteLine("Stopped.");
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr window,int command);
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr window);
}
