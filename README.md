**DbMetaTool**

DbMetaTool to aplikacja konsolowa stworzona w technologii .NET 8.0, służąca do automatyzacji zarządzania schematem bazy danych Firebird 5.0.


**Główne Funkcjonalności**
* **Eksport metadanych (Export)**: Automatyczne pobieranie definicji domen, tabel oraz procedur składowanych i zapisywanie ich do ustrukturyzowanych plików `.sql`.
* **Budowanie bazy (Build)**: Inicjalizacja nowej bazy danych Firebird i odtwarzanie pełnej struktury na podstawie zgromadzonych skryptów.
* **Aktualizacja schematu (Update)**: Inteligentne porównywanie skryptów z istniejącą bazą i aplikowanie zmian (np. dodawanie nowych kolumn lub domen) bez ingerencji w istniejące dane.


**Instrukcja Użycia**

Aplikacja korzysta z predefiniowanych profilów uruchamiania zawartych w pliku `launchSettings.json`. Zamiast ręcznego wpisywania komend w konsoli, można wybrać odpowiedni tryb prosto z menu debugera:
1. **Profil Export:** Uruchamia pobieranie aktualnego schematu z bazy źródłowej i generuje pliki SQL. Jest to podstawowy tryb do wersjonowania zmian w strukturze.
2. **Profil Build:** Służy do testowania poprawności skryptów poprzez stworzenie całkowicie nowej bazy danych i wykonanie na niej wszystkich wyeksportowanych instrukcji.
3. **Profil Update:** Najbezpieczniejszy tryb synchronizacji, który analizuje istniejącą bazę i aplikuje tylko brakujące elementy struktury (nowe domeny, kolumny), co pozwala na aktualizację środowisk bez utraty danych użytkownika.

*Uwaga: Parametry takie jak hasło (domyślnie `masterkey`) oraz ścieżki do plików bazy można szybko dostosować bezpośrednio w pliku `Properties/launchSettings.json`.*

**Wymagania**
* **Runtime**: .NET 8.0 SDK.
* **Baza danych**: Firebird 5.0.
* **Biblioteki**: FirebirdSql.Data.FirebirdClient.
