## TS6 Overlay Modern 3.7.5

- Naprawiono błąd instalacji „Access to the path TS6OverlayModern.exe is denied”. Instalacja korzysta z osobnego pomocnika i czeka na zamknięcie procesu przed podmianą EXE.
- Naprawiono kolejność zamykania aplikacji: nieudane przygotowanie instalacji pozostawia działający program i blokadę pojedynczej instancji. Usuwa to przyczynę powstawania niewidocznych procesów po błędzie instalacji.
- Dodano bezpieczną obsługę istniejącego pliku tylko do odczytu, pierwszej instalacji i przywracania poprzedniej wersji po błędzie.
- Skrót, autostart i wpis odinstalowania są rejestrowane dopiero przez poprawnie uruchomioną, nowo zainstalowaną kopię.
- Nowsze kopie mogą zapisać ustawienia i zamknąć się na żądanie instalatora. Dla starszej kopii, która nadal blokuje plik, instalator podaje konkretną instrukcję zamknięcia procesu.

Jeśli poprzednia instalacja się nie powiodła, zakończ pozostawione procesy TS6OverlayModern w Menedżerze zadań, uruchom TS6OverlayModern-3.7.5.exe i wybierz Ustawienia → Aktualizacje i kopia → Zainstaluj na tym komputerze. Ustawienia i profile pozostają zachowane.
