# TS6 Overlay Modern 3.6.1

Osobny projekt na podstawie TS6 Overlay autorstwa AKI76PL (MIT).

## Zmiany 3.6.1

Odstęp między użytkownikami jest regulowany suwakiem w Ustawienia → Wygląd (0–20 px, domyślnie 2 px). Zmniejszono też wewnętrzne odstępy wierszy. Ustawienie zapisuje się automatycznie i działa w nakładce, podglądzie oraz OBS.

## Nowy interfejs

- Centrum ustawień z boczną nawigacją, ikonami, grupami funkcji i stałym podglądem.
- Granatowa paleta, zaokrąglone przyciski i listy wyboru, czytelniejsze odstępy.
- Płynna zmiana sekcji; animacje respektują ustawienie animacji Windows.
- Domyślny motyw Aurora: turkusowy głos, fioletowy szept i wyróżnione wiersze.
- Większe odstępy i zaokrąglone karty uczestników na nakładce.
- Osobne ustawienia `%APPDATA%\TS6OverlayModern`, instalacja, autostart i identyfikator instancji.
- Aktualizacje kierowane do `aki76pl/ts6-overlay-modern`.

Istniejące ustawienia można przenieść przez eksport i import kopii w panelu ustawień.
Nie uruchamiaj obu aplikacji jednocześnie: używają tych samych globalnych skrótów klawiszowych.

## Windows x64

Uruchom `TS6OverlayModern.exe`. Wydanie self-contained zawiera .NET.
TeamSpeak musi udzielić nowej aplikacji dostępu do API.

## Kompilacja

Windows, .NET SDK 10:

```powershell
dotnet publish TS6Overlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/win-x64
```

## Walidacja wyglądu

```powershell
$env:TS6_OVERLAY_DATA_DIR = Join-Path $PWD 'qa-data'
dotnet run --project TS6Overlay.csproj -c Release -- --design-check
```

Tryb sprawdza konstrukcję i renderuje wszystkie 10 sekcji oraz nakładkę do `qa-data/design-check`.
Nie potwierdza komunikacji z TeamSpeak, OBS, telefonem ani działania skrótów w grze.

## Publikacja nowego repozytorium

Po zalogowaniu `gh auth login`:

```powershell
gh repo create aki76pl/ts6-overlay-modern --public
git push -u origin main
git tag v3.6.0
git push origin v3.6.0
```

Workflow GitHub Actions kompiluje Windows x64 i dołącza plik EXE do wydania z tagu.
