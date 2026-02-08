##**DbMetaTool - Narzędzie do zarządzania strukturą bazy Firebird**
DbMetaTool to aplikacja konsolowa stworzona w technologii .NET 8.0, służąca do automatyzacji zarządzania schematem bazy danych Firebird 5.0.

##**Główne Funkcjonalności**
* **Eksport metadanych (Export)**: Automatyczne pobieranie definicji domen, tabel oraz procedur składowanych i zapisywanie ich do ustrukturyzowanych plików `.sql`.
* **Budowanie bazy (Build)**: Inicjalizacja nowej bazy danych Firebird i odtwarzanie pełnej struktury na podstawie zgromadzonych skryptów.
* **Aktualizacja schematu (Update)**: Inteligentne porównywanie skryptów z istniejącą bazą i aplikowanie zmian (np. dodawanie nowych kolumn lub domen) bez ingerencji w istniejące dane.

##**Instrukcja Użycia**
Alikacja posiada 3 predefiniowanye profile znajdujace się w `launchSettings.json`, z ustawionym domyślnym hasłem, domyślnymi ścieżakmi dla bazy danych i/lub folderów.
**1. Profil Export:** Uruchamia pobieranie aktualnego schematu z bazy źródłowej i generuje pliki SQL. Jest to podstawowy tryb do wersjonowania zmian w strukturze.
**2. Profil Build:** Służy do testowania poprawności skryptów poprzez stworzenie całkowicie nowej bazy danych i wykonanie na niej wszystkich wyeksportowanych instrukcji.
**3. Profil Update:** Najbezpieczniejszy tryb synchronizacji, który analizuje istniejącą bazę i aplikuje tylko brakujące elementy struktury (nowe domeny, kolumny), co pozwala na aktualizację środowisk bez utraty danych użytkownika.

##**Wymagania*** 
**Runtime**: .NET 8.0 SDK.
* **Baza danych**: Firebird 5.0.
* **Biblioteki**: FirebirdSql.Data.FirebirdClient.
