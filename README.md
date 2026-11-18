# Specs Panel

Windows nadzorna plošča za spremljanje računalnika na dodatnem zaslonu.

## Zagon

V tej mapi zaženi `dotnet run --project "Specs Panel"` z .NET 10 SDK.

1. Poveži Samsung tablico prek spacedeska.
2. V nastavitvah zaslona v Windows izberi **Razširi te zaslone**. Z gumbom **Prepoznaj** preveri številko tablice.
3. V aplikaciji Specs Panel izberi ta zaslon in klikni **Celozaslonsko**. Za vrnitev v okno pritisni **Esc** ali **F11**.

Zgornje kartice prikazujejo temperaturo CPU in GPU ter obremenitev RAM-a. Tabela prikazuje zaznane naprave in njihove podprte meritve, vključno s frekvencami, porabo in hitrostjo ventilatorjev, kadar so na voljo. Odčitki se osvežujejo približno vsakih 1,5 sekunde.

Črtica pomeni, da odčitek trenutno ni na voljo. Nekateri senzorji zahtevajo skrbniške pravice. Vir meritev je [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
