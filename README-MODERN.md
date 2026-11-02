## Poprawka 3.7.5 — instalacja

Instalator przygotowuje kopię przed zamknięciem aplikacji i bezpiecznie podmienia EXE po wyjściu procesu. Obsługuje pierwszą instalację, istniejący plik tylko do odczytu oraz przywracanie poprzedniej wersji. Nieudane przygotowanie nie zamyka działającej aplikacji.

Po wcześniejszym błędzie instalacji zakończ pozostawione procesy **TS6OverlayModern** w Menedżerze zadań, uruchom pobraną wersję **3.7.5** i wybierz **Aktualizacje i kopia → Zainstaluj na tym komputerze**.

## Poprawka 3.7.4 — dźwięki powiadomień

W **Powiadomienia → Dźwięki** kliknij **Włącz dźwięki wejścia i wyjścia**, wybierz słuchawki/głośniki i sprawdź **Test wejścia** oraz **Test wyjścia**. Wynik lub błąd zobaczysz pod przyciskami. Zachowano zapisane ustawienia użytkownika; dźwięk wyjścia jest domyślnie włączony dla nowych konfiguracji.

## Poprawka 3.7.3 — aktualizacje Windows

Aktualizator czeka na zakończenie procesu, dopiero potem podmienia EXE. Naprawia to błąd dostępu do `TS6Overlay.old`. Przy chwilowej blokadzie pliku ponawia próbę, a po błędzie uruchomienia przywraca poprzednią wersję.

Jeżeli starsza wersja wyświetla ten błąd: zamknij ją poleceniem **Zamknij** w zasobniku, pobierz `TS6OverlayModern-3.7.3.exe` i uruchom nowy plik. Następnie w **Aktualizacje i kopia** wybierz **Zainstaluj na tym komputerze**, aby zaktualizować również zainstalowaną kopię i autostart. Ustawienia pozostają zachowane.

## Nowości 3.7.2

- **Wygląd → Wskaźnik aktywności głosu**: pulsujący punkt, animowane słupki lub punkt statyczny.
- **Wygląd → Zawartość**: włączanie awatarów. Zdjęcie przypiszesz w **Osoby → Ulubieni → Awatar…**; przycisk **Inicjały** przywraca inicjały.
- **Wygląd → Pakiet wyglądu**: eksport i import ustawień głównych jako `.ts6look`.
- **Zachowanie → Diagnostyka → Sprawdź połączenie**: aktualizowany raport TeamSpeak, API, autoryzacji i zdarzeń.

## Nowości 3.7.0

Wygaszanie nicków, tryb minimalistyczny i edytor układu: **Ustawienia → Wygląd**. Profile: **Zachowanie → Profile gier**. Pseudonimy i kolory: **Osoby → Ulubieni**. Pobieranie aktualizacji: **Aktualizacje i kopia → Sprawdź teraz**.

Pełny opis: [RELEASE-NOTES.md](RELEASE-NOTES.md).

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

Uruchom pobrany plik `TS6OverlayModern-<wersja>.exe` (np. `TS6OverlayModern-3.7.1.exe`). Wydanie self-contained zawiera .NET.
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
