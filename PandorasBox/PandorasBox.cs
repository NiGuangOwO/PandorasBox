using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using PandorasBox.Features;
using PandorasBox.FeaturesSetup;
using PandorasBox.IPC;
using PandorasBox.UI;
using PunishLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PandorasBox;

public class PandorasBox : IDalamudPlugin
{
    public string Name => "Pandora's Box";
    private const string CommandName = "/pandora";
    internal WindowSystem Ws;
    internal MainWindow MainWindow;

    internal static PandorasBox P;
    internal static IDalamudPluginInterface pi;
    internal static Configuration Config;

    public List<FeatureProvider> FeatureProviders = new();
    private FeatureProvider provider;
    public IEnumerable<BaseFeature> Features => FeatureProviders.Where(x => !x.Disposed).SelectMany(x => x.Features).OrderBy(x => x.Name);
    public PandorasBox(IDalamudPluginInterface pluginInterface)
    {
        P = this;
        pi = pluginInterface;
        Initialize();
    }

    private void Initialize()
    {
        ECommonsMain.Init(pi, P, ECommons.Module.All);
        PunishLibMain.Init(pi, "Pandora's Box", new AboutPlugin() { Sponsor = "https://ko-fi.com/taurenkey", Translator = "NiGuangOwO", Afdian = "https://afdian.com/a/NiGuangOwO" });
#if !DEBUG
        if (Svc.PluginInterface.IsDev || !Svc.PluginInterface.SourceRepository.Contains("NiGuangOwO/DalamudPlugins"))
        {
            Svc.NotificationManager.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification()
            {
                Type = Dalamud.Interface.ImGuiNotification.NotificationType.Error,
                Title = "加载验证",
                Content = "由于本地加载或安装来源仓库非NiGuangOwO个人仓库，插件加载失败",
            });
            return;
        }
#endif
        Ws = new();
        MainWindow = new();
        Ws.AddWindow(MainWindow);
        Config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(Svc.PluginInterface);

        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开PandorasBox菜单。",
            ShowInHelp = true
        });

        Svc.PluginInterface.UiBuilder.Draw += Ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Common.Setup();
        PandoraIPC.Init();
        Events.Init();
        AFKTimer.Init();
        provider = new FeatureProvider(Assembly.GetExecutingAssembly());
        provider.LoadFeatures();
        FeatureProviders.Add(provider);

    }


    public void Dispose()
    {
#if !DEBUG
        if (Svc.PluginInterface.IsDev || !Svc.PluginInterface.SourceRepository.Contains("NiGuangOwO/DalamudPlugins"))
        {
            PunishLibMain.Dispose();
            ECommonsMain.Dispose();
            return;
        }
#endif
        Svc.Commands.RemoveHandler(CommandName);
        foreach (var f in Features.Where(x => x is not null && x.Enabled))
        {
            f.Disable();
            f.Dispose();
        }

        provider.UnloadFeatures();

        Svc.PluginInterface.UiBuilder.Draw -= Ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Ws.RemoveAllWindows();
        MainWindow = null;
        Ws = null;
        ECommonsMain.Dispose();
        PunishLibMain.Dispose();
        FeatureProviders.Clear();
        Common.Shutdown();
        PandoraIPC.Dispose();
        Events.Disable();
        AFKTimer.Dispose();
        P = null;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.IsOpen = !MainWindow.IsOpen;
    }

    public void DrawConfigUI()
    {
        MainWindow.IsOpen = !MainWindow.IsOpen;
    }
}

