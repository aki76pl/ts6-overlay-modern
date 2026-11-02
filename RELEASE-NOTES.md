## TS6 Overlay Modern 3.7.4

- Poprawiono odtwarzanie powiadomień: PCM 16-bit stereo 44,1 kHz zamiast formatu IEEE float dla sterowników waveOut.
- Dodano wybór wyjścia audio, test wejścia/wyjścia i widoczny wynik odtwarzania w Powiadomienia → Dźwięki. Błędy trafiają również do lokalnego sound-errors.log.
- Uszkodzony własny plik dźwięku zastępowany jest sygnałem wbudowanym.
- Poprawiono wykrywanie opuszczenia kanału, gdy zdarzenie Remote Apps nie zawiera oldChannelId; poprzedni kanał odczytywany jest z zapamiętanego stanu.
- Odtwarzanie powiadomienia jest niezależne od renderowania nakładki i zapisu archiwum czatu.
- Dźwięk wyjścia jest domyślnie włączony dla nowych ustawień. Zapisane preferencje pozostają zachowane. Przycisk „Włącz dźwięki wejścia i wyjścia” włącza obie opcje i przywraca 60% głośności, jeśli była ustawiona na 0%.

Po aktualizacji: Ustawienia → Powiadomienia → Dźwięki → Włącz dźwięki wejścia i wyjścia. Wybierz właściwe słuchawki/głośniki i użyj przycisków testowych.
