namespace Lutra.App;

public sealed partial class MainWindow
{
    private ReleaseUpdate? availableUpdate;
    private bool updateBusy, updateChecked;
    private bool includePreviews = AppUpdate.CurrentVersion.Contains('-');
    private string updateMessage = "";
    private CancellationTokenSource? updateCancellation;
    private TextBlock? updateStatus;

    private FrameworkElement AboutView()
    {
        var identity = Ui.Row(Ui.Mark(48), Ui.Text("Lutra", 34, bold: true)); identity.Margin = new(0, 0, 0, 16);
        var details = Ui.Stack(identity, Ui.Text(AppUpdate.CurrentVersion, 20, Ui.AccentHex),
            Ui.Text(Ui.T("Independent cooling control for Windows", "Itsenäinen jäähdytysohjain Windowsille"), 17),
            Ui.Text(Ui.T("Temperature in. Cooling under control. Local measurements, editable curves and a background service that keeps working when this window is closed.",
                "Lämpötilat, säätökäyrät ja jäähdytyksen ohjaus samassa paikassa. Mittaukset säilyvät koneellasi, ja taustapalvelu jatkaa ohjausta ikkunan sulkemisen jälkeen."), 14, Ui.MutedHex));
        details.Children[2].SetValue(MarginProperty, new Thickness(0, 14, 0, 10));
        var links = new WrapPanel { Margin = new(0, 22, 0, 18) };
        links.Children.Add(Ui.Button(Ui.T("GitHub project", "GitHub-projekti"), () => Open(Releases.ProjectUrl), true));
        links.Children.Add(Ui.Button(Ui.T("Releases", "Julkaisut"), () => Open(Releases.ProjectUrl + "/releases")));
        links.Children.Add(Ui.Button(Ui.T("Report an issue", "Ilmoita ongelmasta"), () => Open(Releases.ProjectUrl + "/issues")));
        details.Children.Add(links);
        details.Children.Add(Ui.Text(Ui.T("An independent community project. Not affiliated with Aqua Computer. Hardware support and physical verification are documented in the project.",
            "Itsenäinen yhteisöprojekti. Ei liity Aqua Computeriin. Laitetuki ja oikeilla laitteilla tehdyt testit on kuvattu projektissa."), 12, Ui.MutedHex));
        var left = Ui.Panel(details, new Thickness(24)); left.VerticalAlignment = VerticalAlignment.Top;
        var preview = CurveForm.Check(Ui.T("Include preview releases", "Sisällytä esiversiot"), includePreviews);
        preview.IsEnabled = !updateBusy;
        preview.Click += (_, _) => { includePreviews = preview.IsChecked == true; updateChecked = false; availableUpdate = null; updateMessage = ""; Render(); };
        var updates = Ui.Stack(Ui.Text(Ui.T("Software updates", "Ohjelmistopäivitykset"), 22, bold: true),
            Ui.Text(Ui.T("Check this project's GitHub releases and install a newer Windows version here.", "Tarkista tämän projektin GitHub-julkaisut ja asenna uudempi Windows-versio tästä."), 14, Ui.MutedHex), preview);
        updates.Children[1].SetValue(MarginProperty, new Thickness(0, 12, 0, 16));
        updateStatus = Ui.Text(updateMessage.Length > 0 ? updateMessage : updateChecked ? Ui.T("You have the latest available version.", "Käytössä on uusin saatavilla oleva versio.") : Ui.T("Updates are checked only when you ask.", "Päivitykset tarkistetaan pyynnöstäsi."), 14, Ui.AccentHex);
        updateStatus.Margin = new(0, 16, 0, 16); updates.Children.Add(updateStatus);
        var buttons = new WrapPanel();
        var check = Ui.Button(Ui.T("Check for updates", "Tarkista päivitykset"), () => _ = CheckForUpdates()); check.IsEnabled = !updateBusy && !demo; buttons.Children.Add(check);
        if (availableUpdate != null)
        {
            var install = Ui.Button(Ui.T("Install update", "Asenna päivitys"), () => _ = InstallUpdate(), true); install.IsEnabled = !updateBusy && !dirty && AppUpdate.IsInstalled && !demo; buttons.Children.Add(install);
        }
        if (updateBusy) buttons.Children.Add(Ui.Button(Ui.T("Cancel", "Peruuta"), () => updateCancellation?.Cancel()));
        updates.Children.Add(buttons);
        var explanation = Ui.Text(demo ? Ui.T("Update checks and installation are disabled in demo mode. Open Lutra normally to check releases.", "Päivitysten tarkistus ja asennus eivät ole käytössä esittelytilassa. Avaa Lutra normaalisti tarkistaaksesi julkaisut.")
            : dirty ? Ui.T("Apply or revert your curve edits before updating.", "Ota käyrämuutokset käyttöön tai hylkää ne ennen päivitystä.")
            : !AppUpdate.IsInstalled ? Ui.T("You can check releases here. Install a packaged Lutra version to enable automatic installation.", "Voit tarkistaa julkaisut tästä. Asenna Lutran julkaisupaketti ottaaksesi automaattisen asennuksen käyttöön.")
            : Ui.T("Windows asks for administrator approval. Settings and history are kept. The service pauses briefly; QUADRO uses its saved curves and fallback. If startup fails, Lutra attempts to restore the previous version.",
                "Windows pyytää järjestelmänvalvojan hyväksynnän. Asetukset ja historia säilyvät. Palvelu pysähtyy hetkeksi; QUADRO käyttää tallennettuja käyriään ja varatehoa. Jos käynnistys epäonnistuu, Lutra yrittää palauttaa edellisen version."), 12, Ui.MutedHex);
        explanation.Margin = new(0, 18, 0, 0); updates.Children.Add(explanation);
        if (availableUpdate is { } release)
        {
            var notes = Ui.Text(release.Notes.Length > 3000 ? release.Notes[..3000] + "…" : release.Notes, 13, Ui.MutedHex); notes.Margin = new(0, 8, 0, 14);
            updates.Children.Add(new Border { Height = 1, Background = Ui.Line, Margin = new(0, 22, 0, 18) });
            updates.Children.Add(Ui.Text(release.Tag, 18, bold: true)); updates.Children.Add(notes);
            updates.Children.Add(Ui.Button(Ui.T("Read release notes on GitHub", "Lue julkaisutiedot GitHubista"), () => Open(release.PageUrl.AbsoluteUri)));
        }
        var right = Ui.Panel(updates, new Thickness(24)); right.VerticalAlignment = VerticalAlignment.Top;
        return Ui.Columns((0, left), (22, new Border()), (0, right));
    }
    private async Task CheckForUpdates()
    {
        if (updateBusy || demo) return;
        updateBusy = true; updateMessage = Ui.T("Checking GitHub…", "Tarkistetaan GitHubista…"); updateCancellation = new(); Render();
        try
        {
            availableUpdate = await AppUpdate.CheckAsync(includePreviews, updateCancellation.Token); updateChecked = true;
            updateMessage = availableUpdate == null ? Ui.T("You have the latest available version.", "Käytössä on uusin saatavilla oleva versio.") : Ui.T("Available: ", "Saatavilla: ") + availableUpdate.Tag;
        }
        catch (OperationCanceledException) { updateMessage = Ui.T("Update check cancelled.", "Päivitystarkistus peruutettu."); }
        catch (Exception e) { updateMessage = Ui.T("Could not check for updates. Try again. ", "Päivitysten tarkistus epäonnistui. Yritä uudelleen. ") + e.GetBaseException().Message; }
        finally { updateBusy = false; updateCancellation.Dispose(); updateCancellation = null; if (page == "about") Render(); }
    }
    private async Task InstallUpdate()
    {
        if (updateBusy || demo || dirty || availableUpdate == null) return;
        updateBusy = true; updateMessage = Ui.T("Downloading update…", "Ladataan päivitystä…"); updateCancellation = new(); Render();
        try
        {
            var progress = new Progress<double>(fraction => { updateMessage = Ui.T("Downloading and verifying · ", "Ladataan ja tarkistetaan · ") + fraction.ToString("P0"); if (updateStatus != null) updateStatus.Text = updateMessage; });
            var staged = await AppUpdate.DownloadAsync(availableUpdate, progress, updateCancellation.Token);
            updateCancellation.Token.ThrowIfCancellationRequested();
            CaptureForm();
            if (dirty) throw new InvalidOperationException(Ui.T("Apply or revert edits made during the download, then retry the update.", "Ota latauksen aikana tehdyt muutokset käyttöön tai hylkää ne ja yritä päivitystä uudelleen."));
            AppUpdate.StartInstall(staged);
            closing = true; Close();
        }
        catch (OperationCanceledException) { updateMessage = Ui.T("Update cancelled. The installed version is unchanged.", "Päivitys peruutettu. Asennettu versio säilyi ennallaan."); }
        catch (Exception e) { updateMessage = Ui.T("Update could not start. ", "Päivitystä ei voitu käynnistää. ") + e.GetBaseException().Message; }
        finally { updateBusy = false; updateCancellation.Dispose(); updateCancellation = null; if (!closing && page == "about") Render(); }
    }
}
