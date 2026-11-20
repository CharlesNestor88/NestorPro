using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace KeyboardToGamepad;

internal sealed class CharlesPanel : Form
{
    internal const string Release="0.3.9 • DIRETO";
    private static readonly Color Background=Color.FromArgb(16,17,22), Surface=Color.FromArgb(28,29,36),
        Border=Color.FromArgb(55,57,68), Gold=Color.FromArgb(232,188,108), Muted=Color.FromArgb(156,160,172), Red=Color.FromArgb(220,36,69);
    private CharlesRouter? router;
    private string startupError="";
    private CharlesSettings settings=new();
    private readonly Dictionary<int,(ComboBox Primary,ComboBox Secondary)> editors=new();
    private readonly Label savedStatus=new(){AutoSize=true};
    private readonly Label hipSavedStatus=new(){AutoSize=true};
    private readonly Label status=new(){AutoSize=true};
    private readonly Label monitor=new(){AutoSize=true};
    private readonly Label homeStatus=new(){AutoSize=true};
    private readonly Label homeValues=new(){AutoSize=true};
    private readonly Label footer=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
    private readonly NotifyIcon tray=new();
    private readonly System.Windows.Forms.Timer timer=new(){Interval=200};
    private readonly NumericUpDown hip=new(){Minimum=1,Maximum=10000,Value=394};
    private readonly NumericUpDown hipY=new(){Minimum=1,Maximum=10000,Value=394};
    private readonly NumericUpDown adsX=new(){Minimum=1,Maximum=10000,Value=394};
    private readonly NumericUpDown adsY=new(){Minimum=1,Maximum=10000,Value=394};
    private readonly NumericUpDown deadzone=new(){Minimum=0,Maximum=12000,Value=2500};
    private readonly TrackBar recoilSlider=new(){Minimum=0,Maximum=50,TickFrequency=5,SmallChange=1,LargeChange=5};
    private readonly TrackBar leftMotionSlider=new(){Minimum=0,Maximum=40,TickFrequency=5,SmallChange=1,LargeChange=5};
    private readonly TrackBar rightMotionSlider=new(){Minimum=0,Maximum=8,TickFrequency=1,SmallChange=1,LargeChange=1};
    private readonly Label effectsStatus=new(){AutoSize=true};
    private readonly CheckBox nativeInventory=new(){Text="Mochila: mouse livre e W/A/S/D pelo direcional do controle",Checked=true,AutoSize=true};
    private readonly CheckBox nativeShop=new(){Text="Loja: usar teclado e mouse nativos",Checked=false,AutoSize=true};
    private readonly ComboBox dropOutput=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=390};
    private readonly Panel viewport=new(){Dock=DockStyle.Fill,Padding=new Padding(22,16,18,8)};
    private readonly Dictionary<string,FlowLayoutPanel> pages=new();
    private readonly Dictionary<string,Button> navigation=new();
    private readonly List<Panel> cards=new();
    private readonly ControllerArtwork controller=new(){Dock=DockStyle.Fill};
    private readonly ControllerArtwork buttonController=new(){Dock=DockStyle.Top,Height=170,Margin=Padding.Empty,ShowReadout=false};
    private readonly Image watermark;
    private TableLayoutPanel? homeTiles;
    private TableLayoutPanel? buttonLayout;
    private TableLayoutPanel? buttonToolbar;
    private Button pause=null!;
    private Button previewButton=null!;
    private string currentPage="Visão geral";
    private bool pendingSettings;

    public CharlesPanel(bool startAutomatically=true)
    {
        if(startAutomatically)
        {
            try{settings=CharlesSettingsStore.Load();}
            catch(Exception ex){savedStatus.Text="Não foi possível carregar os ajustes: "+ex.Message+" O padrão foi mantido; o arquivo original permanece salvo.";}
        }
        Text=$"NestorPro {Release} • Soft Controle";
        using(var imageStream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("KeyboardToGamepad.AnonymousMask.png")
            ?? throw new FileNotFoundException("A marca visual não foi incluída no executável."))watermark=new Bitmap(imageStream);
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;
        ClientSize=new Size(1000,640);MinimumSize=new Size(960,640);StartPosition=FormStartPosition.CenterScreen;
        BackColor=Background;ForeColor=Color.White;Font=new Font("Segoe UI",10);
        DoubleBuffered=true;
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=Padding.Empty};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,48));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(23,24,30),Margin=Padding.Empty};
        sidebar.Paint+=(_,e)=>{using var pen=new Pen(Border);e.Graphics.DrawLine(pen,sidebar.Width-1,0,sidebar.Width-1,sidebar.Height);};
        shell.Controls.Add(sidebar,0,0);shell.SetRowSpan(sidebar,3);
        var menu=new FlowLayoutPanel{Dock=DockStyle.Top,Height=375,Padding=new Padding(11,14,0,0),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        string[] names={"Visão geral","Mira","Botões","Loja","Inventário","Sistema"};
        string[] icons={"⌂","◎","✥","▣","▤","⚙"};
        string[] captions={"Início","Mira","Botões","Loja","Mochila","Sistema"};
        for(int i=0;i<names.Length;i++)
        {
            string name=names[i];var b=new CompactNavButton(icons[i],captions[i]){Width=46,Height=48,Margin=new Padding(0,0,0,8)};
            b.Click+=(_,_)=>ShowPage(name);navigation.Add(name,b);
            if(name=="Sistema"){b.Dock=DockStyle.Bottom;b.Width=68;b.Height=54;sidebar.Controls.Add(b);}
            else menu.Controls.Add(b);
        }
        sidebar.Controls.Add(menu);
        var header=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=Color.FromArgb(22,23,29)};
        header.Paint+=(_,e)=>{using var pen=new Pen(Border);e.Graphics.DrawLine(pen,0,header.Height-1,header.Width,header.Height-1);};
        var brandMark=new PictureBox{Image=watermark,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent,Location=new Point(21,9),Size=new Size(29,29)};
        header.Controls.Add(brandMark);
        var brand=TextLabel("NESTORPRO",11,Gold,true);brand.Location=new Point(57,15);brand.Margin=Padding.Empty;header.Controls.Add(brand);
        status.ForeColor=Gold;status.Location=new Point(190,16);status.Font=new Font("Segoe UI",8,FontStyle.Bold);header.Controls.Add(status);
        var hideButton=ActionButton("Ocultar",HideToTray);hideButton.Dock=DockStyle.Right;hideButton.Width=115;hideButton.Margin=Padding.Empty;header.Controls.Add(hideButton);
        shell.Controls.Add(header,1,0);shell.Controls.Add(viewport,1,1);
        footer.ForeColor=Muted;footer.Font=new Font("Segoe UI",8);footer.Padding=new Padding(22,0,0,0);shell.Controls.Add(footer,1,2);
        var chrome=new Panel{Dock=DockStyle.Fill,Padding=new Padding(2),BackColor=Background};
        chrome.Paint+=(_,e)=>{using var brush=new LinearGradientBrush(new Point(0,0),new Point(Math.Max(1,chrome.Width),Math.Max(1,chrome.Height)),Color.FromArgb(190,198,208),Color.FromArgb(73,77,88));using var pen=new Pen(brush,2);e.Graphics.DrawRectangle(pen,1,1,Math.Max(0,chrome.Width-3),Math.Max(0,chrome.Height-3));};
        chrome.Controls.Add(shell);Controls.Add(chrome);
        BuildPages();LoadControls(settings);ShowPage("Visão geral");viewport.Resize+=(_,_)=>ResizeCards();ResizeCards();
        nativeInventory.CheckedChanged+=(_,_)=>UpdateMenuOptions();nativeShop.CheckedChanged+=(_,_)=>UpdateMenuOptions();
        dropOutput.SelectedIndexChanged+=(_,_)=>UpdateMenuOptions();
        hip.ValueChanged+=(_,_)=>{if(router!=null)router.MouseGainX=(float)hip.Value;RefreshStatus();};
        hipY.ValueChanged+=(_,_)=>{if(router!=null)router.MouseGainY=(float)hipY.Value;RefreshStatus();};
        adsX.ValueChanged+=(_,_)=>{if(router!=null)router.AdsGainX=(float)adsX.Value;RefreshStatus();};
        adsY.ValueChanged+=(_,_)=>{if(router!=null)router.AdsGainY=(float)adsY.Value;RefreshStatus();};
        deadzone.ValueChanged+=(_,_)=>{if(router!=null)router.MouseDeadzone=(int)deadzone.Value;};
        tray.Icon=Icon;tray.Text="Soft Controle NestorPro";
        var trayMenu=new ContextMenuStrip();trayMenu.Items.Add("Abrir painel",null,(_,_)=>RestoreFromTray());trayMenu.Items.Add("Sair",null,(_,_)=>Close());
        tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=(_,_)=>RestoreFromTray();
        timer.Tick+=(_,_)=>RefreshStatus();timer.Start();FormClosing+=(_,_)=>{tray.Visible=false;Stop();};
        FormClosed+=(_,_)=>{timer.Dispose();trayMenu.Dispose();tray.Dispose();watermark.Dispose();};
        if(startAutomatically)Shown+=(_,_)=>{try{Start();}catch(Exception ex){startupError=ex.Message;}RefreshStatus();};
        RefreshStatus();
    }

    private Label TextLabel(string text,int size=10,Color? color=null,bool bold=false)=>new()
    {
        Text=text,AutoSize=true,ForeColor=color??Color.White,BackColor=Color.Transparent,
        Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,8),MaximumSize=new Size(780,0)
    };
    private Button ActionButton(string text,Action action,bool primary=false)
    {
        var b=new Button{Text=text,Width=175,Height=36,FlatStyle=FlatStyle.Flat,BackColor=primary?Red:Surface,ForeColor=Color.White,
            Cursor=Cursors.Hand,Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(0,0,8,8),AccessibleName=text};
        b.FlatAppearance.BorderColor=primary?Color.FromArgb(224,77,90):Border;
        b.FlatAppearance.MouseOverBackColor=Color.FromArgb(65,34,43);b.FlatAppearance.MouseDownBackColor=Red;
        b.Click+=(_,_)=>{try{action();}catch(Exception ex){status.Text=ex.Message;MessageBox.Show(this,ex.Message,"NestorPro");}};
        return b;
    }
    private FlowLayoutPanel Page(string name,string title,string subtitle)
    {
        var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false,Margin=Padding.Empty,BackColor=Background};
        p.Paint+=(_,e)=>DrawWatermark(e.Graphics,new Rectangle(Math.Max(0,p.Width-590),Math.Max(0,(p.Height-460)/2),440,460),0.22f);
        p.Controls.Add(TextLabel(title,21,null,true));p.Controls.Add(TextLabel(subtitle,9,Muted));
        p.Controls.Add(new Panel{Width=20,Height=5});pages.Add(name,p);viewport.Controls.Add(p);return p;
    }
    private FlowLayoutPanel Card(Control host,string title)
    {
        var frame=new Panel{Width=850,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(16,14,16,8),BackColor=Surface,Margin=new Padding(0,0,0,12)};
        frame.Paint+=(_,e)=>
        {
            int x=Math.Max(0,host.Width-590)-frame.Left;
            int y=Math.Max(0,(host.Height-460)/2)-frame.Top;
            DrawWatermark(e.Graphics,new Rectangle(x,y,440,460),0.20f);
            using var border=new Pen(Border);e.Graphics.DrawRectangle(border,0,0,frame.Width-1,frame.Height-1);
        };
        var flow=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty,BackColor=Color.Transparent};
        flow.Controls.Add(TextLabel(title,12,Gold,true));frame.Controls.Add(flow);host.Controls.Add(frame);cards.Add(frame);return flow;
    }
    private void Paragraph(Control host,string text)=>host.Controls.Add(TextLabel(text,10,Muted));
    private void DrawWatermark(Graphics canvas,Rectangle target,float opacity)
    {
        using var attributes=new ImageAttributes();
        var matrix=new ColorMatrix{Matrix00=1,Matrix11=1,Matrix22=1,Matrix33=opacity,Matrix44=1};
        attributes.SetColorMatrix(matrix,ColorMatrixFlag.Default,ColorAdjustType.Bitmap);
        canvas.DrawImage(watermark,target,0,0,watermark.Width,watermark.Height,GraphicsUnit.Pixel,attributes);
    }
    private Panel DashboardTile(string title,Control value,string note)
    {
        var tile=new Panel{Dock=DockStyle.Fill,BackColor=Surface,Margin=new Padding(0,0,10,0),Padding=new Padding(14,11,10,8)};
        tile.Paint+=(_,e)=>{using var pen=new Pen(Border);e.Graphics.DrawRectangle(pen,0,0,tile.Width-1,tile.Height-1);};
        var stack=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        stack.Controls.Add(TextLabel(title,8,Muted,true));value.Margin=new Padding(0,0,0,5);stack.Controls.Add(value);
        stack.Controls.Add(TextLabel(note,8,Muted));tile.Controls.Add(stack);return tile;
    }
    private void BuildPages()
    {
        var home=Page("Visão geral","Pronto para jogar","Teclado e mouse → NestorPro → controle virtual XInput → jogo");
        var hero=Card(home,"Seu controle, do seu jeito.");
        Paragraph(hero,"O mapeamento inicia com o jogo. Configure teclas e movimento neste painel.");
        var heroGrid=new TableLayoutPanel{Width=790,Height=195,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        heroGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,46));heroGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,14,0,0)};
        homeStatus.ForeColor=Gold;homeStatus.Font=new Font("Segoe UI",10,FontStyle.Bold);homeStatus.MaximumSize=new Size(350,0);homeStatus.Margin=new Padding(0,0,0,18);actions.Controls.Add(homeStatus);
        var heroCommands=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty};
        pause=ActionButton("Pausar mapeamento",ToggleMapping,true);pause.Width=170;heroCommands.Controls.Add(pause);
        var editButtons=ActionButton("Editar botões",()=>ShowPage("Botões"));editButtons.Width=132;heroCommands.Controls.Add(editButtons);
        actions.Controls.Add(heroCommands);actions.Controls.Add(TextLabel("HOME pausa ou retoma  •  Controle direto pelo NestorPro",8,Muted));
        heroGrid.Controls.Add(actions,0,0);heroGrid.Controls.Add(controller,1,0);hero.Controls.Add(heroGrid);
        hero.SizeChanged+=(_,_)=>heroGrid.Width=Math.Max(630,hero.ClientSize.Width);
        homeTiles=new TableLayoutPanel{Width=790,Height=110,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        for(int i=0;i<3;i++)homeTiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        homeValues.ForeColor=Color.White;homeValues.Font=new Font("Segoe UI",14,FontStyle.Bold);
        homeTiles.Controls.Add(DashboardTile("MOUSE → ANALÓGICO",homeValues,"Ganho base atual. Ajuste fino em Mira."),0,0);
        homeTiles.Controls.Add(DashboardTile("SAÍDA DIRETA",TextLabel("XInput",13,null,true),"Antirrecuo e efeitos no próprio NestorPro."),1,0);
        homeTiles.Controls.Add(DashboardTile("MENUS DO JOGO",TextLabel("Loja + Mochila",13,null,true),"Comandos separados para compra e inventário."),2,0);
        home.Controls.Add(homeTiles);
        var aim=Page("Mira","MIRA E EFEITOS","Conversão direta  /  Ajustes no próprio NestorPro");
        var sensitivities=Card(aim,"Sensibilidade do mouse");
        Paragraph(sensitivities,"ADS atua somente enquanto o botão de mirar está pressionado. A leitura adaptativa do mouse foi preservada. Saída linear em XInput, sem uma segunda curva de programa externo.");
        AddNumber(sensitivities,"ADS X • esquerda / direita",adsX);
        AddNumber(sensitivities,"ADS Y • cima / baixo",adsY);
        Paragraph(sensitivities,"Giro sem mirar:");
        AddNumber(sensitivities,"Sensibilidade X • esquerda / direita",hip);
        AddNumber(sensitivities,"Sensibilidade Y • cima / baixo",hipY);
        Paragraph(sensitivities,"Os quatro valores mudam na hora para teste. Clique em Salvar conversão para mantê-los após fechar o NestorPro.");
        sensitivities.Controls.Add(ActionButton("Salvar conversão",SaveMouseGain,true));
        hipSavedStatus.ForeColor=Gold;sensitivities.Controls.Add(hipSavedStatus);
        var effects=Card(aim,"Antirrecuo e AimBuff • ajustes independentes");
        Paragraph(effects,"Zero desliga cada efeito. As barras aplicam na hora para teste; Salvar efeitos mantém os valores após fechar. Loja, mochila e cursor livre não recebem esses efeitos.");
        AddEffectSlider(effects,"Antirrecuo • 0 a 50",recoilSlider,"Compensação vertical somente ao mirar e atirar.");
        AddEffectSlider(effects,"AimBuff • analógico esquerdo • 0 a 40",leftMotionSlider,"Pulsos horizontais ao mirar, somente sem movimento de W/A/S/D.");
        AddEffectSlider(effects,"AimBuff • analógico direito • 0 a 8",rightMotionSlider,"Pulsos horizontais lentos ao mirar com o mouse próximo do centro.");
        Paragraph(effects,"AimBuff é um movimento experimental: não detecta inimigos nem aumenta a configuração de assistência do jogo. O movimento manual tem prioridade e os efeitos param ao soltar a mira.");
        var effectButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty};
        effectButtons.Controls.Add(ActionButton("Salvar efeitos",SaveEffects,true));
        effectButtons.Controls.Add(ActionButton("Desligar efeitos",()=>{recoilSlider.Value=leftMotionSlider.Value=rightMotionSlider.Value=0;SaveEffects();}));
        effects.Controls.Add(effectButtons);
        effectsStatus.ForeColor=Gold;effects.Controls.Add(effectsStatus);
        aim.Controls.SetChildIndex(effects.Parent!,3);
        var previewCard=Card(aim,"Teste do controle direto");
        Paragraph(previewCard,"Ative o teste e mova o mouse sobre este painel: o controle na página Início mostra X e Y com cursor livre. Não produz cliques, disparos nem efeitos. Desative para encerrar o teste.");
        previewButton=ActionButton("Testar controle direto",()=>{if(router==null)Start();router!.PreviewEnabled=!router.PreviewEnabled;RefreshStatus();},true);
        previewCard.Controls.Add(previewButton);
        var buttons=Page("Botões","Botões","Todos os comandos em uma lista: entrada principal e alternativa");
        buttonToolbar=new TableLayoutPanel{Width=790,Height=59,ColumnCount=2,RowCount=2,Margin=new Padding(0,0,0,8)};
        buttonToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));buttonToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,44));
        buttonToolbar.RowStyles.Add(new RowStyle(SizeType.Absolute,39));buttonToolbar.RowStyles.Add(new RowStyle(SizeType.Absolute,20));
        buttonToolbar.Controls.Add(TextLabel("Mapa de comandos",12,Gold,true),0,0);
        var editActions=new FlowLayoutPanel{AutoSize=true,WrapContents=false,FlowDirection=FlowDirection.LeftToRight,Margin=Padding.Empty};
        var restore=ActionButton("Restaurar padrão",RestoreDefaults);restore.Width=145;
        var save=ActionButton("Salvar e aplicar",SaveAndApply,true);save.Width=145;
        editActions.Controls.Add(restore);editActions.Controls.Add(save);buttonToolbar.Controls.Add(editActions,1,0);
        savedStatus.ForeColor=Gold;savedStatus.Margin=Padding.Empty;buttonToolbar.Controls.Add(savedStatus,0,1);
        buttons.Controls.Add(buttonToolbar);
        buttonLayout=new TableLayoutPanel{Width=790,Height=345,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,27));buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,73));
        var buttonGuide=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,BackColor=Surface,Padding=new Padding(10),Margin=new Padding(0,0,12,0)};
        buttonGuide.RowStyles.Add(new RowStyle(SizeType.Absolute,180));buttonGuide.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        buttonGuide.Paint+=(_,e)=>{using var pen=new Pen(Border);e.Graphics.DrawRectangle(pen,0,0,buttonGuide.Width-1,buttonGuide.Height-1);};
        buttonGuide.Controls.Add(buttonController,0,0);
        buttonGuide.Controls.Add(new Label{Dock=DockStyle.Fill,Text="CONTROLE VIRTUAL XINPUT\n\nEscolha as teclas na lista. Cada entrada pertence a um comando.\n\nHOME pausa  •  ESC fecha menus",ForeColor=Muted,Font=new Font("Segoe UI",9),TextAlign=ContentAlignment.TopLeft},0,1);
        buttonLayout.Controls.Add(buttonGuide,0,0);
        var buttonTable=new Panel{Dock=DockStyle.Fill,BackColor=Surface,Margin=Padding.Empty};
        buttonTable.Paint+=(_,e)=>{using var pen=new Pen(Border);e.Graphics.DrawRectangle(pen,0,0,buttonTable.Width-1,buttonTable.Height-1);};
        var headings=new TableLayoutPanel{Dock=DockStyle.Top,Height=36,ColumnCount=3,RowCount=1,BackColor=Color.FromArgb(35,37,46)};
        headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28.5f));headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28.5f));
        foreach(var (label,index) in new[]{("COMANDO",0),("PRINCIPAL",1),("ALTERNATIVA",2)})headings.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0),ForeColor=Muted,Font=new Font("Segoe UI",8,FontStyle.Bold)},index,0);
        var buttonList=new Panel{Dock=DockStyle.Fill,AutoScroll=true};BuildBindingEditor(buttonList);
        buttonTable.Controls.Add(buttonList);buttonTable.Controls.Add(headings);buttonLayout.Controls.Add(buttonTable,1,0);
        buttons.Controls.Add(buttonLayout);
        var shop=Page("Loja","COMPRAS COM CONTROLE","Interação e navegação  /  Comandos sem duplicação");
        var shopCard=Card(shop,"Navegação da loja");
        Paragraph(shopCard,"A entrada de Interagir abre a loja pelo controle virtual. Os comandos de movimento navegam uma posição por pressão. Pular ou Interagir confirmam com A.");
        Paragraph(shopCard,"No modo controle: W/A/S/D navegam; Espaço, F ou clique esquerdo confirmam. Movimento e rolagem do mouse ficam bloqueados na loja para não trocar a seleção. ESC fecha o menu.");StyleCheck(nativeShop);shopCard.Controls.Add(nativeShop);
        Paragraph(shopCard,"O reconhecimento usa Interagir e a visibilidade do cursor; continua experimental. Mudanças nesta página só entram em vigor após Salvar e aplicar.");shopCard.Controls.Add(ActionButton("Salvar e aplicar",SaveAndApply,true));
        var inventory=Page("Inventário","MOCHILA E EQUIPAMENTOS","Navegue, selecione e solte  /  Ajustes do inventário");
        var inv=Card(inventory,"Abertura e navegação");Paragraph(inv,"TAB abre o inventário. A/D percorrem os itens na horizontal; W/S alcançam a linha de munição. As setas também funcionam. ESC fecha o menu.");
        StyleCheck(nativeInventory);inv.Controls.Add(nativeInventory);
        Paragraph(inv,"Mudanças na navegação entram em vigor após Salvar e aplicar.");
        var drop=Card(inventory,"Soltar dinheiro ou item");Paragraph(drop,"Com o item selecionado, R solta uma unidade. Shift+R solta tudo, inclusive todo o dinheiro. Fora do inventário, R recarrega normalmente.");
        dropOutput.Items.AddRange(new object[]{"A — soltar item selecionado","RS — soltar tudo","Tecla física nativa — sem conversão","Y — alternativa"});dropOutput.SelectedIndex=0;
        dropOutput.BackColor=Color.FromArgb(38,41,49);dropOutput.ForeColor=Color.White;dropOutput.Margin=new Padding(0,2,0,16);drop.Controls.Add(dropOutput);
        Paragraph(drop,"Padrão: R envia A por 90 ms para soltar o item selecionado. RS é a opção separada de soltar tudo. Salve antes de testar uma opção diferente.");drop.Controls.Add(ActionButton("Salvar e aplicar",SaveAndApply,true));
        var system=Page("Sistema","CENTRAL DO SISTEMA","Conexão, diagnóstico e drivers");
        var connection=Card(system,"Dispositivos e ativação");Paragraph(connection,"Teclado e mouse físicos → Interception → NestorPro → Xbox 360 virtual via ViGEm → jogo. O DS4Windows não é iniciado nem necessário neste modo.");
        Paragraph(connection,"O mapeamento atua com o Warzone em primeiro plano. Fora do jogo, teclado e mouse permanecem disponíveis. HOME pausa ou retoma.");
        bool installerAvailable=File.Exists(Path.Combine(AppContext.BaseDirectory,"Drivers","install-interception.exe"));
        var installButton=ActionButton(installerAvailable?"Instalar driver":"Instalador não incluído",Install);
        installButton.Enabled=installerAvailable;connection.Controls.Add(installButton);
        Paragraph(connection,installerAvailable?"A instalação solicita permissão de administrador. Reinicie o Windows depois de instalar o driver.":"Esta cópia não inclui o instalador Interception. O driver precisa estar instalado para iniciar o mapeamento.");
        var diagnostics=Card(system,"Monitor de entradas");monitor.ForeColor=Muted;monitor.Margin=new Padding(0,4,0,12);diagnostics.Controls.Add(monitor);
        Paragraph(diagnostics,"Soft Controle NestorPro "+Release+"  •  Teclas personalizáveis e conversão linear do mouse.\nAjustes desta versão em pasta separada, com uma cópia anterior para recuperação.");
        diagnostics.Controls.Add(ActionButton("Abrir ajustes salvos",()=>{Directory.CreateDirectory(CharlesSettingsStore.DirectoryPath);Process.Start(new ProcessStartInfo(CharlesSettingsStore.DirectoryPath){UseShellExecute=true});}));
    }
    private void StyleCheck(CheckBox check){check.ForeColor=Color.White;check.Margin=new Padding(0,6,0,20);}
    private void AddEffectSlider(Control host,string title,TrackBar slider,string help)
    {
        host.Controls.Add(TextLabel(title,9,null,true));
        var row=new TableLayoutPanel{Width=550,Height=32,ColumnCount=2,Margin=Padding.Empty};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,55));
        var value=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Gold,Font=new Font("Segoe UI",12,FontStyle.Bold),Text="0"};
        slider.Dock=DockStyle.Fill;slider.BackColor=Surface;slider.AccessibleName=title;
        slider.ValueChanged+=(_,_)=>{value.Text=slider.Value.ToString();router?.SetEffects(new((int)recoilSlider.Value,(int)leftMotionSlider.Value,(int)rightMotionSlider.Value));effectsStatus.Text="Valores em teste • use Salvar efeitos para manter";};
        row.Controls.Add(slider,0,0);row.Controls.Add(value,1,0);host.Controls.Add(row);host.Controls.Add(TextLabel(help,9,Muted));
    }
    private void SaveEffects()
    {
        var value=CharlesSettingsStore.Load();
        value.RecoilLevel=recoilSlider.Value;value.AimMotionLeft=leftMotionSlider.Value;value.AimMotionRight=rightMotionSlider.Value;
        CharlesSettingsStore.Save(value);settings=value;router?.SetEffects(value.Effects);
        effectsStatus.Text="Efeitos salvos • "+DateTime.Now.ToString("HH:mm:ss");
    }
    private void AddNumber(Control host,string name,NumericUpDown number)
    {
        var row=new TableLayoutPanel{Width=550,Height=52,ColumnCount=2,Margin=new Padding(0,0,0,8)};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));
        row.Controls.Add(new Label{Text=name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.Gainsboro},0,0);
        number.BackColor=Color.FromArgb(38,41,49);number.ForeColor=Gold;number.Font=new Font("Segoe UI",13,FontStyle.Bold);number.Width=128;number.Margin=new Padding(0,8,0,0);row.Controls.Add(number,1,0);host.Controls.Add(row);
    }
    private void BuildBindingEditor(Control host)
    {
        var values=ButtonCatalog.Actions;
        var table=new TableLayoutPanel{Width=560,Height=values.Length*40,ColumnCount=3,RowCount=values.Length,Margin=Padding.Empty};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28.5f));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28.5f));
        for(int i=0;i<values.Length;i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            table.Controls.Add(new Label{Text=values[i].Name,Dock=DockStyle.Fill,Padding=new Padding(10,0,0,0),ForeColor=Color.White,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",9,FontStyle.Bold)},0,i);
            ComboBox Choice(string name){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,Margin=new Padding(2,6,6,5),AccessibleName=name,BackColor=Color.FromArgb(40,42,51),ForeColor=Color.White,Font=new Font("Segoe UI",9)};c.Items.AddRange(ButtonCatalog.Choices.Cast<object>().ToArray());return c;}
            var primary=Choice(values[i].Name+" • entrada principal");var secondary=Choice(values[i].Name+" • alternativa");
            primary.SelectedIndexChanged+=(_,_)=>MarkPending();secondary.SelectedIndexChanged+=(_,_)=>MarkPending();
            table.Controls.Add(primary,1,i);table.Controls.Add(secondary,2,i);editors.Add(values[i].Code,(primary,secondary));
        }
        host.Controls.Add(table);host.SizeChanged+=(_,_)=>table.Width=Math.Max(490,host.ClientSize.Width-18);
    }
    private void LoadControls(CharlesSettings value)
    {
        hip.Value=value.Hip;hipY.Value=value.EffectiveHipY;adsX.Value=value.EffectiveAdsX;adsY.Value=value.EffectiveAdsY;deadzone.Value=value.Deadzone;nativeInventory.Checked=value.NativeInventory;nativeShop.Checked=value.NativeShop;
        dropOutput.SelectedIndex=value.DropOutput=="RS"?1:value.DropOutput=="Native"?2:value.DropOutput=="Y"?3:0;
        recoilSlider.Value=value.RecoilLevel;leftMotionSlider.Value=value.AimMotionLeft;rightMotionSlider.Value=value.AimMotionRight;
        foreach(var action in ButtonCatalog.Actions){var pair=editors[action.Code];var keys=value.Bindings[action.Code];pair.Primary.SelectedItem=ButtonCatalog.Choices.First(c=>c.Code==keys[0]);pair.Secondary.SelectedItem=ButtonCatalog.Choices.First(c=>c.Code==keys[1]);}
    }
    private CharlesSettings ReadControls()
    {
        var value=new CharlesSettings{Hip=(int)hip.Value,HipY=(int)hipY.Value,AdsX=(int)adsX.Value,AdsY=(int)adsY.Value,Deadzone=(int)deadzone.Value,NativeInventory=nativeInventory.Checked,NativeShop=nativeShop.Checked,DropOutput=SelectedDropOutput,RecoilLevel=recoilSlider.Value,AimMotionLeft=leftMotionSlider.Value,AimMotionRight=rightMotionSlider.Value};
        foreach(var (code,pair) in editors)value.Bindings[code]=new[]{((InputChoice)pair.Primary.SelectedItem!).Code,((InputChoice)pair.Secondary.SelectedItem!).Code};
        value.Validate();return value;
    }
    private void SaveAndApply()
    {
        try{ApplySettings(ReadControls());}
        catch(Exception ex){savedStatus.Text="Não foi aplicado: "+ex.Message;ShowPage("Botões");}
    }
    private bool ApplySettings(CharlesSettings value)
    {
        value.Validate();
        var changed=ButtonCatalog.Actions.Where(a=>!settings.Bindings[a.Code].SequenceEqual(value.Bindings[a.Code])).ToArray();
        if(changed.Length>0)
        {
            string names=string.Join(", ",changed.Select(a=>a.Name));
            string prompt="Tem certeza que quer mudar "+(changed.Length==1?"o botão ":"os botões ")+names+"?\n\nA nova configuração só entra em vigor após sua confirmação.";
            if(MessageBox.Show(this,prompt,"Confirmar alteração dos botões",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)
            {
                savedStatus.Text="Mudança cancelada • nenhum botão foi aplicado";
                return false;
            }
        }
        CharlesSettingsStore.Save(value);settings=value;router?.ApplySettings(value);pendingSettings=false;
        savedStatus.Text="Ajustes salvos e aplicados • "+DateTime.Now.ToString("HH:mm:ss");RefreshStatus();return true;
    }
    private static CharlesSettings MouseGainChange(CharlesSettings source,int x,int y,int aimingX,int aimingY)
    {
        var copy=System.Text.Json.JsonSerializer.Deserialize<CharlesSettings>(System.Text.Json.JsonSerializer.Serialize(source))!;
        copy.Hip=x;copy.HipY=y;copy.AdsX=aimingX;copy.AdsY=aimingY;
        copy.Validate();return copy;
    }
    private void SaveMouseGain()
    {
        try
        {
            var value=MouseGainChange(CharlesSettingsStore.Load(),(int)hip.Value,(int)hipY.Value,(int)adsX.Value,(int)adsY.Value);
            CharlesSettingsStore.Save(value);settings=value;
            if(router!=null){router.MouseGainX=value.Hip;router.MouseGainY=value.EffectiveHipY;router.AdsGainX=value.EffectiveAdsX;router.AdsGainY=value.EffectiveAdsY;}
            hipSavedStatus.Text="Conversão salva • "+DateTime.Now.ToString("HH:mm:ss");RefreshStatus();
        }
        catch(Exception ex){hipSavedStatus.Text="Não foi salvo: "+ex.Message;}
    }
    private void RestoreDefaults(){var defaults=new CharlesSettings();LoadControls(defaults);SaveAndApply();}
    internal void VerifyEditor()
    {
        var before=ReadControls();if(before.Validate().Resolve(1001)!=0x200)throw new Exception("Editor não carregou o padrão.");
        var pair=editors[0x21];pair.Primary.SelectedItem=ButtonCatalog.Choices.First(c=>c.Code==0x22);
        var changed=ReadControls();if(changed.Validate().Resolve(0x22)!=0x21)throw new Exception("Editor não produziu configurações do motor.");
        var gainOnly=MouseGainChange(changed,700,850,900,950);
        if(gainOnly.Hip!=700||gainOnly.EffectiveHipY!=850||gainOnly.EffectiveAdsX!=900||gainOnly.EffectiveAdsY!=950||gainOnly.Validate().Resolve(0x22)!=0x21||gainOnly.NativeInventory!=changed.NativeInventory||gainOnly.NativeShop!=changed.NativeShop||gainOnly.DropOutput!=changed.DropOutput)throw new Exception("Salvar conversão alterou menus ou botões.");
        LoadControls(before);
        pendingSettings=false;savedStatus.Text="";RefreshStatus();
    }
    internal void VerifyButtonsLayout()
    {
        ShowPage("Botões");
        if(buttonLayout==null||!buttonController.Visible||buttonController.Width<170||buttonController.Height<150||
           editors.Count!=ButtonCatalog.Actions.Length||editors.Values.Any(pair=>!pair.Primary.Visible||!pair.Secondary.Visible))
            throw new Exception("Imagem fixa ou editor completo dos botões não está disponível.");
    }
    internal void VerifyTray()
    {
        HideToTray();
        if(Visible||!tray.Visible)throw new Exception("Ocultar painel não manteve o ícone na bandeja.");
        RestoreFromTray();
        if(!Visible||tray.Visible)throw new Exception("O painel não voltou da bandeja.");
    }
    internal void ShowPage(string name)
    {
        currentPage=name;foreach(var (key,p) in pages)p.Visible=key==name;
        foreach(var (key,b) in navigation)if(b is CompactNavButton nav)nav.SetSelected(key==name);
        ResizeCards();
    }
    private void ResizeCards()
    {
        int width=Math.Max(680,viewport.ClientSize.Width-viewport.Padding.Horizontal-32);
        foreach(var card in cards){card.MinimumSize=new Size(width,0);card.MaximumSize=new Size(width,0);card.Width=width;
            foreach(Control flow in card.Controls)foreach(Control control in flow.Controls)if(control is Label label)label.MaximumSize=new Size(width-38,0);}
        foreach(var p in pages.Values)foreach(Control child in p.Controls)if(child is Label label)label.MaximumSize=new Size(width,0);
        if(homeTiles!=null)homeTiles.Width=width;
        if(buttonToolbar!=null)buttonToolbar.Width=width;
        if(buttonLayout!=null)buttonLayout.Width=width;
    }
    private void Start()
    {
        if(router!=null)return;
        if(!Interception.IsDriverInstalled())throw new InvalidOperationException("Interception ausente. Use Instalar driver e reinicie quando puder.");
        router=new CharlesRouter(settings);RefreshStatus();
    }
    private void ToggleMapping(){if(router==null){Start();startupError="";}else router.Toggle();RefreshStatus();}
    private void HideToTray(){tray.Visible=true;Hide();}
    internal void RestoreFromTray(){Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;}
    private void Stop(){router?.Dispose();router=null;RefreshStatus();}
    private string SelectedDropOutput=>dropOutput.SelectedIndex==1?"RS":dropOutput.SelectedIndex==2?"Native":dropOutput.SelectedIndex==3?"Y":"A";
    private void UpdateMenuOptions()=>MarkPending();
    private void MarkPending()
    {
        if(!IsHandleCreated)return;
        pendingSettings=true;
        savedStatus.Text="Alterações pendentes • use Salvar e aplicar";
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        bool failed=startupError!=""||router?.Error is {Length:>0};
        status.Text=startupError!=""?"FALHA AO INICIAR":router==null?"●  MAPEAMENTO EM ESPERA":router.Error!=""?"FALHA NO MAPEAMENTO":router.Active?"●  MAPEAMENTO ATIVO  /  "+router.Mode.ToUpperInvariant():router.AutomaticEnabled?"●  PRONTO  /  ATIVAÇÃO AUTOMÁTICA":"●  MAPEAMENTO PAUSADO";
        status.ForeColor=failed?Color.Salmon:router?.Active==true?Color.FromArgb(133,210,162):Gold;
        if(!failed&&router?.PreviewActive==true)status.Text="● TESTE XINPUT / CURSOR LIVRE";
        previewButton.Text=router?.PreviewEnabled==true?"Encerrar teste direto":"Testar controle direto";
        homeStatus.Text=failed?"Verifique o diagnóstico em Sistema":router?.Active==true?"Controle virtual • ativo":router?.AutomaticEnabled==true?"Pronto para entrar no Warzone":router==null?"Aguardando conexão":"Mapeamento pausado";
        pause.Text=router==null?"Iniciar mapeamento":router.AutomaticEnabled?"Pausar mapeamento":"Retomar mapeamento";
        homeValues.Text=$"X {hip.Value:0}  /  Y {hipY.Value:0}";
        monitor.Text=startupError!=""?startupError:router==null?"Nenhuma entrada capturada.":router.Error!=""?router.Error:$"Eventos físicos: {router.Physical}\nEventos consumidos: {router.Consumed}  •  Relatórios virtuais: {router.Reports}\nAnalógico direito: X {router.RightX} / Y {router.RightY}\n{router.LastRoute}";
        footer.Text="XInput direto  •  HOME pausa / retoma  •  "+(pendingSettings?"ALTERAÇÕES PENDENTES — SALVAR E APLICAR":failed?"Consulte Sistema":"Ativação automática no jogo");
        controller.RightX=router?.RightX??0;controller.RightY=router?.RightY??0;controller.MappingActive=router?.Active==true;
        // Redraw the live X/Y readout only while the controller photo is visible.
        if(currentPage=="Visão geral")controller.Invalidate();
    }
    private void Install()
    {
        string installer=Path.Combine(AppContext.BaseDirectory,"Drivers","install-interception.exe");
        if(!File.Exists(installer))throw new FileNotFoundException("Instalador não está na pasta Drivers.");
        if(MessageBox.Show(this,"Instalar o filtro de teclado e mouse no Windows? Será necessário reiniciar depois. O painel não reiniciará o PC.","Instalar Interception",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        Process.Start(new ProcessStartInfo(installer,"/install"){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
        status.Text="Instalação solicitada. Verifique o resultado e reinicie antes de ativar.";
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    private static int ColorRef(Color color)=>color.R|(color.G<<8)|(color.B<<16);
    private void UpdateWindowChrome()
    {
        if(!IsHandleCreated)return;
        try
        {
            int dark=1,round=2;
            int border=ColorRef(ContainsFocus||Focused?Color.FromArgb(171,179,190):Color.FromArgb(95,100,111));
            int caption=ColorRef(Color.FromArgb(22,23,29)),captionText=ColorRef(Gold);
            DwmSetWindowAttribute(Handle,20,ref dark,sizeof(int));
            DwmSetWindowAttribute(Handle,33,ref round,sizeof(int));
            DwmSetWindowAttribute(Handle,34,ref border,sizeof(int));
            DwmSetWindowAttribute(Handle,35,ref caption,sizeof(int));
            DwmSetWindowAttribute(Handle,36,ref captionText,sizeof(int));
        }
        catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateWindowChrome();}
    protected override void OnActivated(EventArgs e){base.OnActivated(e);UpdateWindowChrome();}
    protected override void OnDeactivate(EventArgs e){base.OnDeactivate(e);UpdateWindowChrome();}
}

internal sealed class CompactNavButton : Button
{
    private readonly string glyph,caption;
    private bool selected;
    internal void SetSelected(bool value){selected=value;Invalidate();}
    internal CompactNavButton(string glyph,string caption)
    {
        this.glyph=glyph;this.caption=caption;AccessibleName=caption;FlatStyle=FlatStyle.Flat;
        FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;BackColor=Color.FromArgb(23,24,30);
        ForeColor=Color.White;Cursor=Cursors.Hand;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);
        if(selected||ClientRectangle.Contains(PointToClient(Cursor.Position)))
        {
            using var path=new GraphicsPath();int x=Width>55?11:1;
            var box=new Rectangle(x,1,Math.Max(1,Width-x-(Width>55?11:1)),Math.Max(1,Height-2));
            int r=9;path.AddArc(box.Left,box.Top,r*2,r*2,180,90);path.AddArc(box.Right-r*2,box.Top,r*2,r*2,270,90);
            path.AddArc(box.Right-r*2,box.Bottom-r*2,r*2,r*2,0,90);path.AddArc(box.Left,box.Bottom-r*2,r*2,r*2,90,90);path.CloseFigure();
            using var fill=new SolidBrush(selected?Color.FromArgb(73,32,43):Color.FromArgb(42,44,52));g.FillPath(fill,path);
        }
        if(selected){using var accent=new SolidBrush(Color.FromArgb(220,36,69));g.FillRectangle(accent,0,10,3,Math.Max(0,Height-20));}
        using var iconFont=new Font("Segoe UI Symbol",16,FontStyle.Regular);
        using var captionFont=new Font("Segoe UI",7.5f,FontStyle.Regular);
        using var ink=new SolidBrush(selected?Color.White:Color.FromArgb(180,183,193));
        using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        g.DrawString(glyph,iconFont,ink,new RectangleF(0,3,Width,28),format);
        g.DrawString(caption,captionFont,ink,new RectangleF(0,32,Width,15),format);
    }
}

internal sealed class ControllerArtwork : Control
{
    internal short RightX,RightY;
    internal bool MappingActive;
    internal bool ShowReadout=true;
    private readonly Image productImage;
    public ControllerArtwork()
    {
        DoubleBuffered=true;BackColor=Color.FromArgb(22,22,28);AccessibleName="Controle DualShock 4 virtual e leitura do analógico direito";
        using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("KeyboardToGamepad.ControllerProduct.png")
            ?? throw new FileNotFoundException("Imagem do controle não foi incluída no executável.");
        productImage=new Bitmap(stream);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(Width<=0||Height<=0)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        float imageHeight=ShowReadout?Math.Max(0,Height-30):Height;
        float scale=Math.Min(Width/(float)productImage.Width,imageHeight/productImage.Height);
        float imageWidth=productImage.Width*scale,drawHeight=productImage.Height*scale;
        g.DrawImage(productImage,(Width-imageWidth)/2,(imageHeight-drawHeight)/2,imageWidth,drawHeight);
        if(!ShowReadout)return;
        string text=MappingActive?$"RS  X {RightX:+0;-0;0}   Y {RightY:+0;-0;0}":"CONTROLE VIRTUAL XINPUT  •  RS X/Y AO VIVO";
        using var caption=new Font("Segoe UI",8.5f,FontStyle.Bold);
        using var ink=new SolidBrush(MappingActive?Color.FromArgb(145,220,177):Color.FromArgb(171,172,186));
        var size=g.MeasureString(text,caption);
        g.DrawString(text,caption,ink,(Width-size.Width)/2,Height-25);
    }
    protected override void Dispose(bool disposing){if(disposing)productImage.Dispose();base.Dispose(disposing);}
}
