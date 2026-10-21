using System.Windows.Automation;

namespace Lutra.App;

public sealed partial class MainWindow
{
    private Button? gpuRefreshButton;
    private TextBlock? gpuRefreshStatus;
    private bool gpuRefreshPending;
    private GpuRefreshResult gpuRefreshResult;
    private string gpuRefreshError = "";
    private enum GpuRefreshResult { None, Started, Failed }

    private FrameworkElement GpuRefreshView()
    {
        gpuRefreshButton = Ui.Button("", () => _ = RefreshGpuSensorsAsync());
        gpuRefreshButton.HorizontalAlignment = HorizontalAlignment.Left;
        gpuRefreshButton.Margin = new(0, 14, 0, 0);
        gpuRefreshStatus = Ui.Text("", 12, Ui.MutedHex);
        gpuRefreshStatus.Margin = new(0, 10, 0, 0);
        AutomationProperties.SetLiveSetting(gpuRefreshStatus, AutomationLiveSetting.Polite);
        var description = Ui.Text(Ui.T(
            "If GPU temperatures disappear after a graphics driver update, restart sensor discovery here. Your cooling curves stay unchanged.",
            "Jos GPU-lämpötilat katoavat näytönohjaimen ajuripäivityksen jälkeen, käynnistä antureiden tunnistus tästä. Säätökäyrät säilyvät ennallaan."), 13, Ui.MutedHex);
        description.Margin = new(0, 6, 0, 0);
        var panel = Ui.Panel(Ui.Stack(Ui.Text(Ui.T("GPU sensors", "GPU-anturit"), 19, bold: true), description, gpuRefreshButton, gpuRefreshStatus));
        panel.Margin = new(0, 0, 0, 16);
        return panel;
    }

    private void RefreshGpuAction()
    {
        if (gpuRefreshButton == null || gpuRefreshStatus == null) return;
        string label = gpuRefreshPending ? Ui.T("Restarting discovery…", "Käynnistetään tunnistusta…")
            : Ui.T("Rediscover GPU sensors", "Tunnista GPU-anturit uudelleen");
        gpuRefreshButton.Content = label;
        AutomationProperties.SetName(gpuRefreshButton, label);
        gpuRefreshButton.IsEnabled = !demo && canApply && !busy && !updateBusy;
        gpuRefreshStatus.Foreground = gpuRefreshResult == GpuRefreshResult.Failed ? Ui.Danger : Ui.Muted;
        gpuRefreshStatus.Text = gpuRefreshPending ? Ui.T("Waiting for the service…", "Odotetaan palvelun vastausta…")
            : gpuRefreshResult == GpuRefreshResult.Failed ? Ui.T("Discovery could not restart. Try again. ", "Tunnistusta ei voitu käynnistää. Yritä uudelleen. ") + gpuRefreshError
            : gpuRefreshResult == GpuRefreshResult.Started ? Ui.T(
                "Discovery restarted. Readings may take a moment to return; configured fallback remains active while a sensor is missing.",
                "Tunnistus käynnistetty. Lukemien palautuminen voi kestää hetken; asetettu varateho pysyy käytössä anturin puuttuessa.")
            : demo ? Ui.T("Sensor discovery is disabled in demo mode.", "Antureiden tunnistus ei ole käytössä esittelytilassa.")
            : !canApply ? Ui.T("Connect to the Lutra service to rediscover GPU sensors.", "Yhdistä Lutra-palveluun tunnistaaksesi GPU-anturit uudelleen.")
            : Ui.T("The background service continues to control cooling.", "Taustapalvelu jatkaa jäähdytyksen ohjausta.");
    }

    internal async Task RefreshGpuSensorsAsync(Func<ServiceRequest, Task<ServiceReply>>? send = null)
    {
        if (demo || !canApply || busy || updateBusy) return;
        busy = gpuRefreshPending = true;
        gpuRefreshResult = GpuRefreshResult.None;
        RefreshStatus();
        try
        {
            // A discovery request never captures or applies the user's draft configuration.
            var request = new ServiceRequest("refresh-gpu");
            var reply = await (send == null ? ControlMessages.RequestAsync(request) : send(request));
            if (!reply.Success) throw new IOException(reply.Message);
            gpuRefreshResult = GpuRefreshResult.Started;
        }
        catch (Exception e)
        {
            gpuRefreshResult = GpuRefreshResult.Failed;
            gpuRefreshError = e is OperationCanceledException
                ? Ui.T("The service did not reply in time.", "Palvelu ei vastannut ajoissa.") : e.GetBaseException().Message;
        }
        finally
        {
            busy = gpuRefreshPending = false;
            if (page == "about") Render(); else RefreshStatus();
        }
    }
}
