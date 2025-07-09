# DB-testresults-trx

A command-line utility for storing Visual Studio Test Results (`.trx` files) and their metadata in a relational database.

## 🚀 Overview

**DB-testresults-trx** is a .NET CLI tool that scans a specified directory (recursively) for `.trx` files (Visual Studio/MSBuild test results), extracts relevant metadata, and stores both the files themselves and their summary information in your database. This allows for long-term archiving, auditing, or analytics of test runs across your CI/CD infrastructure.

## ✨ Features

- Recursively scans folders for `.trx` files.
- Extracts summary data (test run info, outcomes, dates, etc.).
- Stores both file metadata and optionally the raw `.trx` files in the database.
- Uses direct SQL (ADO.NET) for storage — no ORM overhead.
- Suitable for integration with CI servers and build pipelines.

## ⚙️ Quick Start

1. **Clone the repository:**
  ```bash
  git clone https://github.com/trippymajo/DB-testresults-trx.git
  cd DB-testresults-trx
  ```
  
2. **Configure your database connection:**
Set the connection string in appsettings.json or pass it via environment variable:
```json
"ConnectionStrings": {
  "Default": "Server=localhost;Database=YourDb;User Id=youruser;Password=yourpassword;"
}
```

3. **Prepare the database:**
Make sure the required tables (e.g., TestRun, TestResult, TestCase, TrxFiles) exist. The project does not handle migrations — tables must be created manually.

4. **Run the utility:**
`dotnet run --project src/DbTestResultsTrx/DbTestResultsTrx.csproj --trxPath "./TestResults"`
The tool will process all .trx files found and insert relevant information into your database.

## 📝 Usage Notes
- Only .trx files are supported.
- The utility is CLI-based — no REST API or web interface included.
- All database interaction is via ADO.NET with native SQL queries.
- Sensitive credentials should not be committed to version control.

## ⚠️ Limitations & Recommendations
- Error handling is basic. The utility may exit on invalid files or DB issues.
- No automatic schema migrations. You must manage table creation/updates.
- No logging by default. For production use, add logging as needed.
- Security: Move sensitive config to environment variables or a secret manager in production.
