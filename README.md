## DbMetaTool - ENG

DbMetaTool is a console application built with .NET 8.0, designed to automate the management of Firebird 5.0 database schemas.

## Key Features

* **Metadata Export (Export)**: Automatically extracts definitions for domains, tables, and stored procedures, saving them into structured `.sql` files.
* **Database Build (Build)**: Initializes a new Firebird database and recreates the entire structure from the gathered scripts.
* **Schema Update (Update)**: Intelligently compares scripts with an existing database and applies changes (e.g., adding new columns or domains) without interfering with existing data.

---

## Usage Instructions

The application uses predefined launch profiles stored in the `launchSettings.json` file. Instead of typing commands manually into the console, you can select the desired mode directly from your IDE's debugger menu:

1.  **Export Profile**: Initiates the extraction of the current schema from the source database and generates SQL files. This is the primary mode for versioning structural changes.
2.  **Build Profile**: Used to test script integrity by creating a completely new database and executing all exported instructions.
3.  **Update Profile**: The safest synchronization mode. It analyzes the existing database and applies only missing structural elements (new domains, columns), allowing you to update environments without losing user data.

> **Note:** Parameters such as the password (default: `masterkey`) and database file paths can be quickly adjusted directly in the `Properties/launchSettings.json` file.

---

## Requirements

* **Runtime**: .NET 8.0 SDK.
* **Database**: Firebird 5.0.
* **Libraries**: `FirebirdSql.Data.FirebirdClient`.
