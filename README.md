# CTU60G

Program byl vytvořen za účelem ulehčit vlastníkům vyššího počtu šedesátkových antén publikaci/synchronizaci bezdrátových spojů na webu https://60ghz.ctu.cz.
  
  Data které by uživatel běžně manuálně vyplňoval na webu je možné programu předat formou jsonu, buďto odkazem na nějakou api generující obsah nebo cestou k souboru. Výstupem programu je publikace spoje případně zaslaný email o vzniklé kolizi ( pokud kolize vzniká rušením pouze spojů, které uživatel sám vlastní, může program sám provést prohlášení o izolaci ).
  
  veškerý postup programu je ukládán do logovacího souboru.
  
## Podporované systémy

program je závislý na balíčku .net core 3.1 runtime vyvýjený společností Microsoft
tento balík je možné nainstalovat na platformy:
Windows
různé distribuce Linux více info zde https://docs.microsoft.com/en-us/dotnet/core/install/linux

## Jak spustit

### Příprava
aby bylo možné program spustit musí být na stroji nainstalovaný balík net core 3.1 runtime
https://dotnet.microsoft.com/download

### Konfigurace
spolu se spustitelným soubore programu musí být ve stejném adresáři konfigurační soubor appsettings.json
jeho struktura vypadá následovně:

```yaml
{
    "Config": {},
    "Behaviour": {},
    "Email": {},
    "Serilog": {}
}
```
konfigurace se dělí na čtyři hlavní bloky.

#### Config

```yaml
        "DataURLOrFilePath": "C:\\Users\\user\\file\\ctu60.json",
        "ResponseWithCTUID": "http://somedomain.cz/path/to/api/api.php?key=someKey&action=actionToPassId",
        "ResponseOnDelete": "http://somedomain.cz/path/to/api/api.php?key=someKey&action=actionToNullId",
        "CTULogin": "someEmail@email.com",
        "CTUPass": "somePass",
        "SignalIsolationConsentIfMyOwn": "true"
```

        



