# Specs Panel

Windows nadzorna plošča za spremljanje računalnika na dodatnem zaslonu.

## Zagon

V tej mapi zaženi `dotnet run --project "Specs Panel"` z .NET 10 SDK.

1. Poveži Samsung tablico prek spacedeska.
2. V nastavitvah zaslona v Windows izberi **Razširi te zaslone**. Z gumbom **Prepoznaj** preveri številko tablice.
3. V aplikaciji Specs Panel izberi ta zaslon in klikni **Celozaslonsko**. Za vrnitev v okno pritisni **Esc** ali **F11**.

**System Specs** prikazuje zaznane naprave, temperaturo CPU-ja in GPU-ja, zasedenost RAM-a ter druge podprte meritve, kot so frekvence in vrtljaji ventilatorjev.

**Processes** prikazuje posamezne procese in njihov PID. Razvrsti jih po deležu skupne zmogljivosti CPU-ja, fizičnem pomnilniku (working set) ali hitrosti branja in pisanja I/O, ki lahko vključuje tudi omrežje in druge naprave. CPU in I/O potrebujeta dva vzorca. Črtica pomeni, da meritev trenutno ni na voljo.

Na podprtih procesorjih Ryzen [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) uporablja gonilnik [PawnIO](https://github.com/namazso/PawnIO.Setup/releases). Če temperatura CPU-ja manjka, klikni gumb v zavihku **System Specs** za odprtje uradnega gonilnika ali ponovni zagon s skrbniškimi pravicami.

## Samodejni prevzem lokalnih gradenj

Zaženi `./Publish-Local.ps1` v PowerShellu in prvič odpri izpisano datoteko `SpecsPanel.exe`. Po vsaki naslednji uspešni gradnji s tem skriptom odprto okno samodejno preklopi na novo različico ter ohrani izbrani zavihek, zaslon in celozaslonski način. Skript za vsako gradnjo ustvari ločeno mapo pod `Specs Panel/bin/Local`.
