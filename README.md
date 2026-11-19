# Specs Panel

Windows nadzorna plošča za spremljanje računalnika na dodatnem zaslonu.

## Zagon

Zaženi `./Publish-Local.ps1` v PowerShellu z .NET 10 SDK, nato odpri izpisano datoteko `SpecsPanel.exe`. Ob zagonu potrdi poziv UAC, če ga Windows prikaže. Aplikacija dobi skrbniške pravice ob zagonu za branje temperaturnih senzorjev CPU-ja.

1. Poveži Samsung tablico prek spacedeska.
2. V nastavitvah zaslona v Windows izberi **Razširi te zaslone**. Z gumbom **Prepoznaj** preveri številko tablice.
3. V aplikaciji Specs Panel izberi ta zaslon in klikni **Celozaslonsko**. Za vrnitev v okno pritisni **Esc** ali **F11**.

**System Specs** prikazuje zaznane naprave, temperaturo CPU-ja in GPU-ja, zasedenost RAM-a ter druge podprte meritve, kot so frekvence in vrtljaji ventilatorjev.

**Processes** prikazuje posamezne procese in njihov PID. Razvrsti jih po deležu skupne zmogljivosti CPU-ja, fizičnem pomnilniku (working set) ali hitrosti branja in pisanja I/O, ki lahko vključuje tudi omrežje in druge naprave. CPU in I/O potrebujeta dva vzorca. Črtica pomeni, da meritev trenutno ni na voljo.

Na podprtih procesorjih Ryzen [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) uporablja gonilnik [PawnIO](https://github.com/namazso/PawnIO.Setup/releases). Če gonilnik manjka, ga lahko odpreš z gumbom v zavihku **System Specs**.

## Samodejni prevzem lokalnih gradenj

Po uspešni gradnji s skriptom `./Publish-Local.ps1` odprto okno aplikacije, ki že teče s skrbniškimi pravicami, samodejno preklopi na novo različico in ohrani izbrani zavihek, zaslon ter celozaslonski način. Skript za vsako gradnjo ustvari ločeno mapo pod `Specs Panel/bin/Local`.
